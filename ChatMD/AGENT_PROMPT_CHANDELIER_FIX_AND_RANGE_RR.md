# TradingBrain — Prompt: Fix Chandelier + Params Ótimos + RR Range/BollingerFade

> Repositório: https://github.com/TavaresBugs/TradingBrain  
> Branch alvo: `feat/chandelier-fix-range-rr` (criar a partir de `main`)  
> Runtime: .NET 10 | Build: `dotnet build ./TradingBrain.slnx` | Testes: `dotnet test ./TradingBrain.slnx`  
> Pré-requisito: `main` contém o commit `006e938` (trade management implementado)

---

## Contexto — duas melhorias independentes num único branch

**Melhoria A — Bug fix + params ótimos (Trend e Momentum)**  
O grid search de 240 combinações para Trend e 576 para Momentum produziu os params ótimos confirmados. Além disso, há um bug de segurança no chandelier: quando o ATR expande após a ativação, o chandelier stop pode cair abaixo do preço de entrada, violando o BE mesmo com `beActivated = true`.

**Melhoria B — RR configurável para Range e BollingerFade**  
Em mercados de range, a matemática favorece alvos fixos de 1.0x-1.2x o risco em vez de trailing. O alvo atual do Range usa `AtrMultiplierTp` fixo nos defaults e o BollingerFade sempre mira a média da Bollinger (aproximadamente 1:1). Precisamos tornar essa proporção configurável para o grid encontrar o valor ótimo empiricamente.

---

## PARTE A — Fix do chandelier e params ótimos

### A.1 — Fix do bug em `EvaluateTrend` e `EvaluateMomentum`

**Arquivo:** `src/TradingBrain.Console/StrategyRules.cs`

O problema está na ordem das verificações quando `beActivated = true`. O chandelier usa o ATR atual, que pode ter expandido desde a ativação, fazendo o stop cair abaixo do entry. O fix é garantir que o chandelier stop nunca seja pior que o entry quando BE está ativo.

**Em `EvaluateTrend` — bloco `position > 0`**, substitua:

```csharp
// DE:
if (beActivated && ChandelierActive(openProfit, initialRiskPoints, _params.ChandelierActivationRMultiple))
{
    var chandelierStop = ChandelierStop(position, extremeFavorable, m["ATR"], _params.ChandelierTrailMultiplier);
    if (bar.Close < chandelierStop)
        return new StrategyDecision(SignalAction.Exit, "Chandelier trail long");
}
if (beActivated && bar.Close <= BreakevenStop(entryPrice))
    return new StrategyDecision(SignalAction.Exit, "Stop BE long");

// PARA:
if (beActivated && ChandelierActive(openProfit, initialRiskPoints, _params.ChandelierActivationRMultiple))
{
    var rawStop = ChandelierStop(position, extremeFavorable, m["ATR"], _params.ChandelierTrailMultiplier);
    // Chandelier nunca viola o BE: stop mínimo é o entry quando beActivated
    var chandelierStop = Math.Max(rawStop, entryPrice);
    if (bar.Close < chandelierStop)
        return new StrategyDecision(SignalAction.Exit, "Chandelier trail long");
}
if (beActivated && bar.Close <= BreakevenStop(entryPrice))
    return new StrategyDecision(SignalAction.Exit, "Stop BE long");
```

**Em `EvaluateTrend` — bloco `position < 0`**, substitua (lógica simétrica):

```csharp
// DE:
if (beActivated && ChandelierActive(openProfit, initialRiskPoints, _params.ChandelierActivationRMultiple))
{
    var chandelierStop = ChandelierStop(position, extremeFavorable, m["ATR"], _params.ChandelierTrailMultiplier);
    if (bar.Close > chandelierStop)
        return new StrategyDecision(SignalAction.Exit, "Chandelier trail short");
}
if (beActivated && bar.Close >= BreakevenStop(entryPrice))
    return new StrategyDecision(SignalAction.Exit, "Stop BE short");

// PARA:
if (beActivated && ChandelierActive(openProfit, initialRiskPoints, _params.ChandelierActivationRMultiple))
{
    var rawStop = ChandelierStop(position, extremeFavorable, m["ATR"], _params.ChandelierTrailMultiplier);
    // Chandelier nunca viola o BE: stop máximo é o entry quando beActivated
    var chandelierStop = Math.Min(rawStop, entryPrice);
    if (bar.Close > chandelierStop)
        return new StrategyDecision(SignalAction.Exit, "Chandelier trail short");
}
if (beActivated && bar.Close >= BreakevenStop(entryPrice))
    return new StrategyDecision(SignalAction.Exit, "Stop BE short");
```

**Aplicar o mesmo fix em `EvaluateMomentum`** — blocos `position > 0` e `position < 0` com a mesma lógica de `Math.Max(rawStop, entryPrice)` e `Math.Min(rawStop, entryPrice)`.

### A.2 — Atualizar defaults confirmados pelo grid

**Arquivo:** `src/TradingBrain.Core/BacktestModels.cs`

No `StrategyTuningParams`, atualize os valores default dos campos de gestão de trade com os params ótimos encontrados pelo grid:

```csharp
// DE:
double BeActivationRMultiple = 0.5,
double ChandelierActivationRMultiple = 1.5,
double ChandelierTrailMultiplier = 2.0,
double TrendAtrStopMultiplier = 2.0,

// PARA:
double BeActivationRMultiple = 1.0,           // grid: BE=1R melhor para Trend
double ChandelierActivationRMultiple = 0.75,  // grid: chandelier ativa cedo com stop largo
double ChandelierTrailMultiplier = 2.0,       // confirmado como único valor viável
double TrendAtrStopMultiplier = 3.0,          // grid: stop 3xATR dominou (Score 1050 vs 98)
```

> **Atenção:** `TrendAtrStopMultiplier` é usado APENAS em `EvaluateTrend`. O `AtrStopMultiplier` geral (usado por Momentum, EMA, Volatility) não muda. Confirme no código que os dois params são distintos antes de alterar.

Para Momentum, os defaults do grid (BE=0.75R, ChandelierActivation=1.25R) se desviam dos novos defaults gerais. Como `BeActivationRMultiple` e `ChandelierActivationRMultiple` são compartilhados, a forma correta é ajustar via grid para Momentum — os defaults acima favorecem Trend. Documente isso num comentário no código:

```csharp
// Nota: BeActivationRMultiple=1.0 e ChandelierActivationRMultiple=0.75 são ótimos para Trend.
// Momentum ótimo: BE=0.75R, ChandelierActivation=1.25R — use grid search para confirmar por strategy.
```

---

## PARTE B — RR configurável para Range e BollingerFade

### B.1 — Adicionar dois novos parâmetros em `BacktestModels.cs`

No `StrategyTuningParams`, adicione ao final da lista de parâmetros (antes do fechamento do record):

```csharp
// NOVOS — adicionados ao final para não quebrar instanciações existentes:
double RangeTargetRatio = 1.0,       // multiplicador: target = stop * RangeTargetRatio
double BbFadeTargetRatio = 1.0)      // fração do caminho até a média: 1.0 = média completa
```

Atualize também o `BaselineLike` adicionando os novos campos com os mesmos defaults:

```csharp
RangeTargetRatio: 1.0,
BbFadeTargetRatio: 1.0
```

### B.2 — Modificar `EvaluateRange` em `StrategyRules.cs`

**Contexto atual:** o exit de TP usa `Math.Abs(bar.Close - entryPrice) >= m["ATR"] * _defaults.AtrMultiplierTp` — o mesmo para todos os parâmetros.

**Mudança:** tornar o target uma função do stop e do `RangeTargetRatio`:

```csharp
// No início do método EvaluateRange, ANTES dos checks de position:
// Calcula stop e target da barra atual baseado nos params
var stopDistance = m["ATR"] * _defaults.AtrMultiplier;      // distância do stop
var targetDistance = stopDistance * _params.RangeTargetRatio; // target = stop × ratio

// Substitua o check de TP/SL:
// DE:
if (position != 0 && Math.Abs(bar.Close - entryPrice) >= m["ATR"] * _defaults.AtrMultiplierTp)
    return new StrategyDecision(SignalAction.Exit, "TP/SL ATR");

// PARA:
if (position > 0 && bar.Close >= entryPrice + targetDistance)
    return new StrategyDecision(SignalAction.Exit, $"TP Range {_params.RangeTargetRatio:F1}R");
if (position < 0 && bar.Close <= entryPrice - targetDistance)
    return new StrategyDecision(SignalAction.Exit, $"TP Range {_params.RangeTargetRatio:F1}R");
if (position > 0 && bar.Close <= entryPrice - stopDistance)
    return new StrategyDecision(SignalAction.Exit, "SL Range");
if (position < 0 && bar.Close >= entryPrice + stopDistance)
    return new StrategyDecision(SignalAction.Exit, "SL Range");
```

> **Importante:** Remova o check de SL/TP original (`Math.Abs(bar.Close - entryPrice) >= ...`) e o mantenhamos separado para long/short como acima. Isso também permite registrar corretamente se foi TP ou SL no `ExitReason`.

O `BE` permanece desativado para Range — não adicione lógica de BE aqui.

### B.3 — Modificar `EvaluateBollingerFade` em `StrategyRules.cs`

**Contexto atual:** o alvo é sempre `m["BbMiddle"]` (média da Bollinger). `BbFadeTargetRatio = 1.0` significa 100% do caminho até a média. `BbFadeTargetRatio = 0.8` significa 80% do caminho — alvo mais próximo, mais atingível.

**Mudança:**

```csharp
// No bloco position > 0, substitua:
// DE:
if (bar.Close >= m["BbMiddle"])
    return new StrategyDecision(SignalAction.Exit, "Alvo media Bollinger long");

// PARA:
var targetLong = m["BbLower"] + (m["BbMiddle"] - m["BbLower"]) * _params.BbFadeTargetRatio;
if (bar.Close >= targetLong)
    return new StrategyDecision(SignalAction.Exit, $"Alvo Bollinger long {_params.BbFadeTargetRatio:F1}x");

// No bloco position < 0, substitua:
// DE:
if (bar.Close <= m["BbMiddle"])
    return new StrategyDecision(SignalAction.Exit, "Alvo media Bollinger short");

// PARA:
var targetShort = m["BbUpper"] - (m["BbUpper"] - m["BbMiddle"]) * _params.BbFadeTargetRatio;
if (bar.Close <= targetShort)
    return new StrategyDecision(SignalAction.Exit, $"Alvo Bollinger short {_params.BbFadeTargetRatio:F1}x");
```

> **Lógica:** `BbFadeTargetRatio = 1.0` → alvo na média (comportamento original, compatível). `BbFadeTargetRatio = 0.8` → alvo em 80% do caminho até a média, ou seja, mais próximo da banda de entrada. `BbFadeTargetRatio = 1.2` → alvo além da média, do outro lado.

### B.4 — Grid search para Range e BollingerFade

**Arquivo:** `src/TradingBrain.Console/GridSearchRunner.cs`

Encontre os métodos de grid específicos de Range e BollingerFade (ou o método genérico que gera os parâmetros) e adicione os novos valores:

```csharp
// Grid para Range:
private static IEnumerable<StrategyTuningParams> RangeGrid()
{
    foreach (var compression in new[] { 0.9, 1.0, 1.05, 1.1, 1.2 })
    foreach (var targetRatio in new[] { 0.8, 1.0, 1.2, 1.4, 1.6 })  // NOVO
        yield return StrategyTuningParams.RefinedDefault with
        {
            RangeCompressionRatio = compression,
            RangeTargetRatio = targetRatio,
            BeActivationRMultiple = 0.0,   // BE desativado para Range
            ChandelierActivationRMultiple = 0.0  // sem trailing para Range
        };
}

// Grid para BollingerFade:
private static IEnumerable<StrategyTuningParams> BollingerFadeGrid()
{
    foreach (var stdDev in new[] { 1.5, 2.0, 2.5 })
    foreach (var rsiOs in new[] { 30, 35, 40 })
    foreach (var rsiOb in new[] { 60, 65, 70 })
    foreach (var targetRatio in new[] { 0.6, 0.8, 1.0, 1.2 })  // NOVO
        yield return StrategyTuningParams.RefinedDefault with
        {
            BbStdDev = stdDev,
            BbFadeRsiOversold = rsiOs,
            BbFadeRsiOverbought = rsiOb,
            BbFadeTargetRatio = targetRatio,
            BeActivationRMultiple = 0.0,   // BE desativado para BollingerFade
            ChandelierActivationRMultiple = 0.0  // sem trailing para BollingerFade
        };
}
```

> **Se o GridSearchRunner não tiver grids separados por strategy**, adicione os parâmetros `RangeTargetRatio` e `BbFadeTargetRatio` nos grids existentes com os valores acima.

### B.5 — Atualizar `ComputeTargetPrice` em `StrategyRules.cs`

O método `ComputeTargetPrice` é usado para registrar o preço alvo nos trades. Atualize para Range e BollingerFade:

```csharp
// Em ComputeTargetPrice:
StrategyKind.Range =>
    direction == SignalAction.Buy
        ? bar.Close + m["ATR"] * _defaults.AtrMultiplier * _params.RangeTargetRatio
        : bar.Close - m["ATR"] * _defaults.AtrMultiplier * _params.RangeTargetRatio,

StrategyKind.BollingerFade =>
    direction == SignalAction.Buy
        ? m["BbLower"] + (m["BbMiddle"] - m["BbLower"]) * _params.BbFadeTargetRatio
        : m["BbUpper"] - (m["BbUpper"] - m["BbMiddle"]) * _params.BbFadeTargetRatio,
```

---

## PARTE C — Testes

**Arquivo:** `tests/TradingBrain.Tests/ChandelierFixTests.cs`

```csharp
using TradingBrain.Core;
using TradingBrain.ConsoleApp;

namespace TradingBrain.Tests;

public class ChandelierFixTests
{
    // Helper: barras subindo linearmente para simular trade vencedor
    private static List<MarketBar> MakeRisingBars(DateTime start, int count, double basePrice, double step)
    {
        var bars = new List<MarketBar>();
        var price = basePrice;
        for (var i = 0; i < count; i++)
        {
            bars.Add(new MarketBar(
                start.AddMinutes(i * 5),
                price, price + Math.Abs(step) * 2, price - 5, price + step, 1000));
            price += step;
        }
        return bars;
    }

    [Fact]
    public void Trend_ChandelierStop_NeverBelowEntry_WhenBeActive()
    {
        // Cenário: trade long que ativa BE, depois ATR expande (simula notícia)
        // O chandelier stop deve ficar >= entryPrice quando beActivated
        var bars = MakeRisingBars(new DateTime(2026, 1, 6, 9, 30, 0), 120, 21000, +8);
        
        var backtester = new StrategyBacktester(StrategyKind.Trend, new StrategyTuningParams(
            TrendAtrStopMultiplier: 3.0,
            BeActivationRMultiple: 1.0,
            ChandelierActivationRMultiple: 0.75,
            ChandelierTrailMultiplier: 2.0));
        var rows = backtester.Run(bars);
        var trades = StrategyBacktester.ExtractTrades(rows, ExecutionSettings.MnqDefault);

        // Nenhum trade que saiu por chandelier deve ter GrossPoints < 0
        var chandelierTrades = trades.Where(t => t.ExitReason.Contains("Chandelier")).ToList();
        Assert.True(chandelierTrades.All(t => t.GrossPoints >= -0.5),  // -0.5 = tolerância de tick
            "Chandelier com BE ativo não deve gerar perda");
    }

    [Fact]
    public void Momentum_ChandelierStop_NeverBelowEntry_WhenBeActive()
    {
        var bars = MakeRisingBars(new DateTime(2026, 1, 6, 9, 0, 0), 80, 21000, +6);
        
        var backtester = new StrategyBacktester(StrategyKind.Momentum, new StrategyTuningParams(
            AtrStopMultiplier: 1.2,
            BeActivationRMultiple: 0.75,
            ChandelierActivationRMultiple: 1.25,
            ChandelierTrailMultiplier: 2.0,
            MomentumVolumeRatio: 1.0,
            MomentumMinMacdAtrRatio: 0.0));
        var rows = backtester.Run(bars);
        var trades = StrategyBacktester.ExtractTrades(rows, ExecutionSettings.MnqDefault);

        var chandelierTrades = trades.Where(t => t.ExitReason.Contains("Chandelier")).ToList();
        Assert.True(chandelierTrades.All(t => t.GrossPoints >= -0.5),
            "Chandelier com BE ativo não deve gerar perda no Momentum");
    }

    [Fact]
    public void Range_TargetRatio_1x_ExitsBefore1x5()
    {
        // Com RangeTargetRatio=1.0, o trade deve sair com lucro próximo de 1R
        // Nunca deve sair com lucro > 1.1R (o target fixo deve ser respeitado)
        var bars = MakeRisingBars(new DateTime(2026, 1, 6, 9, 30, 0), 60, 21000, +4);
        
        var backtester = new StrategyBacktester(StrategyKind.Range, new StrategyTuningParams(
            RangeCompressionRatio: 1.1,
            RangeTargetRatio: 1.0,
            BeActivationRMultiple: 0.0,
            ChandelierActivationRMultiple: 0.0));
        var rows = backtester.Run(bars);
        var trades = StrategyBacktester.ExtractTrades(rows, ExecutionSettings.MnqDefault);

        var tpTrades = trades.Where(t => t.ExitReason.Contains("TP")).ToList();
        // Todos os TPs devem ter RMultiple próximo de 1.0 (com tolerância)
        Assert.True(tpTrades.All(t => t.RMultiple <= 1.15),
            "Range com TargetRatio=1.0 não deve ter RMultiple > 1.15");
    }

    [Fact]
    public void Range_TargetRatio_1x2_ExitsBeyond1x()
    {
        // Com RangeTargetRatio=1.2, os TPs devem ter RMultiple próximo de 1.2
        var bars = MakeRisingBars(new DateTime(2026, 1, 6, 9, 30, 0), 60, 21000, +4);
        
        var backtester = new StrategyBacktester(StrategyKind.Range, new StrategyTuningParams(
            RangeCompressionRatio: 1.1,
            RangeTargetRatio: 1.2,
            BeActivationRMultiple: 0.0,
            ChandelierActivationRMultiple: 0.0));
        var rows = backtester.Run(bars);
        var trades = StrategyBacktester.ExtractTrades(rows, ExecutionSettings.MnqDefault);

        var tpTrades = trades.Where(t => t.ExitReason.Contains("TP")).ToList();
        if (tpTrades.Any())
        {
            Assert.True(tpTrades.All(t => t.RMultiple >= 1.1),
                "Range com TargetRatio=1.2 deve ter RMultiple >= 1.1");
        }
    }

    [Fact]
    public void BollingerFade_TargetRatio_08_ExitsBeforeMiddleBand()
    {
        // Com BbFadeTargetRatio=0.8, deve sair antes de chegar na média
        // Verificamos que o ExitReason contém o novo formato com ratio
        var bars = MakeRisingBars(new DateTime(2026, 1, 6, 9, 30, 0), 60, 21000, -3);
        
        var backtester = new StrategyBacktester(StrategyKind.BollingerFade, new StrategyTuningParams(
            BbStdDev: 2.0,
            BbFadeRsiOversold: 35,
            BbFadeRsiOverbought: 65,
            BbFadeTargetRatio: 0.8,
            BeActivationRMultiple: 0.0,
            ChandelierActivationRMultiple: 0.0));
        var rows = backtester.Run(bars);
        var trades = StrategyBacktester.ExtractTrades(rows, ExecutionSettings.MnqDefault);

        var tpTrades = trades.Where(t => t.ExitReason.Contains("0.8")).ToList();
        // Se houver trades, o ExitReason deve conter "0.8" (o ratio formatado)
        // Ou simplesmente verificar que o ExitReason não diz "media Bollinger" (formato antigo)
        var oldFormatTrades = trades.Where(t => t.ExitReason == "Alvo media Bollinger long" 
                                              || t.ExitReason == "Alvo media Bollinger short").ToList();
        Assert.Empty(oldFormatTrades);
    }
}
```

---

## PARTE D — Verificação completa

```bash
# 1. Build
dotnet build ./TradingBrain.slnx

# 2. Testes (74 existentes + 5 novos = 79 esperados)
dotnet test ./TradingBrain.slnx

# 3. Backtest Trend com novos defaults (deve melhorar de 0.4274 → próximo de 0.49+)
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  outputs/tv-bars/mnq_5m_12mo.csv outputs/fix-trend/ --strategy Trend \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 4. Backtest Range com novos RR (comparar 3 configurações)
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  outputs/tv-bars/mnq_5m_12mo.csv outputs/range-rr10/ --strategy Range \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 5. Grid Range (encontrar o RangeTargetRatio ótimo)
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --grid-search outputs/tv-bars/mnq_5m_12mo.csv outputs/grid-range-rr/ Range \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 6. Grid BollingerFade (encontrar o BbFadeTargetRatio ótimo)
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --grid-search outputs/tv-bars/mnq_5m_12mo.csv outputs/grid-bb-rr/ BollingerFade \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
```

### O que verificar nos resultados

**Chandelier fix (Trend):**
- `outputs/fix-trend/*.trades.csv` — filtrar por `ExitReason = "Chandelier trail long"` e verificar que todos têm `GrossPoints >= 0`
- AvgR deve estar mais próximo do `0.49` original ou acima (era `0.4274` com defaults errados de BE=0.5R)

**Range RR grid:**
- Comparar NetPF e WinRate entre `RangeTargetRatio` de 0.8, 1.0, 1.2, 1.4
- Expectativa: 1.2 deve ter NetPF melhor que 1.0 com WinRate levemente menor
- Se 1.4+ superar 1.2, o mercado de range tem mais movimento do que o esperado
- Se 0.8 superar tudo, o alvo atual já era largo demais

**BollingerFade RR grid:**
- Comparar `BbFadeTargetRatio` de 0.6, 0.8, 1.0, 1.2
- Expectativa: 0.8 ou 1.0 devem superar 1.2+ (target mais próximo = win rate maior em reversão)
- Verificar se o ExitReason mudou do formato antigo ("Alvo media Bollinger") para o novo

---

## Restrições

- **`RangeTargetRatio` e `BbFadeTargetRatio` adicionados ao final** do `StrategyTuningParams` com defaults idênticos ao comportamento atual (1.0) — zero breaking changes em testes existentes
- **`BeActivationRMultiple = 0.0`** nos grids de Range e BollingerFade desativa o BE (a verificação `HasReachedBeTarget` já checa `rMultiple > 0`)
- **`ChandelierActivationRMultiple = 0.0`** nos grids de Range e BollingerFade desativa o chandelier (a verificação `ChandelierActive` já checa `activationRMultiple > 0`)
- **O default `TrendAtrStopMultiplier = 3.0`** afeta APENAS `EvaluateTrend` — confirmar que Momentum, EMA e Volatility usam `AtrStopMultiplier` (campo separado), não `TrendAtrStopMultiplier`
- **74 testes existentes devem continuar passando** — os novos defaults não quebram testes que não testam valores específicos de params
- **Commit e push antes de finalizar:** `git add -A && git commit -m "fix(chandelier): cap stop at entry when BE active; feat(range): configurable RR ratio" && git push origin feat/chandelier-fix-range-rr`

---

## Resumo do que muda em cada arquivo

| Arquivo | Mudança |
|---|---|
| `src/TradingBrain.Core/BacktestModels.cs` | Novos defaults (BE=1R, ChandelierR=0.75, TrendStop=3x) + 2 novos params (RangeTargetRatio, BbFadeTargetRatio) |
| `src/TradingBrain.Console/StrategyRules.cs` | Fix chandelier (Math.Max/Min com entryPrice) em Trend+Momentum + lógica de target em Range e BollingerFade |
| `src/TradingBrain.Console/GridSearchRunner.cs` | Novos valores nos grids de Range e BollingerFade |
| `tests/TradingBrain.Tests/ChandelierFixTests.cs` | Criar — 5 novos testes |

*Gerado em: 2026-05-17 | Próximo ciclo: re-entradas após stop hunting em Trend/Volatility + walk-forward com todos os params ótimos.*

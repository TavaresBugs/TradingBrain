# TradingBrain — Prompt de Implementação: Strategy IbBreakout Canônica

> Repositório: https://github.com/TavaresBugs/TradingBrain  
> Branch alvo: `feat/ib-breakout-strategy` (criar a partir de `main`)  
> Runtime: .NET 10 | Build: `dotnet build ./TradingBrain.slnx` | Testes: `dotnet test ./TradingBrain.slnx`  
> Pré-requisito: `main` com IB classifier, RegimeFilter e WalkForward com regime

---

## Contexto e fundamentação empírica

O IB Breakout é derivado da metodologia Market Profile de J. Peter Steidlmayer (CBOT, 1980s) e é amplamente usado em prop desks e mesas CME. O mecanismo:

- O mercado opera como leilão. A primeira hora (9:30-10:30 ET) estabelece o **consenso inicial de valor** — o Initial Balance.
- Quando esse consenso é rejeitado por participantes que chegam depois das 10:30, movimentos direcionais fortes geralmente seguem.
- **IB estreito** → alta probabilidade de breakout / Trend Day.
- **IB largo** → probabilidade baixa de expansão; mercado tende a permanecer dentro do range.
- Falha de breakout (excesso fora do IB que não fecha fora) → 70-75% de chance de ir ao extremo oposto do IB.

**Diferença crítica em relação ao OrbBreakout existente:**

| | OrbBreakout | IbBreakout (novo) |
|---|---|---|
| Janela | Configurável (9:30-10:00 ou 9:30-10:30) | Canônica: sempre 9:30-10:30 |
| Timeframe interno | Resample M15 | Barras de 5min direto, sem resample |
| Target | ATR-based | Múltiplo do próprio IB range (self-referential) |
| Stop | ATR-based | Extremo oposto do IB (ou midpoint) |
| Entry trigger | Fechamento fora com buffer | Primeiro fechamento de 5min fora do IB |
| Conceito | ORB genérico | Market Profile canônico |

---

## FASE 1 — Limpar enum `StrategyKind` e adicionar `IbBreakout`

### Arquivo: `src/TradingBrain.Core/StrategyKind.cs`

Remova `SessionBreakout` e `GoldBreakout` do enum e adicione `IbBreakout`. O enum final deve ser:

```csharp
public enum StrategyKind
{
    Volatility,
    Momentum,
    Ema,
    Trend,
    Range,
    OrbBreakout,
    VwapReversion,
    BollingerFade,
    SchoolRun,
    IbBreakout   // substitui SessionBreakout e GoldBreakout
}
```

**Motivo da remoção:**
- `SessionBreakout` era um ORB com janela configurável genérica — completamente coberto pelo `OrbBreakout` existente e agora pelo `IbBreakout` canônico.
- `GoldBreakout` era um legado da DLL NinjaTrader calibrado para GC (Gold Futures). Este projeto opera MNQ — a lógica de breakout para MNQ está coberta por `OrbBreakout` e `IbBreakout` com fundamento Market Profile superior.

---

## FASE 2 — Remover referências de `SessionBreakout` e `GoldBreakout`

### 2.1 — `src/TradingBrain.Core/StrategyRegimeMap.cs`

Remova qualquer entrada para `SessionBreakout` e `GoldBreakout` do dicionário `Map`. Se não existirem entradas para eles, nenhuma ação necessária neste arquivo.

### 2.2 — `src/TradingBrain.Console/StrategyRules.cs`

Localize os `case StrategyKind.SessionBreakout:` e `case StrategyKind.GoldBreakout:` (se existirem) e remova-os inteiramente, incluindo os métodos privados associados (`EvaluateSessionBreakout`, `EvaluateGoldBreakout` ou equivalentes).

Se os cases existirem mas não tiverem lógica implementada (ex: `throw new NotImplementedException()` ou `return DecisionResult.Hold(...)`), remova igualmente.

### 2.3 — `src/TradingBrain.Console/GridSearchRunner.cs`

Remova os cases de `SessionBreakout` e `GoldBreakout` do `BuildParameterGrid` (ou equivalente), se existirem.

### 2.4 — `tests/TradingBrain.Tests/`

Busque por `SessionBreakout` e `GoldBreakout` em todos os arquivos de teste (`*.cs`). Para cada ocorrência:
- Se o teste for específico dessas strategies (ex: `GoldBreakout_SomeTest`), **remova o teste inteiro**.
- Se o teste iterar sobre todos os valores do enum (ex: `Enum.GetValues<StrategyKind>()`), **nenhuma alteração necessária** — o enum menor já resolve.
- Se o teste referenciar as strategies por nome em um assert de mapeamento (ex: `StrategyRegimeMap_AllStrategiesHaveMapping`), **remova as linhas** `Assert.True(StrategyRegimeMap.HasFilter(StrategyKind.SessionBreakout))` e equivalente para GoldBreakout.

> **Checagem obrigatória antes de prosseguir:** após as remoções, rode `dotnet build` e corrija qualquer erro de compilação antes de avançar para as próximas fases. Erros esperados são referências ao enum removido em switch statements ou testes.

---

## FASE 3 — Adicionar `IbBreakout` ao `StrategyRegimeMap`

### Arquivo: `src/TradingBrain.Core/StrategyRegimeMap.cs`

Adicione a entrada (regimes Breakout + Trend, conforme base empírica):

```csharp
[StrategyKind.IbBreakout] = new[] { MarketRegime.Breakout, MarketRegime.Trend },
```

---

## FASE 4 — Adicionar parâmetros de tuning ao `StrategyTuningParams`


### Arquivo: `src/TradingBrain.Core/BacktestModels.cs`

Localize o record `StrategyTuningParams` e adicione os campos novos com defaults compatíveis (ao final):

```csharp
// IbBreakout parameters
double IbTargetMultiplier = 1.0,   // target = ibRange * multiplier além do IB
bool   IbUseHalfRangeStop = false, // false = stop no extremo oposto do IB; true = stop no midpoint
double IbMinRangeRatio    = 0.30,  // IB/ATR14 mínimo para aceitar o setup (filtra IBs triviais)
double IbMaxRangeRatio    = 1.80,  // IB/ATR14 máximo para aceitar o setup (filtra IBs muito largos)
bool   IbRequireVolume    = false  // true = exige que o bar de breakout tenha volume > média do IB
```

---



### Arquivo: `src/TradingBrain.Console/StrategyRules.cs`

Adicione o case `IbBreakout` dentro do método `Evaluate` (ou equivalente na partial class). A lógica completa:

```csharp
case StrategyKind.IbBreakout:
    return EvaluateIbBreakout(bar, bars, index, series, metrics, parameters, position, entryPrice);
```

Implemente o método privado:

```csharp
private DecisionResult EvaluateIbBreakout(
    MarketBar bar,
    IReadOnlyList<MarketBar> bars,
    int index,
    PrecomputedSeries series,
    BarMetrics metrics,
    StrategyTuningParams p,
    int position,
    double entryPrice)
{
    var time = bar.Time;
    var hhmm = time.Hour * 100 + time.Minute;

    // --- Calcular Initial Balance (9:30-10:25 inclusive, janela canônica de 1 hora) ---
    // O IB é formado pelas barras ANTERIORES à barra atual de 10:30 em diante.
    // Barras de 5min: 9:30, 9:35, ..., 10:25 = 12 barras (períodos A e B do Market Profile).
    var todayDate = DateOnly.FromDateTime(time);

    double ibHigh = double.NaN;
    double ibLow  = double.NaN;
    double ibVolAvg = 0;
    int ibBarCount = 0;

    for (var k = index - 1; k >= 0; k--)
    {
        var b = bars[k];
        if (DateOnly.FromDateTime(b.Time) != todayDate) break;

        var bHHmm = b.Time.Hour * 100 + b.Time.Minute;
        if (bHHmm < 930 || bHHmm > 1025) continue;

        ibHigh = double.IsNaN(ibHigh) ? b.High : Math.Max(ibHigh, b.High);
        ibLow  = double.IsNaN(ibLow)  ? b.Low  : Math.Min(ibLow,  b.Low);
        ibVolAvg += b.Volume;
        ibBarCount++;
    }

    // IB não está completo (ainda é antes das 10:30 ou não há barras suficientes)
    if (double.IsNaN(ibHigh) || double.IsNaN(ibLow) || ibBarCount < 8)
        return DecisionResult.Hold("IB: incompleto");

    // Janela de entrada: apenas após 10:30 ET, antes das 14:00 ET
    if (hhmm < 1030 || hhmm >= 1400)
    {
        // Gerenciar posição aberta fora da janela
        if (position != 0)
        {
            // Exit forçado às 15:55
            if (hhmm >= 1555)
                return position > 0
                    ? DecisionResult.ExitLong("IB: fim de sessão")
                    : DecisionResult.ExitShort("IB: fim de sessão");
        }
        return DecisionResult.Hold("IB: fora da janela");
    }

    var ibRange = ibHigh - ibLow;
    var atr14   = series.Atr14[index];

    if (atr14 <= 0) return DecisionResult.Hold("IB: ATR inválido");

    var ibRangeRatio = ibRange / atr14;

    // Filtro de qualidade do IB
    if (ibRangeRatio < p.IbMinRangeRatio)
        return DecisionResult.Hold($"IB: range muito estreito ({ibRangeRatio:F2} < {p.IbMinRangeRatio:F2})");
    if (ibRangeRatio > p.IbMaxRangeRatio)
        return DecisionResult.Hold($"IB: range muito largo ({ibRangeRatio:F2} > {p.IbMaxRangeRatio:F2})");

    // Confirmação de volume (opcional)
    ibVolAvg = ibBarCount > 0 ? ibVolAvg / ibBarCount : 0;
    var volumeOk = !p.IbRequireVolume || bar.Volume >= ibVolAvg;

    // Gerenciamento de posição aberta
    if (position != 0)
    {
        var ibMid    = (ibHigh + ibLow) / 2.0;
        var stopLong  = p.IbUseHalfRangeStop ? ibMid : ibLow;
        var stopShort = p.IbUseHalfRangeStop ? ibMid : ibHigh;

        if (position > 0)
        {
            if (bar.Close <= stopLong)  return DecisionResult.ExitLong("IB: stop hit");
            if (bar.Close >= entryPrice + ibRange * p.IbTargetMultiplier)
                                         return DecisionResult.ExitLong("IB: target hit");
            if (hhmm >= 1555)            return DecisionResult.ExitLong("IB: fim de sessão");
        }
        else
        {
            if (bar.Close >= stopShort) return DecisionResult.ExitShort("IB: stop hit");
            if (bar.Close <= entryPrice - ibRange * p.IbTargetMultiplier)
                                         return DecisionResult.ExitShort("IB: target hit");
            if (hhmm >= 1555)            return DecisionResult.ExitShort("IB: fim de sessão");
        }
        return DecisionResult.Hold("IB: em posição");
    }

    // Entrada — apenas um trade por dia
    var alreadyTradedToday = bars
        .Take(index)
        .Any(b => DateOnly.FromDateTime(b.Time) == todayDate
               && b.Time.Hour * 100 + b.Time.Minute >= 1030);
    // Simplificação: verificar se já houve entry hoje buscando no histórico de posição
    // (o backtester rastreia isso externamente — aqui usamos a flag de position == 0 como proxy)

    // Long breakout: fechamento acima do IB High
    if (bar.Close > ibHigh && volumeOk)
        return DecisionResult.EnterLong($"IB: breakout HIGH ib={ibRangeRatio:F2}");

    // Short breakout: fechamento abaixo do IB Low
    if (bar.Close < ibLow && volumeOk)
        return DecisionResult.EnterShort($"IB: breakout LOW ib={ibRangeRatio:F2}");

    return DecisionResult.Hold($"IB: aguardando breakout (H={ibHigh:F2} L={ibLow:F2})");
}
```

> **Nota de implementação:** O backtester já controla `1 trade por dia` via flag interna. Se o seu `StrategyBacktester` não tiver essa flag, adicione um bool `_tradedToday` que reseta quando `DateOnly.FromDateTime(bar.Time)` muda entre barras consecutivas.

---



### Arquivo: `src/TradingBrain.Console/GridSearchRunner.cs`

Localize o método `BuildParameterGrid` (ou equivalente) e adicione o case:

```csharp
case StrategyKind.IbBreakout:
    // 4 × 2 × 2 × 2 = 32 combinações
    foreach (var targetMult  in new[] { 0.5, 1.0, 1.5, 2.0 })
    foreach (var halfStop    in new[] { false, true })
    foreach (var minRatio    in new[] { 0.30, 0.50 })
    foreach (var requireVol  in new[] { false, true })
        yield return new StrategyTuningParams
        {
            IbTargetMultiplier = targetMult,
            IbUseHalfRangeStop = halfStop,
            IbMinRangeRatio    = minRatio,
            IbMaxRangeRatio    = 1.80,
            IbRequireVolume    = requireVol
        };
    break;
```

---



### Arquivo: `src/TradingBrain.Core/PrecomputedSeries.cs`

A lógica de IbBreakout usa `series.Atr14[index]` — a série de ATR já calculada nas barras de 5min. **Não é necessária nenhuma mudança** se `Atr14` já existe em `PrecomputedSeries`. Confirme que a série está disponível antes de prosseguir.

Se não existir: adicione o campo e popule no método `From()` usando `TechnicalIndicators.Atr(bars, 14)`.

---



### Criar `tests/TradingBrain.Tests/IbBreakoutStrategyTests.cs`

```csharp
using TradingBrain.Core;

namespace TradingBrain.Tests;

public class IbBreakoutStrategyTests
{
    private static List<MarketBar> MakeDay(
        DateTime date,
        double ibHigh, double ibLow,
        double postIbClose,
        bool longBreakout = true)
    {
        var bars = new List<MarketBar>();
        var price = (ibHigh + ibLow) / 2;

        // Barras IB: 9:30-10:25 (12 barras de 5min)
        for (var min = 0; min <= 55; min += 5)
        {
            var t = date.AddHours(9).AddMinutes(30 + min);
            bars.Add(new MarketBar(t, price, ibHigh, ibLow, price, 1000));
        }

        // Primeira barra pós-IB: 10:30 — breakout
        var breakoutClose = longBreakout ? ibHigh + 5 : ibLow - 5;
        bars.Add(new MarketBar(date.AddHours(10).AddMinutes(30),
            price, breakoutClose + 2, ibLow, breakoutClose, 2000));

        // Barras restantes até 16:00
        for (var min = 35; min <= 390; min += 5)
        {
            var t = date.AddHours(10).AddMinutes(min);
            bars.Add(new MarketBar(t, price, ibHigh + 10, ibLow - 5, postIbClose, 800));
        }

        return bars;
    }

    [Fact]
    public void IbBreakout_IsRegisteredInStrategyRegimeMap()
    {
        Assert.True(StrategyRegimeMap.HasFilter(StrategyKind.IbBreakout));
        var regimes = StrategyRegimeMap.For(StrategyKind.IbBreakout);
        Assert.Contains(MarketRegime.Breakout, regimes);
        Assert.Contains(MarketRegime.Trend, regimes);
        Assert.DoesNotContain(MarketRegime.NonTrend, regimes);
        Assert.DoesNotContain(MarketRegime.Range, regimes);
    }

    [Fact]
    public void IbBreakout_DefaultParams_AreValid()
    {
        var p = new StrategyTuningParams();
        Assert.True(p.IbTargetMultiplier > 0);
        Assert.True(p.IbMinRangeRatio >= 0);
        Assert.True(p.IbMaxRangeRatio > p.IbMinRangeRatio);
    }

    [Fact]
    public void IbBreakout_GridSearch_Produces32Combinations()
    {
        // Verifica que o grid gera exatamente 32 combos (4×2×2×2)
        var results = GridSearchRunner.BuildParameterGrid(StrategyKind.IbBreakout).ToList();
        Assert.Equal(32, results.Count);
    }
}
```

> Ajuste o namespace e o método `BuildParameterGrid` conforme a visibilidade no seu projeto (pode precisar ser `internal` ou `public`).

---

## FASE 9 — Atualizar `CLAUDE.md`

No arquivo `CLAUDE.md` na raiz do repositório:

1. Remover `SessionBreakout` e `GoldBreakout` da tabela de strategies
2. Adicionar `IbBreakout` com regime `Breakout + Trend`
3. Atualizar contagem de strategies de 10 para 9
4. Atualizar a seção "Próximos passos" — marcar IbBreakout como ✅ após concluir

---



```bash
# 1. Build
dotnet build ./TradingBrain.slnx

# 2. Todos os testes (64 existentes + novos)
dotnet test ./TradingBrain.slnx

# 3. Backtest simples para verificar que a strategy roda
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  outputs/tv-bars/mnq_5m_12mo.csv outputs/ib-breakout-smoke/ --strategy IbBreakout \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 4. Grid search com filtro de regime (IbBreakout só roda em Breakout + Trend)
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --grid-search outputs/tv-bars/mnq_5m_12mo.csv outputs/grid-ib-breakout/ IbBreakout \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 5. Walk-forward com regime filter
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --walk-forward outputs/tv-bars/mnq_5m_12mo.csv outputs/wf-ib-breakout/ IbBreakout \
  --windows 3 \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
```

### Saída esperada do backtest simples (smoke test)

```
Strategy    : IbBreakout
ClosedTrades: > 0 (em 14 meses de Breakout+Trend days, esperamos 30-80 trades)
NetPnL      : qualquer valor — confirmar que não há exception
```

Se `ClosedTrades = 0`: provavelmente o filtro de volume ou range está muito restritivo. Teste com `IbRequireVolume=false` e `IbMinRangeRatio=0.0` para isolar.

---

## Resumo das regras de negócio

| Parâmetro | Significado | Grid values |
|---|---|---|
| `IbTargetMultiplier` | Target = entrada ± ibRange × N | 0.5, 1.0, 1.5, 2.0 |
| `IbUseHalfRangeStop` | Stop no midpoint (true) ou no extremo oposto (false) | false, true |
| `IbMinRangeRatio` | IB/ATR14 mínimo (filtra IBs triviais) | 0.30, 0.50 |
| `IbMaxRangeRatio` | IB/ATR14 máximo (filtra IBs muito largos) | 1.80 (fixo) |
| `IbRequireVolume` | Bar de breakout deve ter volume > média do IB | false, true |

**Regras invariantes (não são parâmetros):**
- IB window: sempre 9:30-10:25 ET (12 barras de 5min, períodos A e B do Market Profile)
- Entrada: apenas após 10:30 ET, não após 14:00 ET
- Confirmação: fechamento de barra, nunca wick
- Exit forçado: 15:55 ET
- 1 trade por dia máximo

---

## Arquivos a criar/modificar

| Arquivo | Ação |
|---|---|
| `src/TradingBrain.Core/StrategyKind.cs` | Remover `SessionBreakout` e `GoldBreakout`; adicionar `IbBreakout` |
| `src/TradingBrain.Core/StrategyRegimeMap.cs` | Remover entradas antigas; adicionar `IbBreakout → [Breakout, Trend]` |
| `src/TradingBrain.Core/BacktestModels.cs` | Adicionar 5 campos `Ib*` ao `StrategyTuningParams` |
| `src/TradingBrain.Console/StrategyRules.cs` | Remover cases de SessionBreakout/GoldBreakout; implementar `EvaluateIbBreakout()` |
| `src/TradingBrain.Console/GridSearchRunner.cs` | Remover grids antigos; adicionar 32 combos para `IbBreakout` |
| `tests/TradingBrain.Tests/` | Remover testes de SessionBreakout/GoldBreakout se existirem |
| `tests/TradingBrain.Tests/IbBreakoutStrategyTests.cs` | Criar — 3 testes básicos |
| `CLAUDE.md` | Atualizar tabela: remover strategies depreciadas, adicionar IbBreakout |

---

*Gerado em: 2026-05-16 | Após esta fase, próximos passos: validação estatística do IB classifier (directionality Trend > Range nos 82k dados), mais dados 2024, e SchoolRun anti-mode.*

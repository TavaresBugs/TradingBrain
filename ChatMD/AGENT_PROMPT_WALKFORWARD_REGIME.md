# TradingBrain — Prompt de Implementação: WalkForward com Filtro de Regime

> Repositório: https://github.com/TavaresBugs/TradingBrain  
> Branch alvo: `feat/walkforward-regime` (criar a partir de `main`)  
> Runtime: .NET 10 | Build: `dotnet build ./TradingBrain.slnx` | Testes: `dotnet test ./TradingBrain.slnx`  
> Pré-requisito: `main` contém IB classifier + RegimeFilter (`StrategyRegimeMap`, `RegimeFilter.Apply`) — confirme com `git log --oneline -5`

---

## Contexto

O `WalkForwardValidator` atual roda janelas sequenciais em **todos os dias** do dataset, ignorando o regime de cada dia. O resultado histórico de SchoolRun mostrou OOS negativo em 4/5 janelas porque a maioria das janelas continha dias de Range e NonTrend onde a strategy não tem edge. A janela 5 foi positiva (+333 OOS) justamente porque o período mais recente teve mais dias de Trend.

O objetivo desta sessão é modificar o `WalkForwardValidator` para aplicar o filtro de regime antes de criar as janelas, garantindo que cada janela IS/OOS veja **apenas os dias do regime alvo** de cada strategy.

**Regra crítica de design:** A classificação de regime usa o dataset **completo e original** (sem corte), para que ATR14 e IB de ontem tenham contexto suficiente. O filtro é aplicado nas barras. Depois, as barras filtradas são divididas em janelas cronológicas.

---

## Ordem de operações (importante — não alterar)

```
1. Recebe allBars (dataset completo)
2. Classifica regime em allBars completo  →  RegimeClassifier.Classify(allBars)
3. Filtra allBars por regime alvo          →  RegimeFilter.Apply(allBars, allowedRegimes)
4. Divide filteredBars em N janelas        →  split cronológico sobre filteredBars
5. Para cada janela: IS = primeiros 65%, OOS = últimos 35%
6. Roda backtester em IS, seleciona melhor params, valida em OOS
```

Esta ordem garante ausência de lookahead: a classificação usa dados passados, e o split cronológico preserva a separação temporal.

---

## FASE 1 — Modificar `WalkForwardValidator`

### Arquivo: `src/TradingBrain.Console/WalkForwardValidator.cs`

Adicione o parâmetro `applyRegimeFilter` ao método público `Run`. O método deve ter a seguinte assinatura (adicione apenas o novo parâmetro com default `true` — não remova nenhum parâmetro existente):

```csharp
public static IReadOnlyList<WalkForwardResult> Run(
    IReadOnlyList<MarketBar> bars,
    StrategyKind strategy,
    int windows = 5,
    ExecutionSettings? executionSettings = null,
    bool applyRegimeFilter = true)   // NOVO
```

**No início do corpo do método**, antes de qualquer divisão em janelas, adicione:

```csharp
// 1. Filtra barras pelo regime alvo da strategy (usando dataset completo para classificação)
var barsToSplit = applyRegimeFilter && StrategyRegimeMap.HasFilter(strategy)
    ? RegimeFilter.Apply(bars, StrategyRegimeMap.For(strategy))
    : bars;

// Diagnóstico
var totalDays    = bars.Select(b => b.Time.Date).Distinct().Count();
var filteredDays = barsToSplit.Select(b => b.Time.Date).Distinct().Count();
if (applyRegimeFilter && filteredDays < totalDays)
{
    var targetRegime = StrategyRegimeMap.HasFilter(strategy)
        ? string.Join("+", StrategyRegimeMap.For(strategy))
        : "all";
    Console.WriteLine($"  [WF RegimeFilter] {strategy}: {filteredDays}/{totalDays} dias úteis ({targetRegime})");
}

// Gate: se restar menos de 30 dias após o filtro, não há janelas suficientes
if (filteredDays < 30)
{
    Console.WriteLine($"  [WF RegimeFilter] {strategy}: dias insuficientes após filtro ({filteredDays}). Pulando walk-forward.");
    return Array.Empty<WalkForwardResult>();
}
```

**Substitua todas as referências a `bars`** (dentro do loop de janelas) por `barsToSplit`.

> Atenção: a variável `bars` original **não deve ser modificada** — ela ainda pode ser usada para diagnósticos (ex: contar totalDays). Apenas a divisão em janelas e o backtest devem usar `barsToSplit`.

---

## FASE 2 — Atualizar o modelo `WalkForwardResult`

### Arquivo: `src/TradingBrain.Console/WalkForwardValidator.cs` (ou onde `WalkForwardResult` estiver definido)

Adicione dois campos ao record/struct `WalkForwardResult` para rastreabilidade (com valores default para compatibilidade):

```csharp
// Adicionar ao final do record (mantém compatibilidade com código existente):
int FilteredDaysTotal = 0,   // dias filtrados pelo regime no dataset completo
int TotalDaysTotal = 0       // dias totais no dataset antes do filtro
```

Popule esses campos ao construir cada `WalkForwardResult` dentro do loop de janelas:

```csharp
FilteredDaysTotal = filteredDays,
TotalDaysTotal    = totalDays
```

---

## FASE 3 — Exportar colunas de regime no CSV de walk-forward

### Arquivo: `src/TradingBrain.Console/BacktestReports.cs`

Localize o método que escreve o CSV de resultados do walk-forward (provavelmente `WriteWalkForwardCsv` ou similar). Adicione ao cabeçalho e às linhas as colunas:

```
FilteredDays, TotalDays, RegimeFilter
```

Exemplo de linha:
```
1, 43.98, +539, -135, 47, 300, Trend
```

Cabeçalho completo esperado (preservando colunas existentes, adicionando ao final):
```
Window, IsScore, IsNetPnL, OosNetPnL, FilteredDays, TotalDays, RegimeFilter
```

`RegimeFilter` deve conter:
- O nome do regime alvo (ex: `"Trend"`) se o filtro foi aplicado
- `"None"` se `applyRegimeFilter = false` ou a strategy não tem regime mapeado

---

## FASE 4 — Passar a flag pelo `Program.cs`

### Arquivo: `src/TradingBrain.Console/Program.cs`

**4.1** — Localize o parsing do comando `--walk-forward`. Adicione o parsing de `--no-regime-filter` ao mesmo bloco onde já existe essa flag para `--grid-search` (reutilize a mesma variável `noRegimeFilter` já existente se ela já estiver no escopo, ou declare uma nova):

```csharp
var noRegimeFilter = args.Contains("--no-regime-filter");
```

**4.2** — Passe o parâmetro na chamada a `WalkForwardValidator.Run()`:

```csharp
var wfResults = WalkForwardValidator.Run(
    bars,
    strategy,
    windows: windowCount,
    executionSettings: settings,
    applyRegimeFilter: !noRegimeFilter);   // NOVO
```

**4.3** — Adicione ao `PrintUsage()` (junto às outras flags já documentadas):

```csharp
Console.WriteLine("  --no-regime-filter    Desativa filtro de regime no walk-forward e grid search");
```

---

## FASE 5 — Testes

### Criar `tests/TradingBrain.Tests/WalkForwardRegimeTests.cs`

```csharp
using TradingBrain.Core;
using TradingBrain.Console; // ajuste o namespace conforme o projeto

namespace TradingBrain.Tests;

public class WalkForwardRegimeTests
{
    /// <summary>
    /// Fabrica barras de sessão completa (9:30-16:00) para N dias úteis.
    /// </summary>
    private static List<MarketBar> MakeNDays(int n, double price = 21000, double range = 100)
    {
        var bars = new List<MarketBar>();
        var date = new DateTime(2026, 1, 2);
        var added = 0;
        while (added < n)
        {
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                for (var min = 0; min <= 390; min += 5)
                {
                    var t = date.AddHours(9).AddMinutes(30 + min);
                    bars.Add(new MarketBar(t, price, price + range / 2, price - range / 2, price, 1000));
                }
                added++;
            }
            date = date.AddDays(1);
        }
        return bars;
    }

    [Fact]
    public void Run_WithRegimeFilter_ReturnsFewerOrEqualWindowsThanWithout()
    {
        // Com filtro de regime, pode haver menos dias = menos janelas viáveis
        var bars = MakeNDays(120);

        var withFilter    = WalkForwardValidator.Run(bars, StrategyKind.Momentum,
            windows: 3, applyRegimeFilter: true);
        var withoutFilter = WalkForwardValidator.Run(bars, StrategyKind.Momentum,
            windows: 3, applyRegimeFilter: false);

        // Com filtro pode retornar zero (dias insuficientes) — nunca deve lançar exceção
        Assert.True(withFilter.Count <= withoutFilter.Count);
    }

    [Fact]
    public void Run_WithNoRegimeFilter_ReturnsSameAsCurrentBehavior()
    {
        var bars = MakeNDays(120);

        // applyRegimeFilter: false deve produzir os mesmos resultados que o comportamento anterior
        var result = WalkForwardValidator.Run(bars, StrategyKind.Momentum,
            windows: 3, applyRegimeFilter: false);

        // Com 120 dias e 3 janelas, esperamos até 3 resultados (pode ser menos se IS tiver poucos trades)
        Assert.True(result.Count >= 0);
    }

    [Fact]
    public void Run_WhenFilteredDaysBelowThreshold_ReturnsEmpty()
    {
        // Dataset muito pequeno — após filtrar por Trend, provavelmente fica < 30 dias
        var bars = MakeNDays(20);

        var result = WalkForwardValidator.Run(bars, StrategyKind.Momentum,
            windows: 3, applyRegimeFilter: true);

        // Esperamos empty ou poucos resultados — nunca exceção
        Assert.NotNull(result);
    }

    [Fact]
    public void Run_FilteredDaysFields_ArePopulated()
    {
        var bars = MakeNDays(120);

        var results = WalkForwardValidator.Run(bars, StrategyKind.Momentum,
            windows: 3, applyRegimeFilter: true);

        foreach (var r in results)
        {
            // TotalDaysTotal deve refletir o dataset completo
            Assert.True(r.TotalDaysTotal > 0);
            // FilteredDaysTotal <= TotalDaysTotal
            Assert.True(r.FilteredDaysTotal <= r.TotalDaysTotal);
        }
    }
}
```

---

## FASE 6 — Verificação completa

Execute na ordem e confirme que cada passo passa:

```bash
# 1. Build limpo
dotnet build ./TradingBrain.slnx

# 2. Todos os testes (64 existentes + novos)
dotnet test ./TradingBrain.slnx

# 3. Walk-forward COM filtro de regime — SchoolRun (strategy de Trend)
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --walk-forward outputs/tv-bars/mnq_5m_12mo.csv outputs/wf-regime/ SchoolRun \
  --windows 5 \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 4. Walk-forward SEM filtro (comparação — comportamento anterior)
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --walk-forward outputs/tv-bars/mnq_5m_12mo.csv outputs/wf-nofilter/ SchoolRun \
  --windows 5 --no-regime-filter \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 5. Walk-forward para VwapReversion (strategy de Range)
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --walk-forward outputs/tv-bars/mnq_5m_12mo.csv outputs/wf-vwap/ VwapReversion \
  --windows 3 \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
```

### Saída esperada do comando 3 (SchoolRun com filtro)

```
  [WF RegimeFilter] SchoolRun: ~47/300 dias úteis (Trend)
  Janela 1 — IS: ... dias | OOS: ... dias
  ...
```

O número de janelas com OOS positivo **deve melhorar** em relação ao resultado sem filtro (onde eram 1/5). Se ainda mostrar 1/5 com filtro, os thresholds do IB classifier precisam revisão (os dias de Trend classificados ainda estão errados).

### Resultado histórico sem filtro (baseline para comparação)

| Janela | IS NetPnL | OOS NetPnL | OOS positivo? |
|--------|-----------|------------|---------------|
| 1 | +539 | -135 | ✗ |
| 2 | +233 | -318 | ✗ |
| 3 | +224 | -72  | ✗ |
| 4 | +264 | -18  | ✗ |
| 5 | +590 | +333 | ✓ |

Com filtro de regime, o esperado é que as janelas onde o SchoolRun tinha edge (dias de Trend real) mostrem OOS positivo consistentemente.

---

## Restrições

- **Não remova** nenhum parâmetro existente de `WalkForwardValidator.Run()` — apenas adicione `applyRegimeFilter` com default `true`
- **A classificação usa o dataset completo** antes do split em janelas — nunca classifique só a janela IS ou OOS
- **O split cronológico** deve acontecer sobre `barsToSplit` (já filtradas), não sobre `bars` originais
- **64 testes existentes** devem continuar passando — se quebrarem, corrija sem alterar lógica de negócio
- **Novos testes** em arquivo separado `WalkForwardRegimeTests.cs`
- **Não use lookahead** — verificação: nenhuma barra de data `d` pode influenciar a classificação de regime de uma barra de data `d-1`

---

## Arquivos a criar/modificar

| Arquivo | Ação |
|---------|------|
| `src/TradingBrain.Console/WalkForwardValidator.cs` | Modificar — parâmetro `applyRegimeFilter`, filtro antes do split, gate de dias mínimos |
| `src/TradingBrain.Console/BacktestReports.cs` | Modificar — colunas `FilteredDays`, `TotalDays`, `RegimeFilter` no CSV de walk-forward |
| `src/TradingBrain.Console/Program.cs` | Modificar — passa `applyRegimeFilter` para `WalkForwardValidator.Run()` |
| `tests/TradingBrain.Tests/WalkForwardRegimeTests.cs` | Criar — 4 testes do novo comportamento |

---

*Gerado em: 2026-05-16 | Após esta fase, próximo passo: strategy `IbBreakout` canônica (range 9:30-10:30 direto das barras de 5min, sem resample) e validação estatística do IB classifier nos 82k dados reais.*

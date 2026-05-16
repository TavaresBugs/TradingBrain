# TradingBrain — Prompt de Implementação: RegimeFilter no GridSearch

> Repositório: https://github.com/TavaresBugs/TradingBrain  
> Branch alvo: `feat/regime-filter` (criar a partir de `main`)  
> Runtime: .NET 10 | Build: `dotnet build ./TradingBrain.slnx` | Testes: `dotnet test ./TradingBrain.slnx`  
> Pré-requisito: `main` contém o IB classifier (`d5358b9`) — confirme com `git log --oneline -3`

---

## Contexto

O `RegimeClassifier` agora classifica cada dia corretamente por tipo (Trend, Range, Breakout, HighVolatility, NonTrend). O problema atual é que o `GridSearchRunner` ignora completamente o regime — ele passa **todos os dias** para o backtester de qualquer strategy. Isso contamina os resultados: a `VwapReversion` (strategy de Range) é penalizada por tentar operar em dias de Trend, onde sua lógica de reversão não funciona.

O objetivo desta sessão é implementar o **RegimeFilter**: antes de rodar o grid search de cada strategy, filtrar as barras para incluir apenas os dias do regime alvo daquela strategy.

---

## Regra de mapeamento Strategy → Regime

Este mapeamento é fixo e reflete a lógica de mercado de cada strategy:

| StrategyKind | Regimes permitidos |
|---|---|
| `Momentum` | `Trend` |
| `Ema` | `Trend` |
| `Trend` | `Trend` |
| `GoldBreakout` | `Breakout` |
| `Range` | `Range` |
| `Volatility` | `HighVolatility` |

**Regra universal:** `NonTrend` é **sempre excluído** de todas as strategies — nesses dias o mercado não tem direção nem volatilidade útil.

`Undefined` é **incluído por padrão** nos dias de estratégias que não têm um regime claro para ele, para não reduzir demais o dataset. Isso é ajustável via parâmetro.

---

## FASE 1 — `StrategyRegimeMap` no Core

### 1.1 — Criar `src/TradingBrain.Core/StrategyRegimeMap.cs`

```csharp
namespace TradingBrain.Core;

/// <summary>
/// Define os regimes de mercado permitidos para cada strategy.
/// Usado pelo RegimeFilter para restringir o dataset antes do backtest.
/// </summary>
public static class StrategyRegimeMap
{
    private static readonly IReadOnlyDictionary<StrategyKind, IReadOnlyList<MarketRegime>> Map =
        new Dictionary<StrategyKind, IReadOnlyList<MarketRegime>>
        {
            [StrategyKind.Momentum]    = new[] { MarketRegime.Trend },
            [StrategyKind.Ema]         = new[] { MarketRegime.Trend },
            [StrategyKind.Trend]       = new[] { MarketRegime.Trend },
            [StrategyKind.GoldBreakout]= new[] { MarketRegime.Breakout },
            [StrategyKind.Range]       = new[] { MarketRegime.Range },
            [StrategyKind.Volatility]  = new[] { MarketRegime.HighVolatility },
        };

    /// <summary>
    /// Retorna os regimes permitidos para a strategy.
    /// Nunca inclui NonTrend.
    /// </summary>
    public static IReadOnlyList<MarketRegime> For(StrategyKind strategy)
        => Map.TryGetValue(strategy, out var regimes)
            ? regimes
            : Array.Empty<MarketRegime>();

    /// <summary>
    /// Retorna true se a strategy tem regime(s) definido(s).
    /// </summary>
    public static bool HasFilter(StrategyKind strategy)
        => Map.ContainsKey(strategy);
}
```

---

## FASE 2 — `RegimeFilter` utilitário no Core

### 2.1 — Criar `src/TradingBrain.Core/RegimeFilter.cs`

```csharp
namespace TradingBrain.Core;

/// <summary>
/// Filtra uma lista de barras para incluir apenas os dias cujo regime
/// está na lista de regimes permitidos.
/// A classificação de regime usa TODAS as barras (sem corte), para que
/// o ATR14 e o IB de ontem sejam calculados com contexto completo.
/// </summary>
public static class RegimeFilter
{
    /// <summary>
    /// Classifica os dias do dataset completo e devolve apenas as barras
    /// dos dias que pertencem a um dos <paramref name="allowedRegimes"/>.
    /// NonTrend é sempre excluído, independente de allowedRegimes.
    /// </summary>
    public static IReadOnlyList<MarketBar> Apply(
        IReadOnlyList<MarketBar> allBars,
        IReadOnlyList<MarketRegime> allowedRegimes)
    {
        if (allowedRegimes.Count == 0)
            return allBars;

        // Classifica usando o dataset completo — sem lookahead
        var regimes = RegimeClassifier.Classify(allBars);

        // Monta o set de datas permitidas
        var allowedDates = new HashSet<DateOnly>(
            regimes
                .Where(r => r.Regime != MarketRegime.NonTrend
                         && allowedRegimes.Contains(r.Regime))
                .Select(r => r.Date));

        // Filtra as barras mantendo a ordem cronológica
        return allBars
            .Where(b => allowedDates.Contains(DateOnly.FromDateTime(b.Time.Date)))
            .ToList();
    }

    /// <summary>
    /// Conta quantos dias distintos existem por regime no dataset.
    /// Útil para diagnóstico antes de rodar o grid.
    /// </summary>
    public static IReadOnlyDictionary<MarketRegime, int> CountDaysByRegime(
        IReadOnlyList<MarketBar> allBars)
    {
        var regimes = RegimeClassifier.Classify(allBars);
        return regimes
            .GroupBy(r => r.Regime)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
```

---

## FASE 3 — Integração no `GridSearchRunner`

### 3.1 — Modificar `GridSearchRunner.Run()` em `src/TradingBrain.Console/GridSearchRunner.cs`

Adicione um parâmetro opcional `applyRegimeFilter` ao método `Run`. Quando `true` (padrão), filtra as barras pelo regime alvo da strategy antes de rodar qualquer combinação de parâmetros.

**Assinatura nova:**
```csharp
public static IReadOnlyList<GridSearchResult> Run(
    IReadOnlyList<MarketBar> bars,
    StrategyKind strategy,
    ExecutionSettings? executionSettings = null,
    bool applyRegimeFilter = true)   // NOVO
```

**Corpo — adicione antes do loop `foreach (var parameters in ...)`:**
```csharp
// Filtra barras pelo regime alvo da strategy
var filteredBars = applyRegimeFilter && StrategyRegimeMap.HasFilter(strategy)
    ? RegimeFilter.Apply(bars, StrategyRegimeMap.For(strategy))
    : bars;

// Diagnóstico: quantos dias úteis restaram após o filtro
var totalDays = bars.Select(b => b.Time.Date).Distinct().Count();
var filteredDays = filteredBars.Select(b => b.Time.Date).Distinct().Count();
if (applyRegimeFilter && filteredDays < totalDays)
{
    Console.WriteLine($"  [RegimeFilter] {strategy}: {filteredDays}/{totalDays} dias após filtro de regime ({StrategyRegimeMap.For(strategy)[0]})");
}

// SUBSTITUA o uso de `bars` por `filteredBars` dentro do loop:
foreach (var parameters in BuildParameterGrid(strategy))
{
    var backtester = new StrategyBacktester(strategy, parameters);
    var rows = backtester.Run(filteredBars);   // <-- filteredBars, não bars
    ...
}
```

### 3.2 — Modificar `ValidateOutOfSample()` — mesmo parâmetro

```csharp
public static IReadOnlyList<GridSearchResult> ValidateOutOfSample(
    IReadOnlyList<MarketBar> bars,
    IReadOnlyList<GridSearchResult> winners,
    ExecutionSettings? executionSettings = null,
    bool applyRegimeFilter = true)   // NOVO
{
    var settings = executionSettings ?? ExecutionSettings.MnqDefault;
    var results = new List<GridSearchResult>();
    foreach (var winner in winners.Take(3))
    {
        // Filtra o OOS pelo mesmo regime
        var filteredBars = applyRegimeFilter && StrategyRegimeMap.HasFilter(winner.Strategy)
            ? RegimeFilter.Apply(bars, StrategyRegimeMap.For(winner.Strategy))
            : bars;

        var backtester = new StrategyBacktester(winner.Strategy, winner.Params);
        var summary = StrategyBacktester.Summarize(backtester.Run(filteredBars), settings)
            with { IsLabel = "OOS" };

        if (summary.ClosedTrades >= MinTradesOos)
        {
            results.Add(winner with { Summary = summary });
        }
    }
    return results;
}
```

### 3.3 — Ajustar gates de trades mínimos

Com filtro de regime, o número de dias cai drasticamente (ex: `HighVolatility` = 12 dias total). Os gates atuais (`>= 30` IS e `>= 15` OOS) vão rejeitar quase tudo.

Adicione constantes ajustáveis e revise a função `Score`:

```csharp
public const int MinTradesOos = 10;        // era 15
public const int MinTradesIsScore = 20;    // era 30 (hardcoded no Score)

private static double Score(BacktestSummary summary)
{
    if (summary.ClosedTrades < MinTradesIsScore)
        return double.NegativeInfinity;

    var pf  = Math.Min(summary.NetProfitFactor, 10.0);
    var rtd = Math.Clamp(summary.ReturnToDrawdown, -5.0, 20.0);
    var confidence = Math.Log10(summary.ClosedTrades + 1);
    return pf * summary.NetExpectancy * confidence * (1.0 + rtd * 0.1);
}
```

> **Atenção:** A fórmula do Score acima substitui a atual (`rtd * Log10(trades)`). Ela usa a fórmula completa já documentada no `TRADINGBRAIN_STATUS.md` (seção 4). Se já existe uma versão diferente da fórmula no arquivo atual, mantenha a versão existente e apenas ajuste o gate de `30` para `MinTradesIsScore`.

### 3.4 — Adicionar coluna `RegimeFilter` no CSV de output

No método `ExportCsv`, adicione `RegimeFilter` após `Strategy` no cabeçalho e nas linhas:

```csharp
// Cabeçalho — adicione "RegimeFilter," após "Strategy,"
writer.WriteLine("Strategy,RegimeFilter,Score,Trades,...");

// Linha — use o mapeamento para saber o regime alvo
var regimeLabel = StrategyRegimeMap.HasFilter(result.Strategy)
    ? string.Join("|", StrategyRegimeMap.For(result.Strategy).Select(r => r.ToString()))
    : "All";

writer.WriteLine(string.Join(",", result.Strategy, regimeLabel, F(Score(s)), ...));
```

---

## FASE 4 — Flag `--no-regime-filter` no `Program.cs`

Adicione uma flag de escape para poder rodar o grid sem filtro (útil para comparação):

```csharp
// Leitura da flag — adicionar ao ReadGridSearchRequest ou como flag independente
var noRegimeFilter = args.Any(a => a.Equals("--no-regime-filter", StringComparison.OrdinalIgnoreCase));
```

Passe `applyRegimeFilter: !noRegimeFilter` nas chamadas a `GridSearchRunner.Run()` e `ValidateOutOfSample()` dentro de `RunGridSearchForStrategy`.

Adicione ao `PrintUsage()`:
```csharp
Console.WriteLine("  --no-regime-filter    Desativa filtro de regime no grid search (roda em todos os dias)");
```

---

## FASE 5 — Diagnóstico automático no `--grid-search`

No início de `RunGridSearch` (em `Program.cs`), antes de rodar qualquer strategy, imprima a distribuição de regimes do dataset para que o usuário saiba com o que está trabalhando:

```csharp
// Adicionar ao início de RunGridSearch, após carregar as barras
var regimeCounts = RegimeFilter.CountDaysByRegime(bars);
Console.WriteLine("=== Distribuição de regimes no dataset ===");
var totalDays = regimeCounts.Values.Sum();
foreach (var (regime, count) in regimeCounts.OrderByDescending(kv => kv.Value))
{
    Console.WriteLine($"  {regime,-16} {count,4} dias  ({100.0 * count / totalDays:F1}%)");
}
Console.WriteLine($"  {"TOTAL",-16} {totalDays,4} dias");
Console.WriteLine();
```

---

## FASE 6 — Testes

### 6.1 — Criar `tests/TradingBrain.Tests/RegimeFilterTests.cs`

```csharp
using TradingBrain.Core;

namespace TradingBrain.Tests;

public class RegimeFilterTests
{
    // Fabrica barras simples para N dias com valores constantes
    private static List<MarketBar> MakeNDays(int n, double price = 21000, double range = 100)
    {
        var bars = new List<MarketBar>();
        var baseDate = new DateTime(2026, 1, 2); // sexta = dia útil
        for (var d = 0; d < n; d++)
        {
            var date = baseDate.AddDays(d);
            // Pula fins de semana
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;
            // Barras de sessão 9:30-16:00
            for (var h = 930; h <= 1600; h += 5)
            {
                var time = new DateTime(date.Year, date.Month, date.Day, h / 100, h % 100, 0);
                bars.Add(new MarketBar(time, price, price + range / 2, price - range / 2, price, 1000));
            }
        }
        return bars;
    }

    [Fact]
    public void Apply_WithEmptyAllowedRegimes_ReturnsAllBars()
    {
        var bars = MakeNDays(30);
        var result = RegimeFilter.Apply(bars, Array.Empty<MarketRegime>());
        Assert.Equal(bars.Count, result.Count);
    }

    [Fact]
    public void Apply_AlwaysExcludesNonTrendDays()
    {
        // Independente do allowedRegimes, NonTrend nunca deve aparecer
        var bars = MakeNDays(30);
        var result = RegimeFilter.Apply(bars, new[] { MarketRegime.NonTrend });
        // NonTrend está em allowedRegimes mas a regra universal remove NonTrend
        var resultDates = result.Select(b => DateOnly.FromDateTime(b.Time.Date)).Distinct().ToList();
        var regimes = RegimeClassifier.Classify(bars);
        var nonTrendDates = regimes.Where(r => r.Regime == MarketRegime.NonTrend).Select(r => r.Date).ToHashSet();
        Assert.True(resultDates.All(d => !nonTrendDates.Contains(d)),
            "Nenhum dia NonTrend deve aparecer no resultado filtrado");
    }

    [Fact]
    public void Apply_ReducesBarCount_WhenRegimeFiltered()
    {
        var bars = MakeNDays(60);
        var result = RegimeFilter.Apply(bars, new[] { MarketRegime.Trend });
        // Com filtro ativo, esperamos menos barras que o total
        Assert.True(result.Count <= bars.Count,
            "Filtro de regime não deve aumentar o número de barras");
    }

    [Fact]
    public void CountDaysByRegime_SumsToTotalDays()
    {
        var bars = MakeNDays(40);
        var counts = RegimeFilter.CountDaysByRegime(bars);
        var classifiedDays = counts.Values.Sum();
        // O classifier pula o primeiro dia (precisa de ATR14),
        // então o total de dias classificados <= dias no dataset
        var distinctDays = bars.Select(b => b.Time.Date).Distinct().Count();
        Assert.True(classifiedDays <= distinctDays);
        Assert.True(classifiedDays > 0);
    }

    [Fact]
    public void StrategyRegimeMap_AllStrategiesHaveMapping()
    {
        // Todas as strategies que têm lógica de regime devem estar no mapa
        Assert.True(StrategyRegimeMap.HasFilter(StrategyKind.Momentum));
        Assert.True(StrategyRegimeMap.HasFilter(StrategyKind.Ema));
        Assert.True(StrategyRegimeMap.HasFilter(StrategyKind.Trend));
        Assert.True(StrategyRegimeMap.HasFilter(StrategyKind.GoldBreakout));
        Assert.True(StrategyRegimeMap.HasFilter(StrategyKind.Range));
        Assert.True(StrategyRegimeMap.HasFilter(StrategyKind.Volatility));
    }

    [Fact]
    public void StrategyRegimeMap_NeverReturnsNonTrendAsTarget()
    {
        // Nenhuma strategy deve ter NonTrend como regime alvo
        foreach (var strategy in Enum.GetValues<StrategyKind>())
        {
            var regimes = StrategyRegimeMap.For(strategy);
            Assert.DoesNotContain(MarketRegime.NonTrend, regimes);
        }
    }
}
```

---

## FASE 7 — Verificação completa

Execute na ordem:

```bash
# 1. Build
dotnet build ./TradingBrain.slnx

# 2. Todos os testes (incluindo os novos)
dotnet test ./TradingBrain.slnx

# 3. Grid com filtro de regime (padrão — deve imprimir distribuição antes)
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --grid-search outputs/tv-bars/mnq_5m_12mo.csv outputs/grid-regime/ \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 4. Grid SEM filtro (comparação)
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --grid-search outputs/tv-bars/mnq_5m_12mo.csv outputs/grid-nofilter/ \
  --no-regime-filter \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 5. Grid para strategy específica com filtro
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --grid-search outputs/tv-bars/mnq_5m_12mo.csv outputs/grid-momentum/ Momentum \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
```

### Saída esperada do comando 3

```
=== Distribuição de regimes no dataset ===
  Range              164 dias  (54.5%)
  Trend               47 dias  (15.6%)
  Breakout            37 dias  (12.3%)
  Undefined           20 dias  ( 6.7%)
  NonTrend            20 dias  ( 6.7%)
  HighVolatility      12 dias  ( 4.0%)
  TOTAL              300 dias

  [RegimeFilter] Momentum: ~47/300 dias após filtro de regime (Trend)
  [RegimeFilter] Range:    ~164/300 dias após filtro de regime (Range)
  ...
```

### Sinais de sucesso

- A coluna `RegimeFilter` aparece no CSV com o valor correto por strategy
- O número de trades IS/OOS cai em relação ao grid sem filtro (esperado — menos dias)
- O `NetProfitFactor` e `NetExpectancy` das strategies melhoram (ou pelo menos deixam de ser diluídos pelos dias errados)
- Nenhum dia `NonTrend` aparece em nenhuma strategy

---

## Restrições

- **Não remova** nenhum parâmetro existente das assinaturas públicas — adicione apenas novos parâmetros opcionais com defaults
- **A classificação de regime sempre usa o dataset completo** — nunca o split IS ou OOS isolado. Isso garante que o ATR14 e o IB de ontem tenham contexto suficiente para os primeiros dias do período
- **O split IS/OOS deve acontecer ANTES do filtro de regime** — `DataSplit.SplitChronological(allBars)` primeiro, depois `RegimeFilter.Apply(split.InSample, ...)` e `RegimeFilter.Apply(split.OutSample, ...)` separadamente
- **58 testes existentes devem continuar passando**

---

## Arquivos a criar/modificar

| Arquivo | Ação |
|---|---|
| `src/TradingBrain.Core/StrategyRegimeMap.cs` | Criar |
| `src/TradingBrain.Core/RegimeFilter.cs` | Criar |
| `src/TradingBrain.Console/GridSearchRunner.cs` | Modificar — parâmetro `applyRegimeFilter`, gates, coluna CSV |
| `src/TradingBrain.Console/Program.cs` | Modificar — flag `--no-regime-filter`, diagnóstico de distribuição |
| `tests/TradingBrain.Tests/RegimeFilterTests.cs` | Criar |

---

*Gerado em: 2026-05-16 | Após esta fase, o próximo passo é o WalkForward com filtro de regime e depois a strategy IbBreakout canônica.*

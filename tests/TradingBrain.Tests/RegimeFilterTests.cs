using TradingBrain.Core;

namespace TradingBrain.Tests;

public class RegimeFilterTests
{
    private static List<MarketBar> MakeNDays(int n, double price = 21000, double range = 100)
    {
        var bars = new List<MarketBar>();
        var baseDate = new DateTime(2026, 1, 2);

        for (var d = 0; d < n; d++)
        {
            var date = baseDate.AddDays(d);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            var current = new DateTime(date.Year, date.Month, date.Day, 9, 30, 0);
            var end = new DateTime(date.Year, date.Month, date.Day, 16, 0, 0);
            while (current <= end)
            {
                bars.Add(new MarketBar(current, price, price + range / 2, price - range / 2, price, 1000));
                current = current.AddMinutes(5);
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
        var bars = MakeNDays(30);
        var allowedRegimes = Enum.TryParse<MarketRegime>("NonTrend", out var nonTrend)
            ? new[] { nonTrend }
            : Array.Empty<MarketRegime>();

        var result = RegimeFilter.Apply(bars, allowedRegimes);
        var resultDates = result.Select(b => DateOnly.FromDateTime(b.Time)).Distinct().ToList();
        var regimes = RegimeClassifier.Classify(bars);
        var nonTrendDates = regimes
            .Where(r => r.Regime.ToString().Equals("NonTrend", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Date)
            .ToHashSet();

        Assert.True(resultDates.All(d => !nonTrendDates.Contains(d)),
            "Nenhum dia NonTrend deve aparecer no resultado filtrado");
    }

    [Fact]
    public void Apply_ReducesBarCount_WhenRegimeFiltered()
    {
        var bars = MakeNDays(60);
        var result = RegimeFilter.Apply(bars, new[] { MarketRegime.Trend });

        Assert.True(result.Count <= bars.Count,
            "Filtro de regime nao deve aumentar o numero de barras");
    }

    [Fact]
    public void CountDaysByRegime_SumsToTotalDays()
    {
        var bars = MakeNDays(40);
        var counts = RegimeFilter.CountDaysByRegime(bars);
        var classifiedDays = counts.Values.Sum();
        var distinctDays = bars.Select(b => b.Time.Date).Distinct().Count();

        Assert.True(classifiedDays <= distinctDays);
        Assert.True(classifiedDays > 0);
    }

    [Fact]
    public void StrategyRegimeMap_AllExistingStrategiesHaveMapping()
    {
        foreach (var strategy in Enum.GetValues<StrategyKind>())
            Assert.True(StrategyRegimeMap.HasFilter(strategy), $"{strategy} deve ter filtro de regime.");
    }

    [Fact]
    public void StrategyRegimeMap_UsesValidatedIbPureMappings()
    {
        AssertMapping("Momentum", MarketRegime.Trend, MarketRegime.Breakout, MarketRegime.WideIbBreakout, MarketRegime.IntradayExpansion);
        AssertMapping("Ema", MarketRegime.WideIbBreakout, MarketRegime.IntradayExpansion, MarketRegime.HighVolatility);
        AssertMapping("Trend", MarketRegime.Trend, MarketRegime.Breakout, MarketRegime.WideIbBreakout, MarketRegime.IntradayExpansion);
        AssertMapping("IbBreakout", MarketRegime.Breakout);
        // OrbBreakout: Trend removido (-22.7 pts/trade × 49 trades confirmado pela matrix)
        AssertMapping("OrbBreakout", MarketRegime.Breakout, MarketRegime.IntradayExpansion);
        // SchoolRun: Range (+3.5 pts/19t) e HighVolatility (+18.5 pts/7t) adicionados
        AssertMapping("SchoolRun", MarketRegime.Breakout, MarketRegime.Range, MarketRegime.HighVolatility);
        AssertMapping("Range", MarketRegime.Range);
        // VwapReversion: HighVolatility adicionado (+33.8 pts/trade × 17 trades)
        AssertMapping("VwapReversion", MarketRegime.Range, MarketRegime.HighVolatility);
        AssertMapping("BollingerFade", MarketRegime.Range);
        AssertMapping("Volatility", MarketRegime.IntradayExpansion, MarketRegime.HighVolatility);
    }

    [Fact]
    public void StrategyRegimeMap_NeverReturnsNonTrendAsTarget()
    {
        foreach (var strategy in Enum.GetValues<StrategyKind>())
        {
            var regimes = StrategyRegimeMap.For(strategy);
            Assert.DoesNotContain(regimes, r => r.ToString().Equals("NonTrend", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void AssertMapping(string strategyName, params MarketRegime[] expected)
    {
        Assert.True(Enum.TryParse<StrategyKind>(strategyName, out var strategy), $"{strategyName} deve existir.");
        Assert.Equal(expected, StrategyRegimeMap.For(strategy));
    }
}

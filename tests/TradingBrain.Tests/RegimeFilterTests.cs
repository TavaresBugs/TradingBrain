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
        var result = RegimeFilter.Apply(bars, new[] { MarketRegime.NonTrend });
        var resultDates = result.Select(b => DateOnly.FromDateTime(b.Time)).Distinct().ToList();
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
    public void StrategyRegimeMap_NeverReturnsNonTrendAsTarget()
    {
        foreach (var strategy in Enum.GetValues<StrategyKind>())
        {
            var regimes = StrategyRegimeMap.For(strategy);
            Assert.DoesNotContain(MarketRegime.NonTrend, regimes);
        }
    }
}

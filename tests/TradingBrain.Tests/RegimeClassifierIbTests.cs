using TradingBrain.Core;

namespace TradingBrain.Tests;

public class RegimeClassifierIbTests
{
    /// <summary>
    /// Fabrica barras de 5min para um dia inteiro com valores simples.
    /// </summary>
    private static List<MarketBar> MakeDayBars(
        DateTime date,
        double open,
        double high,
        double low,
        double close,
        int startHHmm = 930,
        int endHHmm = 1600,
        int stepMin = 5)
    {
        var bars = new List<MarketBar>();
        var current = new DateTime(date.Year, date.Month, date.Day,
            startHHmm / 100, startHHmm % 100, 0);
        var end = new DateTime(date.Year, date.Month, date.Day,
            endHHmm / 100, endHHmm % 100, 0);

        while (current <= end)
        {
            bars.Add(new MarketBar(current, open, high, low, close, 1000));
            current = current.AddMinutes(stepMin);
        }

        return bars;
    }

    [Fact]
    public void Classify_WhenOpenOutsideIBYestAndNarrowIB_ReturnsTrend()
    {
        var day1 = MakeDayBars(new DateTime(2026, 1, 2), 20950, 21000, 20900, 20980);
        var day2Open = MakeDayBars(
            new DateTime(2026, 1, 3), 21010, 21020, 21005, 21015,
            startHHmm: 930,
            endHHmm: 1000);
        var day2Rest = MakeDayBars(
            new DateTime(2026, 1, 3), 21015, 21050, 21010, 21045,
            startHHmm: 1005,
            endHHmm: 1600);

        var allBars = day1.Concat(day2Open).Concat(day2Rest).ToList();
        var result = RegimeClassifier.Classify(allBars);

        Assert.NotEmpty(result);
        var day2Regime = result.First(r => r.Date == DateOnly.FromDateTime(new DateTime(2026, 1, 3)));
        Assert.Equal(MarketRegime.Trend, day2Regime.Regime);
        Assert.True(day2Regime.OpenOutsideIbYest);
    }

    [Fact]
    public void Classify_WhenOpenInsideIBAndNormalWidth_ReturnsRange()
    {
        var day1 = MakeDayBars(new DateTime(2026, 1, 2), 21000, 21100, 20900, 21000);
        var day2 = MakeDayBars(new DateTime(2026, 1, 3), 21000, 21060, 20940, 21010);

        var allBars = day1.Concat(day2).ToList();
        var result = RegimeClassifier.Classify(allBars);

        Assert.NotEmpty(result);
        var day2Regime = result.First(r => r.Date == DateOnly.FromDateTime(new DateTime(2026, 1, 3)));
        Assert.Equal(MarketRegime.Range, day2Regime.Regime);
        Assert.False(day2Regime.OpenOutsideIbYest);
    }

    [Fact]
    public void Classify_WhenGapLargeAndNarrowIB_ReturnsBreakout()
    {
        var day1 = MakeDayBars(new DateTime(2026, 1, 2), 20950, 21050, 20950, 21000);
        var day2 = MakeDayBars(new DateTime(2026, 1, 3), 21100, 21110, 21090, 21105);

        var allBars = day1.Concat(day2).ToList();
        var result = RegimeClassifier.Classify(allBars);

        Assert.NotEmpty(result);
        var day2Regime = result.First(r => r.Date == DateOnly.FromDateTime(new DateTime(2026, 1, 3)));
        Assert.Equal(MarketRegime.Breakout, day2Regime.Regime);
    }

    [Fact]
    public void Classify_NewFields_ArePopulated()
    {
        var day1 = MakeDayBars(new DateTime(2026, 1, 2), 20950, 21050, 20900, 21000);
        var day2 = MakeDayBars(new DateTime(2026, 1, 3), 21000, 21060, 20950, 21010);

        var allBars = day1.Concat(day2).ToList();
        var result = RegimeClassifier.Classify(allBars);

        Assert.NotEmpty(result);
        var day2Regime = result.First(r => r.Date == DateOnly.FromDateTime(new DateTime(2026, 1, 3)));

        Assert.False(double.IsNaN(day2Regime.IbYestHigh));
        Assert.False(double.IsNaN(day2Regime.IbYestLow));
        Assert.True(day2Regime.IbYestHigh >= day2Regime.IbYestLow);

        Assert.False(double.IsNaN(day2Regime.IbToday30MinRatio));
        Assert.True(day2Regime.IbToday30MinRatio >= 0);
    }
}

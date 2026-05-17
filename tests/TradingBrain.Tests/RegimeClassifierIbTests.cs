using TradingBrain.Core;

namespace TradingBrain.Tests;

public class RegimeClassifierIbTests
{
    [Fact]
    public void Classify_IbNearZero_ReturnsNonTrend()
    {
        var bars = PureIbScenarioFactory.MakeDays(
            ibHighYest: 21100,
            ibLowYest: 21000,
            prevClose: 21050,
            openToday: 21051,
            ibHighToday: 21052,
            ibLowToday: 21051,
            cperiodHigh: 21052,
            cperiodLow: 21051,
            overnightHigh: 21055,
            overnightLow: 21048);

        var last = RegimeClassifier.Classify(bars).Last();

        Assert.Equal(MarketRegime.NonTrend, last.Regime);
    }

    [Fact]
    public void Classify_ExtremeOvernight_ReturnsHighVolatility()
    {
        var bars = PureIbScenarioFactory.MakeDays(
            ibHighYest: 21100,
            ibLowYest: 21000,
            prevClose: 21050,
            openToday: 21300,
            ibHighToday: 21350,
            ibLowToday: 21250,
            cperiodHigh: 21340,
            cperiodLow: 21260,
            overnightHigh: 21500,
            overnightLow: 21000,
            sessionTailHigh: 21480,
            sessionTailLow: 21250,
            sessionClose: 21470);

        var last = RegimeClassifier.Classify(bars).Last();

        Assert.Equal(MarketRegime.HighVolatility, last.Regime);
    }

    [Fact]
    public void Classify_DayRegime_HasAllPureIbFields()
    {
        var bars = PureIbScenarioFactory.MakeDays(
            ibHighYest: 21100,
            ibLowYest: 21000,
            prevClose: 21050,
            openToday: 21055,
            ibHighToday: 21080,
            ibLowToday: 21020,
            cperiodHigh: 21075,
            cperiodLow: 21025,
            overnightHigh: 21060,
            overnightLow: 21040);

        var last = RegimeClassifier.Classify(bars).Last();

        Assert.False(double.IsNaN(last.IbHighYest));
        Assert.False(double.IsNaN(last.IbLowYest));
        Assert.True(last.IbHighYest >= last.IbLowYest);
        Assert.True(last.IbFullYest >= 0);
        Assert.True(last.IbFullToday >= 0);
        Assert.True(last.Atr14 > 0);
    }
}

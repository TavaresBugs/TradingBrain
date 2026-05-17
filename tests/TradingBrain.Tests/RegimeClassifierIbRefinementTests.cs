using TradingBrain.Core;

namespace TradingBrain.Tests;

public class RegimeClassifierIbRefinementTests
{
    [Fact]
    public void DayRegime_HasPureIbFields()
    {
        var regime = new DayRegime(
            Date: DateOnly.FromDateTime(DateTime.Today),
            Regime: MarketRegime.Range,
            Reason: "test",
            IbHighYest: 21100,
            IbLowYest: 21000,
            IbFullYest: 0.5,
            IbFullToday: 0.7,
            OpenOutside: false,
            CperiodInside: true,
            OvernightRatio: 0.2,
            GapRatio: 0.1,
            Atr14: 200);

        Assert.Equal(0.7, regime.IbFullToday);
        Assert.True(regime.CperiodInside);
        Assert.False(regime.OpenOutside);
    }

    [Fact]
    public void Classify_OpenOutside_LowOvernight_LowGap_ReturnsTrend()
    {
        var bars = PureIbScenarioFactory.MakeDays(
            ibHighYest: 21100,
            ibLowYest: 21000,
            prevClose: 21080,
            openToday: 21120,
            ibHighToday: 21130,
            ibLowToday: 21115,
            cperiodHigh: 21128,
            cperiodLow: 21116,
            overnightHigh: 21125,
            overnightLow: 21080);

        var last = RegimeClassifier.Classify(bars).Last();

        Assert.True(last.OpenOutside);
        Assert.Equal(MarketRegime.Trend, last.Regime);
    }

    [Fact]
    public void Classify_OpenOutside_HighOvernight_ReturnsBreakout()
    {
        var bars = PureIbScenarioFactory.MakeDays(
            ibHighYest: 21100,
            ibLowYest: 21000,
            prevClose: 21050,
            openToday: 21160,
            ibHighToday: 21180,
            ibLowToday: 21150,
            cperiodHigh: 21178,
            cperiodLow: 21152,
            overnightHigh: 21200,
            overnightLow: 21050);

        var last = RegimeClassifier.Classify(bars).Last();

        Assert.Equal(MarketRegime.Breakout, last.Regime);
    }

    [Fact]
    public void Classify_OpenInside_CperiodInside_ReturnsRange()
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

        Assert.False(last.OpenOutside);
        Assert.True(last.CperiodInside);
        Assert.Equal(MarketRegime.Range, last.Regime);
    }
}

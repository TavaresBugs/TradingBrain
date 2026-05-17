using TradingBrain.Core;

namespace TradingBrain.Tests;

public class StrategyRegimeMapTests
{
    [Fact]
    public void Momentum_MapsOnlyToTrend()
    {
        var regimes = StrategyRegimeMap.For(StrategyKind.Momentum);

        Assert.Equal(new[] { MarketRegime.Trend }, regimes);
    }

    [Fact]
    public void RangeStrategy_StillMapsToRangeRegime()
    {
        var regimes = StrategyRegimeMap.For(StrategyKind.Range);

        Assert.Equal(new[] { MarketRegime.Range }, regimes);
    }

    [Fact]
    public void Volatility_MapsToBreakoutAndWideAndIntradayExpansion()
    {
        var regimes = StrategyRegimeMap.For(StrategyKind.Volatility);

        Assert.Contains(MarketRegime.Breakout, regimes);
        Assert.Contains(MarketRegime.WideIbBreakout, regimes);
        Assert.Contains(MarketRegime.IntradayExpansion, regimes);
        Assert.DoesNotContain(MarketRegime.HighVolatility, regimes);
    }

    [Fact]
    public void VwapReversion_StillCoversRange()
    {
        var regimes = StrategyRegimeMap.For(StrategyKind.VwapReversion);

        Assert.Contains(MarketRegime.Range, regimes);
    }

    [Fact]
    public void BollingerFade_StillCoversRange()
    {
        var regimes = StrategyRegimeMap.For(StrategyKind.BollingerFade);

        Assert.Contains(MarketRegime.Range, regimes);
    }
}

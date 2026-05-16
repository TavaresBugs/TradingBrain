using TradingBrain.ConsoleApp;
using TradingBrain.Core;

namespace TradingBrain.Tests;

public class IbBreakoutStrategyTests
{
    [Fact]
    public void IbBreakout_IsRegisteredInStrategyRegimeMap()
    {
        Assert.True(StrategyRegimeMap.HasFilter(StrategyKind.IbBreakout));

        var regimes = StrategyRegimeMap.For(StrategyKind.IbBreakout);
        Assert.Contains(MarketRegime.Breakout, regimes);
        Assert.Contains(MarketRegime.Trend, regimes);
        Assert.DoesNotContain(regimes, r => r.ToString().Equals("NonTrend", StringComparison.OrdinalIgnoreCase));
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
        var results = GridSearchRunner.BuildParameterGrid(StrategyKind.IbBreakout).ToList();

        Assert.Equal(32, results.Count);
    }
}

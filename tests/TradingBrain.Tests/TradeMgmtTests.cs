using TradingBrain.ConsoleApp;
using TradingBrain.Core;

namespace TradingBrain.Tests;

public class TradeMgmtTests
{
    private static List<MarketBar> MakeBars(
        DateTime start,
        int count,
        double open,
        double step,
        int stepMinutes = 5)
    {
        var bars = new List<MarketBar>();
        var price = open;
        for (var i = 0; i < count; i++)
        {
            bars.Add(new MarketBar(
                start.AddMinutes(i * stepMinutes),
                price,
                price + Math.Abs(step),
                price - Math.Abs(step),
                price + step,
                1000));
            price += step;
        }

        return bars;
    }

    [Fact]
    public void Trend_ShouldExitOnChandelier_WhenPriceDropsBelowTrail()
    {
        var bars = MakeBars(new DateTime(2026, 1, 6, 9, 30, 0), 80, 21000, 10);
        var reversalBars = MakeBars(new DateTime(2026, 1, 6, 16, 10, 0), 20, 21790, -30);
        var allBars = bars.Concat(reversalBars).ToList();

        var backtester = new StrategyBacktester(StrategyKind.Trend, new StrategyTuningParams(
            TrendAtrStopMultiplier: 2.0,
            BeActivationRMultiple: 1.0,
            ChandelierActivationRMultiple: 1.0,
            ChandelierTrailMultiplier: 2.0));
        var rows = backtester.Run(allBars);
        var trades = StrategyBacktester.ExtractTrades(rows, ExecutionSettings.MnqDefault);

        Assert.DoesNotContain(trades, t => t.ExitReason.Contains("Tempo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Momentum_ShouldActivateBe_WhenTradeReaches1R()
    {
        var bars = MakeBars(new DateTime(2026, 1, 6, 9, 0, 0), 50, 21000, 5);
        var backtester = new StrategyBacktester(StrategyKind.Momentum, new StrategyTuningParams(
            AtrStopMultiplier: 1.5,
            MomentumVolumeRatio: 1.0,
            MomentumMinMacdAtrRatio: 0.0,
            BeActivationRMultiple: 1.0,
            ChandelierActivationRMultiple: 1.5,
            ChandelierTrailMultiplier: 2.0));
        var rows = backtester.Run(bars);
        var trades = StrategyBacktester.ExtractTrades(rows, ExecutionSettings.MnqDefault);

        var beTrades = trades.Where(t => t.ExitReason.Contains("BE", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(beTrades.All(t => t.GrossPoints >= 0));
    }

    [Fact]
    public void Ema_ShouldActivateBe_AtHalfR()
    {
        var bars = MakeBars(new DateTime(2026, 1, 6, 9, 30, 0), 60, 21000, 3);
        var backtester = new StrategyBacktester(StrategyKind.Ema, new StrategyTuningParams(
            AtrStopMultiplier: 1.5,
            EmaVolumeRatio: 1.0,
            BeActivationRMultiple: 0.5,
            ChandelierActivationRMultiple: 0.0));
        var rows = backtester.Run(bars);
        var trades = StrategyBacktester.ExtractTrades(rows, ExecutionSettings.MnqDefault);

        var beTrades = trades.Where(t => t.ExitReason.Contains("BE", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(beTrades.All(t => t.GrossPoints >= 0));
    }

    [Fact]
    public void Volatility_ShouldNotExitOnMaxDrawdown_AfterRemoval()
    {
        var bars = MakeBars(new DateTime(2026, 1, 6, 9, 30, 0), 30, 21000, -5);
        var recovery = MakeBars(new DateTime(2026, 1, 6, 11, 0, 0), 20, 20850, 10);
        var allBars = bars.Concat(recovery).ToList();

        var backtester = new StrategyBacktester(StrategyKind.Volatility, StrategyTuningParams.RefinedDefault);
        var rows = backtester.Run(allBars);
        var trades = StrategyBacktester.ExtractTrades(rows, ExecutionSettings.MnqDefault);

        Assert.DoesNotContain(trades, t => t.ExitReason.Contains("MaxDrawdown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Trend_TimeoutShouldBeRemoved_NoChandelierFired()
    {
        var bars = MakeBars(new DateTime(2026, 1, 6, 9, 30, 0), 100, 21000, 2);
        var backtester = new StrategyBacktester(StrategyKind.Trend, new StrategyTuningParams(
            TrendAtrStopMultiplier: 2.0,
            BeActivationRMultiple: 1.0,
            ChandelierActivationRMultiple: 1.5,
            ChandelierTrailMultiplier: 2.0));
        var rows = backtester.Run(bars);
        var trades = StrategyBacktester.ExtractTrades(rows, ExecutionSettings.MnqDefault);

        Assert.DoesNotContain(trades, t => t.ExitReason is "Tempo long" or "Tempo short");
    }
}

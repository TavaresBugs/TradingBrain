using TradingBrain.ConsoleApp;
using TradingBrain.Core;

namespace TradingBrain.Tests;

public sealed class BacktestReportsTests
{
    [Fact]
    public void ExtractTrades_ComputesIntratradeExcursionsFromHighLow()
    {
        var rows = new[]
        {
            Row(0, SignalAction.Buy, "entry", position: 1, close: 100, high: 101, low: 100, stop: 96, target: 108),
            Row(1, SignalAction.None, "", position: 1, close: 105, high: 106, low: 99.5, stop: 96, target: 108),
            Row(2, SignalAction.Exit, "exit", position: 0, close: 104, high: 108, low: 98, stop: 0, target: 0)
        };

        var trade = Assert.Single(StrategyBacktester.ExtractTrades(rows));

        Assert.Equal(0, trade.EntryBarIndex);
        Assert.Equal(2, trade.ExitBarIndex);
        Assert.Equal(2, trade.BarsHeld);
        Assert.Equal(4, trade.RiskPoints);
        Assert.Equal(8, trade.MFEPoints);
        Assert.Equal(2, trade.MAEPoints);
        Assert.Equal(2, trade.MFER);
        Assert.Equal(0.5, trade.MAER);
        Assert.Equal(108, trade.BestFavorablePrice);
        Assert.Equal(98, trade.WorstAdversePrice);
        Assert.True(trade.HitHalfR);
        Assert.True(trade.HitOneR);
        Assert.True(trade.HitOneAndHalfR);
        Assert.True(trade.HitTwoR);
        Assert.False(trade.HitThreeR);
        Assert.True(trade.HitMinusHalfR);
        Assert.False(trade.HitMinusOneR);
        Assert.Equal(1, trade.BarsToHalfR);
        Assert.Equal(1, trade.BarsToOneR);
        Assert.Equal(1, trade.BarsToOneAndHalfR);
        Assert.Equal(2, trade.BarsToTwoR);
        Assert.Null(trade.BarsToThreeR);
        Assert.Equal(2, trade.BarsToMinusHalfR);
        Assert.Null(trade.BarsToMinusOneR);
    }

    private static StrategyBacktestRow Row(
        int index,
        SignalAction signal,
        string reason,
        int position,
        double close,
        double high,
        double low,
        double stop,
        double target)
    {
        var bar = new MarketBar(
            new DateTime(2024, 1, 2, 9, 30, 0).AddMinutes(index * 5),
            close,
            high,
            low,
            close,
            1000);

        return new StrategyBacktestRow(
            "TestStrategy",
            bar,
            signal,
            reason,
            position,
            position == 0 ? 0 : 100,
            0,
            0,
            0,
            0,
            new Dictionary<string, double>(),
            stop,
            target);
    }
}

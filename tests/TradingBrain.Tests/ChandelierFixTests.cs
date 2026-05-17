using TradingBrain.ConsoleApp;
using TradingBrain.Core;

namespace TradingBrain.Tests;

public class ChandelierFixTests
{
    private static List<MarketBar> MakeRisingBars(
        DateTime start,
        int count,
        double basePrice,
        double step)
    {
        var bars = new List<MarketBar>();
        var price = basePrice;
        for (var i = 0; i < count; i++)
        {
            bars.Add(new MarketBar(
                start.AddMinutes(i * 5),
                price,
                price + Math.Abs(step) * 2,
                price - 5,
                price + step,
                1000));
            price += step;
        }

        return bars;
    }

    [Fact]
    public void Trend_ChandelierStop_NeverBelowEntry_WhenBeActive()
    {
        var bars = MakeRisingBars(new DateTime(2026, 1, 6, 9, 30, 0), 120, 21000, 8);
        var backtester = new StrategyBacktester(StrategyKind.Trend, new StrategyTuningParams(
            TrendAtrStopMultiplier: 3.0,
            BeActivationRMultiple: 1.0,
            ChandelierActivationRMultiple: 0.75,
            ChandelierTrailMultiplier: 2.0));
        var rows = backtester.Run(bars);
        var trades = StrategyBacktester.ExtractTrades(rows, ExecutionSettings.MnqDefault);

        var chandelierTrades = trades.Where(t => t.ExitReason.Contains("Chandelier", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(chandelierTrades.All(t => t.GrossPoints >= -0.5),
            "Chandelier com BE ativo nao deve gerar perda");
    }

    [Fact]
    public void Momentum_ChandelierStop_NeverBelowEntry_WhenBeActive()
    {
        var bars = MakeRisingBars(new DateTime(2026, 1, 6, 9, 0, 0), 80, 21000, 6);
        var backtester = new StrategyBacktester(StrategyKind.Momentum, new StrategyTuningParams(
            AtrStopMultiplier: 1.2,
            BeActivationRMultiple: 0.75,
            ChandelierActivationRMultiple: 1.25,
            ChandelierTrailMultiplier: 2.0,
            MomentumVolumeRatio: 1.0,
            MomentumMinMacdAtrRatio: 0.0));
        var rows = backtester.Run(bars);
        var trades = StrategyBacktester.ExtractTrades(rows, ExecutionSettings.MnqDefault);

        var chandelierTrades = trades.Where(t => t.ExitReason.Contains("Chandelier", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(chandelierTrades.All(t => t.GrossPoints >= -0.5),
            "Chandelier com BE ativo nao deve gerar perda no Momentum");
    }

    [Fact]
    public void Range_TargetRatio_1x_ExitsBefore1x5()
    {
        var bars = MakeRisingBars(new DateTime(2026, 1, 6, 9, 30, 0), 60, 21000, 4);
        var backtester = new StrategyBacktester(StrategyKind.Range, new StrategyTuningParams(
            RangeCompressionRatio: 1.1,
            RangeTargetRatio: 1.0,
            BeActivationRMultiple: 0.0,
            ChandelierActivationRMultiple: 0.0));
        var rows = backtester.Run(bars);
        var trades = StrategyBacktester.ExtractTrades(rows, ExecutionSettings.MnqDefault);

        var tpTrades = trades.Where(t => t.ExitReason.Contains("TP", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(tpTrades.All(t => t.RMultiple <= 1.15),
            "Range com TargetRatio=1.0 nao deve ter RMultiple > 1.15");
    }

    [Fact]
    public void Range_TargetRatio_1x2_ExitsBeyond1x()
    {
        var bars = MakeRisingBars(new DateTime(2026, 1, 6, 9, 30, 0), 60, 21000, 4);
        var backtester = new StrategyBacktester(StrategyKind.Range, new StrategyTuningParams(
            RangeCompressionRatio: 1.1,
            RangeTargetRatio: 1.2,
            BeActivationRMultiple: 0.0,
            ChandelierActivationRMultiple: 0.0));
        var rows = backtester.Run(bars);
        var trades = StrategyBacktester.ExtractTrades(rows, ExecutionSettings.MnqDefault);

        var tpTrades = trades.Where(t => t.ExitReason.Contains("TP", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(tpTrades.All(t => t.RMultiple >= 1.1),
            "Range com TargetRatio=1.2 deve ter RMultiple >= 1.1");
    }

    [Fact]
    public void BollingerFade_TargetRatio_08_ExitsBeforeMiddleBand()
    {
        var bars = MakeRisingBars(new DateTime(2026, 1, 6, 9, 30, 0), 60, 21000, -3);
        var backtester = new StrategyBacktester(StrategyKind.BollingerFade, new StrategyTuningParams(
            BbStdDev: 2.0,
            BbFadeRsiOversold: 35,
            BbFadeRsiOverbought: 65,
            BbFadeTargetRatio: 0.8,
            BeActivationRMultiple: 0.0,
            ChandelierActivationRMultiple: 0.0));
        var rows = backtester.Run(bars);
        var trades = StrategyBacktester.ExtractTrades(rows, ExecutionSettings.MnqDefault);

        var oldFormatTrades = trades.Where(t =>
            t.ExitReason == "Alvo media Bollinger long" ||
            t.ExitReason == "Alvo media Bollinger short").ToList();
        Assert.Empty(oldFormatTrades);
    }
}

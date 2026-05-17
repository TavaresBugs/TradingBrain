using TradingBrain.ConsoleApp;
using TradingBrain.Core;

namespace TradingBrain.Tests;

public sealed class SchoolRunStrategyTests
{
    [Fact]
    public void SchoolRun_EntraLong_AcimaDoHighDoCandleReferencia()
    {
        var rows = RunSchoolRun(BuildSchoolRunBars(
            triggerClose: 110,
            baseHigh: 90,
            baseLow: 80,
            overnightSpikeHigh: 90,
            overnightSpikeLow: 80), new StrategyTuningParams(
            SrsReferenceCandle: 2,
            OvernightRangeEndHHmmss: 92500,
            SrsAtrBuffer: 0,
            SrsAtrTargetMultiplier: 100));

        var entry = rows.First(r => r.Signal != SignalAction.None);

        Assert.Equal(SignalAction.Buy, entry.Signal);
        Assert.Equal("SRS: breakout long", entry.Reason);
    }

    [Fact]
    public void AntiSchoolRun_DentroDoOvernight_InverteLongParaShort()
    {
        var rows = RunSchoolRun(BuildSchoolRunBars(triggerClose: 110), new StrategyTuningParams(
            SrsReferenceCandle: 2,
            OvernightRangeEndHHmmss: 92500,
            SrsAtrBuffer: 0,
            SrsAtrTargetMultiplier: 100));

        var entry = rows.First(r => r.Signal != SignalAction.None);

        Assert.Equal(SignalAction.Sell, entry.Signal);
        Assert.Equal("Anti-SRS: long dentro do overnight -> short", entry.Reason);
    }

    [Fact]
    public void AntiSchoolRun_ForaDoOvernight_MantemDirecao()
    {
        var rows = RunSchoolRun(BuildSchoolRunBars(
            triggerClose: 110,
            baseHigh: 90,
            baseLow: 80,
            overnightSpikeHigh: 90,
            overnightSpikeLow: 80), new StrategyTuningParams(
            SrsReferenceCandle: 2,
            OvernightRangeEndHHmmss: 92500,
            SrsAtrBuffer: 0,
            SrsAtrTargetMultiplier: 100));

        var entry = rows.First(r => r.Signal != SignalAction.None);

        Assert.Equal(SignalAction.Buy, entry.Signal);
        Assert.Equal("SRS: breakout long", entry.Reason);
    }

    [Fact]
    public void SchoolRun_RefCandleRangePequeno_NaoAbre()
    {
        var rows = RunSchoolRun(BuildSchoolRunBars(
            triggerClose: 110,
            refHigh: 100.10,
            refLow: 100.00,
            baseHigh: 90,
            baseLow: 80,
            overnightSpikeHigh: 90,
            overnightSpikeLow: 80), new StrategyTuningParams(
            SrsReferenceCandle: 2,
            OvernightRangeEndHHmmss: 92500,
            SrsAtrBuffer: 0,
            SrsMinRangeAtrRatio: 0.3,
            SrsAtrTargetMultiplier: 100));

        Assert.DoesNotContain(rows, r => r.Signal is SignalAction.Buy or SignalAction.Sell);
        Assert.Contains(rows, r => r.Reason == "SRS: ref candle range pequeno");
    }

    [Fact]
    public void SchoolRun_NaoAbreDepoisDaJanelaDeEntrada()
    {
        var rows = RunSchoolRun(BuildSchoolRunBars(
            triggerClose: 110,
            triggerTime: new TimeSpan(11, 5, 0),
            baseHigh: 90,
            baseLow: 80,
            overnightSpikeHigh: 90,
            overnightSpikeLow: 80), new StrategyTuningParams(
            SrsReferenceCandle: 2,
            OvernightRangeEndHHmmss: 92500,
            SrsAtrBuffer: 0,
            SrsAtrTargetMultiplier: 100));

        Assert.DoesNotContain(rows, r => r.Signal is SignalAction.Buy or SignalAction.Sell);
        Assert.Contains(rows, r => r.Reason == "SRS: janela de entrada expirada");
    }

    [Fact]
    public void SchoolRun_NaoAbreSegundoTradeNoMesmoDia()
    {
        var rows = RunSchoolRun(BuildSchoolRunBars(
            triggerClose: 110,
            exitClose: 120,
            secondTriggerClose: 130), new StrategyTuningParams(
            SrsReferenceCandle: 2,
            OvernightRangeEndHHmmss: 92500,
            SrsAtrBuffer: 0,
            SrsAtrTargetMultiplier: 0.01));

        var entries = rows.Count(r => r.Signal is SignalAction.Buy or SignalAction.Sell);

        Assert.Equal(1, entries);
        Assert.Contains(rows, r => r.Signal == SignalAction.Exit);
        Assert.Contains(rows, r => r.Reason == "SRS: trade diario encerrado");
    }

    private static IReadOnlyList<StrategyBacktestRow> RunSchoolRun(
        IReadOnlyList<MarketBar> bars,
        StrategyTuningParams parameters)
    {
        var backtester = new StrategyBacktester(StrategyKind.SchoolRun, parameters);
        return backtester.Run(bars);
    }

    private static IReadOnlyList<MarketBar> BuildSchoolRunBars(
        double triggerClose,
        double? exitClose = null,
        double? secondTriggerClose = null,
        double baseHigh = 101.0,
        double baseLow = 99.0,
        double refHigh = 105.0,
        double refLow = 95.0,
        double overnightSpikeHigh = 200.0,
        double overnightSpikeLow = 50.0,
        TimeSpan? triggerTime = null)
    {
        var date = new DateTime(2024, 1, 2);
        var bars = new List<MarketBar>();

        for (var i = 0; i < 150; i++)
        {
            var time = date.AddMinutes(i * 5);
            var open = 100.0;
            var high = baseHigh;
            var low = baseLow;
            var close = 100.0;

            if (time.TimeOfDay >= new TimeSpan(9, 45, 0) &&
                time.TimeOfDay <= new TimeSpan(9, 55, 0))
            {
                high = refHigh;
                low = refLow;
            }

            if (time.TimeOfDay == new TimeSpan(8, 0, 0))
            {
                high = overnightSpikeHigh;
                low = overnightSpikeLow;
            }

            if (time.TimeOfDay == (triggerTime ?? new TimeSpan(10, 5, 0)))
            {
                close = triggerClose;
                open = close - 0.25;
                high = close;
                low = close - 1.0;
            }

            if (exitClose is not null && time.TimeOfDay == new TimeSpan(10, 10, 0))
            {
                close = exitClose.Value;
                open = close - 0.25;
                high = close;
                low = close - 1.0;
            }

            if (secondTriggerClose is not null && time.TimeOfDay == new TimeSpan(10, 15, 0))
            {
                close = secondTriggerClose.Value;
                open = close - 0.25;
                high = close;
                low = close - 1.0;
            }

            bars.Add(new MarketBar(time, open, high, low, close, 1000));
        }

        return bars;
    }
}

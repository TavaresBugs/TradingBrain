using TradingBrain.ConsoleApp;
using TradingBrain.Core;

namespace TradingBrain.Tests;

public sealed class SchoolRunStrategyTests
{
    [Fact]
    public void SchoolRun_EntraLong_AcimaDoHighDoCandleReferencia()
    {
        var rows = RunSchoolRun(BuildSchoolRunBars(triggerClose: 110), new StrategyTuningParams(
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
            UseAntiMode: true,
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
        var rows = RunSchoolRun(BuildSchoolRunBars(triggerClose: 210), new StrategyTuningParams(
            UseAntiMode: true,
            SrsReferenceCandle: 2,
            OvernightRangeEndHHmmss: 92500,
            SrsAtrBuffer: 0,
            SrsAtrTargetMultiplier: 100));

        var entry = rows.First(r => r.Signal != SignalAction.None);

        Assert.Equal(SignalAction.Buy, entry.Signal);
        Assert.Equal("SRS: breakout long", entry.Reason);
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
        double? secondTriggerClose = null)
    {
        var date = new DateTime(2024, 1, 2);
        var bars = new List<MarketBar>();

        for (var i = 0; i < 130; i++)
        {
            var time = date.AddMinutes(i * 5);
            var open = 100.0;
            var high = 101.0;
            var low = 99.0;
            var close = 100.0;

            if (time.TimeOfDay >= new TimeSpan(0, 15, 0) &&
                time.TimeOfDay <= new TimeSpan(0, 25, 0))
            {
                high = 105.0;
                low = 95.0;
            }

            if (time.TimeOfDay == new TimeSpan(8, 0, 0))
            {
                high = 200.0;
                low = 50.0;
            }

            if (time.TimeOfDay == new TimeSpan(9, 30, 0))
            {
                close = triggerClose;
                open = close - 0.25;
                high = close;
                low = close - 1.0;
            }

            if (exitClose is not null && time.TimeOfDay == new TimeSpan(9, 35, 0))
            {
                close = exitClose.Value;
                open = close - 0.25;
                high = close;
                low = close - 1.0;
            }

            if (secondTriggerClose is not null && time.TimeOfDay == new TimeSpan(9, 40, 0))
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

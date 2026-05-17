using TradingBrain.Core;

namespace TradingBrain.Tests;

internal static class PureIbScenarioFactory
{
    public static List<MarketBar> MakeDays(
        double ibHighYest,
        double ibLowYest,
        double prevClose,
        double openToday,
        double ibHighToday,
        double ibLowToday,
        double cperiodHigh,
        double cperiodLow,
        double overnightHigh,
        double overnightLow,
        double? sessionTailHigh = null,
        double? sessionTailLow = null,
        double? sessionClose = null)
    {
        var bars = new List<MarketBar>();
        var baseDate = new DateTime(2026, 1, 5);

        for (var i = -24; i < 0; i++)
        {
            var date = baseDate.AddDays(i);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            AddSessionBars(bars, date, 21000, 21100, 20900, 21050, 1000);
        }

        AddIbBars(bars, baseDate, ibHighYest, ibLowYest, 1000);
        AddSessionTail(bars, baseDate, ibHighYest, ibLowYest, prevClose, 800);

        if (overnightHigh > 0)
        {
            AddOvernightBars(bars, baseDate, overnightHigh, overnightLow);
        }

        var today = baseDate.AddDays(1);
        while (today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            today = today.AddDays(1);
        }

        AddOpenBar(bars, today, openToday);
        AddIbBars(bars, today, ibHighToday, ibLowToday, 1000);
        AddCperiodBars(bars, today, cperiodHigh, cperiodLow, 800);
        AddSessionTail(
            bars,
            today,
            sessionTailHigh ?? ibHighToday + 5,
            sessionTailLow ?? ibLowToday - 5,
            sessionClose ?? (ibHighToday + ibLowToday) / 2,
            600,
            startMinute: 90);

        return bars.OrderBy(b => b.Time).ToList();
    }

    private static void AddSessionBars(
        List<MarketBar> bars,
        DateTime date,
        double open,
        double high,
        double low,
        double close,
        long volume)
    {
        for (var min = 0; min <= 390; min += 5)
        {
            var time = date.AddHours(9).AddMinutes(30 + min);
            bars.Add(new MarketBar(time, open, high, low, close, volume));
        }
    }

    private static void AddIbBars(List<MarketBar> bars, DateTime date, double high, double low, long volume)
    {
        for (var min = 0; min <= 55; min += 5)
        {
            var time = date.AddHours(9).AddMinutes(30 + min);
            bars.Add(new MarketBar(time, (high + low) / 2, high, low, (high + low) / 2, volume));
        }
    }

    private static void AddSessionTail(
        List<MarketBar> bars,
        DateTime date,
        double high,
        double low,
        double close,
        long volume,
        int startMinute = 60)
    {
        for (var min = startMinute; min <= 390; min += 5)
        {
            var time = date.AddHours(9).AddMinutes(30 + min);
            bars.Add(new MarketBar(time, (high + low) / 2, high, low, close, volume));
        }
    }

    private static void AddOpenBar(List<MarketBar> bars, DateTime date, double open)
        => bars.Add(new MarketBar(date.AddHours(9).AddMinutes(30), open, open + 2, open - 2, open, 1500));

    private static void AddCperiodBars(List<MarketBar> bars, DateTime date, double high, double low, long volume)
    {
        for (var min = 60; min <= 85; min += 5)
        {
            var time = date.AddHours(9).AddMinutes(30 + min);
            bars.Add(new MarketBar(time, (high + low) / 2, high, low, (high + low) / 2, volume));
        }
    }

    private static void AddOvernightBars(List<MarketBar> bars, DateTime date, double high, double low)
    {
        for (var min = 0; min <= 55; min += 5)
        {
            var time = date.AddHours(18).AddMinutes(min);
            bars.Add(new MarketBar(time, (high + low) / 2, high, low, (high + low) / 2, 200));
        }
    }
}

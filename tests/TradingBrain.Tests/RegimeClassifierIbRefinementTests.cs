using TradingBrain.Core;

namespace TradingBrain.Tests;

public class RegimeClassifierIbRefinementTests
{
    private static List<MarketBar> MakeDayBars(
        DateTime date,
        double basePrice,
        double ibRange,
        double cperiodClose,
        bool ibHighFirst = true,
        bool otfUp = false,
        double sessionRange = 200)
    {
        var bars = new List<MarketBar>();
        var ibHigh = basePrice + ibRange / 2.0;
        var ibLow = basePrice - ibRange / 2.0;
        var firstExtremeHigh = ibHighFirst ? ibHigh : basePrice + ibRange * 0.20;
        var firstExtremeLow = ibHighFirst ? basePrice - ibRange * 0.20 : ibLow;
        var secondExtremeHigh = ibHighFirst ? basePrice + ibRange * 0.20 : ibHigh;
        var secondExtremeLow = ibHighFirst ? ibLow : basePrice - ibRange * 0.20;

        for (var i = 0; i < 12; i++)
        {
            var time = date.Date.AddHours(9).AddMinutes(30 + i * 5);
            var high = i < 6 ? firstExtremeHigh : secondExtremeHigh;
            var low = i < 6 ? firstExtremeLow : secondExtremeLow;
            var close = otfUp ? basePrice - 6 + i : basePrice;
            bars.Add(new MarketBar(time, basePrice, high, low, close, 1000));
        }

        for (var i = 0; i < 6; i++)
        {
            var time = date.Date.AddHours(10).AddMinutes(30 + i * 5);
            bars.Add(new MarketBar(time, cperiodClose, cperiodClose, cperiodClose, cperiodClose, 500));
        }

        for (var time = date.Date.AddHours(11); time <= date.Date.AddHours(16); time = time.AddMinutes(5))
        {
            bars.Add(new MarketBar(
                time,
                cperiodClose,
                basePrice + sessionRange / 2.0,
                basePrice - sessionRange / 2.0,
                cperiodClose,
                300));
        }

        return bars;
    }

    private static List<MarketBar> MakeHistoryWithAtr(DateTime start, int weekdays, double price, double sessionRange)
    {
        var bars = new List<MarketBar>();
        var date = start;
        while (bars.Select(b => b.Time.Date).Distinct().Count() < weekdays)
        {
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                bars.AddRange(MakeDayBars(date, price, sessionRange * 0.8, price, sessionRange: sessionRange));
            }

            date = date.AddDays(1);
        }

        return bars;
    }

    [Fact]
    public void DayRegime_HasIbTodayFullRatio_Field()
    {
        var regime = new DayRegime(
            DateOnly.FromDateTime(DateTime.Today),
            MarketRegime.Range,
            1.0,
            0.5,
            0.3,
            0.1,
            double.NaN,
            "test",
            IbTodayFullRatio: 0.75,
            CperiodInsideIb: true,
            IbHighFormedFirst: true);

        Assert.Equal(0.75, regime.IbTodayFullRatio);
        Assert.True(regime.CperiodInsideIb);
        Assert.True(regime.IbHighFormedFirst);
    }

    [Fact]
    public void Classify_NarrowIbWithOtf_ShouldBeTrend()
    {
        const double price = 21000;
        const double atr = 200;
        var bars = MakeHistoryWithAtr(new DateTime(2026, 1, 2), 20, price, atr);
        var lastDate = bars.Max(b => b.Time.Date).AddDays(1);
        while (lastDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            lastDate = lastDate.AddDays(1);
        }

        var narrowIbRange = atr * 0.20;
        var cperiodCloseAboveIb = price + narrowIbRange / 2.0 + 10;
        bars.AddRange(MakeDayBars(lastDate, price, narrowIbRange, cperiodCloseAboveIb, otfUp: true));

        var lastRegime = RegimeClassifier.Classify(bars).Last();

        Assert.Equal(MarketRegime.Trend, lastRegime.Regime);
        Assert.True(lastRegime.IbTodayFullRatio < 0.24,
            $"IbTodayFullRatio deveria ser < 0.24, foi: {lastRegime.IbTodayFullRatio:F3}");
    }

    [Fact]
    public void Classify_NormalIbWithCperiodInside_ShouldBeRange()
    {
        const double price = 21000;
        const double atr = 200;
        var bars = MakeHistoryWithAtr(new DateTime(2026, 1, 2), 20, price, atr);
        var lastDate = bars.Max(b => b.Time.Date).AddDays(1);
        while (lastDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            lastDate = lastDate.AddDays(1);
        }

        bars.AddRange(MakeDayBars(lastDate, price, atr * 0.7, price));

        var lastRegime = RegimeClassifier.Classify(bars).Last();

        Assert.True(lastRegime.CperiodInsideIb);
        Assert.Equal(MarketRegime.Range, lastRegime.Regime);
    }

    [Fact]
    public void Classify_PopulatesCperiodFields_Correctly()
    {
        var bars = MakeHistoryWithAtr(new DateTime(2026, 1, 2), 20, 21000, 200);
        var regimes = RegimeClassifier.Classify(bars);

        foreach (var r in regimes)
        {
            Assert.False(double.IsNaN(r.IbTodayFullRatio),
                $"IbTodayFullRatio nao deveria ser NaN para {r.Date}");
        }
    }

    [Fact]
    public void Classify_IbFormation_HighFirstDetected()
    {
        const double price = 21000;
        const double atr = 200;
        var bars = MakeHistoryWithAtr(new DateTime(2026, 1, 2), 20, price, atr);
        var lastDate = bars.Max(b => b.Time.Date).AddDays(1);
        while (lastDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            lastDate = lastDate.AddDays(1);
        }

        bars.AddRange(MakeDayBars(lastDate, price, atr * 0.8, price, ibHighFirst: true));

        var lastRegime = RegimeClassifier.Classify(bars).Last();

        Assert.True(lastRegime.IbHighFormedFirst);
        Assert.False(lastRegime.IbLowFormedFirst);
    }
}

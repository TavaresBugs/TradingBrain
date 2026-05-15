using TradingBrain.Core;

namespace TradingBrain.Tests;

public sealed class PrecomputedSeriesTests
{
    [Fact]
    public void Ema9_BateComTechnicalIndicatorsEma()
    {
        var bars = Bars(50);
        var closes = bars.Select(b => b.Close).ToList();

        var series = PrecomputedSeries.From(bars);

        Assert.Equal(TechnicalIndicators.Ema(closes, 9), series.Ema9[49], precision: 4);
    }

    [Fact]
    public void Atr14_BateComTechnicalIndicatorsAtr()
    {
        var bars = Bars(50);

        var series = PrecomputedSeries.From(bars);

        Assert.Equal(TechnicalIndicators.Atr(bars, 14), series.Atr14[49], precision: 4);
    }

    [Fact]
    public void Vwap_ResetaNaViradaDoDia()
    {
        var bars = new[]
        {
            new MarketBar(new DateTime(2024, 1, 2, 9, 30, 0), 100, 100, 100, 100, 1),
            new MarketBar(new DateTime(2024, 1, 2, 9, 35, 0), 200, 200, 200, 200, 3),
            new MarketBar(new DateTime(2024, 1, 3, 9, 30, 0), 300, 300, 300, 300, 1),
            new MarketBar(new DateTime(2024, 1, 3, 9, 35, 0), 500, 500, 500, 500, 1),
        };

        var series = PrecomputedSeries.From(bars);

        Assert.Equal(300.0, series.Vwap[2], precision: 4);
        Assert.Equal(400.0, series.Vwap[3], precision: 4);
    }

    [Fact]
    public void Rsi14_BateComTechnicalIndicatorsRsi()
    {
        var bars = Bars(50);
        var closes = bars.Select(b => b.Close).ToList();

        var series = PrecomputedSeries.From(bars);

        Assert.Equal(TechnicalIndicators.Rsi(closes, 14), series.Rsi14[49], precision: 2);
    }

    private static List<MarketBar> Bars(int count)
    {
        var start = new DateTime(2024, 1, 2, 9, 30, 0);
        return Enumerable.Range(0, count)
            .Select(i =>
            {
                var close = 100 + i * 0.5 + Math.Sin(i / 3.0);
                return new MarketBar(
                    start.AddMinutes(i * 5),
                    close - 0.2,
                    close + 1.0,
                    close - 1.0,
                    close,
                    1000 + i);
            })
            .ToList();
    }
}

using TradingBrain.Core;

namespace TradingBrain.Tests;

public sealed class TechnicalIndicatorsTests
{
    [Fact]
    public void Ema_UsesSimpleAverageSeedThenExponentialSmoothing()
    {
        var values = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };

        var result = TechnicalIndicators.Ema(values, 3);

        Assert.Equal(4.0, result, precision: 10);
    }

    [Fact]
    public void Ema_ReturnsNaNWhenThereIsNotEnoughHistory()
    {
        var result = TechnicalIndicators.Ema(new[] { 1.0, 2.0 }, 3);

        Assert.True(double.IsNaN(result));
    }

    [Fact]
    public void Rsi_ReturnsOneHundredWhenThereAreNoLosses()
    {
        var result = TechnicalIndicators.Rsi(new[] { 1.0, 2.0, 3.0 }, 2);

        Assert.Equal(100.0, result, precision: 10);
    }

    [Fact]
    public void Rsi_AppliesWilderSmoothingAfterInitialPeriod()
    {
        var result = TechnicalIndicators.Rsi(new[] { 1.0, 2.0, 1.0, 2.0 }, 2);

        Assert.Equal(75.0, result, precision: 10);
    }

    [Fact]
    public void Atr_AveragesInitialTrueRanges()
    {
        var bars = new[]
        {
            Bar(close: 10, high: 10, low: 10),
            Bar(close: 11, high: 12, low: 9),
            Bar(close: 12, high: 13, low: 10),
            Bar(close: 14, high: 15, low: 11),
        };

        var result = TechnicalIndicators.Atr(bars, 3);

        Assert.Equal(10.0 / 3.0, result, precision: 10);
    }

    [Fact]
    public void Atr_AppliesWilderSmoothingAfterInitialPeriod()
    {
        var bars = new[]
        {
            Bar(close: 10, high: 10, low: 10),
            Bar(close: 11, high: 12, low: 9),
            Bar(close: 12, high: 13, low: 10),
            Bar(close: 14, high: 15, low: 11),
            Bar(close: 15.5, high: 16, low: 15),
        };

        var result = TechnicalIndicators.Atr(bars, 3);

        Assert.Equal(26.0 / 9.0, result, precision: 10);
    }

    [Fact]
    public void Sma_UsesLastPeriodValues()
    {
        var result = TechnicalIndicators.Sma(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 }, 3);

        Assert.Equal(4.0, result, precision: 10);
    }

    [Fact]
    public void Vwap_WeightsTypicalPriceByVolume()
    {
        var bars = new[]
        {
            new MarketBar(DateTime.UnixEpoch, 10, 10, 10, 10, 1),
            new MarketBar(DateTime.UnixEpoch, 20, 20, 20, 20, 3),
        };

        var result = TechnicalIndicators.Vwap(bars);

        Assert.Equal(17.5, result, precision: 10);
    }

    [Fact]
    public void VolumeSma_UsesLastPeriodVolumes()
    {
        var bars = new[]
        {
            Bar(volume: 100),
            Bar(volume: 200),
            Bar(volume: 300),
        };

        var result = TechnicalIndicators.VolumeSma(bars, 2);

        Assert.Equal(250.0, result, precision: 10);
    }

    [Fact]
    public void CandleRangeSma_UsesLastPeriodHighLowRanges()
    {
        var bars = new[]
        {
            Bar(high: 12, low: 10),
            Bar(high: 13, low: 10),
            Bar(high: 15, low: 10),
        };

        var result = TechnicalIndicators.CandleRangeSma(bars, 2);

        Assert.Equal(4.0, result, precision: 10);
    }

    [Fact]
    public void BollingerUpper_ReturnsNaN_WhenNotEnoughData()
    {
        var result = TechnicalIndicators.BollingerUpper(new[] { 1.0, 2.0 }, 3, 2.0);

        Assert.True(double.IsNaN(result));
    }

    [Fact]
    public void BollingerUpper_IsAboveMean_WhenDataReady()
    {
        var values = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };

        var result = TechnicalIndicators.BollingerUpper(values, 5, 2.0);

        Assert.True(result > values.Average());
    }

    [Fact]
    public void BollingerLower_IsBelowMean_WhenDataReady()
    {
        var values = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };

        var result = TechnicalIndicators.BollingerLower(values, 5, 2.0);

        Assert.True(result < values.Average());
    }

    [Fact]
    public void BollingerUpperAndLower_AreSymmetric_AroundMean()
    {
        var values = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
        var mean = values.Average();

        var upper = TechnicalIndicators.BollingerUpper(values, 5, 2.0);
        var lower = TechnicalIndicators.BollingerLower(values, 5, 2.0);

        Assert.Equal(mean - lower, upper - mean, precision: 10);
    }

    [Fact]
    public void Resample_Factor3_AggregatesCorrectly()
    {
        var bars = new[]
        {
            new MarketBar(new DateTime(2024, 1, 2, 9, 30, 0), 100, 105, 99, 103, 1000),
            new MarketBar(new DateTime(2024, 1, 2, 9, 35, 0), 103, 107, 102, 106, 1200),
            new MarketBar(new DateTime(2024, 1, 2, 9, 40, 0), 106, 108, 104, 105, 800),
            new MarketBar(new DateTime(2024, 1, 2, 9, 45, 0), 105, 110, 104, 109, 1500),
            new MarketBar(new DateTime(2024, 1, 2, 9, 50, 0), 109, 112, 108, 111, 900),
            new MarketBar(new DateTime(2024, 1, 2, 9, 55, 0), 111, 113, 109, 112, 600),
        };

        var result = TechnicalIndicators.Resample(bars, 3);

        Assert.Equal(2, result.Count);

        Assert.Equal(new DateTime(2024, 1, 2, 9, 30, 0), result[0].Time);
        Assert.Equal(100, result[0].Open);
        Assert.Equal(108, result[0].High);
        Assert.Equal(99, result[0].Low);
        Assert.Equal(105, result[0].Close);
        Assert.Equal(3000, result[0].Volume);

        Assert.Equal(new DateTime(2024, 1, 2, 9, 45, 0), result[1].Time);
        Assert.Equal(105, result[1].Open);
        Assert.Equal(113, result[1].High);
        Assert.Equal(104, result[1].Low);
        Assert.Equal(112, result[1].Close);
        Assert.Equal(3000, result[1].Volume);
    }

    [Fact]
    public void Resample_Factor1_ReturnsOriginal()
    {
        var bars = new[]
        {
            new MarketBar(new DateTime(2024, 1, 2, 9, 30, 0), 100, 105, 99, 103, 1000),
            new MarketBar(new DateTime(2024, 1, 2, 9, 35, 0), 103, 107, 102, 106, 1200),
        };

        var result = TechnicalIndicators.Resample(bars, 1);

        Assert.Equal(2, result.Count);
        Assert.Equal(bars[0], result[0]);
        Assert.Equal(bars[1], result[1]);
    }

    [Fact]
    public void Resample_BarrasIncompletas_IncluiUltimoGrupo()
    {
        var bars = Enumerable.Range(0, 5)
            .Select(i => new MarketBar(
                new DateTime(2024, 1, 2, 9, 30, 0).AddMinutes(i * 5),
                100 + i,
                105 + i,
                99 + i,
                103 + i,
                1000))
            .ToList();

        var result = TechnicalIndicators.Resample(bars, 3);

        Assert.Equal(2, result.Count);
    }

    private static MarketBar Bar(
        double close = 10,
        double high = 10,
        double low = 10,
        long volume = 1) =>
        new(DateTime.UnixEpoch, close, high, low, close, volume);
}

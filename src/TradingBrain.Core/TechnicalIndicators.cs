namespace TradingBrain.Core;

public static class TechnicalIndicators
{
    /// <summary>
    /// Agrega barras de timeframe menor em barras de timeframe maior por janela de tempo real.
    /// </summary>
    public static IReadOnlyList<MarketBar> Resample(IReadOnlyList<MarketBar> bars, int targetMinutes)
    {
        if (targetMinutes <= 0 || bars.Count == 0)
        {
            return bars;
        }

        if (targetMinutes > 1 && targetMinutes < InferBarIntervalMinutes(bars))
        {
            return ResampleByFactor(bars, targetMinutes);
        }

        var result = new List<MarketBar>();
        var bucket = new List<MarketBar>();
        DateTime? bucketStart = null;

        foreach (var bar in bars)
        {
            var slotMinutes = (bar.Time.Hour * 60 + bar.Time.Minute) / targetMinutes * targetMinutes;
            var slotStart = bar.Time.Date.AddMinutes(slotMinutes);

            if (bucketStart is null || slotStart != bucketStart)
            {
                if (bucket.Count > 0)
                {
                    result.Add(AggregateBucket(bucket, bucketStart.GetValueOrDefault()));
                    bucket.Clear();
                }

                bucketStart = slotStart;
            }

            bucket.Add(bar);
        }

        if (bucket.Count > 0)
        {
            result.Add(AggregateBucket(bucket, bucketStart!.Value));
        }

        return result;
    }

    private static MarketBar AggregateBucket(List<MarketBar> bars, DateTime time)
    {
        return new MarketBar(
            time,
            bars[0].Open,
            bars.Max(b => b.High),
            bars.Min(b => b.Low),
            bars[^1].Close,
            bars.Sum(b => b.Volume));
    }

    private static IReadOnlyList<MarketBar> ResampleByFactor(IReadOnlyList<MarketBar> bars, int factor)
    {
        var result = new List<MarketBar>();
        for (var i = 0; i < bars.Count; i += factor)
        {
            var slice = bars.Skip(i).Take(factor).ToList();
            result.Add(AggregateBucket(slice, slice[0].Time));
        }

        return result;
    }

    private static int InferBarIntervalMinutes(IReadOnlyList<MarketBar> bars)
    {
        if (bars.Count < 2)
        {
            return int.MaxValue;
        }

        for (var i = 1; i < bars.Count; i++)
        {
            var minutes = (int)(bars[i].Time - bars[i - 1].Time).TotalMinutes;
            if (minutes > 0)
            {
                return minutes;
            }
        }

        return int.MaxValue;
    }

    public static double Ema(IReadOnlyList<double> values, int period)
    {
        if (values.Count < period)
        {
            return double.NaN;
        }

        var k = 2.0 / (period + 1);
        var ema = values.Take(period).Average();

        for (var i = period; i < values.Count; i++)
        {
            ema = (values[i] - ema) * k + ema;
        }

        return ema;
    }

    public static double Rsi(IReadOnlyList<double> closes, int period)
    {
        if (closes.Count < period + 1)
        {
            return double.NaN;
        }

        double avgGain = 0;
        double avgLoss = 0;

        for (var i = 1; i <= period; i++)
        {
            var diff = closes[i] - closes[i - 1];
            if (diff >= 0)
            {
                avgGain += diff;
            }
            else
            {
                avgLoss -= diff;
            }
        }

        avgGain /= period;
        avgLoss /= period;

        for (var i = period + 1; i < closes.Count; i++)
        {
            var diff = closes[i] - closes[i - 1];
            avgGain = (avgGain * (period - 1) + Math.Max(diff, 0)) / period;
            avgLoss = (avgLoss * (period - 1) + Math.Max(-diff, 0)) / period;
        }

        if (avgLoss == 0)
        {
            return 100;
        }

        return 100 - 100 / (1 + avgGain / avgLoss);
    }

    public static double Vwap(IReadOnlyList<MarketBar> bars)
    {
        double volumeSum = 0;
        double typicalPriceVolumeSum = 0;

        foreach (var bar in bars)
        {
            var typicalPrice = (bar.High + bar.Low + bar.Close) / 3.0;
            typicalPriceVolumeSum += typicalPrice * bar.Volume;
            volumeSum += bar.Volume;
        }

        return volumeSum == 0 ? double.NaN : typicalPriceVolumeSum / volumeSum;
    }

    public static double Atr(IReadOnlyList<MarketBar> bars, int period)
    {
        if (bars.Count < period + 1)
        {
            return double.NaN;
        }

        var trueRanges = new List<double>(bars.Count - 1);

        for (var i = 1; i < bars.Count; i++)
        {
            var highLow = bars[i].High - bars[i].Low;
            var highClose = Math.Abs(bars[i].High - bars[i - 1].Close);
            var lowClose = Math.Abs(bars[i].Low - bars[i - 1].Close);
            trueRanges.Add(Math.Max(highLow, Math.Max(highClose, lowClose)));
        }

        var atr = trueRanges.Take(period).Average();

        for (var i = period; i < trueRanges.Count; i++)
        {
            atr = (atr * (period - 1) + trueRanges[i]) / period;
        }

        return atr;
    }

    public static double Sma(IReadOnlyList<double> values, int period)
    {
        if (values.Count < period)
        {
            return double.NaN;
        }

        var sum = 0.0;
        for (var i = values.Count - period; i < values.Count; i++)
        {
            sum += values[i];
        }

        return sum / period;
    }

    public static double VolumeSma(IReadOnlyList<MarketBar> bars, int period)
    {
        if (bars.Count < period)
        {
            return double.NaN;
        }

        double sum = 0;
        for (var i = bars.Count - period; i < bars.Count; i++)
        {
            sum += bars[i].Volume;
        }

        return sum / period;
    }

    public static double CandleRangeSma(IReadOnlyList<MarketBar> bars, int period)
    {
        if (bars.Count < period)
        {
            return double.NaN;
        }

        double sum = 0;
        for (var i = bars.Count - period; i < bars.Count; i++)
        {
            sum += bars[i].High - bars[i].Low;
        }

        return sum / period;
    }

    public static double BollingerUpper(IReadOnlyList<double> closes, int period, double stdDevMultiplier)
    {
        if (closes.Count < period)
        {
            return double.NaN;
        }

        var mean = Sma(closes, period);
        var variance = 0.0;
        for (var i = closes.Count - period; i < closes.Count; i++)
        {
            variance += Math.Pow(closes[i] - mean, 2);
        }

        return mean + stdDevMultiplier * Math.Sqrt(variance / period);
    }

    public static double BollingerLower(IReadOnlyList<double> closes, int period, double stdDevMultiplier)
    {
        if (closes.Count < period)
        {
            return double.NaN;
        }

        var mean = Sma(closes, period);
        var variance = 0.0;
        for (var i = closes.Count - period; i < closes.Count; i++)
        {
            variance += Math.Pow(closes[i] - mean, 2);
        }

        return mean - stdDevMultiplier * Math.Sqrt(variance / period);
    }
}

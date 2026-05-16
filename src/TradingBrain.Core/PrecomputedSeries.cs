using System.Collections.ObjectModel;

namespace TradingBrain.Core;

/// <summary>
/// Pre-computa series de indicadores tecnicos alinhadas por indice com as barras.
/// Valores antes do aquecimento ficam como double.NaN.
/// </summary>
public sealed class PrecomputedSeries
{
    public IReadOnlyList<double> Ema9 { get; }
    public IReadOnlyList<double> Ema12 { get; }
    public IReadOnlyList<double> Ema20 { get; }
    public IReadOnlyList<double> Ema21 { get; }
    public IReadOnlyList<double> Ema26 { get; }
    public IReadOnlyList<double> Macd { get; }
    public IReadOnlyList<double> MacdSignal { get; }
    public IReadOnlyList<double> Rsi14 { get; }
    public IReadOnlyList<double> Atr14 { get; }
    public IReadOnlyList<double> AtrSma14 { get; }
    public IReadOnlyList<double> VolumeSma20 { get; }
    public IReadOnlyList<double> CandleRangeSma14 { get; }
    public IReadOnlyList<double> Vwap { get; }
    public IReadOnlyList<double> Highest10 { get; }
    public IReadOnlyList<double> Lowest10 { get; }
    public IReadOnlyList<double> Highest3 { get; }
    public IReadOnlyList<double> Lowest3 { get; }
    public IReadOnlyList<double> SwingHigh20 { get; }
    public IReadOnlyList<double> SwingLow20 { get; }
    public IReadOnlyList<double> BbUpper { get; }
    public IReadOnlyList<double> BbMiddle { get; }
    public IReadOnlyList<double> BbLower { get; }

    public static PrecomputedSeries From(IReadOnlyList<MarketBar> bars, double bbStdDev = 2.0)
    {
        var closes = bars.Select(b => b.Close).ToArray();
        var ema9 = Ema(closes, 9);
        var ema12 = Ema(closes, 12);
        var ema20 = Ema(closes, 20);
        var ema21 = Ema(closes, 21);
        var ema26 = Ema(closes, 26);
        var macd = BuildMacd(ema12, ema26);
        var atr14 = Atr(bars, 14);

        return new PrecomputedSeries(
            ema9,
            ema12,
            ema20,
            ema21,
            ema26,
            macd,
            EmaSparse(macd, 9),
            Rsi(closes, 14),
            atr14,
            SmaFinite(atr14, 14),
            VolumeSma(bars, 20),
            CandleRangeSma(bars, 14),
            BuildVwap(bars),
            Highest(bars, 10),
            Lowest(bars, 10),
            Highest(bars, 3),
            Lowest(bars, 3),
            PreviousHighest(bars, 20),
            PreviousLowest(bars, 20),
            BollingerUpper(closes, 20, bbStdDev),
            Sma(closes, 20),
            BollingerLower(closes, 20, bbStdDev));
    }

    private static double[] Fill(int count)
    {
        var values = new double[count];
        Array.Fill(values, double.NaN);
        return values;
    }

    private static double[] Ema(IReadOnlyList<double> values, int period)
    {
        var result = Fill(values.Count);
        if (values.Count < period)
            return result;

        var seed = 0.0;
        for (var i = 0; i < period; i++)
            seed += values[i];

        var ema = seed / period;
        result[period - 1] = ema;
        var k = 2.0 / (period + 1);

        for (var i = period; i < values.Count; i++)
        {
            ema = (values[i] - ema) * k + ema;
            result[i] = ema;
        }

        return result;
    }

    private static double[] EmaSparse(IReadOnlyList<double> values, int period)
    {
        var result = Fill(values.Count);
        var buffer = new List<double>(period);
        var ema = double.NaN;
        var k = 2.0 / (period + 1);

        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (double.IsNaN(value))
                continue;

            if (buffer.Count < period)
            {
                buffer.Add(value);
                if (buffer.Count == period)
                {
                    ema = buffer.Average();
                    result[i] = ema;
                }
                continue;
            }

            ema = (value - ema) * k + ema;
            result[i] = ema;
        }

        return result;
    }

    private static double[] BuildMacd(IReadOnlyList<double> ema12, IReadOnlyList<double> ema26)
    {
        var result = Fill(ema12.Count);
        for (var i = 0; i < result.Length; i++)
        {
            if (!double.IsNaN(ema12[i]) && !double.IsNaN(ema26[i]))
                result[i] = ema12[i] - ema26[i];
        }

        return result;
    }

    private static double[] Rsi(IReadOnlyList<double> closes, int period)
    {
        var result = Fill(closes.Count);
        if (closes.Count < period + 1)
            return result;

        var avgGain = 0.0;
        var avgLoss = 0.0;
        for (var i = 1; i <= period; i++)
        {
            var diff = closes[i] - closes[i - 1];
            if (diff >= 0)
                avgGain += diff;
            else
                avgLoss -= diff;
        }

        avgGain /= period;
        avgLoss /= period;
        result[period] = avgLoss == 0 ? 100 : 100 - 100 / (1 + avgGain / avgLoss);

        for (var i = period + 1; i < closes.Count; i++)
        {
            var diff = closes[i] - closes[i - 1];
            avgGain = (avgGain * (period - 1) + Math.Max(diff, 0)) / period;
            avgLoss = (avgLoss * (period - 1) + Math.Max(-diff, 0)) / period;
            result[i] = avgLoss == 0 ? 100 : 100 - 100 / (1 + avgGain / avgLoss);
        }

        return result;
    }

    private static double[] Atr(IReadOnlyList<MarketBar> bars, int period)
    {
        var result = Fill(bars.Count);
        if (bars.Count < period + 1)
            return result;

        var trueRanges = new double[bars.Count - 1];
        for (var i = 1; i < bars.Count; i++)
        {
            var highLow = bars[i].High - bars[i].Low;
            var highClose = Math.Abs(bars[i].High - bars[i - 1].Close);
            var lowClose = Math.Abs(bars[i].Low - bars[i - 1].Close);
            trueRanges[i - 1] = Math.Max(highLow, Math.Max(highClose, lowClose));
        }

        var atr = 0.0;
        for (var i = 0; i < period; i++)
            atr += trueRanges[i];

        atr /= period;
        result[period] = atr;

        for (var i = period; i < trueRanges.Length; i++)
        {
            atr = (atr * (period - 1) + trueRanges[i]) / period;
            result[i + 1] = atr;
        }

        return result;
    }

    private static double[] Sma(IReadOnlyList<double> values, int period)
    {
        var result = Fill(values.Count);
        var sum = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            sum += values[i];
            if (i >= period)
                sum -= values[i - period];
            if (i >= period - 1)
                result[i] = sum / period;
        }

        return result;
    }

    private static double[] SmaFinite(IReadOnlyList<double> values, int period)
    {
        var result = Fill(values.Count);
        var finite = new Queue<double>(period);
        var sum = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            if (double.IsNaN(values[i]))
                continue;

            finite.Enqueue(values[i]);
            sum += values[i];
            if (finite.Count > period)
                sum -= finite.Dequeue();
            if (finite.Count == period)
                result[i] = sum / period;
        }

        return result;
    }

    private static double[] VolumeSma(IReadOnlyList<MarketBar> bars, int period)
    {
        var result = Fill(bars.Count);
        var sum = 0.0;
        for (var i = 0; i < bars.Count; i++)
        {
            sum += bars[i].Volume;
            if (i >= period)
                sum -= bars[i - period].Volume;
            if (i >= period - 1)
                result[i] = sum / period;
        }

        return result;
    }

    private static double[] CandleRangeSma(IReadOnlyList<MarketBar> bars, int period)
    {
        var result = Fill(bars.Count);
        var sum = 0.0;
        for (var i = 0; i < bars.Count; i++)
        {
            sum += bars[i].High - bars[i].Low;
            if (i >= period)
                sum -= bars[i - period].High - bars[i - period].Low;
            if (i >= period - 1)
                result[i] = sum / period;
        }

        return result;
    }

    private static double[] BuildVwap(IReadOnlyList<MarketBar> bars)
    {
        var result = Fill(bars.Count);
        var currentDate = DateTime.MinValue;
        var tpv = 0.0;
        var volume = 0.0;
        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            if (bar.Time.Date != currentDate)
            {
                currentDate = bar.Time.Date;
                tpv = 0;
                volume = 0;
            }

            var typicalPrice = (bar.High + bar.Low + bar.Close) / 3.0;
            tpv += typicalPrice * bar.Volume;
            volume += bar.Volume;
            result[i] = volume == 0 ? double.NaN : tpv / volume;
        }

        return result;
    }

    private static double[] Highest(IReadOnlyList<MarketBar> bars, int period)
    {
        var result = Fill(bars.Count);
        for (var i = period - 1; i < bars.Count; i++)
        {
            var value = double.MinValue;
            for (var j = i - period + 1; j <= i; j++)
                value = Math.Max(value, bars[j].High);
            result[i] = value;
        }

        return result;
    }

    private static double[] PreviousHighest(IReadOnlyList<MarketBar> bars, int period)
    {
        var result = Fill(bars.Count);
        for (var i = period; i < bars.Count; i++)
        {
            var value = double.MinValue;
            for (var j = i - period; j < i; j++)
                value = Math.Max(value, bars[j].High);
            result[i] = value;
        }

        return result;
    }

    private static double[] Lowest(IReadOnlyList<MarketBar> bars, int period)
    {
        var result = Fill(bars.Count);
        for (var i = period - 1; i < bars.Count; i++)
        {
            var value = double.MaxValue;
            for (var j = i - period + 1; j <= i; j++)
                value = Math.Min(value, bars[j].Low);
            result[i] = value;
        }

        return result;
    }

    private static double[] PreviousLowest(IReadOnlyList<MarketBar> bars, int period)
    {
        var result = Fill(bars.Count);
        for (var i = period; i < bars.Count; i++)
        {
            var value = double.MaxValue;
            for (var j = i - period; j < i; j++)
                value = Math.Min(value, bars[j].Low);
            result[i] = value;
        }

        return result;
    }

    private static double[] BollingerUpper(IReadOnlyList<double> closes, int period, double stdDevMultiplier)
    {
        var result = Fill(closes.Count);
        FillBollinger(closes, period, stdDevMultiplier, result, upper: true);
        return result;
    }

    private static double[] BollingerLower(IReadOnlyList<double> closes, int period, double stdDevMultiplier)
    {
        var result = Fill(closes.Count);
        FillBollinger(closes, period, stdDevMultiplier, result, upper: false);
        return result;
    }

    private static void FillBollinger(IReadOnlyList<double> closes, int period, double stdDevMultiplier, double[] result, bool upper)
    {
        for (var i = period - 1; i < closes.Count; i++)
        {
            var mean = 0.0;
            for (var j = i - period + 1; j <= i; j++)
                mean += closes[j];
            mean /= period;

            var variance = 0.0;
            for (var j = i - period + 1; j <= i; j++)
                variance += Math.Pow(closes[j] - mean, 2);

            var deviation = stdDevMultiplier * Math.Sqrt(variance / period);
            result[i] = upper ? mean + deviation : mean - deviation;
        }
    }

    private PrecomputedSeries(
        double[] ema9,
        double[] ema12,
        double[] ema20,
        double[] ema21,
        double[] ema26,
        double[] macd,
        double[] macdSignal,
        double[] rsi14,
        double[] atr14,
        double[] atrSma14,
        double[] volumeSma20,
        double[] candleRangeSma14,
        double[] vwap,
        double[] highest10,
        double[] lowest10,
        double[] highest3,
        double[] lowest3,
        double[] swingHigh20,
        double[] swingLow20,
        double[] bbUpper,
        double[] bbMiddle,
        double[] bbLower)
    {
        Ema9 = ReadOnly(ema9);
        Ema12 = ReadOnly(ema12);
        Ema20 = ReadOnly(ema20);
        Ema21 = ReadOnly(ema21);
        Ema26 = ReadOnly(ema26);
        Macd = ReadOnly(macd);
        MacdSignal = ReadOnly(macdSignal);
        Rsi14 = ReadOnly(rsi14);
        Atr14 = ReadOnly(atr14);
        AtrSma14 = ReadOnly(atrSma14);
        VolumeSma20 = ReadOnly(volumeSma20);
        CandleRangeSma14 = ReadOnly(candleRangeSma14);
        Vwap = ReadOnly(vwap);
        Highest10 = ReadOnly(highest10);
        Lowest10 = ReadOnly(lowest10);
        Highest3 = ReadOnly(highest3);
        Lowest3 = ReadOnly(lowest3);
        SwingHigh20 = ReadOnly(swingHigh20);
        SwingLow20 = ReadOnly(swingLow20);
        BbUpper = ReadOnly(bbUpper);
        BbMiddle = ReadOnly(bbMiddle);
        BbLower = ReadOnly(bbLower);
    }

    private static ReadOnlyCollection<double> ReadOnly(double[] values) => Array.AsReadOnly(values);
}

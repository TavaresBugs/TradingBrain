namespace TradingBrain.Core;

public static class RegimeClassifier
{
    private const int SessionOpenHHmm = 930;
    private const int SessionCloseHHmm = 1600;
    private const int OvernightStartHHmm = 1800;

    /// <summary>
    /// Classifica cada dia de mercado usando apenas dados disponiveis
    /// antes das 9:30 ET, sem lookahead.
    /// Retorna um DayRegime por dia de mercado presente nos dados.
    /// </summary>
    public static IReadOnlyList<DayRegime> Classify(IReadOnlyList<MarketBar> bars)
    {
        var byDate = bars
            .GroupBy(b => b.Time.Date)
            .OrderBy(g => g.Key)
            .ToList();

        var atr14ByDate = ComputeDailyAtr14(byDate);
        var kerByDate = ComputeDailyKer(byDate, period: 10);
        var result = new List<DayRegime>();

        for (var d = 1; d < byDate.Count; d++)
        {
            var today = byDate[d].Key;
            var todayBars = byDate[d].OrderBy(b => b.Time).ToList();
            var prevDate = byDate[d - 1].Key;
            var prevBars = byDate[d - 1].OrderBy(b => b.Time).ToList();

            if (!atr14ByDate.TryGetValue(prevDate, out var atr14) || double.IsNaN(atr14) || atr14 <= 0)
            {
                continue;
            }

            var prevSessionBars = prevBars
                .Where(b => ToHHmm(b.Time) >= SessionOpenHHmm && ToHHmm(b.Time) <= SessionCloseHHmm)
                .ToList();

            if (prevSessionBars.Count == 0)
            {
                continue;
            }

            var prevHigh = prevSessionBars.Max(b => b.High);
            var prevLow = prevSessionBars.Min(b => b.Low);
            var prevClose = prevSessionBars.Last().Close;
            var prevRange = prevHigh - prevLow;

            if (prevRange <= 0)
            {
                continue;
            }

            var rangeRatio = prevRange / atr14;
            var closePosition = (prevClose - prevLow) / prevRange;

            var overnightBars = todayBars
                .Where(b => ToHHmm(b.Time) >= OvernightStartHHmm || ToHHmm(b.Time) < SessionOpenHHmm)
                .ToList();

            var prevOvernightBars = prevBars
                .Where(b => ToHHmm(b.Time) >= OvernightStartHHmm)
                .ToList();

            var allOvernightBars = prevOvernightBars.Concat(overnightBars).OrderBy(b => b.Time).ToList();

            var overnightHigh = allOvernightBars.Count > 0 ? allOvernightBars.Max(b => b.High) : double.NaN;
            var overnightLow = allOvernightBars.Count > 0 ? allOvernightBars.Min(b => b.Low) : double.NaN;
            var overnightRange = !double.IsNaN(overnightHigh) && !double.IsNaN(overnightLow)
                ? overnightHigh - overnightLow
                : 0;
            var overnightRatio = overnightRange / atr14;

            var firstBar930 = todayBars.FirstOrDefault(b => ToHHmm(b.Time) >= SessionOpenHHmm);
            var gapRatio = firstBar930 is not null
                ? Math.Abs(firstBar930.Open - prevClose) / atr14
                : 0;

            var ker = kerByDate.TryGetValue(prevDate, out var kerVal) ? kerVal : double.NaN;
            var regime = Classify(rangeRatio, closePosition, overnightRatio, gapRatio, ker, out var reason);

            result.Add(new DayRegime(
                DateOnly.FromDateTime(today),
                regime,
                rangeRatio,
                closePosition,
                overnightRatio,
                gapRatio,
                ker,
                reason));
        }

        return result;
    }

    private static MarketRegime Classify(
        double rangeRatio,
        double closePosition,
        double overnightRatio,
        double gapRatio,
        double ker,
        out string reason)
    {
        // 1. Alta volatilidade - criterio de range extremo, sem mudanca
        if (rangeRatio > 2.0 || overnightRatio > 2.0)
        {
            reason = $"HighVol: rangeRatio={rangeRatio:F2} overnightRatio={overnightRatio:F2}";
            return MarketRegime.HighVolatility;
        }

        var kerAvailable = !double.IsNaN(ker);

        // 2. Breakout - direcional + catalisador externo
        var directional = kerAvailable ? ker > 0.40 : rangeRatio > 1.2;
        if (directional && (gapRatio > 0.40 || overnightRatio > 1.2))
        {
            reason = $"Breakout: ker={ker:F2} gap={gapRatio:F2} overnightRatio={overnightRatio:F2}";
            return MarketRegime.Breakout;
        }

        // 3. Trend - direcional sem catalisador externo forte
        if (kerAvailable ? ker > 0.40 : rangeRatio > 1.2 && (closePosition > 0.60 || closePosition < 0.40))
        {
            reason = $"Trend: ker={ker:F2} rangeRatio={rangeRatio:F2} closePos={closePosition:F2}";
            return MarketRegime.Trend;
        }

        // 4. Range - lateral confirmado pelo KER
        if (kerAvailable ? ker < 0.25 : rangeRatio < 1.1 && gapRatio < 0.30 && overnightRatio < 1.0)
        {
            reason = $"Range: ker={ker:F2} rangeRatio={rangeRatio:F2} gap={gapRatio:F2}";
            return MarketRegime.Range;
        }

        if (!kerAvailable)
        {
            if (gapRatio > 0.40 || overnightRatio > 1.2)
            {
                reason = $"Breakout(noKER): gap={gapRatio:F2} overnight={overnightRatio:F2}";
                return MarketRegime.Breakout;
            }

            if (rangeRatio < 1.1 && gapRatio < 0.30)
            {
                reason = $"Range(noKER): rangeRatio={rangeRatio:F2}";
                return MarketRegime.Range;
            }
        }

        reason = $"Undefined: ker={ker:F2} rangeRatio={rangeRatio:F2} closePos={closePosition:F2} gap={gapRatio:F2}";
        return MarketRegime.Undefined;
    }

    /// <summary>
    /// Calcula ATR14 diario usando os closes das sessoes principais.
    /// Retorna o ATR14 do ultimo candle de cada dia de sessao.
    /// </summary>
    private static Dictionary<DateTime, double> ComputeDailyAtr14(
        IReadOnlyList<IGrouping<DateTime, MarketBar>> byDate)
    {
        var result = new Dictionary<DateTime, double>();
        var atrHistory = new List<double>();
        const int period = 14;

        DateTime? prevDate = null;
        double prevClose = double.NaN;

        foreach (var group in byDate)
        {
            var sessionBars = group
                .Where(b => ToHHmm(b.Time) >= SessionOpenHHmm && ToHHmm(b.Time) <= SessionCloseHHmm)
                .OrderBy(b => b.Time)
                .ToList();

            if (sessionBars.Count == 0)
            {
                continue;
            }

            var dayHigh = sessionBars.Max(b => b.High);
            var dayLow = sessionBars.Min(b => b.Low);
            var dayClose = sessionBars.Last().Close;

            if (!double.IsNaN(prevClose))
            {
                var tr = Math.Max(dayHigh - dayLow,
                    Math.Max(Math.Abs(dayHigh - prevClose),
                        Math.Abs(dayLow - prevClose)));
                atrHistory.Add(tr);

                if (atrHistory.Count >= period)
                {
                    var atr = atrHistory.Count == period
                        ? atrHistory.Average()
                        : (result[prevDate!.Value] * (period - 1) + tr) / period;
                    result[group.Key] = atr;
                }
            }

            prevClose = dayClose;
            prevDate = group.Key;
        }

        return result;
    }

    private static Dictionary<DateTime, double> ComputeDailyKer(
        IReadOnlyList<IGrouping<DateTime, MarketBar>> byDate,
        int period = 10)
    {
        var result = new Dictionary<DateTime, double>();
        var dailyCloses = new List<(DateTime Date, double Close)>();

        foreach (var group in byDate)
        {
            var sessionBars = group
                .Where(b => ToHHmm(b.Time) >= SessionOpenHHmm && ToHHmm(b.Time) <= SessionCloseHHmm)
                .OrderBy(b => b.Time)
                .ToList();

            if (sessionBars.Count == 0)
            {
                continue;
            }

            dailyCloses.Add((group.Key, sessionBars.Last().Close));
        }

        for (var i = period; i < dailyCloses.Count; i++)
        {
            var netChange = Math.Abs(dailyCloses[i].Close - dailyCloses[i - period].Close);
            var totalPath = 0.0;
            for (var j = i - period + 1; j <= i; j++)
            {
                totalPath += Math.Abs(dailyCloses[j].Close - dailyCloses[j - 1].Close);
            }

            var ker = totalPath == 0 ? 0 : netChange / totalPath;
            result[dailyCloses[i].Date] = ker;
        }

        return result;
    }

    private static int ToHHmm(DateTime time) => time.Hour * 100 + time.Minute;
}

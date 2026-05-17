namespace TradingBrain.Core;

public static class RegimeClassifier
{
    private const int SessionOpenHHmm = 930;
    private const int SessionCloseHHmm = 1600;
    private const int IbEndHHmm = 1030;
    private const int IbMid30HHmm = 1000;
    private const int CperiodEndHHmm = 1100;
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

            var atr14IsFallback = false;
            if (!atr14ByDate.TryGetValue(prevDate, out var atr14) || double.IsNaN(atr14) || atr14 <= 0)
            {
                atr14 = prevRange;
                atr14IsFallback = true;
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

            var (ibYestHigh, ibYestLow) = GetIbWindow(prevBars, SessionOpenHHmm, IbEndHHmm);

            var todayIb30Bars = todayBars
                .Where(b => ToHHmm(b.Time) >= SessionOpenHHmm && ToHHmm(b.Time) < IbMid30HHmm)
                .ToList();
            var ibToday30High = todayIb30Bars.Count > 0 ? todayIb30Bars.Max(b => b.High) : double.NaN;
            var ibToday30Low = todayIb30Bars.Count > 0 ? todayIb30Bars.Min(b => b.Low) : double.NaN;
            var ibToday30Range = !double.IsNaN(ibToday30High) && !double.IsNaN(ibToday30Low)
                ? ibToday30High - ibToday30Low
                : 0;
            var ibToday30MinRatio = atr14 > 0 ? ibToday30Range / atr14 : 0;

            var (ibTodayFullHigh, ibTodayFullLow) = GetIbWindow(todayBars, SessionOpenHHmm, IbEndHHmm);
            var ibTodayFullRange = !double.IsNaN(ibTodayFullHigh) && !double.IsNaN(ibTodayFullLow)
                ? ibTodayFullHigh - ibTodayFullLow
                : 0;
            var ibTodayFullRatio = atr14 > 0 ? ibTodayFullRange / atr14 : 0;

            var cPeriodBars = todayBars
                .Where(b => ToHHmm(b.Time) >= IbEndHHmm && ToHHmm(b.Time) < CperiodEndHHmm)
                .OrderBy(b => b.Time)
                .ToList();
            var cPeriodClose = cPeriodBars.Count > 0 ? cPeriodBars.Last().Close : double.NaN;
            var cperiodAboveIb = !double.IsNaN(cPeriodClose)
                && !double.IsNaN(ibTodayFullHigh)
                && cPeriodClose > ibTodayFullHigh;
            var cperiodBelowIb = !double.IsNaN(cPeriodClose)
                && !double.IsNaN(ibTodayFullLow)
                && cPeriodClose < ibTodayFullLow;
            var cperiodAboveIb30 = !double.IsNaN(cPeriodClose)
                && !double.IsNaN(ibToday30High)
                && cPeriodClose > ibToday30High;
            var cperiodBelowIb30 = !double.IsNaN(cPeriodClose)
                && !double.IsNaN(ibToday30Low)
                && cPeriodClose < ibToday30Low;
            var cperiodInsideIb = !double.IsNaN(cPeriodClose)
                && !double.IsNaN(ibTodayFullHigh)
                && !double.IsNaN(ibTodayFullLow)
                && !cperiodAboveIb
                && !cperiodBelowIb;

            var ibFormationBars = todayBars
                .Where(b => ToHHmm(b.Time) >= SessionOpenHHmm && ToHHmm(b.Time) < IbEndHHmm)
                .OrderBy(b => b.Time)
                .ToList();
            var (ibHighFormedFirst, ibLowFormedFirst) = GetIbFormation(
                ibFormationBars,
                ibTodayFullHigh,
                ibTodayFullLow);

            var otfDirection = CheckOneTimeFraming(todayBars);
            var openOutside = !double.IsNaN(ibYestHigh) && !double.IsNaN(ibYestLow)
                && firstBar930 is not null
                && (firstBar930.Open > ibYestHigh || firstBar930.Open < ibYestLow);

            var regime = ClassifyByIB(
                ibYestHigh,
                ibYestLow,
                firstBar930?.Open ?? double.NaN,
                ibToday30MinRatio,
                ibTodayFullRatio,
                overnightRatio,
                gapRatio,
                otfDirection,
                cperiodAboveIb30,
                cperiodBelowIb30,
                cperiodAboveIb,
                cperiodBelowIb,
                cperiodInsideIb,
                atr14IsFallback,
                out var reason);

            var ker = kerByDate.TryGetValue(prevDate, out var kerVal) ? kerVal : double.NaN;

            result.Add(new DayRegime(
                DateOnly.FromDateTime(today),
                regime,
                rangeRatio,
                closePosition,
                overnightRatio,
                gapRatio,
                ker,
                reason,
                IbYestHigh: ibYestHigh,
                IbYestLow: ibYestLow,
                IbToday30MinRatio: ibToday30MinRatio,
                OpenOutsideIbYest: openOutside,
                OneTimeFramingUp: otfDirection == 1,
                OneTimeFramingDown: otfDirection == -1,
                IbTodayFullRatio: ibTodayFullRatio,
                IbTodayFullHigh: ibTodayFullHigh,
                IbTodayFullLow: ibTodayFullLow,
                CperiodAboveIb: cperiodAboveIb,
                CperiodBelowIb: cperiodBelowIb,
                CperiodInsideIb: cperiodInsideIb,
                IbHighFormedFirst: ibHighFormedFirst,
                IbLowFormedFirst: ibLowFormedFirst));
        }

        return result;
    }

    private static MarketRegime ClassifyByIB(
        double ibYestHigh,
        double ibYestLow,
        double openToday,
        double ibToday30MinRatio,
        double ibTodayFullRatio,
        double overnightRatio,
        double gapRatio,
        int otfDirection,
        bool cperiodAboveIb30,
        bool cperiodBelowIb30,
        bool cperiodAboveIb,
        bool cperiodBelowIb,
        bool cperiodInsideIb,
        bool atr14IsFallback,
        out string reason)
    {
        var ibYestValid = !double.IsNaN(ibYestHigh) && !double.IsNaN(ibYestLow);
        var openOutside = ibYestValid && (openToday > ibYestHigh || openToday < ibYestLow);

        if (atr14IsFallback)
        {
            if (ibToday30MinRatio > 2.0 || overnightRatio > 2.0)
            {
                reason = $"HighVol: ib30={ibToday30MinRatio:F2} overnight={overnightRatio:F2}";
                return MarketRegime.HighVolatility;
            }

            if (ibToday30MinRatio < 0.15 && gapRatio < 0.05 && overnightRatio < 0.80)
            {
                reason = $"NonTrend: ib30={ibToday30MinRatio:F2} gap={gapRatio:F2}";
                return MarketRegime.NonTrend;
            }

            if (gapRatio > 0.50 && ibToday30MinRatio < 0.55)
            {
                reason = $"Breakout: gap={gapRatio:F2} ib30={ibToday30MinRatio:F2}";
                return MarketRegime.Breakout;
            }

            if (openOutside && ibToday30MinRatio < 0.60)
            {
                var otfNote = otfDirection != 0 ? $" otf={otfDirection:+0;-0}" : " otf=0(unconfirmed)";
                reason = $"Trend: openOutsideIB ib30={ibToday30MinRatio:F2}{otfNote}";
                return MarketRegime.Trend;
            }

            if (!openOutside && ibToday30MinRatio is >= 0.60 and <= 2.00)
            {
                reason = $"Range: openInsideIB ib30={ibToday30MinRatio:F2}";
                return MarketRegime.Range;
            }

            if (!openOutside && ibToday30MinRatio < 0.60 && gapRatio > 0.20)
            {
                reason = $"Breakout(lateGap): openInsideIB ib30={ibToday30MinRatio:F2} gap={gapRatio:F2}";
                return MarketRegime.Breakout;
            }
        }

        if (ibTodayFullRatio > 2.0 || overnightRatio > 2.0)
        {
            reason = $"HighVol: ibFull={ibTodayFullRatio:F2} overnight={overnightRatio:F2}";
            return MarketRegime.HighVolatility;
        }

        if (ibTodayFullRatio < 0.15 && gapRatio < 0.05 && overnightRatio < 0.80)
        {
            reason = $"NonTrend: ibFull={ibTodayFullRatio:F2} gap={gapRatio:F2}";
            return MarketRegime.NonTrend;
        }

        if (overnightRatio > 1.20 || gapRatio > 1.00)
        {
            reason = $"Breakout: gap={gapRatio:F2} overnight={overnightRatio:F2}";
            return MarketRegime.Breakout;
        }

        if (ibToday30MinRatio is >= 0.45 and < 0.60 && (openOutside || cperiodAboveIb30 || cperiodBelowIb30))
        {
            reason = $"Trend: balancedA ib30={ibToday30MinRatio:F2} openOut={openOutside} cBreak30={cperiodAboveIb30 || cperiodBelowIb30}";
            return MarketRegime.Trend;
        }

        if (ibTodayFullRatio < 0.24 && otfDirection != 0)
        {
            reason = $"Trend: narrowFullOtf ibFull={ibTodayFullRatio:F2} otf={otfDirection:+0;-0}";
            return MarketRegime.Trend;
        }

        if (ibTodayFullRatio is >= 0.50 and < 0.75 && openOutside && otfDirection != 0)
        {
            reason = $"Trend: normalIB+confirm ibFull={ibTodayFullRatio:F2} otf={otfDirection:+0;-0}";
            return MarketRegime.Trend;
        }

        if (ibTodayFullRatio is >= 0.50 and <= 1.50 && !openOutside && cperiodInsideIb)
        {
            reason = $"Range: ibFull={ibTodayFullRatio:F2} cperiodInside openInsideIB";
            return MarketRegime.Range;
        }

        if (ibTodayFullRatio is >= 0.24 and < 0.50 && !openOutside && !cperiodAboveIb && !cperiodBelowIb)
        {
            reason = $"Range: compressedIB ibFull={ibTodayFullRatio:F2} noCperiodBreakout";
            return MarketRegime.Range;
        }

        reason = $"Undefined: openOut={openOutside} ibFull={ibTodayFullRatio:F2} gap={gapRatio:F2} cperiodInside={cperiodInsideIb}";
        return MarketRegime.Undefined;
    }

    private static (bool HighFirst, bool LowFirst) GetIbFormation(
        IReadOnlyList<MarketBar> bars,
        double ibHigh,
        double ibLow)
    {
        if (bars.Count == 0 || double.IsNaN(ibHigh) || double.IsNaN(ibLow))
        {
            return (false, false);
        }

        var firstHighBar = bars.FirstOrDefault(b => b.High >= ibHigh);
        var firstLowBar = bars.FirstOrDefault(b => b.Low <= ibLow);

        if (firstHighBar is not null && firstLowBar is not null)
        {
            return (firstHighBar.Time <= firstLowBar.Time, firstLowBar.Time < firstHighBar.Time);
        }

        if (firstHighBar is not null)
        {
            return (true, false);
        }

        return firstLowBar is not null
            ? (false, true)
            : (false, false);
    }

    private static (double High, double Low) GetIbWindow(
        IEnumerable<MarketBar> bars,
        int startHHmm,
        int endHHmm)
    {
        var window = bars
            .Where(b => ToHHmm(b.Time) >= startHHmm && ToHHmm(b.Time) < endHHmm)
            .ToList();

        if (window.Count == 0)
        {
            return (double.NaN, double.NaN);
        }

        return (window.Max(b => b.High), window.Min(b => b.Low));
    }

    private static int CheckOneTimeFraming(IEnumerable<MarketBar> bars930to1030)
    {
        var closes = bars930to1030
            .Where(b => ToHHmm(b.Time) >= SessionOpenHHmm && ToHHmm(b.Time) <= IbEndHHmm)
            .OrderBy(b => b.Time)
            .Select(b => b.Close)
            .ToList();

        if (closes.Count < 4)
        {
            return 0;
        }

        var sample = closes.TakeLast(Math.Min(closes.Count, 6)).ToList();
        var allUp = sample.Zip(sample.Skip(1)).All(p => p.Second > p.First);
        var allDown = sample.Zip(sample.Skip(1)).All(p => p.Second < p.First);

        return allUp ? 1 : allDown ? -1 : 0;
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

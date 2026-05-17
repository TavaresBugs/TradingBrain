namespace TradingBrain.Core;

public static class RegimeClassifier
{
    private const int SessionOpenHHmm = 930;
    private const int SessionCloseHHmm = 1600;
    private const int IbEndHHmm = 1025;
    private const int CperiodStartHHmm = 1030;
    private const int CperiodEndHHmm = 1055;
    private const int OvernightStartHHmm = 1800;

    // Thresholds calibrados empiricamente em MNQ 12 meses (mar/2025-mai/2026).
    // Rever se o dataset mudar significativamente ou o instrumento trocar.
    private const double NonTrendIbThreshold = 0.05;
    private const double HighVolOvernightThreshold = 1.38;
    private const double BreakoutOvernightThreshold = 1.0;
    private const double BreakoutGapThreshold = 0.40;
    private const double BreakoutIbFullThreshold = 0.75;
    private const double TrendIbFullMaxThreshold = 0.75;
    private const double RangeOvernightThreshold = 0.87;
    private const double RangeIbFullMaxThreshold = 1.50;

    /// <summary>
    /// Classifica cada dia usando sinais IB derivados das barras de mercado.
    /// A classificacao do dia usa somente barras ate o fechamento da propria sessao.
    /// </summary>
    public static IReadOnlyList<DayRegime> Classify(IReadOnlyList<MarketBar> bars)
    {
        var byDate = bars
            .GroupBy(b => b.Time.Date)
            .OrderBy(g => g.Key)
            .ToList();

        var atr14ByDate = ComputeDailyAtr14(byDate);
        var result = new List<DayRegime>();

        for (var d = 1; d < byDate.Count; d++)
        {
            var today = byDate[d].Key;
            var todayBars = byDate[d].OrderBy(b => b.Time).ToList();
            var prevDate = byDate[d - 1].Key;
            var prevBars = byDate[d - 1].OrderBy(b => b.Time).ToList();

            if (!atr14ByDate.TryGetValue(prevDate, out var atr14)
                || double.IsNaN(atr14)
                || atr14 <= 0)
            {
                continue;
            }

            var prevSessionBars = prevBars
                .Where(b => HHmm(b.Time) >= SessionOpenHHmm
                         && HHmm(b.Time) <= SessionCloseHHmm)
                .ToList();

            if (prevSessionBars.Count == 0)
            {
                continue;
            }

            var prevClose = prevSessionBars.Last().Close;

            var prevIbBars = prevSessionBars
                .Where(b => HHmm(b.Time) >= SessionOpenHHmm
                         && HHmm(b.Time) <= IbEndHHmm)
                .ToList();

            var ibHighYest = prevIbBars.Count > 0 ? prevIbBars.Max(b => b.High) : double.NaN;
            var ibLowYest = prevIbBars.Count > 0 ? prevIbBars.Min(b => b.Low) : double.NaN;
            var ibRangeYest = ValidRange(ibHighYest, ibLowYest);
            var ibFullYest = ibRangeYest / atr14;

            var firstBar = todayBars.FirstOrDefault(b => HHmm(b.Time) >= SessionOpenHHmm);
            if (firstBar is null)
            {
                continue;
            }

            var openToday = firstBar.Open;
            var gapRatio = Math.Abs(openToday - prevClose) / atr14;
            var openOutside = !double.IsNaN(ibHighYest)
                && !double.IsNaN(ibLowYest)
                && (openToday > ibHighYest || openToday < ibLowYest);

            var todayIbBars = todayBars
                .Where(b => HHmm(b.Time) >= SessionOpenHHmm
                         && HHmm(b.Time) <= IbEndHHmm)
                .ToList();

            var ibHighToday = todayIbBars.Count > 0 ? todayIbBars.Max(b => b.High) : double.NaN;
            var ibLowToday = todayIbBars.Count > 0 ? todayIbBars.Min(b => b.Low) : double.NaN;
            var ibRangeToday = ValidRange(ibHighToday, ibLowToday);
            var ibFullToday = ibRangeToday / atr14;

            var cperiodBars = todayBars
                .Where(b => HHmm(b.Time) >= CperiodStartHHmm
                         && HHmm(b.Time) <= CperiodEndHHmm)
                .ToList();

            var cperiodInside = cperiodBars.Count > 0
                && !double.IsNaN(ibHighToday)
                && !double.IsNaN(ibLowToday)
                && cperiodBars.Max(b => b.High) <= ibHighToday
                && cperiodBars.Min(b => b.Low) >= ibLowToday;

            var prevOvernightBars = prevBars
                .Where(b => HHmm(b.Time) >= OvernightStartHHmm)
                .ToList();
            var todayOvernightBars = todayBars
                .Where(b => HHmm(b.Time) < SessionOpenHHmm)
                .ToList();
            var allOvernight = prevOvernightBars.Concat(todayOvernightBars).ToList();
            var overnightRange = allOvernight.Count > 0
                ? allOvernight.Max(b => b.High) - allOvernight.Min(b => b.Low)
                : 0;
            var overnightRatio = overnightRange / atr14;

            var regime = Classify(
                ibFullToday,
                openOutside,
                cperiodInside,
                overnightRatio,
                gapRatio,
                out var reason);

            result.Add(new DayRegime(
                Date: DateOnly.FromDateTime(today),
                Regime: regime,
                Reason: reason,
                IbHighYest: ibHighYest,
                IbLowYest: ibLowYest,
                IbFullYest: ibFullYest,
                IbFullToday: ibFullToday,
                OpenOutside: openOutside,
                CperiodInside: cperiodInside,
                OvernightRatio: overnightRatio,
                GapRatio: gapRatio,
                Atr14: atr14));
        }

        return result;
    }

    private static MarketRegime Classify(
        double ibFullToday,
        bool openOutside,
        bool cperiodInside,
        double overnightRatio,
        double gapRatio,
        out string reason)
    {
        if (ibFullToday < NonTrendIbThreshold)
        {
            reason = $"NonTrend: ibFull={ibFullToday:F2}";
            return MarketRegime.NonTrend;
        }

        if (overnightRatio > HighVolOvernightThreshold)
        {
            reason = $"HighVol: overnightRatio={overnightRatio:F2}";
            return MarketRegime.HighVolatility;
        }

        if (openOutside
            && ibFullToday > BreakoutIbFullThreshold)
        {
            reason = $"WideIbBreakout: openOutside=true ibFull={ibFullToday:F2} overnight={overnightRatio:F2} gap={gapRatio:F2}";
            return MarketRegime.WideIbBreakout;
        }

        if (openOutside
            && ibFullToday <= BreakoutIbFullThreshold
            && (overnightRatio > BreakoutOvernightThreshold
                || gapRatio > BreakoutGapThreshold))
        {
            reason = $"Breakout: openOutside=true ibFull={ibFullToday:F2} overnight={overnightRatio:F2} gap={gapRatio:F2}";
            return MarketRegime.Breakout;
        }

        if (openOutside
            && overnightRatio <= BreakoutOvernightThreshold
            && gapRatio <= BreakoutGapThreshold
            && ibFullToday <= TrendIbFullMaxThreshold)
        {
            reason = $"Trend: openOutside=true ibFull={ibFullToday:F2} overnight={overnightRatio:F2} gap={gapRatio:F2}";
            return MarketRegime.Trend;
        }

        if (!openOutside
            && overnightRatio <= RangeOvernightThreshold
            && ibFullToday <= RangeIbFullMaxThreshold
            && cperiodInside)
        {
            reason = $"Range: openOutside=false cperiodInside=true ibFull={ibFullToday:F2} overnight={overnightRatio:F2}";
            return MarketRegime.Range;
        }

        if (!openOutside
            && overnightRatio <= RangeOvernightThreshold
            && ibFullToday <= RangeIbFullMaxThreshold)
        {
            reason = $"IntradayExpansion: openOutside=false cperiodInside=false ibFull={ibFullToday:F2} overnight={overnightRatio:F2}";
            return MarketRegime.IntradayExpansion;
        }

        reason = $"Undefined: openOut={openOutside} ibFull={ibFullToday:F2} gap={gapRatio:F2} overnight={overnightRatio:F2} cperiod={cperiodInside}";
        return MarketRegime.Undefined;
    }

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
                .Where(b => HHmm(b.Time) >= SessionOpenHHmm
                         && HHmm(b.Time) <= SessionCloseHHmm)
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

    private static double ValidRange(double high, double low)
        => !double.IsNaN(high) && !double.IsNaN(low) && high > low ? high - low : 0;

    private static int HHmm(DateTime time) => time.Hour * 100 + time.Minute;
}

namespace TradingBrain.Core;

public enum MarketRegime
{
    Undefined,
    Trend,
    Breakout,
    Range,
    HighVolatility,
    NonTrend
}

public sealed record DayRegime(
    DateOnly Date,
    MarketRegime Regime,
    double RangeRatio,
    double ClosePosition,
    double OvernightRatio,
    double GapRatio,
    double Ker,
    string Reason,
    double IbYestHigh = double.NaN,
    double IbYestLow = double.NaN,
    double IbToday30MinRatio = double.NaN,
    bool OpenOutsideIbYest = false,
    bool OneTimeFramingUp = false,
    bool OneTimeFramingDown = false,
    double IbTodayFullRatio = double.NaN,
    double IbTodayFullHigh = double.NaN,
    double IbTodayFullLow = double.NaN,
    bool CperiodAboveIb = false,
    bool CperiodBelowIb = false,
    bool CperiodInsideIb = false,
    bool IbHighFormedFirst = false,
    bool IbLowFormedFirst = false);

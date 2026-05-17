namespace TradingBrain.Core;

public enum MarketRegime
{
    Undefined,
    Trend,
    Breakout,
    IntradayExpansion,
    Range,
    HighVolatility,
    NonTrend
}

public sealed record DayRegime(
    DateOnly Date,
    MarketRegime Regime,
    string Reason,

    double IbHighYest,
    double IbLowYest,
    double IbFullYest,

    double IbFullToday,
    bool OpenOutside,
    bool CperiodInside,
    double OvernightRatio,
    double GapRatio,
    double Atr14);

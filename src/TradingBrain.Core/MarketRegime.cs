namespace TradingBrain.Core;

public enum MarketRegime
{
    Undefined,
    Trend,
    Breakout,
    WideIbBreakout,
    IntradayExpansion,
    Range,
    HighVolatility,
    NonTrend,
    Limbo
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
    double Atr14,
    double DayRangeAtr = 0,
    double CloseLocation = 0.5,
    double DirectionalEfficiency = 0,
    double IbExtensionAtr = 0,
    bool CloseOutsideIb = false,
    bool BrokeBothIbSides = false,
    int VwapCrossCount = 0);

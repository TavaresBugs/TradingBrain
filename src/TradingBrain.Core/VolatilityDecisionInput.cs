namespace TradingBrain.Core;

public sealed record VolatilityDecisionInput(
    DateTime Time,
    double Close,
    double EmaFast,
    double EmaSlow,
    double Rsi,
    double Vwap,
    double Atr,
    double AtrSma,
    double Volume,
    double VolumeSma,
    bool IsLong,
    bool IsShort,
    int BarsSinceEntry,
    double OpenProfit,
    double HighestProfit);

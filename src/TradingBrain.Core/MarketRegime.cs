namespace TradingBrain.Core;

public enum MarketRegime
{
    Undefined,
    Trend,
    Breakout,
    Range,
    HighVolatility
}

public sealed record DayRegime(
    DateOnly Date,
    MarketRegime Regime,
    double RangeRatio,
    double ClosePosition,
    double OvernightRatio,
    double GapRatio,
    string Reason);

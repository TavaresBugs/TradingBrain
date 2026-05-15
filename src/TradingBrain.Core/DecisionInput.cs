namespace TradingBrain.Core;

public sealed record DecisionInput(
    double ShortAverage,
    double LongAverage,
    double Rsi,
    double LastPrice,
    bool IsLong,
    bool IsShort);

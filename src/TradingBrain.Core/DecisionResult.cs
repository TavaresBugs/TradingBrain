namespace TradingBrain.Core;

public sealed record DecisionResult(
    SignalAction Action,
    string Reason,
    double ShortAverage,
    double LongAverage,
    double Rsi);

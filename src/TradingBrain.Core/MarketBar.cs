namespace TradingBrain.Core;

public sealed record MarketBar(
    DateTime Time,
    double Open,
    double High,
    double Low,
    double Close,
    long Volume);

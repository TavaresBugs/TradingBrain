namespace TradingBrain.Core;

public sealed record StrategyDecision(SignalAction Action, string Reason);

public sealed record StrategyBacktestRow(
    string StrategyName,
    MarketBar Bar,
    SignalAction Signal,
    string Reason,
    int Position,
    double EntryPrice,
    double OpenProfit,
    double RealizedProfit,
    double Equity,
    double Drawdown,
    IReadOnlyDictionary<string, double> Metrics);

public sealed record BacktestSummary(
    string StrategyName,
    int Bars,
    int Signals,
    int ClosedTrades,
    int Wins,
    int Losses,
    double WinRate,
    double GrossProfit,
    double GrossLoss,
    double ProfitFactor,
    double AverageWin,
    double AverageLoss,
    double PayoffRatio,
    double Expectancy,
    double TradeStdDev,
    double GrossPnL,
    double TotalCosts,
    double NetPnL,
    double NetProfitFactor,
    double NetExpectancy,
    double GrossCurrency,
    double NetCurrency,
    double MaxDrawdown,
    double ReturnToDrawdown);

public sealed record TradeResult(
    string StrategyName,
    string Direction,
    DateTime EntryTime,
    DateTime ExitTime,
    double EntryPrice,
    double ExitPrice,
    int BarsHeld,
    double PnL,
    double GrossPoints,
    double NetPoints,
    double GrossCurrency,
    double NetCurrency,
    double TotalCostCurrency,
    double SlippageCostCurrency,
    double SpreadCostCurrency,
    double CommissionCostCurrency,
    int Quantity,
    double MaxFavorableExcursion,
    double MaxAdverseExcursion,
    string EntryReason,
    string ExitReason);

public sealed record StrategyTuningParams(
    double VolatilityMinAtrRatio = 1.1,
    double VolatilityMinVolumeRatio = 1.5,
    bool UseSqueezeFilter = true,
    double VolatilitySqueezeRatio = 0.95,
    double RangeCompressionRatio = 1.05,
    double MomentumMinMacdAtrRatio = 0.1,
    double MomentumVolumeRatio = 1.2,
    double EmaVolumeRatio = 1.1,
    double AtrStopMultiplier = 1.5,
    int TrailingActivationBars = 2,
    double EmaTrailingAtrOffset = 0,
    double TrendAtrStopMultiplier = 2.0,
    double GoldBreakoutAtrStopMultiplier = 2.0)
{
    public static StrategyTuningParams RefinedDefault { get; } = new();

    public static StrategyTuningParams BaselineLike { get; } = new(
        VolatilityMinAtrRatio: 1.0,
        VolatilityMinVolumeRatio: 1.2,
        UseSqueezeFilter: false,
        VolatilitySqueezeRatio: 1.0,
        RangeCompressionRatio: double.PositiveInfinity,
        MomentumMinMacdAtrRatio: 0,
        MomentumVolumeRatio: 1.0,
        EmaVolumeRatio: 1.0,
        AtrStopMultiplier: 1.5,
        TrailingActivationBars: 2,
        EmaTrailingAtrOffset: 0,
        TrendAtrStopMultiplier: 2.0,
        GoldBreakoutAtrStopMultiplier: 2.0);
}

public sealed record GridSearchResult(
    StrategyKind Strategy,
    StrategyTuningParams Params,
    BacktestSummary Summary);

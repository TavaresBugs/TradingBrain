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
    double ReturnToDrawdown,
    string IsLabel = "IS");

public sealed record DataSplit(
    IReadOnlyList<MarketBar> InSample,
    IReadOnlyList<MarketBar> OutSample)
{
    public static DataSplit SplitChronological(IReadOnlyList<MarketBar> bars, double ratio = 0.65)
    {
        ArgumentNullException.ThrowIfNull(bars);
        if (ratio <= 0 || ratio >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ratio), "Ratio must be between 0 and 1.");
        }

        var splitIndex = bars.Count < 2
            ? bars.Count
            : Math.Clamp((int)Math.Floor(bars.Count * ratio), 1, bars.Count - 1);
        return new DataSplit(
            bars.Take(splitIndex).ToList(),
            bars.Skip(splitIndex).ToList());
    }
}

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

public enum VolatilityExpansionMode
{
    Atr,
    CandleRange,
    AtrAndCandleRange
}

public enum VolatilityTrailingMode
{
    VwapEmaChandelier,
    AtrChandelier
}

public sealed record StrategyTuningParams(
    double VolatilityMinAtrRatio = 1.2,
    double VolatilityMinVolumeRatio = 1.5,
    bool UseSqueezeFilter = false,
    double VolatilitySqueezeRatio = 0.95,
    double VolatilityRangeMultiplier = 1.5,
    VolatilityExpansionMode VolatilityExpansionMode = VolatilityExpansionMode.Atr,
    double VwapMinDistance = 0.001,
    double RsiLongMax = 70,
    double RsiShortMin = 30,
    VolatilityTrailingMode VolatilityTrailingMode = VolatilityTrailingMode.VwapEmaChandelier,
    double AtrChandelierMultiplier = 3.0,
    int MaxBarsWithoutProfit = 5,
    double MinProfitAtrRatio = 1.0,
    double RangeCompressionRatio = 1.05,
    double MomentumMinMacdAtrRatio = 0.1,
    double MomentumVolumeRatio = 1.2,
    double EmaVolumeRatio = 1.1,
    double AtrStopMultiplier = 1.5,
    int TrailingActivationBars = 3,
    double EmaTrailingAtrOffset = 0,
    double TrendAtrStopMultiplier = 2.0,
    double GoldBreakoutAtrStopMultiplier = 2.0)
{
    public static StrategyTuningParams RefinedDefault { get; } = new();

    public static StrategyTuningParams BaselineLike { get; } = new(
        VolatilityMinAtrRatio: 1.0,
        VolatilityMinVolumeRatio: 1.2,
        UseSqueezeFilter: false,
        VolatilitySqueezeRatio: 0.95,
        VwapMinDistance: 0,
        RangeCompressionRatio: double.PositiveInfinity,
        MomentumMinMacdAtrRatio: 0,
        MomentumVolumeRatio: 1.0,
        EmaVolumeRatio: 1.0,
        AtrStopMultiplier: 1.5,
        TrailingActivationBars: 3,
        EmaTrailingAtrOffset: 0,
        TrendAtrStopMultiplier: 2.0,
        GoldBreakoutAtrStopMultiplier: 2.0);
}

public sealed record GridSearchResult(
    StrategyKind Strategy,
    StrategyTuningParams Params,
    BacktestSummary Summary);

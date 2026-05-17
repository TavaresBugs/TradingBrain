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
    IReadOnlyDictionary<string, double> Metrics,
    double StopPrice = 0.0,
    double TargetPrice = 0.0);

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
    int EntryBarIndex,
    int ExitBarIndex,
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
    string ExitReason,
    double StopPrice = 0.0,
    double TargetPrice = 0.0,
    double RMultiple = 0.0,
    double RiskPoints = double.NaN,
    double MAEPoints = 0.0,
    double MFEPoints = 0.0,
    double MAER = double.NaN,
    double MFER = double.NaN,
    double BestFavorablePrice = 0.0,
    double WorstAdversePrice = 0.0,
    bool HitHalfR = false,
    bool HitOneR = false,
    bool HitOneAndHalfR = false,
    bool HitTwoR = false,
    bool HitThreeR = false,
    bool HitMinusHalfR = false,
    bool HitMinusOneR = false,
    int? BarsToHalfR = null,
    int? BarsToOneR = null,
    int? BarsToOneAndHalfR = null,
    int? BarsToTwoR = null,
    int? BarsToThreeR = null,
    int? BarsToMinusHalfR = null,
    int? BarsToMinusOneR = null);

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
    double OrbAtrStopMultiplier = 2.0,
    double VwapReversionBand = 0.002,
    int RsiOversold = 35,
    int RsiOverbought = 65,
    double VwapReversionVolumeRatio = 1.1,
    double BbStdDev = 2.0,
    int BbFadeRsiOversold = 35,
    int BbFadeRsiOverbought = 65,
    double SessionBreakoutAtrBuffer = 0.1,
    double SessionMinRangeAtrRatio = 0.5,
    bool UseAntiMode = false,
    int SrsReferenceCandle = 2,
    int OvernightRangeStartHHmmss = 0,
    int OvernightRangeEndHHmmss = 93000,
    double SrsAtrBuffer = 0.1,
    double SrsAtrStopMultiplier = 1.5,
    double SrsAtrTargetMultiplier = 2.0,
    int OrbRangeStartHHmmss = 93000,
    int OrbRangeEndHHmmss = 100000,
    int OrbMinWindowBars = 2,
    double OrbMinRangeAtrRatio = 0.3,
    double OrbBreakoutBuffer = 0.05,
    bool OrbRequireVolume = false,
    double OrbVolumeRatio = 1.1,
    double IbTargetMultiplier = 1.0,
    bool IbUseHalfRangeStop = false,
    double IbMinRangeRatio = 0.0,
    double IbMaxRangeRatio = 10.0,
    bool IbRequireVolume = false,
    int TrendTimeExitBars = 60)
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
        OrbAtrStopMultiplier: 2.0,
        VwapReversionBand: 0.002,
        RsiOversold: 35,
        RsiOverbought: 65,
        VwapReversionVolumeRatio: 1.1,
        BbStdDev: 2.0,
        BbFadeRsiOversold: 35,
        BbFadeRsiOverbought: 65,
        SessionBreakoutAtrBuffer: 0.1,
        SessionMinRangeAtrRatio: 0.5,
        UseAntiMode: false,
        SrsReferenceCandle: 2,
        OvernightRangeStartHHmmss: 0,
        OvernightRangeEndHHmmss: 93000,
        SrsAtrBuffer: 0.1,
        SrsAtrStopMultiplier: 1.5,
        SrsAtrTargetMultiplier: 2.0,
        OrbRangeStartHHmmss: 93000,
        OrbRangeEndHHmmss: 100000,
        OrbMinWindowBars: 2,
        OrbMinRangeAtrRatio: 0.3,
        OrbBreakoutBuffer: 0.05,
        OrbRequireVolume: false,
        OrbVolumeRatio: 1.1,
        IbTargetMultiplier: 1.0,
        IbUseHalfRangeStop: false,
        IbMinRangeRatio: 0.0,
        IbMaxRangeRatio: 10.0,
        IbRequireVolume: false);
}

public sealed record GridSearchResult(
    StrategyKind Strategy,
    StrategyTuningParams Params,
    BacktestSummary Summary);

public sealed record WalkForwardWindow(
    int WindowIndex,
    int IsBars,
    int OosBars,
    GridSearchResult IsWinner,
    GridSearchResult? OosResult,
    int FilteredDaysTotal = 0,
    int TotalDaysTotal = 0);

public sealed record WalkForwardSummary(
    IReadOnlyList<WalkForwardWindow> Windows,
    double MedianOosScore,
    double WinRate,
    double MedianOosTrades,
    double ConsistencyRatio);

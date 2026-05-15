namespace TradingBrain.Core;

public sealed record VolatilityDecisionConfig(
    int ContractSize = 1,
    int SessionStartHHmmss = 93000,
    int SessionEndHHmmss = 160000,
    int EmaFastPeriod = 9,
    int EmaSlowPeriod = 21,
    int RsiPeriod = 14,
    int RsiSmooth = 3,
    int AtrPeriod = 14,
    int AtrSmaPeriod = 14,
    int VolumeSmaPeriod = 20,
    double VolumeThreshold = 1.2,
    bool UseVwapFilter = true,
    double TrailStopMultiplier = 2.0,
    double BreakEvenMultiplier = 1.0,
    int TimeExitBars = 20,
    double MaxDrawdown = 500);

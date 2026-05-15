namespace TradingBrain.Core;

public sealed record ExecutionSettings(
    double TickSize = 0.25,
    double TickValue = 0.50,
    double SlippageTicks = 1,
    double SpreadTicks = 1,
    double CommissionPerSide = 0,
    int Quantity = 1)
{
    public static ExecutionSettings MnqDefault { get; } = new();

    public double SlippageCostCurrency =>
        SlippageTicks * TickValue * Quantity * 2;

    public double SpreadCostCurrency =>
        SpreadTicks * TickValue * Quantity;

    public double CommissionCostCurrency =>
        CommissionPerSide * Quantity * 2;

    public double TotalRoundTripCostCurrency =>
        SlippageCostCurrency + SpreadCostCurrency + CommissionCostCurrency;

    public double PointsToCurrency(double points) =>
        TickSize == 0 ? 0 : points / TickSize * TickValue * Quantity;

    public double CurrencyToPoints(double currency) =>
        TickValue == 0 || Quantity == 0 ? 0 : currency / (TickValue * Quantity) * TickSize;
}

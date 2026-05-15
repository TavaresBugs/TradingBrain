namespace TradingBrain.Core;

public sealed record ExecutionSettings
{
    public ExecutionSettings(
        double tickSize = 0.25,
        double tickValue = 0.50,
        double slippageTicks = 1,
        double spreadTicks = 1,
        double commissionPerSide = 0,
        int quantity = 1)
    {
        ValidatePositive(tickSize, nameof(TickSize));
        ValidatePositive(tickValue, nameof(TickValue));
        ValidateNonNegative(slippageTicks, nameof(SlippageTicks));
        ValidateNonNegative(spreadTicks, nameof(SpreadTicks));
        ValidateNonNegative(commissionPerSide, nameof(CommissionPerSide));

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Quantity), quantity, "Quantity must be greater than zero.");
        }

        if (slippageTicks > spreadTicks)
        {
            throw new ArgumentException("SlippageTicks cannot be greater than SpreadTicks.");
        }

        TickSize = tickSize;
        TickValue = tickValue;
        SlippageTicks = slippageTicks;
        SpreadTicks = spreadTicks;
        CommissionPerSide = commissionPerSide;
        Quantity = quantity;
    }

    public static ExecutionSettings MnqDefault { get; } = new();

    public double TickSize { get; }

    public double TickValue { get; }

    public double SlippageTicks { get; }

    public double SpreadTicks { get; }

    public double CommissionPerSide { get; }

    public int Quantity { get; }

    public double SlippageCostCurrency =>
        SlippageTicks * TickValue * Quantity * 2;

    public double SpreadCostCurrency =>
        SpreadTicks * TickValue * Quantity;

    public double CommissionCostCurrency =>
        CommissionPerSide * Quantity * 2;

    public double TotalRoundTripCostCurrency =>
        SlippageCostCurrency + SpreadCostCurrency + CommissionCostCurrency;

    public double PointsToCurrency(double points) =>
        points / TickSize * TickValue * Quantity;

    public double CurrencyToPoints(double currency) =>
        currency / (TickValue * Quantity) * TickSize;

    private static void ValidatePositive(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"{parameterName} must be finite and greater than zero.");
        }
    }

    private static void ValidateNonNegative(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"{parameterName} must be finite and greater than or equal to zero.");
        }
    }
}

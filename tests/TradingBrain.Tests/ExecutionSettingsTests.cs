using TradingBrain.Core;

namespace TradingBrain.Tests;

public sealed class ExecutionSettingsTests
{
    [Fact]
    public void Constructor_AcceptsValidSettings()
    {
        var settings = new ExecutionSettings(
            tickSize: 0.25,
            tickValue: 0.50,
            slippageTicks: 1,
            spreadTicks: 1,
            commissionPerSide: 0.62,
            quantity: 2);

        Assert.Equal(0.25, settings.TickSize);
        Assert.Equal(4.0, settings.PointsToCurrency(1.0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.25)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_RejectsInvalidTickSize(double tickSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutionSettings(tickSize: tickSize));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.50)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_RejectsInvalidTickValue(double tickValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutionSettings(tickValue: tickValue));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_RejectsInvalidSlippage(double slippageTicks)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutionSettings(slippageTicks: slippageTicks));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_RejectsInvalidSpread(double spreadTicks)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutionSettings(spreadTicks: spreadTicks));
    }

    [Fact]
    public void Constructor_RejectsSlippageGreaterThanSpread()
    {
        Assert.Throws<ArgumentException>(() => new ExecutionSettings(slippageTicks: 2, spreadTicks: 1));
    }

    [Fact]
    public void Constructor_RejectsNegativeCommission()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutionSettings(commissionPerSide: -0.01));
    }

    [Fact]
    public void Constructor_RejectsInvalidQuantity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutionSettings(quantity: 0));
    }
}

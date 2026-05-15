using TradingBrain.Core;

namespace TradingBrain.Tests;

public sealed class DecisionEngineTests
{
    private readonly DecisionEngine _engine = new();

    [Fact]
    public void Evaluate_ReturnsBuyWhenFlatWithBullishAverageAndOversoldRsi()
    {
        var result = _engine.Evaluate(new DecisionInput(20, 10, 29, 100, IsLong: false, IsShort: false));

        Assert.Equal(SignalAction.Buy, result.Action);
    }

    [Fact]
    public void Evaluate_ReturnsSellWhenFlatWithBearishAverageAndOverboughtRsi()
    {
        var result = _engine.Evaluate(new DecisionInput(10, 20, 71, 100, IsLong: false, IsShort: false));

        Assert.Equal(SignalAction.Sell, result.Action);
    }

    [Fact]
    public void Evaluate_ReturnsExitWhenLongAndRsiIsOverbought()
    {
        var result = _engine.Evaluate(new DecisionInput(20, 10, 71, 100, IsLong: true, IsShort: false));

        Assert.Equal(SignalAction.Exit, result.Action);
    }

    [Fact]
    public void Evaluate_ReturnsExitWhenShortAndRsiIsOversold()
    {
        var result = _engine.Evaluate(new DecisionInput(10, 20, 29, 100, IsLong: false, IsShort: true));

        Assert.Equal(SignalAction.Exit, result.Action);
    }

    [Fact]
    public void Evaluate_ReturnsNoneWhenNoRuleMatches()
    {
        var result = _engine.Evaluate(new DecisionInput(10, 20, 50, 100, IsLong: false, IsShort: false));

        Assert.Equal(SignalAction.None, result.Action);
    }

    [Fact]
    public void Evaluate_ReturnsFirstInjectedRuleMatch()
    {
        var engine = new DecisionEngine(new IDecisionRule[]
        {
            new NoMatchRule(),
            new AlwaysBuyRule(),
        });

        var result = engine.Evaluate(new DecisionInput(10, 20, 50, 100, IsLong: false, IsShort: false));

        Assert.Equal(SignalAction.Buy, result.Action);
        Assert.Equal("custom", result.Reason);
    }

    [Fact]
    public void Constructor_RejectsEmptyRuleSet()
    {
        Assert.Throws<ArgumentException>(() => new DecisionEngine(Array.Empty<IDecisionRule>()));
    }

    private sealed class NoMatchRule : IDecisionRule
    {
        public DecisionResult? Evaluate(DecisionInput input) => null;
    }

    private sealed class AlwaysBuyRule : IDecisionRule
    {
        public DecisionResult? Evaluate(DecisionInput input) =>
            new(SignalAction.Buy, "custom", input.ShortAverage, input.LongAverage, input.Rsi);
    }
}

namespace TradingBrain.Core;

public interface IDecisionRule
{
    DecisionResult? Evaluate(DecisionInput input);
}

public sealed class DecisionEngine
{
    private static readonly IReadOnlyList<IDecisionRule> DefaultRules =
    [
        new FlatOversoldBullishAverageBuyRule(),
        new LongOverboughtExitRule(),
        new FlatOverboughtBearishAverageSellRule(),
        new ShortOversoldExitRule(),
    ];

    private readonly IReadOnlyList<IDecisionRule> _rules;

    public DecisionEngine()
        : this(DefaultRules)
    {
    }

    public DecisionEngine(IEnumerable<IDecisionRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = rules.ToList();
        if (_rules.Count == 0)
        {
            throw new ArgumentException("At least one decision rule is required.", nameof(rules));
        }
    }

    public DecisionResult Evaluate(DecisionInput input)
    {
        foreach (var rule in _rules)
        {
            var result = rule.Evaluate(input);
            if (result is not null)
            {
                return result;
            }
        }

        return new DecisionResult(
            SignalAction.None,
            "No signal",
            input.ShortAverage,
            input.LongAverage,
            input.Rsi);
    }
}

public sealed class FlatOversoldBullishAverageBuyRule : IDecisionRule
{
    public DecisionResult? Evaluate(DecisionInput input)
    {
        if (input.IsLong || input.IsShort ||
            input.ShortAverage <= input.LongAverage ||
            input.Rsi >= 30)
        {
            return null;
        }

        return new DecisionResult(
            SignalAction.Buy,
            "Short average above long average and RSI below 30",
            input.ShortAverage,
            input.LongAverage,
            input.Rsi);
    }
}

public sealed class LongOverboughtExitRule : IDecisionRule
{
    public DecisionResult? Evaluate(DecisionInput input)
    {
        if (!input.IsLong || input.Rsi <= 70)
        {
            return null;
        }

        return new DecisionResult(
            SignalAction.Exit,
            "Exit long: RSI above 70",
            input.ShortAverage,
            input.LongAverage,
            input.Rsi);
    }
}

public sealed class FlatOverboughtBearishAverageSellRule : IDecisionRule
{
    public DecisionResult? Evaluate(DecisionInput input)
    {
        if (input.IsLong || input.IsShort ||
            input.ShortAverage >= input.LongAverage ||
            input.Rsi <= 70)
        {
            return null;
        }

        return new DecisionResult(
            SignalAction.Sell,
            "Short average below long average and RSI above 70",
            input.ShortAverage,
            input.LongAverage,
            input.Rsi);
    }
}

public sealed class ShortOversoldExitRule : IDecisionRule
{
    public DecisionResult? Evaluate(DecisionInput input)
    {
        if (!input.IsShort || input.Rsi >= 30)
        {
            return null;
        }

        return new DecisionResult(
            SignalAction.Exit,
            "Exit short: RSI below 30",
            input.ShortAverage,
            input.LongAverage,
            input.Rsi);
    }
}

using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

public static class WalkForwardValidator
{
    public const int DefaultWindows = 5;
    public const double SplitRatio = 0.65;

    public static WalkForwardSummary Run(
        IReadOnlyList<MarketBar> bars,
        StrategyKind strategy,
        int windows = DefaultWindows,
        ExecutionSettings? executionSettings = null)
    {
        ArgumentNullException.ThrowIfNull(bars);
        ValidateWindowCount(bars, windows);
        var splits = SplitIndependentWindows(bars, windows);
        var rows = splits
            .Select((split, index) => RunWindow(split, index + 1, strategy, executionSettings))
            .ToList();

        return BuildSummary(rows);
    }

    private static void ValidateWindowCount(IReadOnlyList<MarketBar> bars, int windows)
    {
        if (windows <= 0)
            throw new ArgumentOutOfRangeException(nameof(windows), "Windows must be greater than zero.");

        if (windows > bars.Count / 2)
            throw new ArgumentException("Not enough bars for independent IS/OOS windows.", nameof(bars));
    }

    private static IReadOnlyList<DataSplit> SplitIndependentWindows(IReadOnlyList<MarketBar> bars, int windows)
    {
        var splits = new List<DataSplit>(windows);
        var start = 0;

        for (var i = 0; i < windows; i++)
        {
            var size = WindowSize(bars.Count, windows, i);
            var segment = bars.Skip(start).Take(size).ToList();
            splits.Add(DataSplit.SplitChronological(segment, SplitRatio));
            start += size;
        }

        return splits;
    }

    private static int WindowSize(int barCount, int windows, int index)
    {
        var baseSize = barCount / windows;
        return baseSize + (index < barCount % windows ? 1 : 0);
    }

    private static WalkForwardWindow RunWindow(
        DataSplit split,
        int index,
        StrategyKind strategy,
        ExecutionSettings? executionSettings)
    {
        var results = GridSearchRunner.Label(
            GridSearchRunner.Run(split.InSample, strategy, executionSettings),
            "IS");
        var winner = results.FirstOrDefault() ?? BuildFallbackWinner(split.InSample, strategy, executionSettings);
        var oos = ValidateWinner(split.OutSample, winner, executionSettings);

        return new WalkForwardWindow(index, split.InSample.Count, split.OutSample.Count, winner, oos);
    }

    private static GridSearchResult BuildFallbackWinner(
        IReadOnlyList<MarketBar> bars,
        StrategyKind strategy,
        ExecutionSettings? executionSettings)
    {
        var backtester = new StrategyBacktester(strategy, StrategyTuningParams.RefinedDefault);
        var summary = StrategyBacktester.Summarize(backtester.Run(bars), executionSettings) with { IsLabel = "IS" };
        return new GridSearchResult(strategy, StrategyTuningParams.RefinedDefault, summary);
    }

    private static GridSearchResult ValidateWinner(
        IReadOnlyList<MarketBar> bars,
        GridSearchResult winner,
        ExecutionSettings? executionSettings)
    {
        var backtester = new StrategyBacktester(winner.Strategy, winner.Params);
        var rows = backtester.Run(bars);
        var summary = StrategyBacktester.Summarize(rows, executionSettings) with { IsLabel = "OOS" };
        return winner with { Summary = summary };
    }

    private static WalkForwardSummary BuildSummary(IReadOnlyList<WalkForwardWindow> windows)
    {
        var scores = windows.Select(OosScoreOrFailure).ToList();
        var trades = windows.Select(w => w.OosResult?.Summary.ClosedTrades ?? 0.0).ToList();
        var winningScores = scores.Count(s => s > 0);
        var positivePnl = windows.Count(w => w.OosResult is not null && w.OosResult.Summary.NetPnL > 0);

        return new WalkForwardSummary(
            windows,
            Median(scores),
            Percent(winningScores, windows.Count),
            Median(trades),
            Ratio(positivePnl, windows.Count));
    }

    private static double OosScoreOrFailure(WalkForwardWindow window)
    {
        return window.OosResult is null
            ? double.NegativeInfinity
            : GridSearchRunner.Score(window.OosResult.Summary);
    }

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
            return 0;

        var ordered = values.OrderBy(v => v).ToList();
        var middle = ordered.Count / 2;
        return ordered.Count % 2 == 1 ? ordered[middle] : (ordered[middle - 1] + ordered[middle]) / 2.0;
    }

    private static double Percent(int count, int total)
    {
        return total == 0 ? 0 : count * 100.0 / total;
    }

    private static double Ratio(int count, int total)
    {
        return total == 0 ? 0 : count / (double)total;
    }
}

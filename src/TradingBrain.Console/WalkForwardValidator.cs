using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

public static class WalkForwardValidator
{
    public const double SplitRatio = 0.65;
    public const int DefaultWindows = 5;
    public const int MinSignificantOosTrades = 5;

    /// <summary>
    /// Walk-forward rolling com N janelas.
    /// Cada janela cobre uma fatia cronológica do dataset.
    /// IS = 65% da janela, OOS = 35%.
    /// Com windows=1 comporta-se como split simples IS/OOS.
    /// </summary>
    public static WalkForwardSummary Run(
        IReadOnlyList<MarketBar> bars,
        StrategyKind strategy,
        int windows = DefaultWindows,
        ExecutionSettings? executionSettings = null,
        bool applyRegimeFilter = true)
    {
        ArgumentNullException.ThrowIfNull(bars);
        if (windows < 1)
            throw new ArgumentOutOfRangeException(nameof(windows), "Windows must be >= 1.");

        var settings = executionSettings ?? ExecutionSettings.MnqDefault;
        var results = new List<WalkForwardWindow>();
        var barsToSplit = applyRegimeFilter && StrategyRegimeMap.HasFilter(strategy)
            ? RegimeFilter.Apply(bars, StrategyRegimeMap.For(strategy))
            : bars;

        var totalDays = bars.Select(b => b.Time.Date).Distinct().Count();
        var filteredDays = barsToSplit.Select(b => b.Time.Date).Distinct().Count();
        if (applyRegimeFilter && filteredDays < totalDays)
        {
            var targetRegime = StrategyRegimeMap.HasFilter(strategy)
                ? string.Join("+", StrategyRegimeMap.For(strategy))
                : "all";
            Console.WriteLine($"  [WF RegimeFilter] {strategy}: {filteredDays}/{totalDays} dias uteis ({targetRegime})");
        }

        if (filteredDays < 30)
        {
            Console.WriteLine($"  [WF RegimeFilter] {strategy}: dias insuficientes apos filtro ({filteredDays}). Pulando walk-forward.");
            return BuildSummary(Array.Empty<WalkForwardWindow>());
        }

        var windowSize = barsToSplit.Count / windows;
        if (windowSize < 1)
            throw new ArgumentException(
                $"Dataset muito pequeno para {windows} janelas. Mínimo de 1 barra por janela.");

        for (var w = 0; w < windows; w++)
        {
            var startIndex = w * windowSize;
            var endIndex = w == windows - 1 ? barsToSplit.Count : startIndex + windowSize;
            var windowBars = barsToSplit.Skip(startIndex).Take(endIndex - startIndex).ToList();

            var splitIndex = Math.Clamp(
                (int)Math.Floor(windowBars.Count * SplitRatio),
                1,
                windowBars.Count - 1);

            var isBars = windowBars.Take(splitIndex).ToList();
            var oosBars = windowBars.Skip(splitIndex).ToList();

            var isResults = GridSearchRunner.Run(isBars, strategy, settings, applyRegimeFilter: false);
            if (isResults.Count == 0)
                continue;

            var isWinner = isResults[0];

            GridSearchResult? rawOosResult = null;
            GridSearchResult? oosResult = null;
            if (oosBars.Count > 0)
            {
                var backtester = new StrategyBacktester(isWinner.Strategy, isWinner.Params);
                var oosSummary = StrategyBacktester.Summarize(
                    backtester.Run(oosBars), settings) with { IsLabel = "OOS" };

                rawOosResult = isWinner with { Summary = oosSummary };
                if (oosSummary.ClosedTrades >= MinSignificantOosTrades)
                    oosResult = rawOosResult;
            }

            results.Add(new WalkForwardWindow(
                WindowIndex: w + 1,
                IsBars: isBars.Count,
                OosBars: oosBars.Count,
                IsWinner: isWinner,
                OosResult: oosResult,
                RawOosResult: rawOosResult,
                FilteredDaysTotal: filteredDays,
                TotalDaysTotal: totalDays));
        }

        return BuildSummary(results);
    }

    private static WalkForwardSummary BuildSummary(IReadOnlyList<WalkForwardWindow> windows)
    {
        if (windows.Count == 0)
        {
            return new WalkForwardSummary(
                Windows: windows,
                MedianOosScore: double.NaN,
                WinRate: 0,
                MedianOosTrades: 0,
                ConsistencyRatio: 0);
        }

        var oosScores = windows
            .Where(w => w.OosResult is not null)
            .Select(w => GridSearchRunner.Score(w.OosResult!.Summary))
            .Where(s => !double.IsNegativeInfinity(s) && !double.IsNaN(s))
            .OrderBy(s => s)
            .ToList();

        var oosTrades = windows
            .Where(w => w.OosResult is not null)
            .Select(w => (double)w.OosResult!.Summary.ClosedTrades)
            .OrderBy(t => t)
            .ToList();

        var positiveOos = windows.Count(w =>
            w.OosResult is not null &&
            w.OosResult.Summary.NetExpectancy > 0 &&
            w.OosResult.Summary.NetProfitFactor > 1.0);

        var medianOosScore = oosScores.Count == 0
            ? double.NaN
            : Median(oosScores);

        var medianOosTrades = oosTrades.Count == 0
            ? 0
            : Median(oosTrades);

        var winRate = windows.Count == 0
            ? 0
            : positiveOos * 100.0 / windows.Count;

        var consistencyRatio = windows.Count == 0
            ? 0
            : windows.Count(w => w.OosResult is not null) * 100.0 / windows.Count;

        return new WalkForwardSummary(
            Windows: windows,
            MedianOosScore: medianOosScore,
            WinRate: winRate,
            MedianOosTrades: medianOosTrades,
            ConsistencyRatio: consistencyRatio);
    }

    private static double Median(IReadOnlyList<double> sorted)
    {
        var n = sorted.Count;
        return n % 2 == 0
            ? (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0
            : sorted[n / 2];
    }
}

using System.Globalization;
using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

public static class GridSearchRunner
{
    public static IReadOnlyList<GridSearchResult> Run(
        IReadOnlyList<MarketBar> bars,
        StrategyKind strategy,
        ExecutionSettings? executionSettings = null)
    {
        var settings = executionSettings ?? ExecutionSettings.MnqDefault;
        var results = new List<GridSearchResult>();
        foreach (var parameters in BuildParameterGrid(strategy))
        {
            var backtester = new StrategyBacktester(strategy, parameters);
            var rows = backtester.Run(bars);
            var summary = StrategyBacktester.Summarize(rows, settings);

            if (summary.ClosedTrades == 0)
            {
                continue;
            }

            results.Add(new GridSearchResult(strategy, parameters, summary));
        }

        return results
            .OrderByDescending(r => Score(r.Summary))
            .ThenByDescending(r => r.Summary.NetProfitFactor)
            .ThenByDescending(r => r.Summary.NetExpectancy)
            .ToList();
    }

    public static void ExportCsv(IReadOnlyList<GridSearchResult> results, string path)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("Strategy,Score,Trades,WinRate,ProfitFactor,Expectancy,GrossPnL,TotalCosts,NetPnL,NetProfitFactor,NetExpectancy,GrossCurrency,NetCurrency,MaxDrawdown,ReturnToDrawdown,VolMinAtr,VolMinVolume,UseSqueeze,SqueezeRatio,VolRangeMultiplier,VolExpansionMode,VwapMinDistance,RsiLongMax,RsiShortMin,VolTrailingMode,AtrChandelier,MaxBarsWithoutProfit,MinProfitAtrRatio,RangeCompression,MomentumMacdAtr,MomentumVolume,EmaVolume,AtrStop,TrailingBars,EmaTrailingOffset,TrendAtrStop,GoldAtrStop");

        foreach (var result in results)
        {
            var p = result.Params;
            var s = result.Summary;
            writer.WriteLine(string.Join(",",
                result.Strategy,
                F(Score(s)),
                s.ClosedTrades.ToString(CultureInfo.InvariantCulture),
                F(s.WinRate),
                F(s.ProfitFactor),
                F(s.Expectancy),
                F(s.GrossPnL),
                F(s.TotalCosts),
                F(s.NetPnL),
                F(s.NetProfitFactor),
                F(s.NetExpectancy),
                F(s.GrossCurrency),
                F(s.NetCurrency),
                F(s.MaxDrawdown),
                F(s.ReturnToDrawdown),
                F(p.VolatilityMinAtrRatio),
                F(p.VolatilityMinVolumeRatio),
                p.UseSqueezeFilter,
                F(p.VolatilitySqueezeRatio),
                F(p.VolatilityRangeMultiplier),
                p.VolatilityExpansionMode,
                F(p.VwapMinDistance),
                F(p.RsiLongMax),
                F(p.RsiShortMin),
                p.VolatilityTrailingMode,
                F(p.AtrChandelierMultiplier),
                p.MaxBarsWithoutProfit.ToString(CultureInfo.InvariantCulture),
                F(p.MinProfitAtrRatio),
                F(p.RangeCompressionRatio),
                F(p.MomentumMinMacdAtrRatio),
                F(p.MomentumVolumeRatio),
                F(p.EmaVolumeRatio),
                F(p.AtrStopMultiplier),
                p.TrailingActivationBars.ToString(CultureInfo.InvariantCulture),
                F(p.EmaTrailingAtrOffset),
                F(p.TrendAtrStopMultiplier),
                F(p.GoldBreakoutAtrStopMultiplier)));
        }
    }

    private static IEnumerable<StrategyTuningParams> BuildParameterGrid(StrategyKind strategy)
    {
        return strategy switch
        {
            StrategyKind.Momentum => MomentumGrid(),
            StrategyKind.Ema => EmaGrid(),
            StrategyKind.Volatility => VolatilityGrid(),
            StrategyKind.Range => RangeGrid(),
            StrategyKind.Trend => TrendGrid(),
            StrategyKind.GoldBreakout => GoldBreakoutGrid(),
            _ => new[] { StrategyTuningParams.RefinedDefault }
        };
    }

    private static IEnumerable<StrategyTuningParams> MomentumGrid()
    {
        foreach (var macdAtr in new[] { 0.0, 0.03, 0.06, 0.1 })
        foreach (var volume in new[] { 1.0, 1.1, 1.2, 1.35 })
        foreach (var stop in new[] { 1.2, 1.5, 1.8, 2.2 })
            yield return StrategyTuningParams.RefinedDefault with
            {
                MomentumMinMacdAtrRatio = macdAtr,
                MomentumVolumeRatio = volume,
                AtrStopMultiplier = stop
            };
    }

    private static IEnumerable<StrategyTuningParams> EmaGrid()
    {
        foreach (var volume in new[] { 1.0, 1.05, 1.1, 1.2 })
        foreach (var stop in new[] { 1.2, 1.5, 1.8, 2.2 })
        foreach (var offset in new[] { 0.0, 0.15, 0.3, 0.5 })
            yield return StrategyTuningParams.RefinedDefault with
            {
                EmaVolumeRatio = volume,
                AtrStopMultiplier = stop,
                EmaTrailingAtrOffset = offset
            };
    }

    private static IEnumerable<StrategyTuningParams> VolatilityGrid()
    {
        foreach (var atr in new[] { 1.0, 1.1, 1.2, 1.3, 1.4, 1.5, 1.6 })
        foreach (var volume in new[] { 1.2, 1.4, 1.6, 1.8, 2.0 })
        foreach (var squeeze in new[] { false, true })
        foreach (var squeezeRatio in new[] { 0.85, 0.95, 1.05 })
            yield return StrategyTuningParams.RefinedDefault with
            {
                VolatilityMinAtrRatio = atr,
                VolatilityMinVolumeRatio = volume,
                UseSqueezeFilter = squeeze,
                VolatilitySqueezeRatio = squeezeRatio
            };

        foreach (var stop in new[] { 1.0, 1.25, 1.5, 1.75, 2.0, 2.25, 2.5 })
        foreach (var trailingBars in new[] { 1, 2, 3, 4, 5, 6 })
        foreach (var trailingMode in Enum.GetValues<VolatilityTrailingMode>())
        foreach (var maxBars in new[] { 3, 5, 7 })
        foreach (var minProfit in new[] { 0.5, 1.0, 1.5 })
            yield return StrategyTuningParams.RefinedDefault with
            {
                AtrStopMultiplier = stop,
                TrailingActivationBars = trailingBars,
                VolatilityTrailingMode = trailingMode,
                MaxBarsWithoutProfit = maxBars,
                MinProfitAtrRatio = minProfit
            };

        foreach (var rsiLongMax in new[] { 60, 62, 64, 66, 68, 70 })
        foreach (var rsiShortMin in new[] { 30, 32, 34, 36, 38, 40 })
        foreach (var vwapDistance in new[] { 0.0, 0.001, 0.002 })
        foreach (var expansionMode in Enum.GetValues<VolatilityExpansionMode>())
            yield return StrategyTuningParams.RefinedDefault with
            {
                RsiLongMax = rsiLongMax,
                RsiShortMin = rsiShortMin,
                VwapMinDistance = vwapDistance,
                VolatilityExpansionMode = expansionMode
            };
    }

    private static IEnumerable<StrategyTuningParams> RangeGrid()
    {
        foreach (var compression in new[] { 1.0, 1.1, 1.25, 1.5, 2.0, double.PositiveInfinity })
        foreach (var stop in new[] { 1.2, 1.5, 1.8, 2.2 })
            yield return StrategyTuningParams.RefinedDefault with
            {
                RangeCompressionRatio = compression,
                AtrStopMultiplier = stop
            };
    }

    private static IEnumerable<StrategyTuningParams> TrendGrid()
    {
        foreach (var stop in new[] { 1.2, 1.6, 2.0, 2.5, 3.0 })
            yield return StrategyTuningParams.RefinedDefault with
            {
                TrendAtrStopMultiplier = stop
            };
    }

    private static IEnumerable<StrategyTuningParams> GoldBreakoutGrid()
    {
        foreach (var stop in new[] { 1.2, 1.6, 2.0, 2.5, 3.0 })
            yield return StrategyTuningParams.RefinedDefault with
            {
                GoldBreakoutAtrStopMultiplier = stop
            };
    }

    private static double Score(BacktestSummary summary)
    {
        if (summary.ClosedTrades < 30)
        {
            return double.NegativeInfinity;
        }

        return summary.ReturnToDrawdown * Math.Log10(summary.ClosedTrades + 1);
    }

    private static string F(double value)
    {
        if (double.IsPositiveInfinity(value))
        {
            return "Infinity";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        if (double.IsNaN(value))
        {
            return "";
        }

        return value.ToString("0.########", CultureInfo.InvariantCulture);
    }
}

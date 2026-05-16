using System.Globalization;
using System.Collections.Concurrent;
using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

public static class GridSearchRunner
{
    public const int MinTradesOos = 10;
    public const int MinTradesIsScore = 20;

    public static IReadOnlyList<GridSearchResult> Run(
        IReadOnlyList<MarketBar> bars,
        StrategyKind strategy,
        ExecutionSettings? executionSettings = null,
        bool applyRegimeFilter = true)
    {
        var settings = executionSettings ?? ExecutionSettings.MnqDefault;
        var filteredBars = applyRegimeFilter && StrategyRegimeMap.HasFilter(strategy)
            ? RegimeFilter.Apply(bars, StrategyRegimeMap.For(strategy))
            : bars;

        var totalDays = bars.Select(b => b.Time.Date).Distinct().Count();
        var filteredDays = filteredBars.Select(b => b.Time.Date).Distinct().Count();
        if (applyRegimeFilter && filteredDays < totalDays)
        {
            var regimeLabel = string.Join("|", StrategyRegimeMap.For(strategy).Select(r => r.ToString()));
            Console.WriteLine($"  [RegimeFilter] {strategy}: {filteredDays}/{totalDays} dias apos filtro de regime ({regimeLabel})");
        }

        if (filteredBars.Count == 0)
            return Array.Empty<GridSearchResult>();

        var series = PrecomputedSeries.From(filteredBars);
        var results = new ConcurrentBag<GridSearchResult>();

        Parallel.ForEach(BuildParameterGrid(strategy), parameters =>
        {
            var backtester = new StrategyBacktester(strategy, parameters, series);
            var rows = backtester.Run(filteredBars);
            var summary = StrategyBacktester.Summarize(rows, settings);

            if (summary.ClosedTrades > 0)
            {
                results.Add(new GridSearchResult(strategy, parameters, summary));
            }
        });

        return results
            .OrderByDescending(r => Score(r.Summary))
            .ThenByDescending(r => r.Summary.NetProfitFactor)
            .ThenByDescending(r => r.Summary.NetExpectancy)
            .ToList();
    }

    public static IReadOnlyList<GridSearchResult> Label(
        IReadOnlyList<GridSearchResult> results,
        string label)
    {
        return results
            .Select(r => r with { Summary = r.Summary with { IsLabel = label } })
            .ToList();
    }

    public static IReadOnlyList<GridSearchResult> ValidateOutOfSample(
        IReadOnlyList<MarketBar> bars,
        IReadOnlyList<GridSearchResult> winners,
        ExecutionSettings? executionSettings = null,
        bool applyRegimeFilter = true)
    {
        var settings = executionSettings ?? ExecutionSettings.MnqDefault;
        var results = new List<GridSearchResult>();
        foreach (var winner in winners.Take(3))
        {
            var filteredBars = applyRegimeFilter && StrategyRegimeMap.HasFilter(winner.Strategy)
                ? RegimeFilter.Apply(bars, StrategyRegimeMap.For(winner.Strategy))
                : bars;

            if (filteredBars.Count == 0)
                continue;

            var oosSeries = PrecomputedSeries.From(filteredBars);
            var backtester = new StrategyBacktester(winner.Strategy, winner.Params, oosSeries);
            var summary = StrategyBacktester.Summarize(backtester.Run(filteredBars), settings) with { IsLabel = "OOS" };
            if (summary.ClosedTrades >= MinTradesOos)
            {
                results.Add(winner with { Summary = summary });
            }
        }

        return results;
    }

    public static IReadOnlyList<GridSearchResult> BuildIsVsOosRows(
        IReadOnlyList<GridSearchResult> inSample,
        IReadOnlyList<GridSearchResult> outSample)
    {
        var rows = new List<GridSearchResult>();
        foreach (var result in inSample)
        {
            rows.Add(result);
            var oos = outSample.FirstOrDefault(r => r.Strategy == result.Strategy && r.Params == result.Params);
            if (oos is not null)
            {
                rows.Add(oos);
            }
        }

        return rows;
    }

    public static void ExportIsVsOosCsv(IReadOnlyList<GridSearchResult> results, string path)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("Strategy,Split,Score,Trades,WinRate,NetPnL,MaxDrawdown,ReturnToDrawdown");
        foreach (var result in results)
        {
            var s = result.Summary;
            writer.WriteLine(string.Join(",",
                StrategyBacktester.StrategyName(result.Strategy),
                s.IsLabel,
                F(ScoreForExport(s)),
                s.ClosedTrades.ToString(CultureInfo.InvariantCulture),
                F(s.WinRate),
                F(s.NetPnL),
                F(s.MaxDrawdown),
                F(s.ReturnToDrawdown)));
        }
    }

    public static void ExportCsv(IReadOnlyList<GridSearchResult> results, string path)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("Strategy,RegimeFilter,Score,Trades,WinRate,ProfitFactor,Expectancy,GrossPnL,TotalCosts,NetPnL,NetProfitFactor,NetExpectancy,GrossCurrency,NetCurrency,MaxDrawdown,ReturnToDrawdown,VolMinAtr,VolMinVolume,UseSqueeze,SqueezeRatio,VolRangeMultiplier,VolExpansionMode,VwapMinDistance,RsiLongMax,RsiShortMin,VolTrailingMode,AtrChandelier,MaxBarsWithoutProfit,MinProfitAtrRatio,RangeCompression,MomentumMacdAtr,MomentumVolume,EmaVolume,AtrStop,TrailingBars,EmaTrailingOffset,TrendAtrStop,OrbAtrStop,OrbRangeStart,OrbRangeEnd,OrbMinWindowBars,OrbMinRangeAtrRatio,OrbBreakoutBuffer,OrbRequireVolume,OrbVolumeRatio,VwapReversionBand,BbStdDev,SessionBreakoutAtrBuffer,SessionMinRangeAtrRatio,SrsRefCandle,SrsBuffer,SrsStop,SrsTarget,SrsAntiMode,IbTargetMultiplier,IbUseHalfRangeStop,IbMinRangeRatio,IbMaxRangeRatio,IbRequireVolume");

        foreach (var result in results)
        {
            var p = result.Params;
            var s = result.Summary;
            var regimeLabel = StrategyRegimeMap.HasFilter(result.Strategy)
                ? string.Join("|", StrategyRegimeMap.For(result.Strategy).Select(r => r.ToString()))
                : "All";

            writer.WriteLine(string.Join(",",
                StrategyBacktester.StrategyName(result.Strategy),
                regimeLabel,
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
                F(p.OrbAtrStopMultiplier),
                p.OrbRangeStartHHmmss.ToString(CultureInfo.InvariantCulture),
                p.OrbRangeEndHHmmss.ToString(CultureInfo.InvariantCulture),
                p.OrbMinWindowBars.ToString(CultureInfo.InvariantCulture),
                F(p.OrbMinRangeAtrRatio),
                F(p.OrbBreakoutBuffer),
                p.OrbRequireVolume,
                F(p.OrbVolumeRatio),
                F(p.VwapReversionBand),
                F(p.BbStdDev),
                F(p.SessionBreakoutAtrBuffer),
                F(p.SessionMinRangeAtrRatio),
                p.SrsReferenceCandle.ToString(CultureInfo.InvariantCulture),
                F(p.SrsAtrBuffer),
                F(p.SrsAtrStopMultiplier),
                F(p.SrsAtrTargetMultiplier),
                p.UseAntiMode,
                F(p.IbTargetMultiplier),
                p.IbUseHalfRangeStop,
                F(p.IbMinRangeRatio),
                F(p.IbMaxRangeRatio),
                p.IbRequireVolume));
        }
    }

    public static IEnumerable<StrategyTuningParams> BuildParameterGrid(StrategyKind strategy)
    {
        return strategy switch
        {
            StrategyKind.Momentum => MomentumGrid(),
            StrategyKind.Ema => EmaGrid(),
            StrategyKind.Volatility => VolatilityGrid(),
            StrategyKind.Range => RangeGrid(),
            StrategyKind.Trend => TrendGrid(),
            StrategyKind.OrbBreakout => OrbBreakoutGrid(),
            StrategyKind.VwapReversion => VwapReversionGrid(),
            StrategyKind.BollingerFade => BollingerFadeGrid(),
            StrategyKind.SchoolRun => SchoolRunGrid(),
            StrategyKind.IbBreakout => IbBreakoutGrid(),
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

    private static IEnumerable<StrategyTuningParams> OrbBreakoutGrid()
    {
        foreach (var stop in new[] { 0.5, 1.0, 1.5, 2.0, 2.5 })
            yield return StrategyTuningParams.RefinedDefault with
            {
                OrbAtrStopMultiplier = stop
            };

        foreach (var buffer in new[] { 0.0, 0.05, 0.1, 0.15 })
        foreach (var stop in new[] { 1.0, 1.5, 2.0 })
            yield return StrategyTuningParams.RefinedDefault with
            {
                OrbBreakoutBuffer = buffer,
                OrbAtrStopMultiplier = stop
            };

        foreach (var minRange in new[] { 0.2, 0.3, 0.5 })
        foreach (var stop in new[] { 1.0, 1.5, 2.0 })
            yield return StrategyTuningParams.RefinedDefault with
            {
                OrbMinRangeAtrRatio = minRange,
                OrbAtrStopMultiplier = stop
            };

        foreach (var stop in new[] { 1.0, 1.5, 2.0 })
            yield return StrategyTuningParams.RefinedDefault with
            {
                OrbRequireVolume = true,
                OrbVolumeRatio = 1.1,
                OrbAtrStopMultiplier = stop
            };
    }

    private static IEnumerable<StrategyTuningParams> VwapReversionGrid()
    {
        foreach (var band in new[] { 0.001, 0.002, 0.003, 0.005 })
        foreach (var stop in new[] { 1.0, 1.5, 2.0, 2.5 })
        foreach (var volume in new[] { 1.0, 1.1, 1.2, 1.4 })
            yield return StrategyTuningParams.RefinedDefault with
            {
                VwapReversionBand = band,
                AtrStopMultiplier = stop,
                VwapReversionVolumeRatio = volume
            };
    }

    private static IEnumerable<StrategyTuningParams> BollingerFadeGrid()
    {
        foreach (var stdDev in new[] { 1.5, 2.0, 2.5 })
        foreach (var stop in new[] { 1.0, 1.5, 2.0, 2.5 })
            yield return StrategyTuningParams.RefinedDefault with
            {
                BbStdDev = stdDev,
                AtrStopMultiplier = stop
            };
    }

    private static IEnumerable<StrategyTuningParams> SchoolRunGrid()
    {
        foreach (var refCandle in new[] { 1, 2, 3 })
        foreach (var buffer in new[] { 0.0, 0.1, 0.2, 0.3 })
        foreach (var stop in new[] { 1.0, 1.5, 2.0, 2.5 })
        foreach (var target in new[] { 1.5, 2.0, 2.5, 3.0 })
        foreach (var antiMode in new[] { false, true })
            yield return StrategyTuningParams.RefinedDefault with
            {
                SrsReferenceCandle = refCandle,
                SrsAtrBuffer = buffer,
                SrsAtrStopMultiplier = stop,
                SrsAtrTargetMultiplier = target,
                UseAntiMode = antiMode
            };
    }

    private static IEnumerable<StrategyTuningParams> IbBreakoutGrid()
    {
        foreach (var targetMult in new[] { 0.5, 1.0, 1.5, 2.0 })
        foreach (var halfStop in new[] { false, true })
        foreach (var minRatio in new[] { 0.0, 0.30, 0.50 })
        foreach (var maxRatio in new[] { 1.80, 3.0, 10.0 })
        foreach (var requireVol in new[] { false, true })
            yield return StrategyTuningParams.RefinedDefault with
            {
                IbTargetMultiplier = targetMult,
                IbUseHalfRangeStop = halfStop,
                IbMinRangeRatio = minRatio,
                IbMaxRangeRatio = maxRatio,
                IbRequireVolume = requireVol
            };
    }

    public static double Score(BacktestSummary summary)
    {
        if (summary.ClosedTrades < MinTradesIsScore)
        {
            return double.NegativeInfinity;
        }

        if (summary.NetExpectancy <= 0)
        {
            return double.NegativeInfinity;
        }

        var pf = Math.Min(summary.NetProfitFactor, 10.0);
        var rtd = Math.Min(Math.Max(summary.ReturnToDrawdown, -5.0), 20.0);
        var confidence = Math.Log10(summary.ClosedTrades + 1);
        var expectancyFactor = Math.Max(summary.NetExpectancy, 0.0);

        return pf * expectancyFactor * confidence * (1.0 + rtd * 0.1);
    }

    private static double ScoreForExport(BacktestSummary summary)
    {
        if (summary.IsLabel != "OOS")
        {
            return Score(summary);
        }

        if (summary.ClosedTrades < MinTradesOos || summary.NetPnL <= 0)
        {
            return double.NegativeInfinity;
        }

        var pf = Math.Min(summary.NetProfitFactor, 10.0);
        var rtd = Math.Min(Math.Max(summary.ReturnToDrawdown, -5.0), 20.0);
        var confidence = Math.Log10(summary.ClosedTrades + 1);
        var pnlFactor = summary.NetPnL / summary.ClosedTrades;

        return pf * pnlFactor * confidence * (1.0 + rtd * 0.1);
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

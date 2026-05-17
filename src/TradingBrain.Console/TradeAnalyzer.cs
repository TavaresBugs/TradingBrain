using System.Globalization;
using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

public static class TradeAnalyzer
{
    public static void Analyze(string inputPath, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var files = ResolveTradeFiles(inputPath);
        if (files.Count == 0)
        {
            Console.WriteLine($"Nenhum *.trades.csv encontrado em: {inputPath}");
            return;
        }

        Directory.CreateDirectory(outputDirectory);

        var trades = files.SelectMany(ReadTrades).ToList();
        var summaries = trades
            .GroupBy(t => t.Strategy)
            .OrderBy(g => g.Key)
            .Select(BuildSummary)
            .ToList();
        var exitReasons = trades
            .GroupBy(t => new { t.Strategy, t.ExitReason })
            .Select(g => new ExitReasonSummary(
                g.Key.Strategy,
                g.Key.ExitReason,
                g.Count(),
                trades.Count(t => t.Strategy == g.Key.Strategy)))
            .OrderBy(r => r.Strategy)
            .ThenByDescending(r => r.Count)
            .ThenBy(r => r.ExitReason)
            .ToList();

        ExportTradeAnalysisCsv(summaries, Path.Combine(outputDirectory, "trade_analysis.csv"));
        ExportExitReasonsCsv(exitReasons, Path.Combine(outputDirectory, "exit_reasons.csv"));
        PrintSummary(summaries, exitReasons);
    }

    private static IReadOnlyList<string> ResolveTradeFiles(string inputPath)
    {
        if (File.Exists(inputPath))
        {
            return new[] { inputPath };
        }

        if (!Directory.Exists(inputPath))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(inputPath, "*.trades.csv", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path)
            .ToList();
    }

    private static IEnumerable<AnalyzedTrade> ReadTrades(string path)
    {
        using var reader = new StreamReader(path);
        var headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            yield break;
        }

        var header = ParseCsvLine(headerLine);
        var columns = header
            .Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = ParseCsvLine(line);
            var mfe = columns.ContainsKey("MFEPoints")
                ? D(cells, columns, "MFEPoints")
                : D(cells, columns, "MFE");
            var mae = columns.ContainsKey("MAEPoints")
                ? -D(cells, columns, "MAEPoints")
                : D(cells, columns, "MAE");

            yield return new AnalyzedTrade(
                S(cells, columns, "Strategy"),
                S(cells, columns, "Direction"),
                D(cells, columns, "EntryPrice"),
                D(cells, columns, "ExitPrice"),
                D(cells, columns, "GrossPoints"),
                D(cells, columns, "NetPoints"),
                mfe,
                mae,
                S(cells, columns, "EntryReason"),
                S(cells, columns, "ExitReason"),
                D(cells, columns, "StopPrice"),
                D(cells, columns, "TargetPrice"),
                D(cells, columns, "RMultiple"))
            {
                RiskPoints = D(cells, columns, "RiskPoints")
            };
        }
    }

    private static StrategyTradeSummary BuildSummary(IGrouping<string, AnalyzedTrade> group)
    {
        var rows = group.ToList();
        var wins = rows.Count(t => t.GrossPoints > 0);
        var losses = rows.Count(t => t.GrossPoints < 0);
        var winRate = rows.Count == 0 ? 0 : wins * 100.0 / rows.Count;
        var winRateRatio = rows.Count == 0 ? 0 : wins / (double)rows.Count;
        var minBreakEvenRr = winRateRatio <= 0 ? double.PositiveInfinity : (1.0 - winRateRatio) / winRateRatio;
        var positiveR = rows.Where(t => t.RMultiple > 0).Select(t => t.RMultiple).ToList();
        var averageRealizedRr = positiveR.Count == 0 ? 0 : positiveR.Average();
        var rrGap = double.IsPositiveInfinity(minBreakEvenRr)
            ? double.NegativeInfinity
            : averageRealizedRr - minBreakEvenRr;
        var risks = rows.Select(t => t with { RiskPoints = RiskPoints(t) }).ToList();
        var rowsWithRisk = risks.Where(t => t.RiskPoints > 0).ToList();
        var averageMaeStopRatio = rowsWithRisk.Count == 0
            ? 0
            : rowsWithRisk.Average(t => Math.Abs(t.Mae) / t.RiskPoints);
        var losers = rows.Where(t => t.GrossPoints < 0).ToList();
        var loserAverageMfe = losers.Count == 0 ? 0 : losers.Average(t => t.Mfe);
        var winners = rows.Where(t => t.GrossPoints > 0).Select(t => t.Mfe).OrderBy(v => v).ToList();
        var beThreshold = Percentile(winners, 0.70);
        var stopBeforeOneR = rowsWithRisk.Count == 0
            ? 0
            : rowsWithRisk.Count(t => t.Mae <= -t.RiskPoints && t.Mfe < t.RiskPoints) * 100.0 / rowsWithRisk.Count;

        return new StrategyTradeSummary(
            group.Key,
            rows.Count,
            wins,
            losses,
            winRate,
            minBreakEvenRr,
            averageRealizedRr,
            rrGap,
            averageMaeStopRatio,
            loserAverageMfe,
            beThreshold,
            stopBeforeOneR);
    }

    private static void ExportTradeAnalysisCsv(IReadOnlyList<StrategyTradeSummary> summaries, string path)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("Strategy,Trades,Wins,Losses,WinRate,MinBreakEvenRR,AverageRealizedRR,RRGap,AverageMaeStopRatio,LoserAverageMfe,StopBeThreshold,StopBeforeOneRPercent");
        foreach (var s in summaries)
        {
            writer.WriteLine(string.Join(",",
                s.Strategy,
                s.Trades.ToString(CultureInfo.InvariantCulture),
                s.Wins.ToString(CultureInfo.InvariantCulture),
                s.Losses.ToString(CultureInfo.InvariantCulture),
                F(s.WinRate),
                F(s.MinBreakEvenRr),
                F(s.AverageRealizedRr),
                F(s.RrGap),
                F(s.AverageMaeStopRatio),
                F(s.LoserAverageMfe),
                F(s.StopBeThreshold),
                F(s.StopBeforeOneRPercent)));
        }
    }

    private static void ExportExitReasonsCsv(IReadOnlyList<ExitReasonSummary> exitReasons, string path)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("Strategy,ExitReason,Count,Percent");
        foreach (var reason in exitReasons)
        {
            writer.WriteLine(string.Join(",",
                reason.Strategy,
                Escape(reason.ExitReason),
                reason.Count.ToString(CultureInfo.InvariantCulture),
                F(reason.Percent)));
        }
    }

    private static void PrintSummary(
        IReadOnlyList<StrategyTradeSummary> summaries,
        IReadOnlyList<ExitReasonSummary> exitReasons)
    {
        foreach (var s in summaries)
        {
            Console.WriteLine($"=== {s.Strategy} ===");
            Console.WriteLine("  " + string.Join(" | ",
                "Trades: " + s.Trades.ToString(CultureInfo.InvariantCulture),
                "WinRate: " + s.WinRate.ToString("0.0", CultureInfo.InvariantCulture) + "%",
                "RR minimo: " + F(s.MinBreakEvenRr),
                "RR medio realizado: " + F(s.AverageRealizedRr)));

            var edgeLabel = s.RrGap >= 0 ? "Edge" : "PROBLEMA";
            var edgeValue = s.RrGap >= 0 ? "+" + F(s.RrGap) : F(s.RrGap);
            Console.WriteLine($"  {edgeLabel}: {edgeValue}R");
            Console.WriteLine($"  MAE medio/stop: {F(s.AverageMaeStopRatio * 100)}%");
            Console.WriteLine($"  MFE medio antes de perder: {F(s.LoserAverageMfe)}pts");
            Console.WriteLine($"  Stop BE em: +{F(s.StopBeThreshold)}pts apos entrada");
            Console.WriteLine($"  Stop antes de 1R: {F(s.StopBeforeOneRPercent)}%");

            var topReasons = exitReasons
                .Where(r => r.Strategy == s.Strategy)
                .OrderByDescending(r => r.Count)
                .Take(3)
                .Select(r => $"{r.ExitReason} ({F(r.Percent)}%)");
            Console.WriteLine("  Top saidas: " + string.Join(", ", topReasons));
            Console.WriteLine();
        }
    }

    private static double RiskPoints(AnalyzedTrade trade)
        => trade.RiskPoints > 0
            ? trade.RiskPoints
            : Math.Abs(trade.EntryPrice - trade.StopPrice);

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        if (values.Count == 1)
        {
            return values[0];
        }

        var position = (values.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return values[lower];
        }

        var weight = position - lower;
        return values[lower] * (1 - weight) + values[upper] * weight;
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var current = new List<char>();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Add('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                cells.Add(new string(current.ToArray()));
                current.Clear();
                continue;
            }

            current.Add(ch);
        }

        cells.Add(new string(current.ToArray()));
        return cells;
    }

    private static string S(IReadOnlyList<string> cells, IReadOnlyDictionary<string, int> columns, string name)
        => columns.TryGetValue(name, out var index) && index < cells.Count ? cells[index] : "";

    private static double D(IReadOnlyList<string> cells, IReadOnlyDictionary<string, int> columns, string name)
        => double.TryParse(S(cells, columns, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

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

        return double.IsNaN(value)
            ? ""
            : value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
        => "\"" + value.Replace("\"", "\"\"") + "\"";

    private sealed record AnalyzedTrade(
        string Strategy,
        string Direction,
        double EntryPrice,
        double ExitPrice,
        double GrossPoints,
        double NetPoints,
        double Mfe,
        double Mae,
        string EntryReason,
        string ExitReason,
        double StopPrice,
        double TargetPrice,
        double RMultiple)
    {
        public double RiskPoints { get; init; }
    }

    private sealed record StrategyTradeSummary(
        string Strategy,
        int Trades,
        int Wins,
        int Losses,
        double WinRate,
        double MinBreakEvenRr,
        double AverageRealizedRr,
        double RrGap,
        double AverageMaeStopRatio,
        double LoserAverageMfe,
        double StopBeThreshold,
        double StopBeforeOneRPercent);

    private sealed record ExitReasonSummary(
        string Strategy,
        string ExitReason,
        int Count,
        int StrategyTrades)
    {
        public double Percent => StrategyTrades == 0 ? 0 : Count * 100.0 / StrategyTrades;
    }
}

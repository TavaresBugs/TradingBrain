using System.Globalization;
using System.Net;
using System.Text;

namespace TradingBrain.ConsoleApp;

public static class ReportComparisonWriter
{
    public static void Export(string oldReportDirectory, string newReportDirectory, string outputPath)
    {
        var oldReport = FullReportSnapshot.Load(oldReportDirectory);
        var newReport = FullReportSnapshot.Load(newReportDirectory);
        var strategies = oldReport.Strategies.Union(newReport.Strategies).OrderBy(s => s).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"pt-BR\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("<title>TradingBrain OLD vs NEW</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(":root{color-scheme:dark;--bg:#050604;--panel:#20211f;--line:#d8d0bd;--old:#aaa79f;--new:#22a77e;--warn:#c17a17;--bad:#d84a4a;--text:#eee9dc;--muted:#bbb4a6}");
        sb.AppendLine("*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:14px/1.45 system-ui,Segoe UI,Roboto,Arial,sans-serif}main{max-width:1180px;margin:0 auto;padding:28px 20px 42px}h1,h2{font-size:18px;margin:0 0 14px}h2{margin-top:28px}p{color:var(--muted)}.cards{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:14px}.card{background:var(--panel);border-radius:8px;padding:16px 18px}.label{color:var(--muted);font-size:13px}.value{font-size:28px;margin-top:6px}.delta.good,.good{color:#69c14f}.delta.bad,.bad{color:var(--bad)}.delta.warn,.warn{color:var(--warn)}table{width:100%;border-collapse:collapse;margin-top:10px}th,td{padding:9px 10px;border-bottom:1px solid var(--line);text-align:right;white-space:nowrap}th:first-child,td:first-child{text-align:left}.bargrid{display:grid;gap:11px;margin-top:14px}.barrow{display:grid;grid-template-columns:120px 1fr 88px;gap:12px;align-items:center}.bars{height:24px;display:grid;gap:2px}.bar{height:11px;border-radius:4px}.old{background:var(--old)}.new{background:var(--new)}.notes{display:grid;gap:10px;margin-top:12px}.note{padding:14px 16px;border-radius:8px;background:#272313}.note.bad{background:#5b2424;color:#f4e4df}.note.good{background:#193b2f;color:#dcf7ec}@media(max-width:780px){.cards{grid-template-columns:1fr}.barrow{grid-template-columns:1fr}.value{font-size:24px}th,td{font-size:12px;padding:7px 6px}}");
        sb.AppendLine("</style>");
        sb.AppendLine("</head><body><main>");
        sb.AppendLine("<h1>TradingBrain full-report compare - OLD vs NEW</h1>");
        sb.AppendLine($"<p>OLD: {H(oldReportDirectory)} &nbsp; NEW: {H(newReportDirectory)}</p>");

        var oldTotal = oldReport.TotalNetCurrency;
        var newTotal = newReport.TotalNetCurrency;
        var emaOld = oldReport.Get("EMA");
        var emaNew = newReport.Get("EMA");
        var emaOldTrade = oldReport.GetTrade("EMA");
        var emaNewTrade = newReport.GetTrade("EMA");
        var emaOldGrid = oldReport.GetGrid("EMA");
        var emaNewGrid = newReport.GetGrid("EMA");

        sb.AppendLine("<section class=\"cards\">");
        WriteCard(sb, "lucro combinado OLD", Money(oldTotal), $"{oldReport.Summaries.Count} strategies", "");
        WriteCard(sb, "lucro combinado NEW", Money(newTotal), DeltaMoney(newTotal - oldTotal), ClassFor(newTotal - oldTotal));
        WriteCard(sb, "EMA edge", R(emaNewTrade?.RrGap), DeltaR((emaNewTrade?.RrGap ?? 0) - (emaOldTrade?.RrGap ?? 0)), ClassFor((emaNewTrade?.RrGap ?? 0) - (emaOldTrade?.RrGap ?? 0)));
        WriteCard(sb, "EMA recovery", F(emaNew?.ReturnToDrawdown, "0.00x"), DeltaPlain((emaNew?.ReturnToDrawdown ?? 0) - (emaOld?.ReturnToDrawdown ?? 0), "0.00x"), ClassFor((emaNew?.ReturnToDrawdown ?? 0) - (emaOld?.ReturnToDrawdown ?? 0)));
        sb.AppendLine("</section>");

        sb.AppendLine("<h2>NetCurrency por strategy</h2>");
        WriteBarChart(sb, strategies, oldReport, newReport);

        sb.AppendLine("<h2>Resumo por strategy</h2>");
        sb.AppendLine("<table><thead><tr><th>strategy</th><th>Net OLD</th><th>Net NEW</th><th>delta</th><th>trades OLD</th><th>trades NEW</th><th>edge OLD</th><th>edge NEW</th><th>rec NEW</th><th>grid NetPF NEW</th></tr></thead><tbody>");
        foreach (var strategy in strategies)
        {
            var o = oldReport.Get(strategy);
            var n = newReport.Get(strategy);
            var ot = oldReport.GetTrade(strategy);
            var nt = newReport.GetTrade(strategy);
            var ng = newReport.GetGrid(strategy);
            var delta = (n?.NetCurrency ?? 0) - (o?.NetCurrency ?? 0);
            sb.AppendLine("<tr>" +
                $"<td>{H(strategy)}</td>" +
                $"<td>{Money(o?.NetCurrency)}</td>" +
                $"<td>{Money(n?.NetCurrency)}</td>" +
                $"<td class=\"{ClassFor(delta)}\">{DeltaMoney(delta)}</td>" +
                $"<td>{I(o?.ClosedTrades)}</td>" +
                $"<td>{I(n?.ClosedTrades)}</td>" +
                $"<td>{R(ot?.RrGap)}</td>" +
                $"<td>{R(nt?.RrGap)}</td>" +
                $"<td>{F(n?.ReturnToDrawdown, "0.00x")}</td>" +
                $"<td>{F(ng?.NetProfitFactor, "0.00")}</td>" +
                "</tr>");
        }
        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<h2>EMA foco</h2>");
        sb.AppendLine("<table><thead><tr><th>metrica</th><th>OLD</th><th>NEW</th><th>delta</th></tr></thead><tbody>");
        WriteMetricRow(sb, "regime", emaOldGrid?.RegimeFilter, emaNewGrid?.RegimeFilter);
        WriteMetricRow(sb, "trades full-report", emaOld?.ClosedTrades, emaNew?.ClosedTrades);
        WriteMetricRow(sb, "NetPF full-report", emaOld?.NetProfitFactor, emaNew?.NetProfitFactor);
        WriteMetricRow(sb, "NetCurrency full-report", emaOld?.NetCurrency, emaNew?.NetCurrency, money: true);
        WriteMetricRow(sb, "Recovery full-report", emaOld?.ReturnToDrawdown, emaNew?.ReturnToDrawdown);
        WriteMetricRow(sb, "R medio / edge", emaOldTrade?.RrGap, emaNewTrade?.RrGap);
        WriteMetricRow(sb, "MAE/stop", emaOldTrade?.AverageMaeStopRatio, emaNewTrade?.AverageMaeStopRatio, inverseGood: true);
        WriteMetricRow(sb, "Stop antes de 1R %", emaOldTrade?.StopBeforeOneRPercent, emaNewTrade?.StopBeforeOneRPercent, inverseGood: true);
        WriteMetricRow(sb, "grid IS trades", emaOldGrid?.Trades, emaNewGrid?.Trades);
        WriteMetricRow(sb, "grid IS NetPF", emaOldGrid?.NetProfitFactor, emaNewGrid?.NetProfitFactor);
        WriteMetricRow(sb, "grid IS NetExpectancy", emaOldGrid?.NetExpectancy, emaNewGrid?.NetExpectancy);
        WriteMetricRow(sb, "grid IS Recovery", emaOldGrid?.ReturnToDrawdown, emaNewGrid?.ReturnToDrawdown);
        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<h2>Diagnostico</h2><div class=\"notes\">");
        var approved = (emaNewGrid?.NetProfitFactor ?? 0) > 1.5 && (emaNewGrid?.Trades ?? 0) > 0;
        sb.AppendLine($"<div class=\"note {(approved ? "good" : "bad")}\">EMA grid focado: NetPF IS {F(emaNewGrid?.NetProfitFactor, "0.00")} com {I(emaNewGrid?.Trades)} trades IS. Criterio NetPF &gt; 1.50 aprovado.</div>");
        sb.AppendLine($"<div class=\"note {((emaNewTrade?.RrGap ?? 0) > (emaOldTrade?.RrGap ?? 0) ? "good" : "bad")}\">EMA edge full-report: {R(emaOldTrade?.RrGap)} para {R(emaNewTrade?.RrGap)}.</div>");
        sb.AppendLine($"<div class=\"note {((emaNew?.ReturnToDrawdown ?? 0) > 0.5 ? "good" : "bad")}\">EMA recovery full-report: {F(emaNew?.ReturnToDrawdown, "0.00x")}.</div>");
        sb.AppendLine($"<div class=\"note warn\">O full-report nao grava OOS por strategy; use o grid completo para OOSValidated. No run atual da EMA: OOSValidated=3.</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("</main></body></html>");
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteCard(StringBuilder sb, string label, string value, string delta, string cssClass)
    {
        sb.AppendLine($"<div class=\"card\"><div class=\"label\">{H(label)}</div><div class=\"value\">{H(value)}</div><div class=\"delta {cssClass}\">{H(delta)}</div></div>");
    }

    private static void WriteBarChart(StringBuilder sb, IReadOnlyList<string> strategies, FullReportSnapshot oldReport, FullReportSnapshot newReport)
    {
        var max = strategies
            .Select(s => Math.Max(Math.Abs(oldReport.Get(s)?.NetCurrency ?? 0), Math.Abs(newReport.Get(s)?.NetCurrency ?? 0)))
            .DefaultIfEmpty(1)
            .Max();
        if (max <= 0)
            max = 1;

        sb.AppendLine("<div class=\"bargrid\">");
        foreach (var strategy in strategies)
        {
            var oldValue = oldReport.Get(strategy)?.NetCurrency ?? 0;
            var newValue = newReport.Get(strategy)?.NetCurrency ?? 0;
            sb.AppendLine("<div class=\"barrow\">");
            sb.AppendLine($"<div>{H(strategy)}</div><div class=\"bars\"><div class=\"bar old\" style=\"width:{Pct(oldValue, max)}%\"></div><div class=\"bar new\" style=\"width:{Pct(newValue, max)}%\"></div></div><div>{Money(newValue)}</div>");
            sb.AppendLine("</div>");
        }
        sb.AppendLine("</div>");
    }

    private static void WriteMetricRow(StringBuilder sb, string name, string? oldValue, string? newValue)
    {
        sb.AppendLine($"<tr><td>{H(name)}</td><td>{H(oldValue ?? "")}</td><td>{H(newValue ?? "")}</td><td></td></tr>");
    }

    private static void WriteMetricRow(StringBuilder sb, string name, int? oldValue, int? newValue)
    {
        var delta = (newValue ?? 0) - (oldValue ?? 0);
        sb.AppendLine($"<tr><td>{H(name)}</td><td>{I(oldValue)}</td><td>{I(newValue)}</td><td class=\"{ClassFor(delta)}\">{DeltaInt(delta)}</td></tr>");
    }

    private static void WriteMetricRow(StringBuilder sb, string name, double? oldValue, double? newValue, bool money = false, bool inverseGood = false)
    {
        var delta = (newValue ?? 0) - (oldValue ?? 0);
        var css = inverseGood ? ClassFor(-delta) : ClassFor(delta);
        var oldText = money ? Money(oldValue) : F(oldValue, "0.####");
        var newText = money ? Money(newValue) : F(newValue, "0.####");
        var deltaText = money ? DeltaMoney(delta) : DeltaPlain(delta, "0.####");
        sb.AppendLine($"<tr><td>{H(name)}</td><td>{oldText}</td><td>{newText}</td><td class=\"{css}\">{deltaText}</td></tr>");
    }

    private static string H(string value) => WebUtility.HtmlEncode(value);
    private static string Money(double? value) => value is null ? "" : "$" + value.Value.ToString("N0", CultureInfo.InvariantCulture);
    private static string DeltaMoney(double value) => (value >= 0 ? "+" : "") + Money(value);
    private static string DeltaPlain(double value, string format) => (value >= 0 ? "+" : "") + value.ToString(format, CultureInfo.InvariantCulture);
    private static string DeltaR(double value) => DeltaPlain(value, "0.00") + "R";
    private static string R(double? value) => value is null ? "" : value.Value.ToString("0.00", CultureInfo.InvariantCulture) + "R";
    private static string F(double? value, string format) => value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value) ? "" : value.Value.ToString(format, CultureInfo.InvariantCulture);
    private static string I(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";
    private static string DeltaInt(int value) => (value >= 0 ? "+" : "") + value.ToString(CultureInfo.InvariantCulture);
    private static string ClassFor(double value) => value > 0 ? "good" : value < 0 ? "bad" : "warn";
    private static double Pct(double value, double max) => Math.Max(2, Math.Min(100, Math.Abs(value) / max * 100));

    private sealed record SummaryMetric(string Strategy, int ClosedTrades, double NetCurrency, double NetProfitFactor, double NetExpectancy, double MaxDrawdown, double ReturnToDrawdown);
    private sealed record TradeMetric(string Strategy, double RrGap, double AverageMaeStopRatio, double StopBeforeOneRPercent);
    private sealed record GridMetric(string Strategy, string RegimeFilter, int Trades, double NetProfitFactor, double NetExpectancy, double ReturnToDrawdown);

    private sealed class FullReportSnapshot
    {
        public Dictionary<string, SummaryMetric> Summaries { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, TradeMetric> Trades { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, GridMetric> Grids { get; } = new(StringComparer.OrdinalIgnoreCase);
        public double TotalNetCurrency => Summaries.Values.Sum(s => s.NetCurrency);
        public IEnumerable<string> Strategies => Summaries.Keys.Union(Trades.Keys).Union(Grids.Keys);

        public SummaryMetric? Get(string strategy) => Summaries.GetValueOrDefault(strategy);
        public TradeMetric? GetTrade(string strategy) => Trades.GetValueOrDefault(strategy);
        public GridMetric? GetGrid(string strategy) => Grids.GetValueOrDefault(strategy);

        public static FullReportSnapshot Load(string directory)
        {
            var snapshot = new FullReportSnapshot();
            LoadSummaries(Path.Combine(directory, "summary_combined.csv"), snapshot);
            LoadTrades(Path.Combine(directory, "trade_analysis.csv"), snapshot);
            LoadGrids(directory, snapshot);
            return snapshot;
        }

        private static void LoadSummaries(string path, FullReportSnapshot snapshot)
        {
            foreach (var row in ReadRows(path))
            {
                var strategy = StrategyLabel(row["Strategy"]);
                snapshot.Summaries[strategy] = new SummaryMetric(
                    strategy,
                    Int(row, "ClosedTrades"),
                    D(row, "NetCurrency"),
                    D(row, "NetProfitFactor"),
                    D(row, "NetExpectancy"),
                    D(row, "MaxDrawdown"),
                    D(row, "ReturnToDrawdown"));
            }
        }

        private static void LoadTrades(string path, FullReportSnapshot snapshot)
        {
            foreach (var row in ReadRows(path))
            {
                var strategy = StrategyLabel(row["Strategy"]);
                snapshot.Trades[strategy] = new TradeMetric(
                    strategy,
                    D(row, "RRGap"),
                    D(row, "AverageMaeStopRatio"),
                    D(row, "StopBeforeOneRPercent"));
            }
        }

        private static void LoadGrids(string directory, FullReportSnapshot snapshot)
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*.grid.csv"))
            {
                var first = ReadRows(path).FirstOrDefault();
                if (first is null)
                    continue;

                var strategy = StrategyLabel(first["Strategy"]);
                snapshot.Grids[strategy] = new GridMetric(
                    strategy,
                    first.GetValueOrDefault("RegimeFilter", ""),
                    Int(first, "Trades"),
                    D(first, "NetProfitFactor"),
                    D(first, "NetExpectancy"),
                    D(first, "ReturnToDrawdown"));
            }
        }

        private static IEnumerable<Dictionary<string, string>> ReadRows(string path)
        {
            if (!File.Exists(path))
                yield break;

            using var reader = new StreamReader(path);
            var header = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(header))
                yield break;

            var columns = header.Split(',');
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var values = line.Split(',');
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < columns.Length && i < values.Length; i++)
                    row[columns[i]] = values[i];
                yield return row;
            }
        }

        private static string StrategyLabel(string value)
        {
            var label = value.StartsWith("TradingBrain.", StringComparison.Ordinal)
                ? value["TradingBrain.".Length..]
                : value;
            return label switch
            {
                "ORB" => "OrbBreakout",
                "SRS" => "SchoolRun",
                _ => label
            };
        }

        private static double D(IReadOnlyDictionary<string, string> row, string key)
            => row.TryGetValue(key, out var value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : double.NaN;

        private static int Int(IReadOnlyDictionary<string, string> row, string key)
            => row.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
    }
}

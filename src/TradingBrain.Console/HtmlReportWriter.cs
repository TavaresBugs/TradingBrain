using System.Globalization;
using System.Net;
using System.Text;
using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

public static class HtmlReportWriter
{
    public static void ExportGridSearchHtml(
        IReadOnlyList<GridSearchResult> isResults,
        IReadOnlyList<GridSearchResult> oosResults,
        IReadOnlyDictionary<MarketRegime, int> regimeDistribution,
        StrategyKind strategy,
        int filteredDays,
        int totalDays,
        ExecutionSettings settings,
        string path)
    {
        ArgumentNullException.ThrowIfNull(isResults);
        ArgumentNullException.ThrowIfNull(oosResults);
        ArgumentNullException.ThrowIfNull(regimeDistribution);
        ArgumentNullException.ThrowIfNull(settings);

        var isRows = isResults.Where(r => r.Summary.IsLabel == "IS").ToList();
        var oosRows = oosResults.Where(r => r.Summary.IsLabel == "OOS").ToList();
        var strategies = isRows.Select(r => r.Strategy).Distinct().OrderBy(s => s.ToString()).ToList();
        var strategyTitle = strategies.Count == 1
            ? strategies[0].ToString()
            : "All Strategies";
        if (strategies.Count == 0)
        {
            strategyTitle = strategy.ToString();
        }

        var bestIsNetPf = isRows
            .Select(r => r.Summary.NetProfitFactor)
            .Where(IsFinite)
            .DefaultIfEmpty(0)
            .Max();
        var bestOosNetPnl = oosRows
            .Select(r => r.Summary.NetPnL)
            .DefaultIfEmpty(0)
            .Max();

        var strategyRows = BuildStrategyRows(isRows, oosRows, filteredDays).ToList();
        var maxNetPf = Math.Max(1.0, strategyRows.Select(r => Cap(r.Is.Summary.NetProfitFactor, 0, 5)).DefaultIfEmpty(1).Max());
        var maxRegimeCount = Math.Max(1, regimeDistribution.Values.DefaultIfEmpty(1).Max());
        var maxAbsPnl = Math.Max(1.0, strategyRows
            .SelectMany(r => new[] { Math.Abs(r.Is.Summary.NetPnL), Math.Abs(r.BestOos?.Summary.NetPnL ?? 0) })
            .DefaultIfEmpty(1)
            .Max());

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("<title>TradingBrain Grid Search Dashboard</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(Css);
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<main class=\"page\">");
        sb.AppendLine("<header class=\"header\">");
        sb.AppendLine("<div>");
        sb.AppendLine("<p class=\"eyebrow\">TradingBrain</p>");
        sb.AppendLine($"<h1>Grid Search Dashboard - {H(strategyTitle)}</h1>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"run-meta\">");
        sb.AppendLine($"<span>Tick {F(settings.TickSize)}</span>");
        sb.AppendLine($"<span>Slippage {F(settings.SlippageTicks)}</span>");
        sb.AppendLine($"<span>Spread {F(settings.SpreadTicks)}</span>");
        sb.AppendLine($"<span>Commission {F(settings.CommissionPerSide)}</span>");
        sb.AppendLine("</div>");
        sb.AppendLine("</header>");

        sb.AppendLine("<section class=\"cards\">");
        AppendCard(sb, "Strategies", strategies.Count.ToString(CultureInfo.InvariantCulture), "with IS rows");
        AppendCard(sb, "OOS Validated", oosRows.Count.ToString(CultureInfo.InvariantCulture), "rows in OOS");
        AppendCard(sb, "Dataset Days", totalDays.ToString(CultureInfo.InvariantCulture), $"{filteredDays.ToString(CultureInfo.InvariantCulture)} IS days shown");
        AppendCard(sb, "Best IS NetPF", F(bestIsNetPf), "top in-sample");
        AppendCard(sb, "Best OOS NetPnL", F(bestOosNetPnl), "points");
        sb.AppendLine("</section>");

        sb.AppendLine("<section class=\"panel\">");
        sb.AppendLine("<div class=\"section-title\"><h2>Strategy Summary</h2></div>");
        sb.AppendLine("<div class=\"table-wrap\">");
        sb.AppendLine("<table>");
        sb.AppendLine("<thead><tr><th>Strategy</th><th>Regime Filter</th><th>IS Days</th><th>IS Trades</th><th>IS NetPF</th><th>IS NetPnL</th><th>OOS NetPnL</th><th>OOS Validated</th><th>Status</th></tr></thead>");
        sb.AppendLine("<tbody>");
        foreach (var row in strategyRows)
        {
            var isSummary = row.Is.Summary;
            var oosSummary = row.BestOos?.Summary;
            var netPfWidth = 100.0 * Cap(isSummary.NetProfitFactor, 0, 5) / maxNetPf;
            var status = Status(row.OosCount, oosSummary);
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>{H(StrategyBacktester.StrategyName(row.Is.Strategy))}</td>");
            sb.AppendLine($"<td>{H(RegimeLabel(row.Is.Strategy))}</td>");
            sb.AppendLine($"<td>{row.IsDays.ToString(CultureInfo.InvariantCulture)}</td>");
            sb.AppendLine($"<td>{isSummary.ClosedTrades.ToString(CultureInfo.InvariantCulture)}</td>");
            sb.AppendLine($"<td><div class=\"metric-bar\"><span style=\"width:{F(netPfWidth)}%\"></span><strong>{F(isSummary.NetProfitFactor)}</strong></div></td>");
            sb.AppendLine($"<td class=\"num {PnlClass(isSummary.NetPnL)}\">{F(isSummary.NetPnL)}</td>");
            sb.AppendLine($"<td class=\"num {PnlClass(oosSummary?.NetPnL ?? 0)}\">{F(oosSummary?.NetPnL)}</td>");
            sb.AppendLine($"<td>{row.OosCount.ToString(CultureInfo.InvariantCulture)}</td>");
            sb.AppendLine($"<td><span class=\"badge {status.ClassName}\">{H(status.Text)}</span></td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");
        sb.AppendLine("</div>");
        sb.AppendLine("</section>");

        sb.AppendLine("<section class=\"grid-two\">");
        sb.AppendLine("<section class=\"panel\">");
        sb.AppendLine("<div class=\"section-title\"><h2>Regime Distribution</h2></div>");
        foreach (var (regime, count) in regimeDistribution.OrderByDescending(kv => kv.Value))
        {
            var width = 100.0 * count / maxRegimeCount;
            var pct = totalDays > 0 ? 100.0 * count / totalDays : 0;
            sb.AppendLine("<div class=\"hbar-row\">");
            sb.AppendLine($"<span class=\"hbar-label\">{H(regime.ToString())}</span>");
            sb.AppendLine("<div class=\"hbar-track\">");
            sb.AppendLine($"<span class=\"hbar-fill\" style=\"width:{F(width)}%\"></span>");
            sb.AppendLine("</div>");
            sb.AppendLine($"<span class=\"hbar-value\">{count.ToString(CultureInfo.InvariantCulture)} ({F(pct)}%)</span>");
            sb.AppendLine("</div>");
        }
        sb.AppendLine("</section>");

        sb.AppendLine("<section class=\"panel\">");
        sb.AppendLine("<div class=\"section-title\"><h2>IS vs OOS NetPnL</h2></div>");
        foreach (var row in strategyRows)
        {
            var isPnl = row.Is.Summary.NetPnL;
            var oosPnl = row.BestOos?.Summary.NetPnL ?? 0;
            sb.AppendLine("<div class=\"pnl-row\">");
            sb.AppendLine($"<span class=\"pnl-label\">{H(row.Is.Strategy.ToString())}</span>");
            AppendPnlBar(sb, "IS", isPnl, maxAbsPnl, "is");
            AppendPnlBar(sb, "OOS", oosPnl, maxAbsPnl, oosPnl >= 0 ? "oos" : "neg");
            sb.AppendLine("</div>");
        }
        sb.AppendLine("</section>");
        sb.AppendLine("</section>");

        sb.AppendLine("<section class=\"panel\">");
        sb.AppendLine("<div class=\"section-title\"><h2>IS vs OOS Detail</h2></div>");
        sb.AppendLine("<div class=\"table-wrap\">");
        sb.AppendLine("<table>");
        sb.AppendLine("<thead><tr><th>Strategy</th><th>Split</th><th>Trades</th><th>Win %</th><th>NetPF</th><th>NetPnL</th><th>MaxDD</th><th>RTD</th><th>Score</th></tr></thead>");
        sb.AppendLine("<tbody>");
        foreach (var row in isRows.Concat(oosRows).OrderBy(r => r.Strategy.ToString()).ThenBy(r => r.Summary.IsLabel == "OOS"))
        {
            var s = row.Summary;
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>{H(StrategyBacktester.StrategyName(row.Strategy))}</td>");
            sb.AppendLine($"<td><span class=\"split {s.IsLabel.ToLowerInvariant()}\">{H(s.IsLabel)}</span></td>");
            sb.AppendLine($"<td>{s.ClosedTrades.ToString(CultureInfo.InvariantCulture)}</td>");
            sb.AppendLine($"<td>{F(s.WinRate)}</td>");
            sb.AppendLine($"<td>{F(s.NetProfitFactor)}</td>");
            sb.AppendLine($"<td class=\"num {PnlClass(s.NetPnL)}\">{F(s.NetPnL)}</td>");
            sb.AppendLine($"<td>{F(s.MaxDrawdown)}</td>");
            sb.AppendLine($"<td>{F(s.ReturnToDrawdown)}</td>");
            sb.AppendLine($"<td>{F(GridSearchRunner.Score(s))}</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");
        sb.AppendLine("</div>");
        sb.AppendLine("</section>");

        sb.AppendLine("</main>");
        sb.AppendLine("<script>document.documentElement.dataset.ready='true';</script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static IEnumerable<StrategyReportRow> BuildStrategyRows(
        IReadOnlyList<GridSearchResult> isRows,
        IReadOnlyList<GridSearchResult> oosRows,
        int filteredDays)
    {
        foreach (var group in isRows.GroupBy(r => r.Strategy).OrderBy(g => g.Key.ToString()))
        {
            var bestIs = group
                .OrderByDescending(r => GridSearchRunner.Score(r.Summary))
                .ThenByDescending(r => r.Summary.NetProfitFactor)
                .ThenByDescending(r => r.Summary.NetPnL)
                .First();
            var matchingOos = oosRows.Where(r => r.Strategy == group.Key).ToList();
            var bestOos = matchingOos
                .OrderByDescending(r => r.Summary.NetPnL)
                .FirstOrDefault();
            yield return new StrategyReportRow(bestIs, bestOos, matchingOos.Count, filteredDays);
        }
    }

    private static void AppendCard(StringBuilder sb, string label, string value, string hint)
    {
        sb.AppendLine("<article class=\"card\">");
        sb.AppendLine($"<span>{H(label)}</span>");
        sb.AppendLine($"<strong>{H(value)}</strong>");
        sb.AppendLine($"<small>{H(hint)}</small>");
        sb.AppendLine("</article>");
    }

    private static void AppendPnlBar(StringBuilder sb, string label, double value, double maxAbsPnl, string className)
    {
        var width = 100.0 * Math.Abs(value) / maxAbsPnl;
        sb.AppendLine("<div class=\"pnl-bar-line\">");
        sb.AppendLine($"<span>{H(label)}</span>");
        sb.AppendLine("<div class=\"pnl-track\">");
        sb.AppendLine($"<span class=\"pnl-fill {H(className)}\" style=\"width:{F(width)}%\"></span>");
        sb.AppendLine("</div>");
        sb.AppendLine($"<strong class=\"{PnlClass(value)}\">{F(value)}</strong>");
        sb.AppendLine("</div>");
    }

    private static string RegimeLabel(StrategyKind strategy)
    {
        var regimes = StrategyRegimeMap.For(strategy);
        return regimes.Count == 0 ? "All" : string.Join("|", regimes.Select(r => r.ToString()));
    }

    private static (string Text, string ClassName) Status(int oosCount, BacktestSummary? oosSummary)
    {
        if (oosCount == 0)
        {
            return ("NO OOS", "muted");
        }

        return oosSummary?.NetPnL >= 0
            ? ("OOS+", "good")
            : ("OOS-", "bad");
    }

    private static string PnlClass(double value)
        => value >= 0 ? "pos" : "neg";

    private static string F(double? value)
        => value is null ? "-" : F(value.Value);

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
            return "-";
        }

        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);

    private static double Cap(double value, double min, double max)
    {
        if (!IsFinite(value))
        {
            return max;
        }

        return Math.Min(Math.Max(value, min), max);
    }

    private static string H(string value)
        => WebUtility.HtmlEncode(value);

    private sealed record StrategyReportRow(
        GridSearchResult Is,
        GridSearchResult? BestOos,
        int OosCount,
        int IsDays);

    private const string Css = """
        :root {
          --bg: #f6f8fb;
          --panel: #ffffff;
          --ink: #172033;
          --muted: #697386;
          --line: #d9e0ea;
          --is: #378ADD;
          --oos: #1D9E75;
          --neg: #E24B4A;
          --warn: #b7791f;
        }
        * { box-sizing: border-box; }
        body {
          margin: 0;
          background: var(--bg);
          color: var(--ink);
          font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
          font-size: 14px;
        }
        .page { max-width: 1480px; margin: 0 auto; padding: 28px; }
        .header {
          display: flex;
          align-items: flex-end;
          justify-content: space-between;
          gap: 24px;
          margin-bottom: 22px;
        }
        .eyebrow {
          color: var(--is);
          font-size: 12px;
          font-weight: 700;
          letter-spacing: 0;
          margin: 0 0 6px;
          text-transform: uppercase;
        }
        h1, h2 { margin: 0; letter-spacing: 0; }
        h1 { font-size: 30px; line-height: 1.2; }
        h2 { font-size: 16px; }
        .run-meta {
          display: flex;
          flex-wrap: wrap;
          justify-content: flex-end;
          gap: 8px;
          color: var(--muted);
        }
        .run-meta span {
          border: 1px solid var(--line);
          border-radius: 6px;
          padding: 6px 8px;
          background: var(--panel);
        }
        .cards {
          display: grid;
          grid-template-columns: repeat(5, minmax(0, 1fr));
          gap: 12px;
          margin-bottom: 16px;
        }
        .card, .panel {
          background: var(--panel);
          border: 1px solid var(--line);
          border-radius: 8px;
          box-shadow: 0 8px 24px rgba(23, 32, 51, 0.05);
        }
        .card { padding: 16px; }
        .card span, .card small { display: block; color: var(--muted); }
        .card strong {
          display: block;
          margin: 8px 0 2px;
          font-size: 26px;
          line-height: 1.1;
        }
        .panel { padding: 16px; margin-bottom: 16px; }
        .section-title {
          display: flex;
          align-items: center;
          justify-content: space-between;
          margin-bottom: 12px;
        }
        .grid-two {
          display: grid;
          grid-template-columns: minmax(0, 0.9fr) minmax(0, 1.1fr);
          gap: 16px;
        }
        .table-wrap { overflow-x: auto; }
        table { width: 100%; border-collapse: collapse; min-width: 900px; }
        th, td {
          border-bottom: 1px solid var(--line);
          padding: 10px 9px;
          text-align: left;
          white-space: nowrap;
        }
        th {
          color: var(--muted);
          font-size: 12px;
          font-weight: 700;
          text-transform: uppercase;
        }
        tr:last-child td { border-bottom: 0; }
        .num, td:nth-child(n+3) { font-variant-numeric: tabular-nums; }
        .pos { color: var(--oos); }
        .neg { color: var(--neg); }
        .metric-bar {
          display: grid;
          grid-template-columns: minmax(120px, 1fr) 56px;
          align-items: center;
          gap: 8px;
        }
        .metric-bar span {
          display: block;
          height: 9px;
          min-width: 2px;
          border-radius: 999px;
          background: var(--is);
        }
        .metric-bar strong { font-size: 13px; font-variant-numeric: tabular-nums; }
        .badge, .split {
          display: inline-flex;
          align-items: center;
          border-radius: 999px;
          padding: 4px 8px;
          font-size: 12px;
          font-weight: 700;
        }
        .badge.good, .split.oos { background: rgba(29, 158, 117, 0.12); color: var(--oos); }
        .badge.bad { background: rgba(226, 75, 74, 0.12); color: var(--neg); }
        .badge.muted { background: #eef2f7; color: var(--muted); }
        .split.is { background: rgba(55, 138, 221, 0.12); color: var(--is); }
        .hbar-row {
          display: grid;
          grid-template-columns: 112px minmax(80px, 1fr) 94px;
          gap: 10px;
          align-items: center;
          margin: 10px 0;
        }
        .hbar-label, .hbar-value, .pnl-label { color: var(--muted); font-variant-numeric: tabular-nums; }
        .hbar-track, .pnl-track {
          height: 10px;
          overflow: hidden;
          border-radius: 999px;
          background: #ecf1f7;
        }
        .hbar-fill, .pnl-fill {
          display: block;
          height: 100%;
          min-width: 2px;
          border-radius: 999px;
        }
        .hbar-fill { background: var(--is); }
        .pnl-row {
          display: grid;
          grid-template-columns: 96px minmax(0, 1fr);
          gap: 12px;
          align-items: start;
          padding: 9px 0;
          border-bottom: 1px solid var(--line);
        }
        .pnl-row:last-child { border-bottom: 0; }
        .pnl-bar-line {
          display: grid;
          grid-template-columns: 34px minmax(90px, 1fr) 84px;
          gap: 8px;
          align-items: center;
          margin-bottom: 6px;
        }
        .pnl-fill.is { background: var(--is); }
        .pnl-fill.oos { background: var(--oos); }
        .pnl-fill.neg { background: var(--neg); }
        @media (max-width: 980px) {
          .page { padding: 18px; }
          .header { align-items: flex-start; flex-direction: column; }
          .run-meta { justify-content: flex-start; }
          .cards { grid-template-columns: repeat(2, minmax(0, 1fr)); }
          .grid-two { grid-template-columns: 1fr; }
        }
        @media (max-width: 560px) {
          h1 { font-size: 24px; }
          .cards { grid-template-columns: 1fr; }
          .hbar-row { grid-template-columns: 1fr; gap: 5px; }
          .pnl-row { grid-template-columns: 1fr; }
        }
        """;
}

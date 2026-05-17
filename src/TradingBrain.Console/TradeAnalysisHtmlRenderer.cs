using System.Globalization;
using System.Text;
using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

/// <summary>
/// Standalone HTML renderer for TradeAnalyzer results.
/// Covers Task 2: R-distribution, bars-to-R analysis, exit reason breakdown.
/// Called by TradeAnalyzer.ExportHtml() and by Program.cs --full-report.
/// </summary>
internal static class TradeAnalysisHtmlRenderer
{
    private static readonly string[] RBucketLabels =
    [
        "≤-3R", "-2.5R", "-2R", "-1.5R", "-1R", "-0.5R",
        "0R", "+0.5R", "+1R", "+1.5R", "+2R", "+2.5R", "≥+3R"
    ];

    // Bucket edges (lower inclusive, upper exclusive) – 0-indexed
    // Bucket i: lower = -3.0 + i*0.5  upper = lower + 0.5
    // First bucket catches anything ≤ -3; last bucket catches anything ≥ +3.

    public static string Build(
        IReadOnlyList<AnalyzerTradeSummary> summaries,
        IReadOnlyList<AnalyzerExitReason> exitReasons,
        IReadOnlyList<AnalyzerTrade> trades)
    {
        var sb = new StringBuilder();
        sb.AppendLine(HtmlHead);

        sb.AppendLine("<body>");
        sb.AppendLine("<main class=\"page\">");
        sb.AppendLine("<header class=\"header\">");
        sb.AppendLine("<div><p class=\"eyebrow\">TradingBrain</p><h1>Trade Analysis Report</h1></div>");
        sb.AppendLine($"<div class=\"run-meta\"><span>Gerado: {DateTime.Now:yyyy-MM-dd HH:mm}</span></div>");
        sb.AppendLine("</header>");

        // Summary cards
        var totalTrades = summaries.Sum(s => s.Trades);
        var avgWinRate = summaries.Count > 0 ? summaries.Average(s => s.WinRate) : 0;
        var avgR = trades.Count > 0 && trades.Any(t => double.IsFinite(t.RMultiple))
            ? trades.Where(t => double.IsFinite(t.RMultiple)).Average(t => t.RMultiple) : 0;
        var pctPositiveR = trades.Count > 0
            ? trades.Count(t => t.RMultiple > 0) * 100.0 / trades.Count : 0;

        sb.AppendLine("<section class=\"cards\">");
        AppendCard(sb, "Total Trades", totalTrades.ToString(CultureInfo.InvariantCulture), "todas as strategies");
        AppendCard(sb, "Avg Win Rate", F1(avgWinRate) + "%", "média simples");
        AppendCard(sb, "R Médio", F2(avgR) + "R", "gross R");
        AppendCard(sb, "% R > 0", F1(pctPositiveR) + "%", "trades com R positivo");
        sb.AppendLine("</section>");

        // R-distribution per strategy
        sb.AppendLine(BuildRDistributionSection(summaries, trades));

        // Bars-to-R analysis
        sb.AppendLine(BuildBarsToRSection(summaries, trades));

        // Exit reason breakdown
        sb.AppendLine(BuildExitReasonSection(exitReasons));

        sb.AppendLine("</main>");
        sb.AppendLine("<script>document.documentElement.dataset.ready='true';</script>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  2a  R-distribution histogram
    // ─────────────────────────────────────────────────────────────────────

    private static string BuildRDistributionSection(
        IReadOnlyList<AnalyzerTradeSummary> summaries,
        IReadOnlyList<AnalyzerTrade> trades)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<section class=\"panel\" id=\"r-distribution\">");
        sb.AppendLine("<div class=\"section-title\"><h2>2a — R-Múltiplo: Distribuição</h2></div>");

        var byStrategy = trades.GroupBy(t => t.Strategy).OrderBy(g => g.Key).ToList();

        foreach (var group in byStrategy)
        {
            var stratTrades = group.Where(t => double.IsFinite(t.RMultiple)).ToList();
            if (stratTrades.Count == 0) continue;

            var buckets = new int[RBucketLabels.Length];
            foreach (var t in stratTrades)
            {
                var idx = RBucketIndex(t.RMultiple);
                buckets[idx]++;
            }

            var maxBucket = Math.Max(1, buckets.Max());
            var avgRVal = stratTrades.Average(t => t.RMultiple);
            var medianR = Median(stratTrades.Select(t => t.RMultiple).ToList());
            var pctPos = stratTrades.Count(t => t.RMultiple > 0) * 100.0 / stratTrades.Count;
            var expectance = stratTrades.Average(t => t.RMultiple);

            sb.AppendLine($"<div class=\"rdist-strategy\">");
            sb.AppendLine($"<h3>{H(group.Key)}</h3>");
            sb.AppendLine("<div class=\"rdist-meta\">");
            AppendMini(sb, "R médio", F2(avgRVal) + "R");
            AppendMini(sb, "R mediana", F2(medianR) + "R");
            AppendMini(sb, "% R>0", F1(pctPos) + "%");
            AppendMini(sb, "Expectância", F2(expectance) + "R");
            sb.AppendLine("</div>");

            // SVG histogram
            const int W = 780, H_SVG = 180, PadL = 8, PadR = 8, PadT = 10, PadB = 28;
            var barW = (W - PadL - PadR) / buckets.Length;
            var innerH = H_SVG - PadT - PadB;

            sb.AppendLine($"<svg viewBox=\"0 0 {W} {H_SVG}\" style=\"width:100%;max-width:{W}px;display:block;margin:10px 0\" xmlns=\"http://www.w3.org/2000/svg\">");

            for (var i = 0; i < buckets.Length; i++)
            {
                var barH = (int)((double)buckets[i] / maxBucket * innerH);
                var x = PadL + i * barW;
                var y = PadT + innerH - barH;
                // Pivot index: bucket 6 = "0R"
                var color = i < 6 ? "#E24B4A" : "#1D9E75";
                if (buckets[i] > 0)
                    sb.AppendLine($"<rect x=\"{x + 2}\" y=\"{y}\" width=\"{barW - 4}\" height=\"{barH}\" fill=\"{color}\" rx=\"3\"/>");
                if (buckets[i] > 0)
                    sb.AppendLine($"<text x=\"{x + barW / 2}\" y=\"{y - 2}\" text-anchor=\"middle\" font-size=\"9\" fill=\"#697386\">{buckets[i]}</text>");
                sb.AppendLine($"<text x=\"{x + barW / 2}\" y=\"{PadT + innerH + 16}\" text-anchor=\"middle\" font-size=\"9\" fill=\"#697386\">{H(RBucketLabels[i])}</text>");
            }

            // Zero line at pivot (bucket 6)
            var zeroX = PadL + 6 * barW + barW / 2;
            sb.AppendLine($"<line x1=\"{zeroX}\" y1=\"{PadT}\" x2=\"{zeroX}\" y2=\"{PadT + innerH}\" stroke=\"#697386\" stroke-width=\"1\" stroke-dasharray=\"4,3\"/>");
            sb.AppendLine("</svg>");
            sb.AppendLine("</div>"); // rdist-strategy
        }

        sb.AppendLine("</section>");
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  2b  Bars-to-R analysis
    // ─────────────────────────────────────────────────────────────────────

    private static string BuildBarsToRSection(
        IReadOnlyList<AnalyzerTradeSummary> summaries,
        IReadOnlyList<AnalyzerTrade> trades)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<section class=\"panel\" id=\"bars-to-r\">");
        sb.AppendLine("<div class=\"section-title\"><h2>2b — Bars-to-R Analysis</h2></div>");
        sb.AppendLine("<div class=\"table-wrap\"><table>");
        sb.AppendLine("<thead><tr><th>Strategy</th><th>Trades</th><th>Hit 0.5R</th><th>Hit 1R</th><th>Hit 1.5R</th><th>Hit 2R</th><th>Hit 3R</th><th>Hit -0.5R</th><th>Hit -1R</th></tr></thead>");
        sb.AppendLine("<tbody>");

        foreach (var s in summaries)
        {
            var stratTrades = trades.Where(t => t.Strategy == s.Strategy).ToList();
            if (stratTrades.Count == 0) continue;

            var n = stratTrades.Count;
            var hitHalf = s.HitHalfRPct;
            var hitOne = s.HitOneRPct;
            var hitOneHalf = s.HitOneAndHalfRPct;
            var hitTwo = s.HitTwoRPct;
            var hitThree = s.HitThreeRPct;
            var hitMHalf = s.HitMinusHalfRPct;
            var hitMOne = s.HitMinusOneRPct;

            sb.AppendLine("<tr>");
            sb.AppendLine($"<td><b>{H(s.Strategy)}</b></td>");
            sb.AppendLine($"<td>{n}</td>");
            sb.AppendLine($"<td>{PctCell(hitHalf)}</td>");
            sb.AppendLine($"<td>{PctCell(hitOne)}</td>");
            sb.AppendLine($"<td>{PctCell(hitOneHalf)}</td>");
            sb.AppendLine($"<td>{PctCell(hitTwo)}</td>");
            sb.AppendLine($"<td>{PctCell(hitThree)}</td>");
            sb.AppendLine($"<td class=\"neg\">{F1(hitMHalf)}%</td>");
            sb.AppendLine($"<td class=\"neg\">{F1(hitMOne)}%</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table></div>");
        sb.AppendLine("<p class=\"hint\">Interpretação: se 80% atingem 1R mas só 40% atingem 2R, o target ótimo está próximo de 1R.</p>");
        sb.AppendLine("</section>");
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  2c  Exit reason breakdown
    // ─────────────────────────────────────────────────────────────────────

    private static string BuildExitReasonSection(IReadOnlyList<AnalyzerExitReason> exitReasons)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<section class=\"panel\" id=\"exit-reasons\">");
        sb.AppendLine("<div class=\"section-title\"><h2>2c — Exit Reason Breakdown</h2></div>");

        foreach (var stratGroup in exitReasons.GroupBy(r => r.Strategy).OrderBy(g => g.Key))
        {
            var reasons = stratGroup.OrderByDescending(r => r.Count).ToList();
            var maxCount = Math.Max(1, reasons.Max(r => r.Count));

            sb.AppendLine($"<div class=\"exit-strat\">");
            sb.AppendLine($"<h3>{H(stratGroup.Key)}</h3>");
            sb.AppendLine("<div class=\"exit-bars\">");
            foreach (var r in reasons)
            {
                var pct = r.Percent;
                var barW = (int)(pct * 2); // 100% → 200px
                var color = ExitColor(r.ExitReason);
                sb.AppendLine($"<div class=\"exit-bar-row\">");
                sb.AppendLine($"<span class=\"exit-label\">{H(r.ExitReason)}</span>");
                sb.AppendLine($"<div class=\"exit-track\"><div class=\"exit-fill\" style=\"width:{barW}px;background:{color}\"></div></div>");
                sb.AppendLine($"<span class=\"exit-pct\">{F1(pct)}%</span>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div></div>");
        }

        sb.AppendLine("</section>");
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static int RBucketIndex(double r)
    {
        // Buckets: ≤-3, -2.5, -2, -1.5, -1, -0.5, 0, +0.5, +1, +1.5, +2, +2.5, ≥+3
        if (r <= -3.0) return 0;
        if (r >= 3.0) return 12;
        var idx = (int)Math.Floor((r + 3.0) / 0.5);
        return Math.Clamp(idx, 0, 12);
    }

    private static double Median(IReadOnlyList<double> sorted)
    {
        if (sorted.Count == 0) return 0;
        var list = sorted.OrderBy(v => v).ToList();
        var mid = list.Count / 2;
        return list.Count % 2 == 0 ? (list[mid - 1] + list[mid]) / 2.0 : list[mid];
    }

    private static string PctCell(double pct)
    {
        var cls = pct >= 50 ? "pos" : pct >= 30 ? "warn" : "neg";
        return $"<span class=\"{cls}\">{F1(pct)}%</span>";
    }

    private static string ExitColor(string reason)
    {
        if (reason.Contains("Target", StringComparison.OrdinalIgnoreCase)) return "#1D9E75";
        if (reason.Contains("Stop BE", StringComparison.OrdinalIgnoreCase)) return "#378ADD";
        if (reason.Contains("Stop", StringComparison.OrdinalIgnoreCase)) return "#E24B4A";
        if (reason.Contains("Time", StringComparison.OrdinalIgnoreCase)) return "#b7791f";
        return "#9B59B6";
    }

    private static void AppendCard(StringBuilder sb, string label, string value, string hint)
    {
        sb.AppendLine("<article class=\"card\">");
        sb.AppendLine($"<span>{H(label)}</span><strong>{H(value)}</strong><small>{H(hint)}</small>");
        sb.AppendLine("</article>");
    }

    private static void AppendMini(StringBuilder sb, string label, string value)
        => sb.AppendLine($"<div class=\"mini\"><span>{H(label)}</span><b>{value}</b></div>");

    private static string H(string s) => System.Net.WebUtility.HtmlEncode(s);
    private static string F1(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString("0.#", CultureInfo.InvariantCulture);
    private static string F2(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString("0.##", CultureInfo.InvariantCulture);

    // ─────────────────────────────────────────────────────────────────────
    //  CSS + HTML head
    // ─────────────────────────────────────────────────────────────────────

    private const string HtmlHead = """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>TradingBrain Trade Analysis</title>
        <style>
        :root { --bg:#f6f8fb; --panel:#ffffff; --ink:#172033; --muted:#697386; --line:#d9e0ea;
                --is:#378ADD; --oos:#1D9E75; --neg:#E24B4A; --warn:#b7791f; }
        * { box-sizing:border-box; }
        body { margin:0; background:var(--bg); color:var(--ink);
               font-family:Inter, ui-sans-serif, system-ui, sans-serif; font-size:14px; }
        .page { max-width:1200px; margin:0 auto; padding:28px; }
        .header { display:flex; align-items:flex-end; justify-content:space-between; gap:24px; margin-bottom:22px; }
        .eyebrow { color:var(--is); font-size:12px; font-weight:700; letter-spacing:0; margin:0 0 6px; text-transform:uppercase; }
        h1,h2,h3 { margin:0; letter-spacing:0; }
        h1 { font-size:28px; }
        h2 { font-size:16px; }
        h3 { font-size:14px; color:var(--muted); margin:16px 0 8px; }
        .run-meta { display:flex; flex-wrap:wrap; justify-content:flex-end; gap:8px; color:var(--muted); }
        .run-meta span { border:1px solid var(--line); border-radius:6px; padding:6px 8px; background:var(--panel); }
        .cards { display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:12px; margin-bottom:16px; }
        .card,.panel { background:var(--panel); border:1px solid var(--line); border-radius:8px;
                       box-shadow:0 8px 24px rgba(23,32,51,.05); }
        .card { padding:16px; }
        .card span,.card small { display:block; color:var(--muted); }
        .card strong { display:block; margin:8px 0 2px; font-size:24px; line-height:1.1; }
        .panel { padding:16px; margin-bottom:16px; }
        .section-title { display:flex; align-items:center; justify-content:space-between; margin-bottom:12px; }
        .table-wrap { overflow-x:auto; }
        table { width:100%; border-collapse:collapse; min-width:700px; }
        th,td { border-bottom:1px solid var(--line); padding:9px 10px; text-align:left; white-space:nowrap; }
        th { color:var(--muted); font-size:12px; font-weight:700; text-transform:uppercase; }
        tr:last-child td { border-bottom:0; }
        .pos { color:var(--oos); }
        .neg { color:var(--neg); }
        .warn { color:var(--warn); }
        /* R-distribution */
        .rdist-strategy { margin-bottom:24px; }
        .rdist-meta { display:flex; gap:14px; flex-wrap:wrap; margin-bottom:6px; }
        .mini { display:flex; flex-direction:column; font-size:12px; padding:6px 10px;
                background:#f3f6fa; border-radius:6px; }
        .mini span { color:var(--muted); font-size:10px; text-transform:uppercase; }
        .mini b { font-size:14px; margin-top:2px; }
        /* Exit bars */
        .exit-strat { margin-bottom:20px; }
        .exit-bars { display:flex; flex-direction:column; gap:6px; }
        .exit-bar-row { display:grid; grid-template-columns:200px 210px 50px; gap:8px; align-items:center; }
        .exit-label { font-size:12px; color:var(--muted); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
        .exit-track { height:14px; border-radius:4px; background:#ecf1f7; overflow:hidden; }
        .exit-fill { height:100%; border-radius:4px; }
        .exit-pct { font-size:12px; font-variant-numeric:tabular-nums; }
        /* Hint */
        .hint { font-size:12px; color:var(--muted); margin-top:8px; font-style:italic; }
        @media(max-width:900px){.cards{grid-template-columns:repeat(2,1fr)}.exit-bar-row{grid-template-columns:1fr 1fr 40px}}
        </style>
        </head>
        """;
}

// ─────────────────────────────────────────────────────────────────────────
//  DTOs used by TradeAnalysisHtmlRenderer (so TradeAnalyzer can stay private)
// ─────────────────────────────────────────────────────────────────────────

internal sealed record AnalyzerTrade(
    string Strategy,
    double RMultiple,
    bool HitHalfR,
    bool HitOneR,
    bool HitOneAndHalfR,
    bool HitTwoR,
    bool HitThreeR,
    bool HitMinusHalfR,
    bool HitMinusOneR,
    string ExitReason);

internal sealed record AnalyzerTradeSummary(
    string Strategy,
    int Trades,
    double WinRate,
    double HitHalfRPct,
    double HitOneRPct,
    double HitOneAndHalfRPct,
    double HitTwoRPct,
    double HitThreeRPct,
    double HitMinusHalfRPct,
    double HitMinusOneRPct);

internal sealed record AnalyzerExitReason(
    string Strategy,
    string ExitReason,
    int Count,
    int StrategyTrades)
{
    public double Percent => StrategyTrades == 0 ? 0 : Count * 100.0 / StrategyTrades;
}

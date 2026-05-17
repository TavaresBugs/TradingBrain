using System.Globalization;
using System.Net;
using System.Text;
using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

/// <summary>
/// Renders SVG inline sections for equity curve, drawdown, and MFE/MAE scatter.
/// All methods return raw HTML strings to be injected into larger HTML documents.
/// </summary>
internal static class EquityCurveRenderer
{
    // ── palette: one colour per strategy (cycled) ─────────────────────────
    private static readonly string[] Palette =
    [
        "#378ADD", "#1D9E75", "#F5A623", "#E24B4A", "#9B59B6",
        "#1ABC9C", "#E67E22", "#2ECC71", "#3498DB", "#E91E63"
    ];

    // ─────────────────────────────────────────────────────────────────────
    //  1a  Equity curve + drawdown
    // ─────────────────────────────────────────────────────────────────────

    public static string RenderEquityCurveSection(IReadOnlyList<TradeResult> trades)
    {
        var byStrategy = trades
            .GroupBy(t => t.StrategyName)
            .OrderBy(g => g.Key)
            .ToList();

        if (byStrategy.Count == 0)
            return "<section class=\"panel\"><div class=\"section-title\"><h2>Equity Curve</h2></div><p style=\"color:var(--muted)\">Sem trades.</p></section>";

        var sb = new StringBuilder();
        sb.AppendLine("<section class=\"panel\">");
        sb.AppendLine("<div class=\"section-title\"><h2>Equity Curve</h2></div>");

        // Legend
        sb.AppendLine("<div class=\"eq-legend\">");
        for (var i = 0; i < byStrategy.Count; i++)
        {
            var color = Palette[i % Palette.Length];
            sb.AppendLine($"<span class=\"eq-legend-item\"><span class=\"eq-dot\" style=\"background:{color}\"></span>{H(byStrategy[i].Key)}</span>");
        }
        sb.AppendLine("</div>");

        // SVG
        const int W = 900, H_SVG = 280, PadL = 60, PadR = 20, PadT = 20, PadB = 40;
        var innerW = W - PadL - PadR;
        var innerH = H_SVG - PadT - PadB;

        // Gather all equity points across strategies for Y-axis range
        var allEquities = byStrategy
            .SelectMany(g => BuildEquityPoints(g.ToList()))
            .Select(p => p.Equity)
            .ToList();

        if (allEquities.Count == 0)
        {
            sb.AppendLine("</section>");
            return sb.ToString();
        }

        var yMin = allEquities.Min();
        var yMax = allEquities.Max();
        var yRange = yMax - yMin;
        if (yRange < 1) yRange = 1;
        // Pad a bit
        yMin -= yRange * 0.05;
        yMax += yRange * 0.05;
        yRange = yMax - yMin;

        sb.AppendLine($"<svg viewBox=\"0 0 {W} {H_SVG}\" style=\"width:100%;max-width:{W}px;display:block;overflow:visible\" xmlns=\"http://www.w3.org/2000/svg\">");

        // Background rect
        sb.AppendLine($"<rect x=\"{PadL}\" y=\"{PadT}\" width=\"{innerW}\" height=\"{innerH}\" fill=\"#f9fafb\" rx=\"4\"/>");

        // Zero line
        var zeroY = PadT + innerH - (int)((0 - yMin) / yRange * innerH);
        zeroY = Math.Clamp(zeroY, PadT, PadT + innerH);
        sb.AppendLine($"<line x1=\"{PadL}\" y1=\"{zeroY}\" x2=\"{PadL + innerW}\" y2=\"{zeroY}\" stroke=\"#d0d7de\" stroke-width=\"1\" stroke-dasharray=\"4,3\"/>");

        // Y axis labels
        for (var tick = 0; tick <= 4; tick++)
        {
            var val = yMin + yRange * tick / 4.0;
            var ty = PadT + innerH - (int)((val - yMin) / yRange * innerH);
            sb.AppendLine($"<line x1=\"{PadL - 4}\" y1=\"{ty}\" x2=\"{PadL}\" y2=\"{ty}\" stroke=\"#aab\" stroke-width=\"1\"/>");
            sb.AppendLine($"<text x=\"{PadL - 7}\" y=\"{ty + 4}\" text-anchor=\"end\" font-size=\"10\" fill=\"#697386\">{F1(val)}</text>");
        }

        // Per-strategy equity lines + drawdown shading
        for (var i = 0; i < byStrategy.Count; i++)
        {
            var group = byStrategy[i];
            var color = Palette[i % Palette.Length];
            var points = BuildEquityPoints(group.ToList());
            if (points.Count == 0) continue;

            var n = points.Count;

            // Map to SVG coords
            double SvgX(int idx) => PadL + (double)idx / (n - 1) * innerW;
            double SvgY(double eq) => PadT + innerH - (eq - yMin) / yRange * innerH;

            // Drawdown shading
            var ddSb = new StringBuilder();
            ddSb.Append($"M {F(SvgX(0))} {F(SvgY(points[0].Peak))}");
            for (var j = 1; j < n; j++)
                ddSb.Append($" L {F(SvgX(j))} {F(SvgY(points[j].Peak))}");
            for (var j = n - 1; j >= 0; j--)
                ddSb.Append($" L {F(SvgX(j))} {F(SvgY(points[j].Equity))}");
            ddSb.Append(" Z");
            sb.AppendLine($"<path d=\"{ddSb}\" fill=\"{color}\" fill-opacity=\"0.08\"/>");

            // Equity polyline
            var pts = string.Join(" ", Enumerable.Range(0, n)
                .Select(j => $"{F(SvgX(j))},{F(SvgY(points[j].Equity))}"));
            sb.AppendLine($"<polyline points=\"{pts}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"2\" stroke-linejoin=\"round\"/>");
        }

        // Axes
        sb.AppendLine($"<line x1=\"{PadL}\" y1=\"{PadT}\" x2=\"{PadL}\" y2=\"{PadT + innerH}\" stroke=\"#aab\" stroke-width=\"1\"/>");
        sb.AppendLine($"<line x1=\"{PadL}\" y1=\"{PadT + innerH}\" x2=\"{PadL + innerW}\" y2=\"{PadT + innerH}\" stroke=\"#aab\" stroke-width=\"1\"/>");
        sb.AppendLine($"<text x=\"{PadL + innerW / 2}\" y=\"{PadT + innerH + 30}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#697386\">Trade #</text>");
        sb.AppendLine($"<text x=\"{PadL - 45}\" y=\"{PadT + innerH / 2}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#697386\" transform=\"rotate(-90,{PadL - 45},{PadT + innerH / 2})\">Net Pts</text>");

        sb.AppendLine("</svg>");

        // Metrics cards per strategy
        sb.AppendLine("<div class=\"eq-metrics-grid\">");
        for (var i = 0; i < byStrategy.Count; i++)
        {
            var group = byStrategy[i];
            var color = Palette[i % Palette.Length];
            var tradeList = group.ToList();
            var points = BuildEquityPoints(tradeList);
            if (points.Count == 0) continue;

            var maxDD = points.Max(p => p.Peak - p.Equity);
            var finalEq = points[^1].Equity;
            var recoveryFactor = maxDD > 0 ? finalEq / maxDD : double.NaN;
            var (maxLoss, maxWin) = ComputeStreaks(tradeList);

            sb.AppendLine($"<div class=\"eq-strat-card\" style=\"border-left:3px solid {color}\">");
            sb.AppendLine($"<div class=\"eq-strat-name\">{H(group.Key)}</div>");
            sb.AppendLine("<div class=\"eq-strat-metrics\">");
            AppendMetricChip(sb, "Max DD", F1(maxDD) + " pts");
            AppendMetricChip(sb, "Recovery", double.IsNaN(recoveryFactor) ? "-" : F2(recoveryFactor) + "x");
            AppendMetricChip(sb, "Max Loss Streak", maxLoss.ToString(CultureInfo.InvariantCulture));
            AppendMetricChip(sb, "Max Win Streak", maxWin.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
        }
        sb.AppendLine("</div>");

        sb.AppendLine("</section>");
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  1c  MFE vs MAE scatter
    // ─────────────────────────────────────────────────────────────────────

    public static string RenderMfeMaeSection(IReadOnlyList<TradeResult> trades)
    {
        var validTrades = trades
            .Where(t => t.MFEPoints >= 0 && t.MAEPoints >= 0)
            .ToList();

        if (validTrades.Count == 0)
            return "<section class=\"panel\"><div class=\"section-title\"><h2>Trade Excursion Analysis</h2></div><p style=\"color:var(--muted)\">Sem dados de excursão.</p></section>";

        const int W = 600, H_SVG = 400, Pad = 60;
        var innerW = W - Pad * 2;
        var innerH = H_SVG - Pad * 2;

        var maxMae = validTrades.Max(t => t.MAEPoints);
        var maxMfe = validTrades.Max(t => t.MFEPoints);
        var axisMax = Math.Max(maxMae, maxMfe);
        if (axisMax < 1) axisMax = 1;

        var sb = new StringBuilder();
        sb.AppendLine("<section class=\"panel\">");
        sb.AppendLine("<div class=\"section-title\"><h2>Trade Excursion Analysis (MFE vs MAE)</h2></div>");
        sb.AppendLine("<div class=\"scatter-layout\">");
        sb.AppendLine($"<svg viewBox=\"0 0 {W} {H_SVG}\" style=\"width:100%;max-width:{W}px;display:block\" xmlns=\"http://www.w3.org/2000/svg\">");

        // Background
        sb.AppendLine($"<rect x=\"{Pad}\" y=\"{Pad}\" width=\"{innerW}\" height=\"{innerH}\" fill=\"#f9fafb\" rx=\"4\"/>");

        // Diagonal MAE=MFE reference line
        sb.AppendLine($"<line x1=\"{Pad}\" y1=\"{Pad + innerH}\" x2=\"{Pad + innerW}\" y2=\"{Pad}\" stroke=\"#aab\" stroke-width=\"1\" stroke-dasharray=\"5,4\"/>");

        // Grid lines
        for (var t = 1; t <= 4; t++)
        {
            var gx = Pad + (int)((double)t / 4 * innerW);
            var gy = Pad + (int)((double)t / 4 * innerH);
            sb.AppendLine($"<line x1=\"{gx}\" y1=\"{Pad}\" x2=\"{gx}\" y2=\"{Pad + innerH}\" stroke=\"#e0e4ea\" stroke-width=\"1\"/>");
            sb.AppendLine($"<line x1=\"{Pad}\" y1=\"{gy}\" x2=\"{Pad + innerW}\" y2=\"{gy}\" stroke=\"#e0e4ea\" stroke-width=\"1\"/>");
        }

        // Points
        foreach (var trade in validTrades)
        {
            var cx = Pad + (int)(trade.MAEPoints / axisMax * innerW);
            var cy = Pad + innerH - (int)(trade.MFEPoints / axisMax * innerH);
            cx = Math.Clamp(cx, Pad, Pad + innerW);
            cy = Math.Clamp(cy, Pad, Pad + innerH);
            var color = trade.NetPoints >= 0 ? "#1D9E75" : "#E24B4A";
            sb.AppendLine($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"4\" fill=\"{color}\" fill-opacity=\"0.65\"/>");
        }

        // Axes
        sb.AppendLine($"<line x1=\"{Pad}\" y1=\"{Pad}\" x2=\"{Pad}\" y2=\"{Pad + innerH}\" stroke=\"#697386\" stroke-width=\"1\"/>");
        sb.AppendLine($"<line x1=\"{Pad}\" y1=\"{Pad + innerH}\" x2=\"{Pad + innerW}\" y2=\"{Pad + innerH}\" stroke=\"#697386\" stroke-width=\"1\"/>");

        // Axis labels
        sb.AppendLine($"<text x=\"{Pad + innerW / 2}\" y=\"{H_SVG - 8}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#697386\">MAE (pts adverso)</text>");
        sb.AppendLine($"<text x=\"{Pad - 45}\" y=\"{Pad + innerH / 2}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#697386\" transform=\"rotate(-90,{Pad - 45},{Pad + innerH / 2})\">MFE (pts favorável)</text>");

        // Tick labels
        for (var t = 0; t <= 4; t++)
        {
            var val = axisMax * t / 4.0;
            var gx = Pad + (int)((double)t / 4 * innerW);
            var gy = Pad + innerH - (int)((double)t / 4 * innerH);
            sb.AppendLine($"<text x=\"{gx}\" y=\"{Pad + innerH + 14}\" text-anchor=\"middle\" font-size=\"9\" fill=\"#697386\">{F1(val)}</text>");
            sb.AppendLine($"<text x=\"{Pad - 5}\" y=\"{gy + 3}\" text-anchor=\"end\" font-size=\"9\" fill=\"#697386\">{F1(val)}</text>");
        }

        sb.AppendLine("</svg>");

        // Metrics
        var mfeGtMae = validTrades.Count(t => t.MFEPoints > t.MAEPoints);
        var pctMfeGtMae = validTrades.Count > 0 ? mfeGtMae * 100.0 / validTrades.Count : 0;
        var tradesWithRisk = validTrades.Where(t => t.RiskPoints > 0 && double.IsFinite(t.RiskPoints)).ToList();
        var avgMaeStop = tradesWithRisk.Count > 0 ? tradesWithRisk.Average(t => t.MAEPoints / t.RiskPoints) * 100.0 : double.NaN;
        var losers = validTrades.Where(t => t.NetPoints < 0).ToList();
        var loserAvgMfe = losers.Count > 0 ? losers.Average(t => t.MFEPoints) : 0;

        sb.AppendLine("<div class=\"scatter-metrics\">");
        AppendScatterMetric(sb, "MFE &gt; MAE", $"{F1(pctMfeGtMae)}% <small>(ideal ≥60%)</small>", pctMfeGtMae >= 60 ? "good" : "bad");
        AppendScatterMetric(sb, "MAE médio / Stop", double.IsNaN(avgMaeStop) ? "-" : $"{F1(avgMaeStop)}% <small>(ideal 35–60%)</small>", double.IsNaN(avgMaeStop) || (avgMaeStop >= 35 && avgMaeStop <= 60) ? "good" : "warn");
        AppendScatterMetric(sb, "MFE médio (perdedores)", $"{F2(loserAvgMfe)} pts <small>(>0 = saída prematura)</small>", loserAvgMfe > 2 ? "warn" : "good");
        sb.AppendLine("</div>");

        sb.AppendLine("</div>"); // scatter-layout
        sb.AppendLine("</section>");
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  1b  Trade detail table (last 200 per strategy)
    // ─────────────────────────────────────────────────────────────────────

    public static string RenderTradeDetailSection(IReadOnlyList<TradeResult> trades)
    {
        if (trades.Count == 0)
            return "<section class=\"panel\"><div class=\"section-title\"><h2>Trade Detail</h2></div><p style=\"color:var(--muted)\">Sem trades.</p></section>";

        const int MaxPerStrategy = 200;

        var sb = new StringBuilder();
        sb.AppendLine("<section class=\"panel\">");
        sb.AppendLine("<div class=\"section-title\"><h2>Trade Detail</h2></div>");
        sb.AppendLine("<div class=\"table-wrap\">");
        sb.AppendLine("<table class=\"trade-detail-table\">");
        sb.AppendLine("<thead><tr><th>#</th><th>Data</th><th>Strategy</th><th>Dir</th><th>Entrada</th><th>Saída</th><th>Bars</th><th>Gross</th><th>Net</th><th>R</th><th>MFE</th><th>MAE</th><th>Saída</th></tr></thead>");
        sb.AppendLine("<tbody>");

        var byStrategy = trades
            .GroupBy(t => t.StrategyName)
            .OrderBy(g => g.Key);

        var tradeNum = 0;
        foreach (var group in byStrategy)
        {
            var all = group.OrderBy(t => t.EntryTime).ToList();
            var total = all.Count;
            var shown = all.TakeLast(MaxPerStrategy).ToList();

            if (shown.Count < total)
                sb.AppendLine($"<tr><td colspan=\"13\" style=\"color:var(--muted);font-style:italic;text-align:center\">— {H(group.Key)}: mostrando últimos {shown.Count} de {total} trades —</td></tr>");

            foreach (var t in shown)
            {
                tradeNum++;
                var rowClass = t.NetPoints >= 0 ? "trade-win" : "trade-loss";
                sb.AppendLine($"<tr class=\"{rowClass}\">");
                sb.AppendLine($"<td>{tradeNum}</td>");
                sb.AppendLine($"<td>{t.EntryTime:yyyy-MM-dd HH:mm}</td>");
                sb.AppendLine($"<td>{H(t.StrategyName)}</td>");
                sb.AppendLine($"<td>{H(t.Direction)}</td>");
                sb.AppendLine($"<td>{F2(t.EntryPrice)}</td>");
                sb.AppendLine($"<td>{F2(t.ExitPrice)}</td>");
                sb.AppendLine($"<td>{t.BarsHeld}</td>");
                sb.AppendLine($"<td class=\"{(t.GrossPoints >= 0 ? "pos" : "neg")}\">{F2(t.GrossPoints)}</td>");
                sb.AppendLine($"<td class=\"{(t.NetPoints >= 0 ? "pos" : "neg")}\">{F2(t.NetPoints)}</td>");
                var rStr = double.IsNaN(t.RMultiple) ? "-" : F2(t.RMultiple);
                sb.AppendLine($"<td class=\"{(t.RMultiple >= 0 ? "pos" : "neg")}\">{rStr}</td>");
                sb.AppendLine($"<td>{F2(t.MFEPoints)}</td>");
                sb.AppendLine($"<td>{F2(t.MAEPoints)}</td>");
                sb.AppendLine($"<td style=\"font-size:11px\">{H(t.ExitReason)}</td>");
                sb.AppendLine("</tr>");
            }
        }

        sb.AppendLine("</tbody></table>");
        sb.AppendLine("</div>");
        sb.AppendLine("</section>");
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────

    private sealed record EquityPoint(double Equity, double Peak);

    private static List<EquityPoint> BuildEquityPoints(IReadOnlyList<TradeResult> trades)
    {
        var points = new List<EquityPoint>(trades.Count);
        var cumEq = 0.0;
        var peak = 0.0;
        foreach (var t in trades)
        {
            cumEq += t.NetPoints;
            if (cumEq > peak) peak = cumEq;
            points.Add(new EquityPoint(cumEq, peak));
        }
        return points;
    }

    private static (int MaxLoss, int MaxWin) ComputeStreaks(IReadOnlyList<TradeResult> trades)
    {
        var maxLoss = 0; var maxWin = 0;
        var curLoss = 0; var curWin = 0;
        foreach (var t in trades)
        {
            if (t.NetPoints < 0) { curLoss++; curWin = 0; }
            else { curWin++; curLoss = 0; }
            if (curLoss > maxLoss) maxLoss = curLoss;
            if (curWin > maxWin) maxWin = curWin;
        }
        return (maxLoss, maxWin);
    }

    private static void AppendMetricChip(StringBuilder sb, string label, string value)
    {
        sb.AppendLine($"<div class=\"eq-chip\"><span class=\"eq-chip-label\">{label}</span><span class=\"eq-chip-value\">{value}</span></div>");
    }

    private static void AppendScatterMetric(StringBuilder sb, string label, string value, string cls)
    {
        sb.AppendLine($"<div class=\"scatter-metric {cls}\"><span class=\"sm-label\">{label}</span><span class=\"sm-value\">{value}</span></div>");
    }

    private static string H(string s) => WebUtility.HtmlEncode(s);
    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string F1(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString("0.#", CultureInfo.InvariantCulture);
    private static string F2(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString("0.##", CultureInfo.InvariantCulture);
}

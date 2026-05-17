using System.Globalization;
using System.Text;
using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

public static class RegimeReportWriter
{
    public static string Build(
        IReadOnlyList<MarketBar> bars,
        ExecutionSettings settings)
    {
        var regimes = RegimeClassifier.Classify(bars);
        var sb = new StringBuilder();

        sb.Append(BuildDistribution(regimes));
        sb.Append(BuildStrategyResults(bars, settings));
        sb.Append(BuildUndefinedBreakdown(regimes));
        sb.Append(BuildMonthlyByRegime(regimes));

        return WrapHtml(sb.ToString(), regimes.Count);
    }

    private static string BuildDistribution(IReadOnlyList<DayRegime> regimes)
    {
        var total = regimes.Count;
        var rows = regimes
            .GroupBy(r => r.Regime)
            .OrderByDescending(g => g.Count())
            .Select(g =>
            {
                var list = g.ToList();
                var ibAvg = Avg(list, d => d.IbTodayFullRatio);
                var gapAvg = Avg(list, d => d.GapRatio);
                var ovAvg = Avg(list, d => d.OvernightRatio);
                var cPct = Pct(list, d => d.CperiodInsideIb);
                var openOutPct = Pct(list, d => d.OpenOutsideIbYest);
                var otfPct = Pct(list, d => d.OneTimeFramingUp || d.OneTimeFramingDown);
                var color = RegimeColor(g.Key);
                var pct = total == 0 ? 0 : list.Count * 100.0 / total;
                return $"""
                    <tr>
                      <td><span class="badge" style="background:{color}">{g.Key}</span></td>
                      <td><b>{list.Count}</b></td>
                      <td>{pct:F1}%</td>
                      <td>{ibAvg:F2}x</td>
                      <td>{gapAvg:F2}x</td>
                      <td>{ovAvg:F2}x</td>
                      <td>{cPct:F0}%</td>
                      <td>{openOutPct:F0}%</td>
                      <td>{otfPct:F0}%</td>
                    </tr>
                    """;
            });

        return $"""
            <section>
              <h2>1 - Distribuicao de Regimes <span class="sub">({regimes.Count} dias)</span></h2>
              <table>
                <tr>
                  <th>Regime</th><th>Dias</th><th>%</th>
                  <th>IB Full avg</th><th>Gap avg</th><th>Overnight avg</th>
                  <th>C-period Inside%</th><th>Open Outside%</th><th>OTF%</th>
                </tr>
                {string.Join("\n", rows)}
              </table>
            </section>
            """;
    }

    private static string BuildStrategyResults(
        IReadOnlyList<MarketBar> bars,
        ExecutionSettings settings)
    {
        var strategies = new[]
        {
            StrategyKind.Trend,
            StrategyKind.IbBreakout,
            StrategyKind.VwapReversion,
            StrategyKind.BollingerFade,
            StrategyKind.OrbBreakout,
            StrategyKind.SchoolRun,
            StrategyKind.Momentum,
        };

        var rows = new StringBuilder();
        foreach (var strategy in strategies)
        {
            var allowed = StrategyRegimeMap.For(strategy);
            if (allowed.Count == 0)
            {
                continue;
            }

            var filtered = RegimeFilter.Apply(bars, allowed);
            var days = filtered.Select(b => b.Time.Date).Distinct().Count();

            var backtester = new StrategyBacktester(strategy);
            var backtestRows = backtester.Run(filtered);
            var summary = StrategyBacktester.Summarize(backtestRows, settings);

            var regimeLabel = string.Join("+", allowed.Select(r => r.ToString()));
            var pfClass = summary.NetProfitFactor >= 2.0 ? "good"
                : summary.NetProfitFactor >= 1.2 ? "ok" : "bad";

            rows.AppendLine($"""
                <tr>
                  <td><b>{strategy}</b></td>
                  <td class="dim">{regimeLabel}</td>
                  <td>{days}</td>
                  <td>{summary.ClosedTrades}</td>
                  <td>{summary.WinRate:F1}%</td>
                  <td class="{pfClass}">{summary.NetProfitFactor:F2}</td>
                  <td>{summary.NetExpectancy:F1}</td>
                  <td>{summary.NetPnL:F1}</td>
                  <td>{summary.MaxDrawdown:F1}</td>
                  <td>{summary.ReturnToDrawdown:F2}</td>
                </tr>
                """);
        }

        return $"""
            <section>
              <h2>2 - Resultados por Strategy <span class="sub">(params RefinedDefault)</span></h2>
              <table>
                <tr>
                  <th>Strategy</th><th>Regimes</th><th>Dias</th><th>Trades</th>
                  <th>Win%</th><th>Net PF</th><th>Expectancy</th>
                  <th>Net Pts</th><th>Max DD</th><th>RTD</th>
                </tr>
                {rows}
              </table>
            </section>
            """;
    }

    private static string BuildUndefinedBreakdown(IReadOnlyList<DayRegime> regimes)
    {
        var undefined = regimes
            .Where(r => r.Regime == MarketRegime.Undefined)
            .OrderBy(r => r.Date)
            .ToList();

        if (undefined.Count == 0)
        {
            return "<section><h2>3 - Dias Undefined</h2><p>Nenhum.</p></section>";
        }

        var clusters = undefined
            .GroupBy(r =>
            {
                if (r.OpenOutsideIbYest && r.IbTodayFullRatio < 0.50) return "OpenOutside+NarrowIB";
                if (r.OpenOutsideIbYest) return "OpenOutside+NormalIB";
                if (r.GapRatio > 0.50) return "HighGap+NoBreakout";
                if (r.OvernightRatio > 1.20) return "HighOvernight+NoBreakout";
                if (r.IbTodayFullRatio > 1.50) return "WideIB";
                return "NoStrongSignal";
            })
            .OrderByDescending(g => g.Count())
            .Select(g => $"""
                <tr>
                  <td><b>{g.Key}</b></td>
                  <td>{g.Count()}</td>
                  <td>{Avg(g.ToList(), d => d.IbTodayFullRatio):F2}x</td>
                  <td>{Avg(g.ToList(), d => d.GapRatio):F2}x</td>
                  <td>{Avg(g.ToList(), d => d.OvernightRatio):F2}x</td>
                </tr>
                """);

        var detailRows = undefined.Select(r => $"""
            <tr>
              <td>{r.Date}</td>
              <td>{r.IbTodayFullRatio:F2}x</td>
              <td>{r.GapRatio:F2}x</td>
              <td>{r.OvernightRatio:F2}x</td>
              <td>{(r.CperiodInsideIb ? "in" : r.CperiodAboveIb ? "up" : "down")}</td>
              <td>{(r.OpenOutsideIbYest ? "yes" : "")}</td>
              <td>{(r.OneTimeFramingUp ? "up" : r.OneTimeFramingDown ? "down" : "")}</td>
              <td class="dim">{H(r.Reason)}</td>
            </tr>
            """);

        return $"""
            <section>
              <h2>3 - Dias Undefined <span class="sub">({undefined.Count} dias)</span></h2>
              <h3>Clusters de padrao</h3>
              <table>
                <tr><th>Padrao</th><th>Dias</th><th>IB avg</th><th>Gap avg</th><th>Overnight avg</th></tr>
                {string.Join("\n", clusters)}
              </table>
              <h3>Lista completa</h3>
              <table>
                <tr>
                  <th>Data</th><th>IB Full</th><th>Gap</th><th>Overnight</th>
                  <th>C-period</th><th>OpenOut</th><th>OTF</th><th>Reason</th>
                </tr>
                {string.Join("\n", detailRows)}
              </table>
            </section>
            """;
    }

    private static string BuildMonthlyByRegime(IReadOnlyList<DayRegime> regimes)
    {
        var monthly = regimes
            .GroupBy(r => r.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var list = g.ToList();
                var counts = Enum.GetValues<MarketRegime>()
                    .ToDictionary(m => m, m => list.Count(r => r.Regime == m));

                var cells = string.Join("", Enum.GetValues<MarketRegime>()
                    .Select(m =>
                    {
                        var count = counts.GetValueOrDefault(m);
                        var style = count > 0
                            ? $"background:{RegimeColor(m)};opacity:{0.3 + Math.Min(count, 20) * 0.035:F2}"
                            : "";
                        return $"<td style=\"{style}\">{(count > 0 ? count.ToString(CultureInfo.InvariantCulture) : "")}</td>";
                    }));

                return $"<tr><td><b>{g.Key}</b></td>{cells}<td>{list.Count}</td></tr>";
            });

        var headers = string.Join("", Enum.GetValues<MarketRegime>()
            .Select(m => $"<th style=\"color:{RegimeColor(m)}\">{m}</th>"));

        return $"""
            <section>
              <h2>4 - Distribuicao Mensal por Regime</h2>
              <table>
                <tr><th>Mes</th>{headers}<th>Total</th></tr>
                {string.Join("\n", monthly)}
              </table>
            </section>
            """;
    }

    private static string WrapHtml(string content, int totalDays) => $$"""
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head>
          <meta charset="utf-8">
          <title>TradingBrain - Regime Report</title>
          <style>
            body { font-family: system-ui, sans-serif; background: #0f1117; color: #e0e0e0;
                    margin: 0; padding: 24px; }
            h2   { color: #fff; border-bottom: 1px solid #333; padding-bottom: 6px; margin-top: 40px; }
            h3   { color: #aaa; font-size: 0.95em; margin-top: 20px; }
            .sub { font-size: 0.75em; color: #888; font-weight: normal; }
            table{ border-collapse: collapse; width: 100%; margin-top: 12px; font-size: 0.88em; }
            th   { background: #1e2030; color: #bbb; padding: 8px 12px; text-align: left;
                    border-bottom: 2px solid #333; white-space: nowrap; }
            td   { padding: 6px 12px; border-bottom: 1px solid #222; }
            tr:hover td { background: #1a1d2e; }
            .badge { padding: 2px 8px; border-radius: 4px; color: #fff; font-size: 0.85em; }
            .dim { color: #888; font-size: 0.85em; }
            .good{ color: #4caf50; font-weight: bold; }
            .ok  { color: #ff9800; }
            .bad { color: #f44336; }
            section { margin-bottom: 48px; }
          </style>
        </head>
        <body>
          <h1>TradingBrain - Regime Report</h1>
          <p style="color:#888">Dataset: {{totalDays}} dias classificados &nbsp;|&nbsp;
             Gerado: {{DateTime.Now:yyyy-MM-dd HH:mm}}</p>
          {{content}}
        </body>
        </html>
        """;

    private static double Avg(IList<DayRegime> list, Func<DayRegime, double> selector)
    {
        var values = list.Select(selector).Where(v => !double.IsNaN(v)).ToList();
        return values.Count == 0 ? 0 : values.Average();
    }

    private static double Pct(IList<DayRegime> list, Func<DayRegime, bool> predicate)
        => list.Count == 0 ? 0 : list.Count(predicate) * 100.0 / list.Count;

    private static string RegimeColor(MarketRegime regime) => regime switch
    {
        MarketRegime.Trend => "#4caf50",
        MarketRegime.Breakout => "#2196f3",
        MarketRegime.Range => "#ff9800",
        MarketRegime.HighVolatility => "#f44336",
        MarketRegime.NonTrend => "#9e9e9e",
        MarketRegime.Undefined => "#673ab7",
        _ => "#555"
    };

    private static string H(string value)
        => value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
}

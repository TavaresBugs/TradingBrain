using System.Globalization;
using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

public sealed partial class StrategyBacktester
{
    public static BacktestSummary Summarize(
        IReadOnlyList<StrategyBacktestRow> rows,
        ExecutionSettings? executionSettings = null)
    {
        var tradeResults = ExtractTrades(rows, executionSettings);
        var trades = tradeResults.Select(t => t.PnL).ToList();
        var netTrades = tradeResults.Select(t => t.NetPoints).ToList();
        var wins = trades.Count(p => p > 0);
        var losses = trades.Count(p => p < 0);
        var grossProfit = trades.Where(p => p > 0).Sum();
        var grossLoss = trades.Where(p => p < 0).Sum();
        var netGrossProfit = netTrades.Where(p => p > 0).Sum();
        var netGrossLoss = netTrades.Where(p => p < 0).Sum();
        var averageWin = wins == 0 ? 0 : grossProfit / wins;
        var averageLoss = losses == 0 ? 0 : grossLoss / losses;
        var winRate = trades.Count == 0 ? 0 : wins * 100.0 / trades.Count;
        var lossRate = trades.Count == 0 ? 0 : losses * 100.0 / trades.Count;
        var expectancy = trades.Count == 0 ? 0 : (winRate / 100.0 * averageWin) + (lossRate / 100.0 * averageLoss);
        var netWins = netTrades.Count(p => p > 0);
        var netLosses = netTrades.Count(p => p < 0);
        var netWinRate = netTrades.Count == 0 ? 0 : netWins * 100.0 / netTrades.Count;
        var netLossRate = netTrades.Count == 0 ? 0 : netLosses * 100.0 / netTrades.Count;
        var averageNetWin = netWins == 0 ? 0 : netGrossProfit / netWins;
        var averageNetLoss = netLosses == 0 ? 0 : netGrossLoss / netLosses;
        var netExpectancy = netTrades.Count == 0 ? 0 : (netWinRate / 100.0 * averageNetWin) + (netLossRate / 100.0 * averageNetLoss);
        var profitFactor = grossLoss == 0 ? (grossProfit > 0 ? double.PositiveInfinity : 0) : grossProfit / Math.Abs(grossLoss);
        var netProfitFactor = netGrossLoss == 0 ? (netGrossProfit > 0 ? double.PositiveInfinity : 0) : netGrossProfit / Math.Abs(netGrossLoss);
        var payoffRatio = averageLoss == 0 ? (averageWin > 0 ? double.PositiveInfinity : 0) : averageWin / Math.Abs(averageLoss);
        var grossPnl = trades.Sum();
        var netPnl = netTrades.Sum();
        var totalCosts = tradeResults.Sum(t => t.TotalCostCurrency);
        var grossCurrency = tradeResults.Sum(t => t.GrossCurrency);
        var netCurrency = tradeResults.Sum(t => t.NetCurrency);
        var maxDrawdown = rows.Count == 0 ? 0 : rows.Max(r => r.Drawdown);

        return new BacktestSummary(
            rows.FirstOrDefault()?.StrategyName ?? "",
            rows.Count,
            rows.Count(r => r.Signal != SignalAction.None),
            trades.Count,
            wins,
            losses,
            winRate,
            grossProfit,
            grossLoss,
            profitFactor,
            averageWin,
            averageLoss,
            payoffRatio,
            expectancy,
            StandardDeviation(trades),
            grossPnl,
            totalCosts,
            netPnl,
            netProfitFactor,
            netExpectancy,
            grossCurrency,
            netCurrency,
            maxDrawdown,
            maxDrawdown == 0 ? 0 : netPnl / maxDrawdown);
    }

    public static IReadOnlyList<TradeResult> ExtractTrades(
        IReadOnlyList<StrategyBacktestRow> rows,
        ExecutionSettings? executionSettings = null)
    {
        var settings = executionSettings ?? ExecutionSettings.MnqDefault;
        var trades = new List<TradeResult>();
        StrategyBacktestRow? entry = null;
        var tradeRows = new List<StrategyBacktestRow>();

        foreach (var row in rows)
        {
            if ((row.Signal == SignalAction.Buy || row.Signal == SignalAction.Sell) && entry == null)
            {
                entry = row;
                tradeRows.Clear();
                tradeRows.Add(row);
                continue;
            }

            if (entry != null)
            {
                tradeRows.Add(row);
            }

            if (row.Signal == SignalAction.Exit && entry != null)
            {
                var direction = entry.Signal == SignalAction.Buy ? 1 : -1;
                var grossPoints = (row.Bar.Close - entry.Bar.Close) * direction;
                var grossCurrency = settings.PointsToCurrency(grossPoints);
                var slippageCostCurrency = settings.SlippageCostCurrency;
                var spreadCostCurrency = settings.SpreadCostCurrency;
                var commissionCostCurrency = settings.CommissionCostCurrency;
                var totalCostCurrency = settings.TotalRoundTripCostCurrency;
                var netCurrency = grossCurrency - totalCostCurrency;
                var netPoints = settings.CurrencyToPoints(netCurrency);
                var excursions = tradeRows
                    .Select(r => (r.Bar.Close - entry.Bar.Close) * direction)
                    .ToList();

                trades.Add(new TradeResult(
                    entry.StrategyName,
                    direction > 0 ? "Long" : "Short",
                    entry.Bar.Time,
                    row.Bar.Time,
                    entry.Bar.Close,
                    row.Bar.Close,
                    Math.Max(0, tradeRows.Count - 1),
                    grossPoints,
                    grossPoints,
                    netPoints,
                    grossCurrency,
                    netCurrency,
                    totalCostCurrency,
                    slippageCostCurrency,
                    spreadCostCurrency,
                    commissionCostCurrency,
                    settings.Quantity,
                    excursions.Count == 0 ? 0 : excursions.Max(),
                    excursions.Count == 0 ? 0 : excursions.Min(),
                    entry.Reason,
                    row.Reason));

                entry = null;
                tradeRows.Clear();
            }
        }

        return trades;
    }

    public static void ExportCsv(IReadOnlyList<StrategyBacktestRow> rows, string path)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("Strategy,Time,Open,High,Low,Close,Volume,Signal,Reason,Position,EntryPrice,OpenProfit,RealizedProfit,Equity,Drawdown,EMA9,EMA21,RSI,VWAP,ATR,ATRSMA,VolumeSMA,MACD,MACDSignal,RangeFilter,Trend");

        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(",",
                row.StrategyName,
                row.Bar.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                F(row.Bar.Open),
                F(row.Bar.High),
                F(row.Bar.Low),
                F(row.Bar.Close),
                row.Bar.Volume.ToString(CultureInfo.InvariantCulture),
                row.Signal,
                Escape(row.Reason),
                row.Position.ToString(CultureInfo.InvariantCulture),
                F(row.EntryPrice),
                F(row.OpenProfit),
                F(row.RealizedProfit),
                F(row.Equity),
                F(row.Drawdown),
                F(row.Metrics["EMA9"]),
                F(row.Metrics["EMA21"]),
                F(row.Metrics["RSI"]),
                F(row.Metrics["VWAP"]),
                F(row.Metrics["ATR"]),
                F(row.Metrics["ATRSMA"]),
                F(row.Metrics["VolumeSMA"]),
                F(row.Metrics["MACD"]),
                F(row.Metrics["MACDSignal"]),
                F(row.Metrics["RangeFilter"]),
                F(row.Metrics["Trend"])));
        }
    }

    public static void ExportTradesCsv(IReadOnlyList<TradeResult> trades, string path)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("Strategy,Direction,EntryTime,ExitTime,EntryPrice,ExitPrice,BarsHeld,PnL,GrossPoints,NetPoints,GrossCurrency,NetCurrency,TotalCostCurrency,SlippageCostCurrency,SpreadCostCurrency,CommissionCostCurrency,Quantity,MFE,MAE,EntryReason,ExitReason");

        foreach (var trade in trades)
        {
            writer.WriteLine(string.Join(",",
                trade.StrategyName,
                trade.Direction,
                trade.EntryTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                trade.ExitTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                F(trade.EntryPrice),
                F(trade.ExitPrice),
                trade.BarsHeld.ToString(CultureInfo.InvariantCulture),
                F(trade.PnL),
                F(trade.GrossPoints),
                F(trade.NetPoints),
                F(trade.GrossCurrency),
                F(trade.NetCurrency),
                F(trade.TotalCostCurrency),
                F(trade.SlippageCostCurrency),
                F(trade.SpreadCostCurrency),
                F(trade.CommissionCostCurrency),
                trade.Quantity.ToString(CultureInfo.InvariantCulture),
                F(trade.MaxFavorableExcursion),
                F(trade.MaxAdverseExcursion),
                Escape(trade.EntryReason),
                Escape(trade.ExitReason)));
        }
    }

    public static void ExportSummaryCsv(IReadOnlyList<BacktestSummary> summaries, string path)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("Strategy,Bars,Signals,ClosedTrades,Wins,Losses,WinRate,GrossProfit,GrossLoss,ProfitFactor,AverageWin,AverageLoss,PayoffRatio,Expectancy,TradeStdDev,GrossPnL,TotalCosts,NetPnL,NetProfitFactor,NetExpectancy,GrossCurrency,NetCurrency,MaxDrawdown,ReturnToDrawdown");

        foreach (var summary in summaries)
        {
            writer.WriteLine(string.Join(",",
                summary.StrategyName,
                summary.Bars.ToString(CultureInfo.InvariantCulture),
                summary.Signals.ToString(CultureInfo.InvariantCulture),
                summary.ClosedTrades.ToString(CultureInfo.InvariantCulture),
                summary.Wins.ToString(CultureInfo.InvariantCulture),
                summary.Losses.ToString(CultureInfo.InvariantCulture),
                F(summary.WinRate),
                F(summary.GrossProfit),
                F(summary.GrossLoss),
                F(summary.ProfitFactor),
                F(summary.AverageWin),
                F(summary.AverageLoss),
                F(summary.PayoffRatio),
                F(summary.Expectancy),
                F(summary.TradeStdDev),
                F(summary.GrossPnL),
                F(summary.TotalCosts),
                F(summary.NetPnL),
                F(summary.NetProfitFactor),
                F(summary.NetExpectancy),
                F(summary.GrossCurrency),
                F(summary.NetCurrency),
                F(summary.MaxDrawdown),
                F(summary.ReturnToDrawdown)));
        }
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

        return double.IsNaN(value)
            ? ""
            : value.ToString("0.########", CultureInfo.InvariantCulture);
    }

    private static string Escape(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private static double StandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count <= 1)
        {
            return 0;
        }

        var average = values.Average();
        var variance = values.Sum(v => Math.Pow(v - average, 2)) / (values.Count - 1);
        return Math.Sqrt(variance);
    }
}

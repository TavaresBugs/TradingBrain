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
        var entryBarIndex = -1;
        var tradeRows = new List<StrategyBacktestRow>();

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];

            if ((row.Signal == SignalAction.Buy || row.Signal == SignalAction.Sell) && entry == null)
            {
                entry = row;
                entryBarIndex = rowIndex;
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
                var exitPrice = IsBreakevenExit(row.Reason) ? entry.Bar.Close : row.Bar.Close;
                var grossPoints = (exitPrice - entry.Bar.Close) * direction;
                var grossCurrency = settings.PointsToCurrency(grossPoints);
                var slippageCostCurrency = settings.SlippageCostCurrency;
                var spreadCostCurrency = settings.SpreadCostCurrency;
                var commissionCostCurrency = settings.CommissionCostCurrency;
                var totalCostCurrency = settings.TotalRoundTripCostCurrency;
                var netCurrency = grossCurrency - totalCostCurrency;
                var netPoints = settings.CurrencyToPoints(netCurrency);
                var excursionRows = tradeRows.Count > 1
                    ? tradeRows.Skip(1).ToList()
                    : tradeRows;
                var high = excursionRows.Max(r => r.Bar.High);
                var low = excursionRows.Min(r => r.Bar.Low);
                var mfePoints = direction > 0
                    ? Math.Max(0, high - entry.Bar.Close)
                    : Math.Max(0, entry.Bar.Close - low);
                var maePoints = direction > 0
                    ? Math.Max(0, entry.Bar.Close - low)
                    : Math.Max(0, high - entry.Bar.Close);
                var bestFavorablePrice = direction > 0 ? high : low;
                var worstAdversePrice = direction > 0 ? low : high;
                var stopPrice = entry.StopPrice;
                var targetPrice = entry.TargetPrice;
                var riskPoints = StopIsValid(entry.Bar.Close, stopPrice)
                    ? Math.Abs(entry.Bar.Close - stopPrice)
                    : double.NaN;
                var rMultiple = RiskIsValid(riskPoints) ? grossPoints / riskPoints : double.NaN;
                var maeR = RiskIsValid(riskPoints) ? maePoints / riskPoints : double.NaN;
                var mfeR = RiskIsValid(riskPoints) ? mfePoints / riskPoints : double.NaN;
                var barsToHalfR = BarsToThreshold(tradeRows, entry.Bar.Close, direction, riskPoints, 0.5, favorable: true);
                var barsToOneR = BarsToThreshold(tradeRows, entry.Bar.Close, direction, riskPoints, 1.0, favorable: true);
                var barsToOneAndHalfR = BarsToThreshold(tradeRows, entry.Bar.Close, direction, riskPoints, 1.5, favorable: true);
                var barsToTwoR = BarsToThreshold(tradeRows, entry.Bar.Close, direction, riskPoints, 2.0, favorable: true);
                var barsToThreeR = BarsToThreshold(tradeRows, entry.Bar.Close, direction, riskPoints, 3.0, favorable: true);
                var barsToMinusHalfR = BarsToThreshold(tradeRows, entry.Bar.Close, direction, riskPoints, 0.5, favorable: false);
                var barsToMinusOneR = BarsToThreshold(tradeRows, entry.Bar.Close, direction, riskPoints, 1.0, favorable: false);

                trades.Add(new TradeResult(
                    entry.StrategyName,
                    direction > 0 ? "Long" : "Short",
                    entry.Bar.Time,
                    row.Bar.Time,
                    entryBarIndex,
                    rowIndex,
                    entry.Bar.Close,
                    exitPrice,
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
                    mfePoints,
                    -maePoints,
                    entry.Reason,
                    row.Reason,
                    stopPrice,
                    targetPrice,
                    rMultiple,
                    riskPoints,
                    maePoints,
                    mfePoints,
                    maeR,
                    mfeR,
                    bestFavorablePrice,
                    worstAdversePrice,
                    barsToHalfR is not null,
                    barsToOneR is not null,
                    barsToOneAndHalfR is not null,
                    barsToTwoR is not null,
                    barsToThreeR is not null,
                    barsToMinusHalfR is not null,
                    barsToMinusOneR is not null,
                    barsToHalfR,
                    barsToOneR,
                    barsToOneAndHalfR,
                    barsToTwoR,
                    barsToThreeR,
                    barsToMinusHalfR,
                    barsToMinusOneR));

                entry = null;
                entryBarIndex = -1;
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
        writer.WriteLine("Strategy,Direction,EntryTime,ExitTime,EntryBarIndex,ExitBarIndex,EntryPrice,ExitPrice,BarsHeld,PnL,GrossPoints,NetPoints,GrossCurrency,NetCurrency,TotalCostCurrency,SlippageCostCurrency,SpreadCostCurrency,CommissionCostCurrency,Quantity,MFE,MAE,EntryReason,ExitReason,StopPrice,TargetPrice,RMultiple,RiskPoints,MAEPoints,MFEPoints,MAER,MFER,BestFavorablePrice,WorstAdversePrice,HitHalfR,HitOneR,HitOneAndHalfR,HitTwoR,HitThreeR,HitMinusHalfR,HitMinusOneR,BarsToHalfR,BarsToOneR,BarsToOneAndHalfR,BarsToTwoR,BarsToThreeR,BarsToMinusHalfR,BarsToMinusOneR");

        foreach (var trade in trades)
        {
            writer.WriteLine(string.Join(",",
                trade.StrategyName,
                trade.Direction,
                trade.EntryTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                trade.ExitTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                trade.EntryBarIndex.ToString(CultureInfo.InvariantCulture),
                trade.ExitBarIndex.ToString(CultureInfo.InvariantCulture),
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
                Escape(trade.ExitReason),
                F(trade.StopPrice),
                F(trade.TargetPrice),
                F(trade.RMultiple),
                F(trade.RiskPoints),
                F(trade.MAEPoints),
                F(trade.MFEPoints),
                F(trade.MAER),
                F(trade.MFER),
                F(trade.BestFavorablePrice),
                F(trade.WorstAdversePrice),
                B(trade.HitHalfR),
                B(trade.HitOneR),
                B(trade.HitOneAndHalfR),
                B(trade.HitTwoR),
                B(trade.HitThreeR),
                B(trade.HitMinusHalfR),
                B(trade.HitMinusOneR),
                I(trade.BarsToHalfR),
                I(trade.BarsToOneR),
                I(trade.BarsToOneAndHalfR),
                I(trade.BarsToTwoR),
                I(trade.BarsToThreeR),
                I(trade.BarsToMinusHalfR),
                I(trade.BarsToMinusOneR)));
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

    private static int? BarsToThreshold(
        IReadOnlyList<StrategyBacktestRow> tradeRows,
        double entryPrice,
        int direction,
        double riskPoints,
        double thresholdR,
        bool favorable)
    {
        if (!RiskIsValid(riskPoints))
        {
            return null;
        }

        var thresholdPoints = riskPoints * thresholdR;
        foreach (var (tradeRow, offset) in tradeRows.Select((tradeRow, offset) => (tradeRow, offset)))
        {
            if (offset == 0)
            {
                continue;
            }

            var bar = tradeRow.Bar;
            var excursion = favorable
                ? FavorableExcursionPoints(bar, entryPrice, direction)
                : AdverseExcursionPoints(bar, entryPrice, direction);
            if (excursion >= thresholdPoints)
            {
                return offset;
            }
        }

        return null;
    }

    private static double FavorableExcursionPoints(MarketBar bar, double entryPrice, int direction)
        => direction > 0
            ? Math.Max(0, bar.High - entryPrice)
            : Math.Max(0, entryPrice - bar.Low);

    private static double AdverseExcursionPoints(MarketBar bar, double entryPrice, int direction)
        => direction > 0
            ? Math.Max(0, entryPrice - bar.Low)
            : Math.Max(0, bar.High - entryPrice);

    private static bool StopIsValid(double entryPrice, double stopPrice)
        => double.IsFinite(stopPrice) &&
           double.IsFinite(entryPrice) &&
           stopPrice > 0 &&
           Math.Abs(entryPrice - stopPrice) > 0;

    private static bool RiskIsValid(double riskPoints)
        => double.IsFinite(riskPoints) && riskPoints > 0;

    private static bool IsBreakevenExit(string reason)
        => reason.Contains("Stop BE", StringComparison.OrdinalIgnoreCase);

    private static string B(bool value) => value ? "true" : "false";

    private static string I(int? value)
        => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "";

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

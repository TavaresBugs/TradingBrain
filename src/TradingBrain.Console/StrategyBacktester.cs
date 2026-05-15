using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

public sealed partial class StrategyBacktester
{
    private readonly StrategyKind _strategy;
    private readonly StrategyDefaults _defaults;
    private readonly StrategyTuningParams _params;

    public StrategyBacktester(StrategyKind strategy, StrategyTuningParams? tuningParams = null)
    {
        _strategy = strategy;
        _defaults = StrategyDefaults.For(strategy);
        _params = tuningParams ?? StrategyTuningParams.RefinedDefault;
    }

    public IReadOnlyList<StrategyBacktestRow> Run(IReadOnlyList<MarketBar> bars)
    {
        var rows = new List<StrategyBacktestRow>(bars.Count);
        var closes = new List<double>(bars.Count);
        var highs = new List<double>(bars.Count);
        var lows = new List<double>(bars.Count);
        var history = new List<MarketBar>(bars.Count);
        var sessionHistory = new List<MarketBar>();
        var atrValues = new List<double>();
        var macdValues = new List<double>();
        var position = 0;
        var entryPrice = 0.0;
        var entryBarIndex = -1;
        var realizedProfit = 0.0;
        var peakEquity = 0.0;
        var trendState = 0;
        var rangeState = 0;
        var currentDate = DateTime.MinValue;

        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            closes.Add(bar.Close);
            highs.Add(bar.High);
            lows.Add(bar.Low);
            history.Add(bar);

            if (bar.Time.Date != currentDate)
            {
                currentDate = bar.Time.Date;
                sessionHistory.Clear();
            }
            sessionHistory.Add(bar);

            var metrics = BuildMetrics(history, sessionHistory, closes, highs, lows, atrValues, macdValues);

            if (!double.IsNaN(metrics["ATR"]))
            {
                atrValues.Add(metrics["ATR"]);
                metrics = BuildMetrics(history, sessionHistory, closes, highs, lows, atrValues, macdValues);
            }

            if (!double.IsNaN(metrics["MACD"]))
            {
                macdValues.Add(metrics["MACD"]);
                metrics = BuildMetrics(history, sessionHistory, closes, highs, lows, atrValues, macdValues);
            }

            var openProfit = position == 0 ? 0 : (bar.Close - entryPrice) * position;
            var barsSinceEntry = entryBarIndex < 0 ? 0 : i - entryBarIndex;
            var decision = IndicatorsReady(metrics)
                ? Evaluate(bar, history, metrics, position, entryPrice, openProfit, barsSinceEntry, ref trendState, ref rangeState)
                : new StrategyDecision(SignalAction.None, "Aquecendo indicadores");

            var signal = decision.Action;
            if (signal == SignalAction.Buy && position == 0)
            {
                position = 1;
                entryPrice = bar.Close;
                entryBarIndex = i;
            }
            else if (signal == SignalAction.Sell && position == 0)
            {
                position = -1;
                entryPrice = bar.Close;
                entryBarIndex = i;
            }
            else if (signal == SignalAction.Exit && position != 0)
            {
                realizedProfit += openProfit;
                position = 0;
                entryPrice = 0;
                entryBarIndex = -1;
                openProfit = 0;
            }

            var equity = realizedProfit + openProfit;
            peakEquity = Math.Max(peakEquity, equity);
            var drawdown = peakEquity - equity;

            rows.Add(new StrategyBacktestRow(
                StrategyName(_strategy),
                bar,
                signal,
                decision.Reason,
                position,
                entryPrice,
                openProfit,
                realizedProfit,
                equity,
                drawdown,
                metrics));
        }

        return rows;
    }
}

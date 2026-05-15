using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

public sealed partial class StrategyBacktester
{
    private readonly StrategyKind _strategy;
    private readonly StrategyDefaults _defaults;
    private readonly StrategyTuningParams _params;
    private readonly PrecomputedSeries? _series;
    private IReadOnlyList<MarketBar>? _resampledBars;

    public StrategyBacktester(
        StrategyKind strategy,
        StrategyTuningParams? tuningParams = null,
        PrecomputedSeries? precomputed = null)
    {
        _strategy = strategy;
        _defaults = StrategyDefaults.For(strategy);
        _params = tuningParams ?? StrategyTuningParams.RefinedDefault;
        _series = precomputed;
    }

    public IReadOnlyList<StrategyBacktestRow> Run(IReadOnlyList<MarketBar> bars)
    {
        var rows = new List<StrategyBacktestRow>(bars.Count);
        var series = _series ?? PrecomputedSeries.From(bars);
        _resampledBars = TechnicalIndicators.Resample(bars, targetMinutes: 15);
        var position = 0;
        var entryPrice = 0.0;
        var entryBarIndex = -1;
        var realizedProfit = 0.0;
        var peakEquity = 0.0;
        var trendState = 0;
        var rangeState = 0;
        var schoolRunState = 0;

        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];

            if (i == 0 || bars[i - 1].Time.Date != bar.Time.Date)
                schoolRunState = 0;

            var metrics = BuildMetrics(series, i);

            var openProfit = position == 0 ? 0 : (bar.Close - entryPrice) * position;
            var barsSinceEntry = entryBarIndex < 0 ? 0 : i - entryBarIndex;
            var decision = IndicatorsReady(metrics)
                ? Evaluate(bar, bars, i, metrics, position, entryPrice, openProfit, barsSinceEntry, ref trendState, ref rangeState, ref schoolRunState)
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

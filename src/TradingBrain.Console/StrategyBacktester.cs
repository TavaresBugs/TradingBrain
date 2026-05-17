using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

public sealed partial class StrategyBacktester
{
    private readonly StrategyKind _strategy;
    private readonly StrategyDefaults _defaults;
    private readonly StrategyTuningParams _params;
    private readonly PrecomputedSeries? _series;
    private IReadOnlyList<MarketBar>? _resampledBars;
    private double _orbWindowHigh = double.NaN;
    private double _orbWindowLow = double.NaN;
    private DateOnly _lastTrendDate;
    private DateOnly _lastRangeDate;
    private DateOnly _srsContextDate = DateOnly.MinValue;
    private MarketBar? _srsContextRefCandle;
    private double _srsContextOvernightHigh = double.NaN;
    private double _srsContextOvernightLow = double.NaN;

    public StrategyBacktester(
        StrategyKind strategy,
        StrategyTuningParams? tuningParams = null,
        PrecomputedSeries? precomputed = null)
    {
        _strategy = strategy;
        _defaults = StrategyDefaults.For(strategy);
        _params = tuningParams ?? DefaultTuningParamsFor(strategy);
        _series = precomputed;
    }

    public IReadOnlyList<StrategyBacktestRow> Run(IReadOnlyList<MarketBar> bars)
    {
        var rows = new List<StrategyBacktestRow>(bars.Count);
        var series = _series ?? PrecomputedSeries.From(bars);
        _resampledBars = TechnicalIndicators.Resample(bars, targetMinutes: 15);
        _srsContextDate = DateOnly.MinValue;
        _srsContextRefCandle = null;
        _srsContextOvernightHigh = double.NaN;
        _srsContextOvernightLow = double.NaN;
        var position = 0;
        var entryPrice = 0.0;
        var entryBarIndex = -1;
        var entryStopPrice = 0.0;
        var entryTargetPrice = 0.0;
        var realizedProfit = 0.0;
        var peakEquity = 0.0;
        var trendState = 0;
        var rangeState = 0;
        var schoolRunState = 0;
        var orbState = 0;
        var ibState = 0;
        var initialRiskPoints = double.NaN;
        var beActivated = false;
        var extremeFavorable = double.NaN;

        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];

            if (i == 0 || bars[i - 1].Time.Date != bar.Time.Date)
            {
                schoolRunState = 0;
                orbState = 0;
                ibState = 0;
                _orbWindowHigh = double.NaN;
                _orbWindowLow = double.NaN;
            }

            var metrics = BuildMetrics(series, i);

            var openProfit = position == 0 ? 0 : (bar.Close - entryPrice) * position;
            var barsSinceEntry = entryBarIndex < 0 ? 0 : i - entryBarIndex;
            var decision = IndicatorsReady(metrics)
                ? Evaluate(
                    bar,
                    bars,
                    i,
                    metrics,
                    position,
                    entryPrice,
                    openProfit,
                    barsSinceEntry,
                    ref trendState,
                    ref rangeState,
                    ref schoolRunState,
                    ref orbState,
                    ref ibState,
                    ref initialRiskPoints,
                    ref beActivated,
                    ref extremeFavorable)
                : new StrategyDecision(SignalAction.None, "Aquecendo indicadores");

            var signal = decision.Action;
            var stopPrice = 0.0;
            var targetPrice = 0.0;
            if (signal == SignalAction.Buy && position == 0)
            {
                stopPrice = ComputeStopPrice(bar, bars, i, metrics, SignalAction.Buy);
                targetPrice = ComputeTargetPrice(bar, metrics, SignalAction.Buy);
                entryStopPrice = stopPrice;
                entryTargetPrice = targetPrice;
                position = 1;
                entryPrice = bar.Close;
                entryBarIndex = i;
            }
            else if (signal == SignalAction.Sell && position == 0)
            {
                stopPrice = ComputeStopPrice(bar, bars, i, metrics, SignalAction.Sell);
                targetPrice = ComputeTargetPrice(bar, metrics, SignalAction.Sell);
                entryStopPrice = stopPrice;
                entryTargetPrice = targetPrice;
                position = -1;
                entryPrice = bar.Close;
                entryBarIndex = i;
            }
            else if (signal == SignalAction.Exit && position != 0)
            {
                realizedProfit += IsBreakevenExit(decision.Reason) ? 0 : openProfit;
                position = 0;
                entryPrice = 0;
                entryBarIndex = -1;
                entryStopPrice = 0;
                entryTargetPrice = 0;
                initialRiskPoints = double.NaN;
                beActivated = false;
                extremeFavorable = double.NaN;
                openProfit = 0;
            }
            else if (position != 0)
            {
                stopPrice = entryStopPrice;
                targetPrice = entryTargetPrice;
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
                metrics,
                stopPrice,
                targetPrice));
        }

        return rows;
    }

    private static StrategyTuningParams DefaultTuningParamsFor(StrategyKind strategy)
        => strategy switch
        {
            StrategyKind.Trend => StrategyTuningParams.RefinedDefault with
            {
                BeActivationRMultiple = 1.0,
                ChandelierActivationRMultiple = 0.75,
                ChandelierTrailMultiplier = 2.0
            },
            StrategyKind.Momentum => StrategyTuningParams.RefinedDefault with
            {
                BeActivationRMultiple = 0.75,
                ChandelierActivationRMultiple = 1.25,
                ChandelierTrailMultiplier = 2.0
            },
            StrategyKind.Ema => StrategyTuningParams.RefinedDefault with
            {
                BeActivationRMultiple = 0.5,
                ChandelierActivationRMultiple = 0.0
            },
            _ => StrategyTuningParams.RefinedDefault
        };
}

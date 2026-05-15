namespace TradingBrain.Core;

public sealed class IndicatorState
{
    private readonly Queue<double> _shortWindow = new();
    private readonly Queue<double> _longWindow = new();
    private readonly int _shortPeriod;
    private readonly int _longPeriod;
    private readonly int _rsiPeriod;
    private double _shortSum;
    private double _longSum;
    private double? _previousClose;
    private readonly Queue<double> _gains = new();
    private readonly Queue<double> _losses = new();
    private double _gainSum;
    private double _lossSum;

    public IndicatorState(int shortPeriod = 20, int longPeriod = 50, int rsiPeriod = 14)
    {
        if (shortPeriod <= 0) throw new ArgumentOutOfRangeException(nameof(shortPeriod));
        if (longPeriod <= 0) throw new ArgumentOutOfRangeException(nameof(longPeriod));
        if (rsiPeriod <= 0) throw new ArgumentOutOfRangeException(nameof(rsiPeriod));
        if (shortPeriod >= longPeriod) throw new ArgumentException("A media curta deve ser menor que a media longa.");

        _shortPeriod = shortPeriod;
        _longPeriod = longPeriod;
        _rsiPeriod = rsiPeriod;
    }

    public bool IsReady => _shortWindow.Count == _shortPeriod &&
                           _longWindow.Count == _longPeriod &&
                           _gains.Count == _rsiPeriod;

    public double ShortAverage { get; private set; }
    public double LongAverage { get; private set; }
    public double Rsi { get; private set; } = 50;

    public void Update(MarketBar bar)
    {
        AddToWindow(_shortWindow, ref _shortSum, bar.Close, _shortPeriod);
        AddToWindow(_longWindow, ref _longSum, bar.Close, _longPeriod);

        ShortAverage = _shortWindow.Count == 0 ? 0 : _shortSum / _shortWindow.Count;
        LongAverage = _longWindow.Count == 0 ? 0 : _longSum / _longWindow.Count;

        if (_previousClose is not null)
        {
            var change = bar.Close - _previousClose.Value;
            var gain = Math.Max(change, 0);
            var loss = Math.Max(-change, 0);

            AddToWindow(_gains, ref _gainSum, gain, _rsiPeriod);
            AddToWindow(_losses, ref _lossSum, loss, _rsiPeriod);

            if (_gains.Count == _rsiPeriod)
            {
                Rsi = _lossSum == 0
                    ? 100
                    : 100 - (100 / (1 + (_gainSum / _lossSum)));
            }
        }

        _previousClose = bar.Close;
    }

    private static void AddToWindow(Queue<double> window, ref double sum, double value, int period)
    {
        window.Enqueue(value);
        sum += value;

        if (window.Count > period)
        {
            sum -= window.Dequeue();
        }
    }
}

// Exemplo para colar/adaptar dentro do NinjaTrader.
// Este arquivo nao compila fora do NinjaTrader porque depende de Strategy, SMA, RSI e EnterLong.

using TradingBrain.Core;

public class NinjaBotIA : Strategy
{
    private readonly MotorDeDecisao _motor = new();

    protected override void OnBarUpdate()
    {
        if (CurrentBar < 50)
        {
            return;
        }

        var input = new DecisionInput(
            ShortAverage: SMA(20)[0],
            LongAverage: SMA(50)[0],
            Rsi: RSI(14, 3)[0],
            LastPrice: Close[0],
            IsLong: Position.MarketPosition == MarketPosition.Long,
            IsShort: Position.MarketPosition == MarketPosition.Short);

        var decision = _motor.Evaluate(input);

        switch (decision.Action)
        {
            case SignalAction.Buy:
                EnterLong();
                break;
            case SignalAction.Sell:
                EnterShort();
                break;
            case SignalAction.Exit:
                ExitLong();
                ExitShort();
                break;
        }
    }
}

namespace TradingBrain.Core;

public sealed class MotorDeDecisao
{
    public DecisionResult Evaluate(DecisionInput input)
    {
        if (!input.IsLong && !input.IsShort &&
            input.ShortAverage > input.LongAverage &&
            input.Rsi < 30)
        {
            return new DecisionResult(
                SignalAction.Buy,
                "Media curta acima da longa e RSI abaixo de 30",
                input.ShortAverage,
                input.LongAverage,
                input.Rsi);
        }

        if (input.IsLong && input.Rsi > 70)
        {
            return new DecisionResult(
                SignalAction.Exit,
                "Saida de compra: RSI acima de 70",
                input.ShortAverage,
                input.LongAverage,
                input.Rsi);
        }

        if (!input.IsLong && !input.IsShort &&
            input.ShortAverage < input.LongAverage &&
            input.Rsi > 70)
        {
            return new DecisionResult(
                SignalAction.Sell,
                "Media curta abaixo da longa e RSI acima de 70",
                input.ShortAverage,
                input.LongAverage,
                input.Rsi);
        }

        if (input.IsShort && input.Rsi < 30)
        {
            return new DecisionResult(
                SignalAction.Exit,
                "Saida de venda: RSI abaixo de 30",
                input.ShortAverage,
                input.LongAverage,
                input.Rsi);
        }

        return new DecisionResult(
            SignalAction.None,
            "Sem sinal",
            input.ShortAverage,
            input.LongAverage,
            input.Rsi);
    }
}

namespace TradingBrain.Core;

public sealed class VolatilityDecisionEngine
{
    private readonly VolatilityDecisionConfig _config;

    public VolatilityDecisionEngine(VolatilityDecisionConfig config)
    {
        _config = config;
    }

    public DecisionResult Evaluate(VolatilityDecisionInput input)
    {
        if (!IsInsideSession(input.Time))
        {
            if (input.IsLong || input.IsShort)
            {
                return Result(SignalAction.Exit, "Fora da janela operacional", input);
            }

            return Result(SignalAction.None, "Fora da janela operacional", input);
        }

        if ((input.IsLong || input.IsShort) && input.OpenProfit <= -_config.MaxDrawdown)
        {
            return Result(SignalAction.Exit, "MaxDrawdown atingido", input);
        }

        if ((input.IsLong || input.IsShort) && input.BarsSinceEntry >= _config.TimeExitBars)
        {
            return Result(SignalAction.Exit, "Saida por tempo maximo em barras", input);
        }

        var volatilityOk = input.Atr > input.AtrSma && input.Volume > input.VolumeSma * _config.VolumeThreshold;
        var longVwapOk = !_config.UseVwapFilter || input.Close > input.Vwap;
        var shortVwapOk = !_config.UseVwapFilter || input.Close < input.Vwap;
        var longTrend = input.EmaFast > input.EmaSlow && longVwapOk && input.Rsi >= 50;
        var shortTrend = input.EmaFast < input.EmaSlow && shortVwapOk && input.Rsi <= 50;

        if (!input.IsLong && !input.IsShort && volatilityOk && longTrend)
        {
            return Result(SignalAction.Buy, "Volatilidade ok, EMA rapida acima, preco acima do VWAP e RSI comprador", input);
        }

        if (!input.IsLong && !input.IsShort && volatilityOk && shortTrend)
        {
            return Result(SignalAction.Sell, "Volatilidade ok, EMA rapida abaixo, preco abaixo do VWAP e RSI vendedor", input);
        }

        if (input.IsLong && (input.EmaFast < input.EmaSlow || (_config.UseVwapFilter && input.Close < input.Vwap)))
        {
            return Result(SignalAction.Exit, "Saida long por perda de tendencia", input);
        }

        if (input.IsShort && (input.EmaFast > input.EmaSlow || (_config.UseVwapFilter && input.Close > input.Vwap)))
        {
            return Result(SignalAction.Exit, "Saida short por perda de tendencia", input);
        }

        return Result(SignalAction.None, "Sem sinal", input);
    }

    private bool IsInsideSession(DateTime time)
    {
        var value = time.Hour * 10000 + time.Minute * 100 + time.Second;
        return value >= _config.SessionStartHHmmss && value <= _config.SessionEndHHmmss;
    }

    private static DecisionResult Result(SignalAction action, string reason, VolatilityDecisionInput input) =>
        new(action, reason, input.EmaFast, input.EmaSlow, input.Rsi);
}

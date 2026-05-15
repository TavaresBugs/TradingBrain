using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

public sealed partial class StrategyBacktester
{
    private StrategyDecision Evaluate(
        MarketBar bar,
        IReadOnlyList<MarketBar> history,
        IReadOnlyDictionary<string, double> m,
        int position,
        double entryPrice,
        double openProfit,
        int barsSinceEntry,
        ref int trendState,
        ref int rangeState)
    {
        if (!IsInsideSession(bar.Time) && position == 0)
        {
            return new StrategyDecision(SignalAction.None, "Fora da sessao");
        }

        return _strategy switch
        {
            StrategyKind.Volatility => EvaluateVolatility(bar, m, position, entryPrice, openProfit, barsSinceEntry),
            StrategyKind.Trend => EvaluateTrend(bar, m, position, entryPrice, barsSinceEntry, ref trendState),
            StrategyKind.Range => EvaluateRange(bar, m, position, entryPrice, barsSinceEntry, ref rangeState),
            StrategyKind.Momentum => EvaluateMomentum(bar, m, position, entryPrice, barsSinceEntry),
            StrategyKind.OrbBreakout => EvaluateOrbBreakout(bar, history, m, position, entryPrice, barsSinceEntry),
            StrategyKind.Ema => EvaluateEma(bar, m, position, entryPrice, barsSinceEntry),
            StrategyKind.VwapReversion => EvaluateVwapReversion(bar, m, position, entryPrice, barsSinceEntry),
            StrategyKind.BollingerFade => EvaluateBollingerFade(bar, m, position, entryPrice, barsSinceEntry),
            StrategyKind.SessionBreakout => EvaluateSessionBreakout(bar, history, m, position, entryPrice, barsSinceEntry),
            _ => new StrategyDecision(SignalAction.None, "Strategy nao implementada")
        };
    }

    private StrategyDecision EvaluateVolatility(
        MarketBar bar,
        IReadOnlyDictionary<string, double> m,
        int position,
        double entryPrice,
        double openProfit,
        int barsSinceEntry)
    {
        if (position != 0 && IsAtOrAfterCloseAll(bar.Time))
        {
            return new StrategyDecision(SignalAction.Exit, "Fora da janela operacional");
        }

        if (position == 0 && IsAtOrAfterCloseAll(bar.Time))
        {
            return new StrategyDecision(SignalAction.None, "Fora da janela operacional");
        }

        if (position != 0 && openProfit <= -_defaults.MaxDrawdownPoints)
        {
            return new StrategyDecision(SignalAction.Exit, "MaxDrawdown atingido");
        }

        var stopAtr = m["ATR"] * _params.AtrStopMultiplier;
        var trailingLong = Math.Max(m["VWAP"], m["EMA21"]);
        var trailingShort = Math.Min(m["VWAP"], m["EMA21"]);
        var atrTrailingLong = m["Highest3"] - m["ATR"] * _params.AtrChandelierMultiplier;
        var atrTrailingShort = m["Lowest3"] + m["ATR"] * _params.AtrChandelierMultiplier;

        if (position > 0)
        {
            if (bar.Close <= entryPrice - stopAtr)
                return new StrategyDecision(SignalAction.Exit, "Stop ATR long");
            if (barsSinceEntry > _params.TrailingActivationBars &&
                _params.VolatilityTrailingMode == VolatilityTrailingMode.VwapEmaChandelier &&
                bar.Close < trailingLong)
                return new StrategyDecision(SignalAction.Exit, "Chandelier long - fechamento abaixo do trailing");
            if (barsSinceEntry > _params.TrailingActivationBars &&
                _params.VolatilityTrailingMode == VolatilityTrailingMode.AtrChandelier &&
                bar.Close < atrTrailingLong)
                return new StrategyDecision(SignalAction.Exit, "ATR Chandelier long - fechamento abaixo do trailing");
            if (barsSinceEntry <= _params.TrailingActivationBars && m["RSI"] > 75)
                return new StrategyDecision(SignalAction.Exit, "RSI extremo long");
            if (barsSinceEntry >= _params.MaxBarsWithoutProfit && openProfit < m["ATR"] * _params.MinProfitAtrRatio)
                return new StrategyDecision(SignalAction.Exit, "Tempo maximo sem lucro minimo");
            return new StrategyDecision(SignalAction.None, "Em posicao long");
        }

        if (position < 0)
        {
            if (bar.Close >= entryPrice + stopAtr)
                return new StrategyDecision(SignalAction.Exit, "Stop ATR short");
            if (barsSinceEntry > _params.TrailingActivationBars &&
                _params.VolatilityTrailingMode == VolatilityTrailingMode.VwapEmaChandelier &&
                bar.Close > trailingShort)
                return new StrategyDecision(SignalAction.Exit, "Chandelier short - fechamento acima do trailing");
            if (barsSinceEntry > _params.TrailingActivationBars &&
                _params.VolatilityTrailingMode == VolatilityTrailingMode.AtrChandelier &&
                bar.Close > atrTrailingShort)
                return new StrategyDecision(SignalAction.Exit, "ATR Chandelier short - fechamento acima do trailing");
            if (barsSinceEntry <= _params.TrailingActivationBars && m["RSI"] < 25)
                return new StrategyDecision(SignalAction.Exit, "RSI extremo short");
            if (barsSinceEntry >= _params.MaxBarsWithoutProfit && openProfit < m["ATR"] * _params.MinProfitAtrRatio)
                return new StrategyDecision(SignalAction.Exit, "Tempo maximo sem lucro minimo");
            return new StrategyDecision(SignalAction.None, "Em posicao short");
        }

        var squeeze = !_params.UseSqueezeFilter || m["ATRPrev"] <= m["ATRSMA"] * _params.VolatilitySqueezeRatio;
        var atrExpansionOk = m["ATR"] > m["ATRSMA"] * _params.VolatilityMinAtrRatio;
        var rangeExpansionOk = bar.High - bar.Low > m["CandleRangeSMA"] * _params.VolatilityRangeMultiplier;
        var expansionOk = _params.VolatilityExpansionMode switch
        {
            VolatilityExpansionMode.CandleRange => rangeExpansionOk,
            VolatilityExpansionMode.AtrAndCandleRange => atrExpansionOk && rangeExpansionOk,
            _ => atrExpansionOk
        };
        var volatilityOk = expansionOk &&
                           bar.Volume > m["VolumeSMA"] * _params.VolatilityMinVolumeRatio;
        var vwapLongOk = bar.Close > m["VWAP"] * (1 + _params.VwapMinDistance);
        var vwapShortOk = bar.Close < m["VWAP"] * (1 - _params.VwapMinDistance);
        var longTrend = m["EMA9"] > m["EMA21"] &&
                        vwapLongOk &&
                        m["RSI"] >= 50 &&
                        m["RSI"] <= _params.RsiLongMax;
        var shortTrend = m["EMA9"] < m["EMA21"] &&
                         vwapShortOk &&
                         m["RSI"] >= _params.RsiShortMin &&
                         m["RSI"] <= 50;

        if (volatilityOk && squeeze && longTrend)
            return new StrategyDecision(SignalAction.Buy, "Squeeze bullish com volume");
        if (volatilityOk && squeeze && shortTrend)
            return new StrategyDecision(SignalAction.Sell, "Squeeze bearish com volume");

        return new StrategyDecision(SignalAction.None, "Sem sinal");
    }

    private StrategyDecision EvaluateTrend(MarketBar bar, IReadOnlyDictionary<string, double> m, int position, double entryPrice, int barsSinceEntry, ref int trendState)
    {
        var mid = (m["Highest10"] + m["Lowest10"]) / 2.0;
        var upper = mid + m["ATR"] * _defaults.AtrMultiplier;
        var lower = mid - m["ATR"] * _defaults.AtrMultiplier;
        var newTrend = bar.Close > upper ? 1 : bar.Close < lower ? -1 : trendState;

        if (position > 0)
        {
            if (bar.Close <= entryPrice - m["ATR"] * _params.TrendAtrStopMultiplier)
                return new StrategyDecision(SignalAction.Exit, "Stop dinamico long");
            if (newTrend < 0)
                return new StrategyDecision(SignalAction.Exit, "Trend virou contra long");
            if (barsSinceEntry >= _defaults.TimeExitBars)
                return new StrategyDecision(SignalAction.Exit, "Tempo long");
            return new StrategyDecision(SignalAction.None, "Em tendencia long");
        }

        if (position < 0)
        {
            if (bar.Close >= entryPrice + m["ATR"] * _params.TrendAtrStopMultiplier)
                return new StrategyDecision(SignalAction.Exit, "Stop dinamico short");
            if (newTrend > 0)
                return new StrategyDecision(SignalAction.Exit, "Trend virou contra short");
            if (barsSinceEntry >= _defaults.TimeExitBars)
                return new StrategyDecision(SignalAction.Exit, "Tempo short");
            return new StrategyDecision(SignalAction.None, "Em tendencia short");
        }

        var changed = newTrend != 0 && newTrend != trendState;
        trendState = newTrend;

        if (changed && newTrend > 0 && bar.Close > upper + m["ATR"] * 0.2 && m["RSI"] > 50)
            return new StrategyDecision(SignalAction.Buy, "Trend up confirmado");
        if (changed && newTrend < 0 && bar.Close < lower - m["ATR"] * 0.2 && m["RSI"] < 50)
            return new StrategyDecision(SignalAction.Sell, "Trend down confirmado");

        return new StrategyDecision(SignalAction.None, "Sem sinal");
    }

    private StrategyDecision EvaluateRange(MarketBar bar, IReadOnlyDictionary<string, double> m, int position, double entryPrice, int barsSinceEntry, ref int rangeState)
    {
        var filter = m["RangeFilter"];
        var band = m["ATR"] * _defaults.AtrMultiplier;
        var newState = bar.Close > filter + band ? 1 : bar.Close < filter - band ? -1 : rangeState;
        var changed = newState != 0 && newState != rangeState;
        rangeState = newState;

        if (position != 0 && Math.Abs(bar.Close - entryPrice) >= m["ATR"] * _defaults.AtrMultiplierTp)
            return new StrategyDecision(SignalAction.Exit, "TP/SL ATR");
        if (position > 0 && bar.Close < filter)
            return new StrategyDecision(SignalAction.Exit, "Fim do rompimento long");
        if (position < 0 && bar.Close > filter)
            return new StrategyDecision(SignalAction.Exit, "Fim do rompimento short");
        if (position != 0 && barsSinceEntry >= _defaults.TimeExitBars)
            return new StrategyDecision(SignalAction.Exit, "Tempo");

        var compressionOk = m["ATR"] <= m["ATRSMA"] * _params.RangeCompressionRatio;
        if (position == 0 && changed && newState > 0 && compressionOk && bar.Close > filter + band)
            return new StrategyDecision(SignalAction.Buy, "Rompimento de range comprimido");
        if (position == 0 && changed && newState < 0 && compressionOk && bar.Close < filter - band)
            return new StrategyDecision(SignalAction.Sell, "Rompimento de range comprimido");

        return new StrategyDecision(SignalAction.None, "Sem sinal");
    }

    private StrategyDecision EvaluateMomentum(MarketBar bar, IReadOnlyDictionary<string, double> m, int position, double entryPrice, int barsSinceEntry)
    {
        var stop = m["ATR"] * _params.AtrStopMultiplier;
        if (position > 0)
        {
            if (bar.Close <= entryPrice - stop)
                return new StrategyDecision(SignalAction.Exit, "Stop long");
            if (m["MACD"] < m["MACDSignal"] && m["RSI"] < 50)
                return new StrategyDecision(SignalAction.Exit, "MACD contra e RSI fraco");
            if (barsSinceEntry >= _defaults.TimeExitBars)
                return new StrategyDecision(SignalAction.Exit, "Tempo long");
            return new StrategyDecision(SignalAction.None, "Momentum long ativo");
        }

        if (position < 0)
        {
            if (bar.Close >= entryPrice + stop)
                return new StrategyDecision(SignalAction.Exit, "Stop short");
            if (m["MACD"] > m["MACDSignal"] && m["RSI"] > 50)
                return new StrategyDecision(SignalAction.Exit, "MACD contra e RSI forte");
            if (barsSinceEntry >= _defaults.TimeExitBars)
                return new StrategyDecision(SignalAction.Exit, "Tempo short");
            return new StrategyDecision(SignalAction.None, "Momentum short ativo");
        }

        var macdDiff = Math.Abs(m["MACD"] - m["MACDSignal"]);
        var minDiff = Math.Max(TickSizeLikeMinimum(), m["ATR"] * _params.MomentumMinMacdAtrRatio);
        var volumeOk = bar.Volume > m["VolumeSMA"] * _params.MomentumVolumeRatio;

        if (macdDiff > minDiff && volumeOk && m["MACD"] > m["MACDSignal"] && m["RSI"] > 55 && bar.Close > m["EMA21"])
            return new StrategyDecision(SignalAction.Buy, "Momentum forte long");
        if (macdDiff > minDiff && volumeOk && m["MACD"] < m["MACDSignal"] && m["RSI"] < 45 && bar.Close < m["EMA21"])
            return new StrategyDecision(SignalAction.Sell, "Momentum forte short");

        return new StrategyDecision(SignalAction.None, "Sem sinal");
    }

    private StrategyDecision EvaluateOrbBreakout(MarketBar bar, IReadOnlyList<MarketBar> history, IReadOnlyDictionary<string, double> m, int position, double entryPrice, int barsSinceEntry)
    {
        var start = _defaults.SessionStartHHmmss;
        var end = _defaults.SessionEndHHmmss;
        var closeAll = _defaults.CloseAllHHmmss;
        var hhmmss = ToHHmmss(bar.Time);
        if (position != 0 && hhmmss >= closeAll)
            return new StrategyDecision(SignalAction.Exit, "Fechamento horario");
        if (position > 0 && bar.Close <= entryPrice - m["ATR"] * _params.OrbAtrStopMultiplier)
            return new StrategyDecision(SignalAction.Exit, "Stop ATR long");
        if (position < 0 && bar.Close >= entryPrice + m["ATR"] * _params.OrbAtrStopMultiplier)
            return new StrategyDecision(SignalAction.Exit, "Stop ATR short");
        if (position != 0 && barsSinceEntry >= _defaults.TimeExitBars)
            return new StrategyDecision(SignalAction.Exit, "Tempo");
        if (position != 0)
            return new StrategyDecision(SignalAction.None, "Em posicao");

        var windowM15 = (_resampledBars ?? history)
            .Where(b => b.Time.Date == bar.Time.Date && ToHHmmss(b.Time) >= start && ToHHmmss(b.Time) <= end)
            .ToList();

        if (hhmmss <= end || hhmmss > AddHoursHHmmss(end, 1) || windowM15.Count < 2)
            return new StrategyDecision(SignalAction.None, "Aguardando rompimento da janela M15");

        var windowHigh = windowM15.Max(b => b.High);
        var windowLow = windowM15.Min(b => b.Low);
        var windowRange = windowHigh - windowLow;
        if (windowRange <= m["ATR"] * 0.5)
            return new StrategyDecision(SignalAction.None, "Amplitude baixa");

        if (bar.Close > windowHigh + m["ATR"] * 0.1)
            return new StrategyDecision(SignalAction.Buy, "Breakout long com folga");
        if (bar.Close < windowLow - m["ATR"] * 0.1)
            return new StrategyDecision(SignalAction.Sell, "Breakout short com folga");

        return new StrategyDecision(SignalAction.None, "Sem rompimento");
    }

    private StrategyDecision EvaluateEma(MarketBar bar, IReadOnlyDictionary<string, double> m, int position, double entryPrice, int barsSinceEntry)
    {
        var atrStop = m["ATR"] * _params.AtrStopMultiplier;
        if (position > 0)
        {
            if (bar.Close <= entryPrice - atrStop)
                return new StrategyDecision(SignalAction.Exit, "Stop long");
            if (bar.Close < m["EMA21"] - m["ATR"] * _params.EmaTrailingAtrOffset)
                return new StrategyDecision(SignalAction.Exit, "Perdeu EMA21");
            if (m["RSI"] > 75)
                return new StrategyDecision(SignalAction.Exit, "RSI sobrecomprado");
            if (barsSinceEntry >= _defaults.TimeExitBars)
                return new StrategyDecision(SignalAction.Exit, "Tempo long");
            return new StrategyDecision(SignalAction.None, "EMA long ativo");
        }

        if (position < 0)
        {
            if (bar.Close >= entryPrice + atrStop)
                return new StrategyDecision(SignalAction.Exit, "Stop short");
            if (bar.Close > m["EMA21"] + m["ATR"] * _params.EmaTrailingAtrOffset)
                return new StrategyDecision(SignalAction.Exit, "Perdeu EMA21");
            if (m["RSI"] < 25)
                return new StrategyDecision(SignalAction.Exit, "RSI sobrevendido");
            if (barsSinceEntry >= _defaults.TimeExitBars)
                return new StrategyDecision(SignalAction.Exit, "Tempo short");
            return new StrategyDecision(SignalAction.None, "EMA short ativo");
        }

        var volumeOk = bar.Volume > m["VolumeSMA"] * _params.EmaVolumeRatio;
        var bullish = m["EMA9"] > m["EMA21"] && m["SwingHigh"] > 0 && bar.Close > m["SwingHigh"];
        var bearish = m["EMA9"] < m["EMA21"] && m["SwingLow"] > 0 && bar.Close < m["SwingLow"];

        if (bullish && volumeOk && m["RSI"] > 50)
            return new StrategyDecision(SignalAction.Buy, "EMA cross + swing rompido com volume");
        if (bearish && volumeOk && m["RSI"] < 50)
            return new StrategyDecision(SignalAction.Sell, "EMA cross + swing rompido com volume");

        return new StrategyDecision(SignalAction.None, "Sem sinal");
    }

    private StrategyDecision EvaluateVwapReversion(MarketBar bar, IReadOnlyDictionary<string, double> m, int position, double entryPrice, int barsSinceEntry)
    {
        if (position != 0 && IsAtOrAfterCloseAll(bar.Time))
            return new StrategyDecision(SignalAction.Exit, "Fechamento horario");
        if (position == 0 && IsAtOrAfterCloseAll(bar.Time))
            return new StrategyDecision(SignalAction.None, "Fora da janela operacional");

        var stop = m["ATR"] * _params.AtrStopMultiplier;
        if (position > 0)
        {
            if (bar.Close <= entryPrice - stop)
                return new StrategyDecision(SignalAction.Exit, "Stop ATR long");
            if (bar.Close >= m["VWAP"])
                return new StrategyDecision(SignalAction.Exit, "Retorno ao VWAP long");
            if (m["RSI"] > 70)
                return new StrategyDecision(SignalAction.Exit, "RSI extremo long");
            if (barsSinceEntry >= _defaults.TimeExitBars)
                return new StrategyDecision(SignalAction.Exit, "Tempo long");
            return new StrategyDecision(SignalAction.None, "Reversao VWAP long ativa");
        }

        if (position < 0)
        {
            if (bar.Close >= entryPrice + stop)
                return new StrategyDecision(SignalAction.Exit, "Stop ATR short");
            if (bar.Close <= m["VWAP"])
                return new StrategyDecision(SignalAction.Exit, "Retorno ao VWAP short");
            if (m["RSI"] < 30)
                return new StrategyDecision(SignalAction.Exit, "RSI extremo short");
            if (barsSinceEntry >= _defaults.TimeExitBars)
                return new StrategyDecision(SignalAction.Exit, "Tempo short");
            return new StrategyDecision(SignalAction.None, "Reversao VWAP short ativa");
        }

        var volumeOk = bar.Volume > m["VolumeSMA"] * _params.VwapReversionVolumeRatio;
        var longSetup = bar.Close < m["VWAP"] * (1 - _params.VwapReversionBand) &&
                        m["RSI"] < _params.RsiOversold &&
                        volumeOk;
        var shortSetup = bar.Close > m["VWAP"] * (1 + _params.VwapReversionBand) &&
                         m["RSI"] > _params.RsiOverbought &&
                         volumeOk;

        if (longSetup)
            return new StrategyDecision(SignalAction.Buy, "Reversao VWAP long");
        if (shortSetup)
            return new StrategyDecision(SignalAction.Sell, "Reversao VWAP short");

        return new StrategyDecision(SignalAction.None, "Sem sinal");
    }

    private StrategyDecision EvaluateBollingerFade(MarketBar bar, IReadOnlyDictionary<string, double> m, int position, double entryPrice, int barsSinceEntry)
    {
        if (position != 0 && IsAtOrAfterCloseAll(bar.Time))
            return new StrategyDecision(SignalAction.Exit, "Fechamento horario");
        if (position == 0 && IsAtOrAfterCloseAll(bar.Time))
            return new StrategyDecision(SignalAction.None, "Fora da janela operacional");

        var bbUpper = m["BbMiddle"] + (m["BbUpper"] - m["BbMiddle"]) * (_params.BbStdDev / 2.0);
        var bbLower = m["BbMiddle"] - (m["BbMiddle"] - m["BbLower"]) * (_params.BbStdDev / 2.0);
        var stop = m["ATR"] * _params.AtrStopMultiplier;
        if (position > 0)
        {
            if (bar.Close <= entryPrice - stop)
                return new StrategyDecision(SignalAction.Exit, "Stop banda long");
            if (bar.Close >= m["BbMiddle"])
                return new StrategyDecision(SignalAction.Exit, "Alvo media Bollinger long");
            if (barsSinceEntry >= _defaults.TimeExitBars)
                return new StrategyDecision(SignalAction.Exit, "Tempo long");
            return new StrategyDecision(SignalAction.None, "Bollinger fade long ativo");
        }

        if (position < 0)
        {
            if (bar.Close >= entryPrice + stop)
                return new StrategyDecision(SignalAction.Exit, "Stop banda short");
            if (bar.Close <= m["BbMiddle"])
                return new StrategyDecision(SignalAction.Exit, "Alvo media Bollinger short");
            if (barsSinceEntry >= _defaults.TimeExitBars)
                return new StrategyDecision(SignalAction.Exit, "Tempo short");
            return new StrategyDecision(SignalAction.None, "Bollinger fade short ativo");
        }

        var bullishReversal = bar.Low <= bbLower && bar.Close > bar.Open && m["RSI"] < _params.BbFadeRsiOversold;
        var bearishReversal = bar.High >= bbUpper && bar.Close < bar.Open && m["RSI"] > _params.BbFadeRsiOverbought;

        if (bullishReversal)
            return new StrategyDecision(SignalAction.Buy, "Fade Bollinger long");
        if (bearishReversal)
            return new StrategyDecision(SignalAction.Sell, "Fade Bollinger short");

        return new StrategyDecision(SignalAction.None, "Sem sinal");
    }

    private StrategyDecision EvaluateSessionBreakout(
        MarketBar bar,
        IReadOnlyList<MarketBar> history,
        IReadOnlyDictionary<string, double> m,
        int position,
        double entryPrice,
        int barsSinceEntry)
    {
        var rangeStart = _defaults.SessionStartHHmmss;
        var rangeEnd = _defaults.SessionEndHHmmss;
        var entryDeadline = AddHoursHHmmss(rangeEnd, 1);
        var closeAll = _defaults.CloseAllHHmmss;
        var hhmmss = ToHHmmss(bar.Time);

        if (position != 0 && hhmmss >= closeAll)
            return new StrategyDecision(SignalAction.Exit, "Fechamento horario");
        if (position > 0 && bar.Close <= entryPrice - m["ATR"] * _params.AtrStopMultiplier)
            return new StrategyDecision(SignalAction.Exit, "Stop ATR long");
        if (position < 0 && bar.Close >= entryPrice + m["ATR"] * _params.AtrStopMultiplier)
            return new StrategyDecision(SignalAction.Exit, "Stop ATR short");
        if (position != 0 && barsSinceEntry >= _defaults.TimeExitBars)
            return new StrategyDecision(SignalAction.Exit, "Tempo");
        if (position != 0)
            return new StrategyDecision(SignalAction.None, "Em posicao");

        var window = history
            .Where(b => b.Time.Date == bar.Time.Date && ToHHmmss(b.Time) >= rangeStart && ToHHmmss(b.Time) <= rangeEnd)
            .ToList();

        if (hhmmss <= rangeEnd || hhmmss > entryDeadline || window.Count < 2)
            return new StrategyDecision(SignalAction.None, "Aguardando rompimento da janela");

        var windowHigh = window.Max(b => b.High);
        var windowLow = window.Min(b => b.Low);
        var windowRange = windowHigh - windowLow;
        if (windowRange < m["ATR"] * _params.SessionMinRangeAtrRatio)
            return new StrategyDecision(SignalAction.None, "Amplitude baixa");

        if (bar.Close > windowHigh + m["ATR"] * _params.SessionBreakoutAtrBuffer)
            return new StrategyDecision(SignalAction.Buy, "Breakout de sessao long");
        if (bar.Close < windowLow - m["ATR"] * _params.SessionBreakoutAtrBuffer)
            return new StrategyDecision(SignalAction.Sell, "Breakout de sessao short");

        return new StrategyDecision(SignalAction.None, "Sem rompimento");
    }

    private static Dictionary<string, double> BuildMetrics(
        IReadOnlyList<MarketBar> history,
        IReadOnlyList<MarketBar> sessionHistory,
        IReadOnlyList<double> closes,
        IReadOnlyList<double> highs,
        IReadOnlyList<double> lows,
        IReadOnlyList<double> atrValues,
        IReadOnlyList<double> macdValues)
    {
        var ema9 = TechnicalIndicators.Ema(closes, 9);
        var ema12 = TechnicalIndicators.Ema(closes, 12);
        var ema21 = TechnicalIndicators.Ema(closes, 21);
        var ema26 = TechnicalIndicators.Ema(closes, 26);
        var macd = double.IsNaN(ema12) || double.IsNaN(ema26) ? double.NaN : ema12 - ema26;

        return new Dictionary<string, double>
        {
            ["EMA9"] = ema9,
            ["EMA21"] = ema21,
            ["RSI"] = TechnicalIndicators.Rsi(closes, 14),
            ["VWAP"] = TechnicalIndicators.Vwap(sessionHistory),
            ["ATR"] = TechnicalIndicators.Atr(history, 14),
            ["ATRPrev"] = atrValues.Count < 2 ? double.NaN : atrValues[^2],
            ["ATRSMA"] = TechnicalIndicators.Sma(atrValues, 14),
            ["CandleRangeSMA"] = TechnicalIndicators.CandleRangeSma(history, 14),
            ["VolumeSMA"] = TechnicalIndicators.VolumeSma(history, 20),
            ["MACD"] = macd,
            ["MACDSignal"] = TechnicalIndicators.Ema(macdValues, 9),
            ["BbMiddle"] = TechnicalIndicators.Sma(closes, 20),
            ["BbUpper"] = TechnicalIndicators.BollingerUpper(closes, 20, 2.0),
            ["BbLower"] = TechnicalIndicators.BollingerLower(closes, 20, 2.0),
            ["Highest10"] = highs.Count < 10 ? double.NaN : highs.TakeLast(10).Max(),
            ["Lowest10"] = lows.Count < 10 ? double.NaN : lows.TakeLast(10).Min(),
            ["Highest3"] = highs.Count < 3 ? double.NaN : highs.TakeLast(3).Max(),
            ["Lowest3"] = lows.Count < 3 ? double.NaN : lows.TakeLast(3).Min(),
            ["RangeFilter"] = TechnicalIndicators.Ema(closes, 20),
            ["Trend"] = double.NaN,
            ["SwingHigh"] = highs.Count < 20 ? double.NaN : highs.TakeLast(20).Max(),
            ["SwingLow"] = lows.Count < 20 ? double.NaN : lows.TakeLast(20).Min()
        };
    }

    private static bool IndicatorsReady(IReadOnlyDictionary<string, double> metrics)
    {
        var required = new[] { "EMA9", "EMA21", "RSI", "VWAP", "ATR", "ATRSMA", "CandleRangeSMA", "VolumeSMA", "Highest3", "Lowest3", "BbUpper", "BbLower" };
        return required.All(k => !double.IsNaN(metrics[k]) && !double.IsInfinity(metrics[k]));
    }

    private bool IsInsideSession(DateTime time)
    {
        var value = ToHHmmss(time);
        return value >= _defaults.SessionStartHHmmss && value <= _defaults.SessionEndHHmmss;
    }

    private bool IsAtOrAfterCloseAll(DateTime time) => ToHHmmss(time) >= _defaults.CloseAllHHmmss;

    public static string StrategyName(StrategyKind kind) => kind switch
    {
        StrategyKind.Volatility => "NinjaBotIAVolatility_v1_0_0_0",
        StrategyKind.Trend => "NinjaBotIATrend_v1_0_0_1",
        StrategyKind.Range => "NinjaBotIARange_v1_0_0_0",
        StrategyKind.Momentum => "NinjaBotIAMomentum_v1_0_0_0",
        StrategyKind.OrbBreakout => "OrbBreakout_v1",
        StrategyKind.Ema => "ema",
        StrategyKind.VwapReversion => "VwapReversion_v1",
        StrategyKind.BollingerFade => "BollingerFade_v1",
        StrategyKind.SessionBreakout => "SessionBreakout_v1",
        _ => kind.ToString()
    };

    private static int ToHHmmss(DateTime time) => time.Hour * 10000 + time.Minute * 100 + time.Second;

    private static int AddHoursHHmmss(int hhmmss, int hours)
    {
        var hour = hhmmss / 10000;
        var minute = hhmmss / 100 % 100;
        var second = hhmmss % 100;
        var time = new DateTime(2000, 1, 1, hour, minute, second).AddHours(hours);
        return ToHHmmss(time);
    }

    private static double TickSizeLikeMinimum() => 0.25;

    private sealed record StrategyDefaults(
        int SessionStartHHmmss,
        int SessionEndHHmmss,
        int CloseAllHHmmss,
        int TimeExitBars,
        double TargetPoints,
        double StopPoints,
        double AtrMultiplier,
        double AtrMultiplierTp,
        double VolumeThreshold,
        double MaxDrawdownPoints)
    {
        public static StrategyDefaults For(StrategyKind strategy) => strategy switch
        {
            StrategyKind.Volatility => new(93000, 101000, 101000, 11, 40, 8, 3.4, 3.4, 1.2, 8),
            StrategyKind.Trend => new(93000, 143000, 143000, 60, 76.25, 62.5, 2.0, 2.0, 1.0, 62.5),
            StrategyKind.Range => new(0, 124500, 124500, 40, 40, 40, 4.0, 3.0, 1.0, 40),
            StrategyKind.Momentum => new(90000, 110000, 110000, 30, 80, 73.75, 1.0, 1.0, 1.0, 73.75),
            StrategyKind.OrbBreakout => new(83000, 100000, 165500, 60, 73.75, 26.25, 1.0, 1.0, 1.0, 26.25),
            StrategyKind.Ema => new(90000, 170000, 170000, 30, 40, 25, 1.0, 1.0, 1.0, 25),
            StrategyKind.VwapReversion => new(93000, 160000, 160000, 20, 40, 15, 1.0, 1.0, 1.1, 15),
            StrategyKind.BollingerFade => new(93000, 160000, 160000, 30, 40, 20, 1.0, 1.0, 1.0, 20),
            StrategyKind.SessionBreakout => new(93000, 100000, 165500, 40, 60, 25, 1.0, 1.0, 1.0, 25),
            _ => new(90000, 170000, 170000, 30, 40, 25, 1.0, 1.0, 1.0, 25)
        };
    }
}

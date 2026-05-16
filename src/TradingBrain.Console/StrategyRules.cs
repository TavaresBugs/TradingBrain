using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

public sealed partial class StrategyBacktester
{
    private StrategyDecision Evaluate(
        MarketBar bar,
        IReadOnlyList<MarketBar> bars,
        int barIndex,
        IReadOnlyDictionary<string, double> m,
        int position,
        double entryPrice,
        double openProfit,
        int barsSinceEntry,
        ref int trendState,
        ref int rangeState,
        ref int schoolRunState,
        ref int orbState,
        ref int ibState)
    {
        var usesOwnSessionGate = _strategy is
            StrategyKind.OrbBreakout or
            StrategyKind.SchoolRun or
            StrategyKind.IbBreakout;

        if (!usesOwnSessionGate && !IsInsideSession(bar.Time) && position == 0)
        {
            return new StrategyDecision(SignalAction.None, "Fora da sessao");
        }

        return _strategy switch
        {
            StrategyKind.Volatility => EvaluateVolatility(bar, m, position, entryPrice, openProfit, barsSinceEntry),
            StrategyKind.Trend => EvaluateTrend(bar, m, position, entryPrice, barsSinceEntry, ref trendState),
            StrategyKind.Range => EvaluateRange(bar, m, position, entryPrice, barsSinceEntry, ref rangeState),
            StrategyKind.Momentum => EvaluateMomentum(bar, m, position, entryPrice, barsSinceEntry),
            StrategyKind.OrbBreakout => EvaluateOrbBreakout(bar, bars, barIndex, m, position, entryPrice, barsSinceEntry, ref orbState),
            StrategyKind.Ema => EvaluateEma(bar, m, position, entryPrice, barsSinceEntry),
            StrategyKind.VwapReversion => EvaluateVwapReversion(bar, m, position, entryPrice, barsSinceEntry),
            StrategyKind.BollingerFade => EvaluateBollingerFade(bar, m, position, entryPrice, barsSinceEntry),
            StrategyKind.SchoolRun => EvaluateSchoolRun(bar, m, position, entryPrice, barsSinceEntry, ref schoolRunState),
            StrategyKind.IbBreakout => EvaluateIbBreakout(bar, bars, barIndex, m, position, entryPrice, ref ibState),
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

    private StrategyDecision EvaluateOrbBreakout(
        MarketBar bar,
        IReadOnlyList<MarketBar> bars,
        int barIndex,
        IReadOnlyDictionary<string, double> m,
        int position,
        double entryPrice,
        int barsSinceEntry,
        ref int orbState)
    {
        var hhmmss = ToHHmmss(bar.Time);
        var rangeStart = _params.OrbRangeStartHHmmss;
        var rangeEnd = _params.OrbRangeEndHHmmss;
        var closeAll = _defaults.CloseAllHHmmss;

        if (position != 0 && hhmmss >= closeAll)
        {
            orbState = 2;
            return new StrategyDecision(SignalAction.Exit, "ORB: fechamento horario");
        }

        if (position > 0)
        {
            var stopLevel = double.IsNaN(_orbWindowLow)
                ? entryPrice - m["ATR"] * _params.OrbAtrStopMultiplier
                : _orbWindowLow - m["ATR"] * _params.OrbAtrStopMultiplier * 0.1;
            if (bar.Close <= stopLevel)
            {
                orbState = 2;
                return new StrategyDecision(SignalAction.Exit, "ORB: stop long");
            }
            if (barsSinceEntry >= _defaults.TimeExitBars)
            {
                orbState = 2;
                return new StrategyDecision(SignalAction.Exit, "ORB: tempo");
            }
            return new StrategyDecision(SignalAction.None, "ORB: em posicao long");
        }

        if (position < 0)
        {
            var stopLevel = double.IsNaN(_orbWindowHigh)
                ? entryPrice + m["ATR"] * _params.OrbAtrStopMultiplier
                : _orbWindowHigh + m["ATR"] * _params.OrbAtrStopMultiplier * 0.1;
            if (bar.Close >= stopLevel)
            {
                orbState = 2;
                return new StrategyDecision(SignalAction.Exit, "ORB: stop short");
            }
            if (barsSinceEntry >= _defaults.TimeExitBars)
            {
                orbState = 2;
                return new StrategyDecision(SignalAction.Exit, "ORB: tempo");
            }
            return new StrategyDecision(SignalAction.None, "ORB: em posicao short");
        }

        if (orbState == 2)
            return new StrategyDecision(SignalAction.None, "ORB: trade diario encerrado");

        if (hhmmss > AddHoursHHmmss(rangeEnd, 1))
            return new StrategyDecision(SignalAction.None, "ORB: janela de entrada expirada");

        if (hhmmss <= rangeEnd)
            return new StrategyDecision(SignalAction.None, "ORB: formando range");

        var windowM15 = (_resampledBars ?? bars.Take(barIndex + 1))
            .Where(b => b.Time.Date == bar.Time.Date &&
                        ToHHmmss(b.Time) >= rangeStart &&
                        ToHHmmss(b.Time) <= rangeEnd)
            .ToList();

        if (windowM15.Count < _params.OrbMinWindowBars)
            return new StrategyDecision(SignalAction.None, "ORB: barras insuficientes na janela");

        var windowHigh = windowM15.Max(b => b.High);
        var windowLow = windowM15.Min(b => b.Low);
        var windowRange = windowHigh - windowLow;
        if (windowRange < m["ATR"] * _params.OrbMinRangeAtrRatio)
            return new StrategyDecision(SignalAction.None, "ORB: amplitude baixa");

        var volumeOk = !_params.OrbRequireVolume ||
                       bar.Volume > m["VolumeSMA"] * _params.OrbVolumeRatio;

        if (bar.Close > windowHigh + m["ATR"] * _params.OrbBreakoutBuffer && volumeOk)
        {
            _orbWindowHigh = windowHigh;
            _orbWindowLow = windowLow;
            orbState = 2;
            return new StrategyDecision(SignalAction.Buy, "ORB: breakout long");
        }
        if (bar.Close < windowLow - m["ATR"] * _params.OrbBreakoutBuffer && volumeOk)
        {
            _orbWindowHigh = windowHigh;
            _orbWindowLow = windowLow;
            orbState = 2;
            return new StrategyDecision(SignalAction.Sell, "ORB: breakout short");
        }

        return new StrategyDecision(SignalAction.None, "ORB: sem rompimento");
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

    private StrategyDecision EvaluateIbBreakout(
        MarketBar bar,
        IReadOnlyList<MarketBar> bars,
        int barIndex,
        IReadOnlyDictionary<string, double> m,
        int position,
        double entryPrice,
        ref int ibState)
    {
        var hhmm = bar.Time.Hour * 100 + bar.Time.Minute;
        var today = DateOnly.FromDateTime(bar.Time);
        var ibHigh = double.NaN;
        var ibLow = double.NaN;
        var ibVolume = 0.0;
        var ibBarCount = 0;

        for (var k = barIndex - 1; k >= 0; k--)
        {
            var candidate = bars[k];
            if (DateOnly.FromDateTime(candidate.Time) != today)
                break;

            var candidateHHmm = candidate.Time.Hour * 100 + candidate.Time.Minute;
            if (candidateHHmm < 930 || candidateHHmm > 1025)
                continue;

            ibHigh = double.IsNaN(ibHigh) ? candidate.High : Math.Max(ibHigh, candidate.High);
            ibLow = double.IsNaN(ibLow) ? candidate.Low : Math.Min(ibLow, candidate.Low);
            ibVolume += candidate.Volume;
            ibBarCount++;
        }

        if (double.IsNaN(ibHigh) || double.IsNaN(ibLow) || ibBarCount < 8)
            return new StrategyDecision(SignalAction.None, "IB: incompleto");

        var ibRange = ibHigh - ibLow;
        var atr14 = m["ATR"];
        if (atr14 <= 0)
            return new StrategyDecision(SignalAction.None, "IB: ATR invalido");

        var ibRangeRatio = ibRange / atr14;
        var ibMid = (ibHigh + ibLow) / 2.0;
        var stopLong = _params.IbUseHalfRangeStop ? ibMid : ibLow;
        var stopShort = _params.IbUseHalfRangeStop ? ibMid : ibHigh;

        if (position > 0)
        {
            if (bar.Close <= stopLong)
                return new StrategyDecision(SignalAction.Exit, "IB: stop hit");
            if (bar.Close >= entryPrice + ibRange * _params.IbTargetMultiplier)
                return new StrategyDecision(SignalAction.Exit, "IB: target hit");
            if (hhmm >= 1555)
                return new StrategyDecision(SignalAction.Exit, "IB: fim de sessao");
            return new StrategyDecision(SignalAction.None, "IB: em posicao");
        }

        if (position < 0)
        {
            if (bar.Close >= stopShort)
                return new StrategyDecision(SignalAction.Exit, "IB: stop hit");
            if (bar.Close <= entryPrice - ibRange * _params.IbTargetMultiplier)
                return new StrategyDecision(SignalAction.Exit, "IB: target hit");
            if (hhmm >= 1555)
                return new StrategyDecision(SignalAction.Exit, "IB: fim de sessao");
            return new StrategyDecision(SignalAction.None, "IB: em posicao");
        }

        if (hhmm < 1030 || hhmm >= 1400)
            return new StrategyDecision(SignalAction.None, "IB: fora da janela");
        if (ibState == 2)
            return new StrategyDecision(SignalAction.None, "IB: trade diario encerrado");
        if (ibRangeRatio < _params.IbMinRangeRatio)
            return new StrategyDecision(SignalAction.None, $"IB: range muito estreito ({ibRangeRatio:F2} < {_params.IbMinRangeRatio:F2})");
        if (ibRangeRatio > _params.IbMaxRangeRatio)
            return new StrategyDecision(SignalAction.None, $"IB: range muito largo ({ibRangeRatio:F2} > {_params.IbMaxRangeRatio:F2})");

        var ibVolumeAverage = ibBarCount > 0 ? ibVolume / ibBarCount : 0;
        var volumeOk = !_params.IbRequireVolume || bar.Volume >= ibVolumeAverage;

        if (bar.Close > ibHigh && volumeOk)
        {
            ibState = 2;
            return new StrategyDecision(SignalAction.Buy, $"IB: breakout HIGH ib={ibRangeRatio:F2}");
        }

        if (bar.Close < ibLow && volumeOk)
        {
            ibState = 2;
            return new StrategyDecision(SignalAction.Sell, $"IB: breakout LOW ib={ibRangeRatio:F2}");
        }

        return new StrategyDecision(SignalAction.None, $"IB: aguardando breakout (H={ibHigh:F2} L={ibLow:F2})");
    }

    private StrategyDecision EvaluateSchoolRun(
        MarketBar bar,
        IReadOnlyDictionary<string, double> m,
        int position,
        double entryPrice,
        int barsSinceEntry,
        ref int schoolRunState)
    {
        if (position != 0 && IsAtOrAfterCloseAll(bar.Time))
            return new StrategyDecision(SignalAction.Exit, "SRS: fechamento forcado");

        var stop = m["ATR"] * _params.SrsAtrStopMultiplier;
        var target = m["ATR"] * _params.SrsAtrTargetMultiplier;

        if (position > 0)
        {
            if (bar.Close <= entryPrice - stop)
                return new StrategyDecision(SignalAction.Exit, "SRS: stop long");
            if (bar.Close >= entryPrice + target)
                return new StrategyDecision(SignalAction.Exit, "SRS: target long");
            if (barsSinceEntry >= _defaults.TimeExitBars)
                return new StrategyDecision(SignalAction.Exit, "SRS: timeout long");
            return new StrategyDecision(SignalAction.None, "SRS: em posicao long");
        }

        if (position < 0)
        {
            if (bar.Close >= entryPrice + stop)
                return new StrategyDecision(SignalAction.Exit, "SRS: stop short");
            if (bar.Close <= entryPrice - target)
                return new StrategyDecision(SignalAction.Exit, "SRS: target short");
            if (barsSinceEntry >= _defaults.TimeExitBars)
                return new StrategyDecision(SignalAction.Exit, "SRS: timeout short");
            return new StrategyDecision(SignalAction.None, "SRS: em posicao short");
        }

        if (schoolRunState == 2)
            return new StrategyDecision(SignalAction.None, "SRS: trade diario encerrado");
        if (IsAtOrAfterCloseAll(bar.Time) || ToHHmmss(bar.Time) > _defaults.SessionEndHHmmss)
            return new StrategyDecision(SignalAction.None, "SRS: fora da janela");

        var m15Today = (_resampledBars ?? Array.Empty<MarketBar>())
            .Where(b => b.Time.Date == bar.Time.Date)
            .ToList();

        if (m15Today.Count < _params.SrsReferenceCandle)
            return new StrategyDecision(SignalAction.None, "SRS: aguardando candle de referencia M15");

        var refCandle = m15Today[_params.SrsReferenceCandle - 1];
        var refCloseTime = refCandle.Time.AddMinutes(15);
        schoolRunState = 1;

        if (bar.Time <= refCloseTime)
            return new StrategyDecision(SignalAction.None, "SRS: candle de referencia ainda aberto");

        var overnightBars = (_resampledBars ?? Array.Empty<MarketBar>())
            .Where(b => b.Time.Date == bar.Time.Date &&
                        ToHHmmss(b.Time) >= _params.OvernightRangeStartHHmmss &&
                        ToHHmmss(b.Time) <= _params.OvernightRangeEndHHmmss)
            .ToList();

        var overnightHigh = overnightBars.Count > 0 ? overnightBars.Max(b => b.High) : double.NaN;
        var overnightLow = overnightBars.Count > 0 ? overnightBars.Min(b => b.Low) : double.NaN;
        var insideOvernight = !double.IsNaN(overnightHigh) &&
                              !double.IsNaN(overnightLow) &&
                              bar.Close < overnightHigh &&
                              bar.Close > overnightLow;

        var buffer = m["ATR"] * _params.SrsAtrBuffer;
        var rawLong = bar.Close > refCandle.High + buffer;
        var rawShort = bar.Close < refCandle.Low - buffer;

        if (!rawLong && !rawShort)
            return new StrategyDecision(SignalAction.None, "SRS: sem rompimento do candle de referencia");

        SignalAction action;
        string reason;
        if (_params.UseAntiMode && insideOvernight)
        {
            action = rawLong ? SignalAction.Sell : SignalAction.Buy;
            reason = rawLong ? "Anti-SRS: long dentro do overnight -> short" : "Anti-SRS: short dentro do overnight -> long";
        }
        else
        {
            action = rawLong ? SignalAction.Buy : SignalAction.Sell;
            reason = rawLong ? "SRS: breakout long" : "SRS: breakout short";
        }

        schoolRunState = 2;
        return new StrategyDecision(action, reason);
    }

    private static IReadOnlyDictionary<string, double> BuildMetrics(PrecomputedSeries s, int i)
    {
        return new Dictionary<string, double>
        {
            ["EMA9"] = s.Ema9[i],
            ["EMA21"] = s.Ema21[i],
            ["RSI"] = s.Rsi14[i],
            ["VWAP"] = s.Vwap[i],
            ["ATR"] = s.Atr14[i],
            ["ATRPrev"] = i > 0 ? s.Atr14[i - 1] : double.NaN,
            ["ATRSMA"] = s.AtrSma14[i],
            ["CandleRangeSMA"] = s.CandleRangeSma14[i],
            ["VolumeSMA"] = s.VolumeSma20[i],
            ["MACD"] = s.Macd[i],
            ["MACDSignal"] = s.MacdSignal[i],
            ["Highest10"] = s.Highest10[i],
            ["Lowest10"] = s.Lowest10[i],
            ["Highest3"] = s.Highest3[i],
            ["Lowest3"] = s.Lowest3[i],
            ["RangeFilter"] = s.Ema20[i],
            ["Trend"] = double.NaN,
            ["SwingHigh"] = s.SwingHigh20[i],
            ["SwingLow"] = s.SwingLow20[i],
            ["BbUpper"] = s.BbUpper[i],
            ["BbMiddle"] = s.BbMiddle[i],
            ["BbLower"] = s.BbLower[i],
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
        StrategyKind.Volatility => "TradingBrain.Volatility",
        StrategyKind.Trend => "TradingBrain.Trend",
        StrategyKind.Range => "TradingBrain.Range",
        StrategyKind.Momentum => "TradingBrain.Momentum",
        StrategyKind.OrbBreakout => "TradingBrain.ORB",
        StrategyKind.Ema => "TradingBrain.EMA",
        StrategyKind.VwapReversion => "TradingBrain.VwapReversion",
        StrategyKind.BollingerFade => "TradingBrain.BollingerFade",
        StrategyKind.SchoolRun => "TradingBrain.SRS",
        StrategyKind.IbBreakout => "TradingBrain.IbBreakout",
        _ => $"TradingBrain.{kind}"
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
            StrategyKind.SchoolRun => new(93000, 160000, 160000, 20, 60, 30, 1.0, 1.0, 1.0, 30),
            StrategyKind.IbBreakout => new(103000, 140000, 155500, 60, 60, 30, 1.0, 1.0, 1.0, 30),
            _ => new(90000, 170000, 170000, 30, 40, 25, 1.0, 1.0, 1.0, 25)
        };
    }
}

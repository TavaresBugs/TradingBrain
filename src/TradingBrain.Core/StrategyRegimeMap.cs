namespace TradingBrain.Core;

/// <summary>
/// Define os regimes de mercado permitidos para cada strategy.
/// Usado pelo RegimeFilter para restringir o dataset antes do backtest.
/// </summary>
public static class StrategyRegimeMap
{
    private static readonly IReadOnlyDictionary<StrategyKind, IReadOnlyList<MarketRegime>> Map =
        new Dictionary<StrategyKind, IReadOnlyList<MarketRegime>>
        {
            [StrategyKind.Momentum] = new[] { MarketRegime.Trend },
            [StrategyKind.Ema] = new[] { MarketRegime.Trend },
            [StrategyKind.Trend] = new[] { MarketRegime.Trend },
            [StrategyKind.OrbBreakout] = new[] { MarketRegime.Breakout },
            [StrategyKind.SessionBreakout] = new[] { MarketRegime.Breakout },
            [StrategyKind.SchoolRun] = new[] { MarketRegime.Breakout },
            [StrategyKind.Range] = new[] { MarketRegime.Range },
            [StrategyKind.VwapReversion] = new[] { MarketRegime.Range },
            [StrategyKind.BollingerFade] = new[] { MarketRegime.Range },
            [StrategyKind.Volatility] = new[] { MarketRegime.HighVolatility },
        };

    /// <summary>
    /// Retorna os regimes permitidos para a strategy.
    /// Nunca inclui NonTrend.
    /// </summary>
    public static IReadOnlyList<MarketRegime> For(StrategyKind strategy)
        => Map.TryGetValue(strategy, out var regimes)
            ? regimes
            : Array.Empty<MarketRegime>();

    /// <summary>
    /// Retorna true se a strategy tem regime(s) definido(s).
    /// </summary>
    public static bool HasFilter(StrategyKind strategy)
        => Map.ContainsKey(strategy);
}

namespace TradingBrain.Core;

/// <summary>
/// Define os regimes de mercado permitidos para cada strategy.
/// Usado pelo RegimeFilter para restringir o dataset antes do backtest.
/// </summary>
public static class StrategyRegimeMap
{
    private static readonly IReadOnlyDictionary<StrategyKind, IReadOnlyList<MarketRegime>> Map =
        BuildMap();

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

    private static IReadOnlyDictionary<StrategyKind, IReadOnlyList<MarketRegime>> BuildMap()
    {
        var map = new Dictionary<StrategyKind, IReadOnlyList<MarketRegime>>();

        Add(map, "Momentum", MarketRegime.Trend);
        Add(map, "Ema", MarketRegime.WideIbBreakout, MarketRegime.IntradayExpansion, MarketRegime.HighVolatility);
        Add(map, "Trend", MarketRegime.Trend, MarketRegime.Breakout, MarketRegime.WideIbBreakout, MarketRegime.IntradayExpansion);
        Add(map, "IbBreakout", MarketRegime.Trend, MarketRegime.Breakout, MarketRegime.WideIbBreakout, MarketRegime.IntradayExpansion, MarketRegime.HighVolatility);
        Add(map, "OrbBreakout", MarketRegime.Trend, MarketRegime.Breakout, MarketRegime.WideIbBreakout, MarketRegime.IntradayExpansion);
        Add(map, "SchoolRun", MarketRegime.Breakout, MarketRegime.HighVolatility); // Range amplo virou rotacional: -0.4 pts/trade em SRS
        Add(map, "Range", MarketRegime.Range);
        Add(map, "VwapReversion", MarketRegime.Range, MarketRegime.HighVolatility); // +33.8 pts/trade em HV (17t)
        Add(map, "BollingerFade", MarketRegime.Range);
        Add(map, "Volatility", MarketRegime.Breakout, MarketRegime.WideIbBreakout, MarketRegime.IntradayExpansion);

        return map;
    }

    private static void Add(
        IDictionary<StrategyKind, IReadOnlyList<MarketRegime>> map,
        string strategyName,
        params MarketRegime[] regimes)
    {
        if (Enum.TryParse<StrategyKind>(strategyName, ignoreCase: false, out var strategy))
            map[strategy] = regimes;
    }
}

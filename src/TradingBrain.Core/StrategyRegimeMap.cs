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
        Add(map, "Ema", MarketRegime.Trend);
        Add(map, "Trend", MarketRegime.Trend);
        Add(map, "IbBreakout", MarketRegime.Breakout, MarketRegime.Trend);
        Add(map, "OrbBreakout", MarketRegime.Breakout, MarketRegime.Trend);
        Add(map, "SchoolRun", MarketRegime.Trend, MarketRegime.Breakout);
        Add(map, "Range", MarketRegime.Range, MarketRegime.Undefined);
        Add(map, "VwapReversion", MarketRegime.Range, MarketRegime.Undefined);
        Add(map, "BollingerFade", MarketRegime.Range, MarketRegime.Undefined);
        Add(map, "Volatility", MarketRegime.HighVolatility);

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

namespace TradingBrain.Core;

/// <summary>
/// Filtra uma lista de barras para incluir apenas os dias cujo regime
/// está na lista de regimes permitidos.
/// A classificação de regime usa TODAS as barras (sem corte), para que
/// o ATR14 e o IB de ontem sejam calculados com contexto completo.
/// </summary>
public static class RegimeFilter
{
    /// <summary>
    /// Classifica os dias do dataset completo e devolve apenas as barras
    /// dos dias que pertencem a um dos <paramref name="allowedRegimes"/>.
    /// NonTrend é sempre excluído quando existir no enum, independente de allowedRegimes.
    /// </summary>
    public static IReadOnlyList<MarketBar> Apply(
        IReadOnlyList<MarketBar> allBars,
        IReadOnlyList<MarketRegime> allowedRegimes,
        bool includeUndefined = false)
    {
        if (allowedRegimes.Count == 0)
            return allBars;

        var regimesByDate = RegimeClassifier.Classify(allBars)
            .ToDictionary(r => r.Date);

        var allowedSet = allowedRegimes
            .Where(r => !IsNonTrend(r))
            .ToHashSet();

        if (allowedSet.Count == 0)
            return Array.Empty<MarketBar>();

        return allBars
            .Where(b =>
            {
                var date = DateOnly.FromDateTime(b.Time);
                if (!regimesByDate.TryGetValue(date, out var dayRegime))
                    return false;

                return !IsNonTrend(dayRegime.Regime)
                    && (allowedSet.Contains(dayRegime.Regime)
                        || (includeUndefined && dayRegime.Regime == MarketRegime.Undefined));
            })
            .ToList();
    }

    /// <summary>
    /// Conta quantos dias distintos existem por regime no dataset.
    /// Útil para diagnóstico antes de rodar o grid.
    /// </summary>
    public static IReadOnlyDictionary<MarketRegime, int> CountDaysByRegime(
        IReadOnlyList<MarketBar> allBars)
    {
        var regimes = RegimeClassifier.Classify(allBars);
        return regimes
            .GroupBy(r => r.Regime)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private static bool IsNonTrend(MarketRegime regime)
        => regime.ToString().Equals("NonTrend", StringComparison.OrdinalIgnoreCase);
}

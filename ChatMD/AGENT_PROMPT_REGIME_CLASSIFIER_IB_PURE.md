# TradingBrain — Prompt: RegimeClassifier IB Puro (substituição completa do KER)

> Repositório: https://github.com/TavaresBugs/TradingBrain  
> Branch alvo: `feat/regime-classifier-ib-pure` (criar a partir de `main`)  
> Runtime: .NET 10 | Build: `dotnet build ./TradingBrain.slnx` | Testes: `dotnet test ./TradingBrain.slnx`  
> Testes atuais: 84/84

---

## Contexto e decisão

O `RegimeClassifier` atual usa KER (Kaufman Efficiency Ratio) como sinal primário. A análise empírica sobre 300 dias reais de MNQ provou que o KER está bloqueando 102 dias de Trend como Undefined — resultando em 52.3% do dataset sem regime útil.

Os sinais IB (Initial Balance) derivados dos dados reais discriminam os regimes com clareza superior:

| Regime | Sinal primário | Sinal secundário |
|---|---|---|
| Range | Open Outside = false + C-period Inside | — |
| Trend | Open Outside = true + overnight ≤ 1.0x + gap ≤ 0.40x | IbFull ≤ 0.75x |
| Breakout | Open Outside = true + (overnight > 1.0x OU gap > 0.40x) | — |
| HighVolatility | Overnight > 2.0x | — |
| NonTrend | IbFull < 0.05x (IB zero) | — |
| Undefined | nenhuma regra disparou | — |

**KER é removido completamente.** `ComputeDailyKer()` é deletado. O campo `Ker` no `DayRegime` é substituído por campos IB.

---

## Definições dos sinais IB (usar em todo o código)

```
IB de ontem   = barras de 9:30 a 10:25 da sessão anterior (12 barras de 5min)
IbHighYest    = max(high) das barras IB de ontem
IbLowYest     = min(low) das barras IB de ontem
IbRangeYest   = IbHighYest - IbLowYest

IB de hoje    = barras de 9:30 a 10:25 da sessão atual
IbHighToday   = max(high) das barras IB de hoje
IbLowToday    = min(low) das barras IB de hoje
IbRangeToday  = IbHighToday - IbLowToday

OpenOutside   = openToday > IbHighYest OU openToday < IbLowYest
IbFullToday   = IbRangeToday / Atr14Diario
IbFullYest    = IbRangeYest  / Atr14Diario

C-period      = barras de 10:30 a 10:55 (primeiras 6 barras após o IB)
CperiodHigh   = max(high) das barras C-period de hoje
CperiodLow    = min(low) das barras C-period de hoje
CperiodInside = CperiodHigh <= IbHighToday AND CperiodLow >= IbLowToday

OvernightRange = range completo das barras entre 18:00 de ontem e 09:25 de hoje
OvernightRatio = OvernightRange / Atr14Diario

GapRatio       = |openToday - prevClose| / Atr14Diario
```

> **Atenção ao lookahead:** `IbFullToday` e `CperiodInside` usam barras de hoje — isso é válido porque o classificador é usado apenas para filtrar o dataset ANTES de qualquer split IS/OOS. A classificação de um dia `d` nunca influencia o backtest de outro dia.

---

## FASE 1 — Atualizar `DayRegime`

### Arquivo: `src/TradingBrain.Core/MarketRegime.cs`

Substitua o record `DayRegime` inteiro pelo seguinte. Campos antigos incompatíveis com o novo modelo são removidos (`RangeRatio`, `ClosePosition`, `Ker`). Campos novos são adicionados:

```csharp
public sealed record DayRegime(
    DateOnly Date,
    MarketRegime Regime,
    string   Reason,

    // Sinais IB de ontem (disponíveis antes de 9:30)
    double   IbHighYest,
    double   IbLowYest,
    double   IbFullYest,       // IbRangeYest / Atr14

    // Sinais da sessão de hoje (calculados sobre barras de hoje)
    double   IbFullToday,      // IbRangeToday / Atr14
    bool     OpenOutside,      // open de hoje fora do IB de ontem
    bool     CperiodInside,    // C-period (10:30-10:55) dentro do IB de hoje
    double   OvernightRatio,   // overnight range / Atr14
    double   GapRatio,         // |open - prevClose| / Atr14
    double   Atr14             // ATR14 diário usado na classificação
);
```

---

## FASE 2 — Reescrever `RegimeClassifier`

### Arquivo: `src/TradingBrain.Core/RegimeClassifier.cs`

Substitua o arquivo completo pelo código abaixo. Nada do arquivo atual deve ser preservado exceto o namespace e as constantes de horário.

```csharp
namespace TradingBrain.Core;

public static class RegimeClassifier
{
    private const int SessionOpenHHmm    = 930;
    private const int SessionCloseHHmm   = 1600;
    private const int IbEndHHmm          = 1025; // última barra de 5min dentro do IB
    private const int CperiodStartHHmm   = 1030;
    private const int CperiodEndHHmm     = 1055; // 6 barras de 5min após o IB
    private const int OvernightStartHHmm = 1800;

    /// <summary>
    /// Classifica cada dia usando sinais IB (Initial Balance) derivados
    /// das barras de mercado. Sem lookahead: a classificação do dia D
    /// usa apenas dados disponíveis até o fechamento da sessão do dia D.
    /// Retorna um DayRegime por dia de mercado presente nos dados.
    /// </summary>
    public static IReadOnlyList<DayRegime> Classify(IReadOnlyList<MarketBar> bars)
    {
        var byDate = bars
            .GroupBy(b => b.Time.Date)
            .OrderBy(g => g.Key)
            .ToList();

        var atr14ByDate = ComputeDailyAtr14(byDate);
        var result      = new List<DayRegime>();

        for (var d = 1; d < byDate.Count; d++)
        {
            var today     = byDate[d].Key;
            var todayBars = byDate[d].OrderBy(b => b.Time).ToList();
            var prevDate  = byDate[d - 1].Key;
            var prevBars  = byDate[d - 1].OrderBy(b => b.Time).ToList();

            // ATR14 do dia anterior — necessário para normalizar os ratios
            if (!atr14ByDate.TryGetValue(prevDate, out var atr14)
                || double.IsNaN(atr14) || atr14 <= 0)
                continue;

            // --- Sinais de ontem ---

            var prevSessionBars = prevBars
                .Where(b => HHmm(b.Time) >= SessionOpenHHmm
                         && HHmm(b.Time) <= SessionCloseHHmm)
                .ToList();

            if (prevSessionBars.Count == 0) continue;

            var prevClose = prevSessionBars.Last().Close;

            var prevIbBars = prevSessionBars
                .Where(b => HHmm(b.Time) >= SessionOpenHHmm
                         && HHmm(b.Time) <= IbEndHHmm)
                .ToList();

            var ibHighYest  = prevIbBars.Count > 0 ? prevIbBars.Max(b => b.High) : double.NaN;
            var ibLowYest   = prevIbBars.Count > 0 ? prevIbBars.Min(b => b.Low)  : double.NaN;
            var ibRangeYest = ValidRange(ibHighYest, ibLowYest);
            var ibFullYest  = ibRangeYest / atr14;

            // --- Sinais de hoje ---

            var firstBar = todayBars.FirstOrDefault(b => HHmm(b.Time) >= SessionOpenHHmm);
            if (firstBar is null) continue;

            var openToday   = firstBar.Open;
            var gapRatio    = Math.Abs(openToday - prevClose) / atr14;
            var openOutside = !double.IsNaN(ibHighYest) && !double.IsNaN(ibLowYest)
                              && (openToday > ibHighYest || openToday < ibLowYest);

            // IB de hoje
            var todayIbBars = todayBars
                .Where(b => HHmm(b.Time) >= SessionOpenHHmm
                         && HHmm(b.Time) <= IbEndHHmm)
                .ToList();

            var ibHighToday  = todayIbBars.Count > 0 ? todayIbBars.Max(b => b.High) : double.NaN;
            var ibLowToday   = todayIbBars.Count > 0 ? todayIbBars.Min(b => b.Low)  : double.NaN;
            var ibRangeToday = ValidRange(ibHighToday, ibLowToday);
            var ibFullToday  = ibRangeToday / atr14;

            // C-period (10:30-10:55) — primeiras 6 barras após o IB
            var cperiodBars = todayBars
                .Where(b => HHmm(b.Time) >= CperiodStartHHmm
                         && HHmm(b.Time) <= CperiodEndHHmm)
                .ToList();

            var cperiodInside = cperiodBars.Count > 0
                && !double.IsNaN(ibHighToday)
                && !double.IsNaN(ibLowToday)
                && cperiodBars.Max(b => b.High) <= ibHighToday
                && cperiodBars.Min(b => b.Low)  >= ibLowToday;

            // Overnight (18:00 ontem → 09:25 hoje)
            var prevOvernightBars = prevBars
                .Where(b => HHmm(b.Time) >= OvernightStartHHmm)
                .ToList();
            var todayOvernightBars = todayBars
                .Where(b => HHmm(b.Time) < SessionOpenHHmm)
                .ToList();
            var allOvernight    = prevOvernightBars.Concat(todayOvernightBars).ToList();
            var overnightRange  = allOvernight.Count > 0
                ? allOvernight.Max(b => b.High) - allOvernight.Min(b => b.Low)
                : 0;
            var overnightRatio = overnightRange / atr14;

            // --- Classificação ---
            var regime = Classify(
                ibFullToday, openOutside, cperiodInside,
                overnightRatio, gapRatio,
                out var reason);

            result.Add(new DayRegime(
                Date:           DateOnly.FromDateTime(today),
                Regime:         regime,
                Reason:         reason,
                IbHighYest:     ibHighYest,
                IbLowYest:      ibLowYest,
                IbFullYest:     ibFullYest,
                IbFullToday:    ibFullToday,
                OpenOutside:    openOutside,
                CperiodInside:  cperiodInside,
                OvernightRatio: overnightRatio,
                GapRatio:       gapRatio,
                Atr14:          atr14));
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Regras IB puras — derivadas empiricamente de 300 dias reais de MNQ
    // -------------------------------------------------------------------------
    private static MarketRegime Classify(
        double ibFullToday,
        bool   openOutside,
        bool   cperiodInside,
        double overnightRatio,
        double gapRatio,
        out string reason)
    {
        // 1. NonTrend — IB zero ou inexistente (feriado, fechamento antecipado, dado faltando)
        if (ibFullToday < 0.05)
        {
            reason = $"NonTrend: ibFull={ibFullToday:F2}";
            return MarketRegime.NonTrend;
        }

        // 2. HighVolatility — expansão overnight extrema (independente de direção)
        if (overnightRatio > 2.0)
        {
            reason = $"HighVol: overnightRatio={overnightRatio:F2}";
            return MarketRegime.HighVolatility;
        }

        // 3. Breakout — rejeita o valor de ontem + catalisador externo forte
        //    Gap alto OU overnight alto indicam participantes institucionais
        //    forçando o preço fora do equilíbrio anterior com momentum
        if (openOutside && (overnightRatio > 1.0 || gapRatio > 0.40))
        {
            reason = $"Breakout: openOutside=true overnight={overnightRatio:F2} gap={gapRatio:F2}";
            return MarketRegime.Breakout;
        }

        // 4. Trend — rejeita o valor de ontem, movimento orgânico sem catalisador extremo
        //    Open Outside + IB comprimido + sem expansão overnight = dia direcional sequencial
        if (openOutside && overnightRatio <= 1.0 && gapRatio <= 0.40 && ibFullToday <= 0.75)
        {
            reason = $"Trend: openOutside=true ibFull={ibFullToday:F2} overnight={overnightRatio:F2} gap={gapRatio:F2}";
            return MarketRegime.Trend;
        }

        // 5. Range — mercado aceita o valor (não rejeita o IB de ontem)
        //    C-period dentro do IB de hoje confirma que a amplitude de abertura
        //    foi absorvida sem expansão direcional
        if (!openOutside && cperiodInside)
        {
            reason = $"Range: openOutside=false cperiodInside=true ibFull={ibFullToday:F2}";
            return MarketRegime.Range;
        }

        // 6. Undefined — sinal ambíguo, não operar
        reason = $"Undefined: openOut={openOutside} ibFull={ibFullToday:F2} gap={gapRatio:F2} overnight={overnightRatio:F2} cperiod={cperiodInside}";
        return MarketRegime.Undefined;
    }

    // -------------------------------------------------------------------------
    // ATR14 diário — igual ao anterior, sem mudança
    // -------------------------------------------------------------------------
    private static Dictionary<DateTime, double> ComputeDailyAtr14(
        IReadOnlyList<IGrouping<DateTime, MarketBar>> byDate)
    {
        var result     = new Dictionary<DateTime, double>();
        const int period = 14;
        double prevClose = double.NaN;
        DateTime? prevDate = null;

        foreach (var group in byDate)
        {
            var sessionBars = group
                .Where(b => HHmm(b.Time) >= SessionOpenHHmm
                         && HHmm(b.Time) <= SessionCloseHHmm)
                .OrderBy(b => b.Time)
                .ToList();

            if (sessionBars.Count == 0) continue;

            var dayHigh  = sessionBars.Max(b => b.High);
            var dayLow   = sessionBars.Min(b => b.Low);
            var dayClose = sessionBars.Last().Close;

            if (!double.IsNaN(prevClose))
            {
                var tr = Math.Max(dayHigh - dayLow,
                         Math.Max(Math.Abs(dayHigh - prevClose),
                                  Math.Abs(dayLow  - prevClose)));

                if (result.Count == 0 || !result.ContainsKey(prevDate!.Value))
                {
                    // Ainda acumulando — não temos ATR14 ainda
                }

                var histCount = result.Count;
                if (histCount == 0)
                {
                    // Primeiro TR — guardar para iniciar a média
                    result[group.Key] = tr;
                }
                else if (histCount < period)
                {
                    // Ainda acumulando — média simples
                    var prevAtr = result[prevDate!.Value];
                    result[group.Key] = (prevAtr * histCount + tr) / (histCount + 1);
                }
                else
                {
                    // EMA-style ATR14
                    var prevAtr = result[prevDate!.Value];
                    result[group.Key] = (prevAtr * (period - 1) + tr) / period;
                }
            }

            prevClose = dayClose;
            prevDate  = group.Key;
        }

        return result;
    }

    private static double ValidRange(double high, double low)
        => !double.IsNaN(high) && !double.IsNaN(low) && high > low ? high - low : 0;

    private static int HHmm(DateTime time) => time.Hour * 100 + time.Minute;
}
```

> **Atenção:** O método `ComputeDailyAtr14` foi levemente corrigido — a implementação original tinha um bug sutil na contagem de histórico para inicializar a média simples antes do período 14. O novo código usa `result.Count` como proxy. Se o comportamento atual do ATR14 for satisfatório e você não quiser risco de regressão, mantenha o método original exatamente como está e apenas remova o `ComputeDailyKer`.

---

## FASE 3 — Remover referências ao campo `Ker`

### Busca global por `\.Ker` e `\.KER` em todo o projeto

Execute no terminal:
```bash
grep -rn "\.Ker\b\|\.KER\b\|kerByDate\|ComputeDailyKer\|DayRegime.*ker\|ker:" \
  src/ tests/ probes/ --include="*.cs"
```

Para cada ocorrência encontrada:
- `RegimeReportWriter.cs` — substitua `regime.Ker` pelo sinal IB equivalente ou remova a coluna da tabela HTML
- Testes que usam `DayRegime(... ker: ...)` — atualize para o novo record
- Qualquer `DayRegime` construído manualmente nos testes — atualize para os novos campos

---

## FASE 4 — Atualizar `RegimeReportWriter`

### Arquivo: `src/TradingBrain.Console/RegimeReportWriter.cs`

O relatório atual já computa `openOutside`, `ibFull`, `cperiodInside` diretamente dos bars. Após esta implementação, esses valores estão em `DayRegime` — leia de lá.

**Tabela de distribuição de regimes (seção 1):** substitua a coluna `KER avg` (se existir) pelas colunas:
```
IbFull avg | Gap avg | Overnight avg | Open Outside% | C-period Inside%
```
Esses campos já existem no novo `DayRegime`.

**Tabela de Undefined (seção 3):** a classificação de clusters (`OpenOutside+NarrowIB`, etc.) pode ser derivada diretamente de `DayRegime.OpenOutside`, `DayRegime.IbFullToday`, `DayRegime.CperiodInside`.

**Não altere o formato HTML** — apenas a fonte dos dados.

---

## FASE 5 — Atualizar `StrategyRegimeMap`

### Arquivo: `src/TradingBrain.Core/StrategyRegimeMap.cs`

Com a correção do classificador, o mapeamento precisa refletir a nova realidade onde Trend terá ~140 dias (era 32). Aplique as seguintes mudanças:

```csharp
// Trend strategy (Donchian) — funciona em Trend puro e em Breakout com continuação
[StrategyKind.Trend] = new[] { MarketRegime.Trend, MarketRegime.Breakout },

// SchoolRun — apenas Trend puro (candle de referência M15 precisa de direção sequencial)
[StrategyKind.SchoolRun] = new[] { MarketRegime.Trend },

// OrbBreakout — Breakout + Trend (gap + expansão OU direção sequencial)
[StrategyKind.OrbBreakout] = new[] { MarketRegime.Breakout, MarketRegime.Trend },
```

Os demais mapeamentos permanecem inalterados.

---

## FASE 6 — Testes

### 6.1 — Verificar testes existentes que constroem `DayRegime` manualmente

```bash
grep -rn "new DayRegime\|DayRegime(" tests/ --include="*.cs"
```

Atualize todos os construtores para o novo record. O record tem campos nomeados — use named arguments para clareza.

### 6.2 — Criar `tests/TradingBrain.Tests/RegimeClassifierPureIbTests.cs`

```csharp
using TradingBrain.Core;

namespace TradingBrain.Tests;

public class RegimeClassifierPureIbTests
{
    // Fabrica dias de mercado com perfil controlado
    private static List<MarketBar> MakeDays(
        int warmupDays,
        double ibHighYest, double ibLowYest, double prevClose,
        double openToday,
        double ibHighToday, double ibLowToday,
        double cperiodHigh, double cperiodLow,
        double overnightHigh, double overnightLow)
    {
        var bars = new List<MarketBar>();
        var baseDate = new DateTime(2026, 1, 5); // segunda-feira

        // Warmup: 20 dias normais para ATR14 convergir
        for (var i = -warmupDays; i < 0; i++)
        {
            var d = baseDate.AddDays(i);
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            AddSessionBars(bars, d, 21000, 21100, 20900, 21050, 1000);
        }

        // Dia "ontem" — IB e close controlados
        AddIbBars(bars,     baseDate, ibHighYest, ibLowYest, 1000);
        AddSessionTail(bars, baseDate, ibHighYest, ibLowYest, prevClose, 800);

        // Overnight de hoje
        if (overnightHigh > 0)
            AddOvernightBars(bars, baseDate, overnightHigh, overnightLow);

        // Dia "hoje" — open, IB e C-period controlados
        var today = baseDate.AddDays(1);
        while (today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) today = today.AddDays(1);

        AddOpenBar(bars,    today, openToday);
        AddIbBars(bars,     today, ibHighToday, ibLowToday, 1000);
        AddCperiodBars(bars, today, cperiodHigh, cperiodLow, 800);
        AddSessionTail(bars, today, ibHighToday + 5, ibLowToday - 5, (ibHighToday + ibLowToday) / 2, 600);

        return bars;
    }

    // ---- helpers ----
    private static void AddSessionBars(List<MarketBar> bars, DateTime date,
        double open, double high, double low, double close, long vol)
    {
        for (var min = 0; min <= 390; min += 5)
        {
            var t = date.AddHours(9).AddMinutes(30 + min);
            bars.Add(new MarketBar(t, open, high, low, close, vol));
        }
    }
    private static void AddIbBars(List<MarketBar> bars, DateTime date,
        double high, double low, long vol)
    {
        for (var min = 0; min <= 55; min += 5)
        {
            var t = date.AddHours(9).AddMinutes(30 + min);
            bars.Add(new MarketBar(t, (high + low) / 2, high, low, (high + low) / 2, vol));
        }
    }
    private static void AddSessionTail(List<MarketBar> bars, DateTime date,
        double high, double low, double close, long vol)
    {
        for (var min = 60; min <= 390; min += 5)
        {
            var t = date.AddHours(9).AddMinutes(30 + min);
            bars.Add(new MarketBar(t, (high + low) / 2, high, low, close, vol));
        }
    }
    private static void AddOpenBar(List<MarketBar> bars, DateTime date, double open)
        => bars.Add(new MarketBar(date.AddHours(9).AddMinutes(30),
            open, open + 2, open - 2, open, 1500));
    private static void AddCperiodBars(List<MarketBar> bars, DateTime date,
        double high, double low, long vol)
    {
        for (var min = 60; min <= 85; min += 5)
        {
            var t = date.AddHours(9).AddMinutes(30 + min);
            bars.Add(new MarketBar(t, (high + low) / 2, high, low, (high + low) / 2, vol));
        }
    }
    private static void AddOvernightBars(List<MarketBar> bars, DateTime date,
        double high, double low)
    {
        for (var min = 0; min <= 55; min += 5)
        {
            var t = date.AddHours(18).AddMinutes(min);
            bars.Add(new MarketBar(t, (high + low) / 2, high, low, (high + low) / 2, 200));
        }
    }

    // ---- cenários ----

    [Fact]
    public void Classify_OpenOutside_LowOvernight_LowGap_ReturnsTrend()
    {
        // Open acima do IB de ontem, overnight e gap moderados → Trend
        var bars = MakeDays(20,
            ibHighYest: 21100, ibLowYest: 21000, prevClose: 21080,
            openToday:  21120,                          // open fora do IB (acima)
            ibHighToday: 21130, ibLowToday: 21115,      // IB de hoje estreito
            cperiodHigh: 21128, cperiodLow: 21116,      // C-period dentro do IB de hoje
            overnightHigh: 21125, overnightLow: 21080); // overnight moderado

        var results = RegimeClassifier.Classify(bars);
        var last = results.LastOrDefault();

        Assert.NotNull(last);
        Assert.True(last.OpenOutside, "OpenOutside deveria ser true");
        Assert.Equal(MarketRegime.Trend, last.Regime);
    }

    [Fact]
    public void Classify_OpenOutside_HighOvernight_ReturnsBreakout()
    {
        // Open fora do IB + overnight alto → Breakout
        var bars = MakeDays(20,
            ibHighYest: 21100, ibLowYest: 21000, prevClose: 21050,
            openToday:  21160,                           // gap grande
            ibHighToday: 21180, ibLowToday: 21150,
            cperiodHigh: 21178, cperiodLow: 21152,
            overnightHigh: 21200, overnightLow: 21050);  // overnight alto > 1.0x ATR

        var results = RegimeClassifier.Classify(bars);
        var last = results.LastOrDefault();

        Assert.NotNull(last);
        Assert.Equal(MarketRegime.Breakout, last.Regime);
    }

    [Fact]
    public void Classify_OpenInside_CperiodInside_ReturnsRange()
    {
        // Open dentro do IB de ontem + C-period dentro do IB de hoje → Range
        var bars = MakeDays(20,
            ibHighYest: 21100, ibLowYest: 21000, prevClose: 21050,
            openToday:  21055,                           // open DENTRO do IB de ontem
            ibHighToday: 21080, ibLowToday: 21020,
            cperiodHigh: 21075, cperiodLow: 21025,       // C-period dentro do IB de hoje
            overnightHigh: 21060, overnightLow: 21040);

        var results = RegimeClassifier.Classify(bars);
        var last = results.LastOrDefault();

        Assert.NotNull(last);
        Assert.False(last.OpenOutside, "OpenOutside deveria ser false");
        Assert.True(last.CperiodInside, "CperiodInside deveria ser true");
        Assert.Equal(MarketRegime.Range, last.Regime);
    }

    [Fact]
    public void Classify_IbNearZero_ReturnsNonTrend()
    {
        // IB de hoje próximo a zero → NonTrend (feriado / dado faltando)
        var bars = MakeDays(20,
            ibHighYest: 21100, ibLowYest: 21000, prevClose: 21050,
            openToday:  21051,
            ibHighToday: 21052, ibLowToday: 21051,       // IB de 1 ponto — quase zero
            cperiodHigh: 21052, cperiodLow: 21051,
            overnightHigh: 21055, overnightLow: 21048);

        var results = RegimeClassifier.Classify(bars);
        var last = results.LastOrDefault();

        Assert.NotNull(last);
        Assert.Equal(MarketRegime.NonTrend, last.Regime);
    }

    [Fact]
    public void Classify_ExtremeOvernight_ReturnsHighVolatility()
    {
        // Overnight range extremo → HighVolatility
        var bars = MakeDays(20,
            ibHighYest: 21100, ibLowYest: 21000, prevClose: 21050,
            openToday:  21300,
            ibHighToday: 21350, ibLowToday: 21250,
            cperiodHigh: 21340, cperiodLow: 21260,
            overnightHigh: 21500, overnightLow: 21000);   // range de 500 pts overnight

        var results = RegimeClassifier.Classify(bars);
        var last = results.LastOrDefault();

        Assert.NotNull(last);
        Assert.Equal(MarketRegime.HighVolatility, last.Regime);
    }

    [Fact]
    public void Classify_DayRegime_HasAllIbFields()
    {
        var bars = MakeDays(20,
            ibHighYest: 21100, ibLowYest: 21000, prevClose: 21080,
            openToday:  21120, ibHighToday: 21130, ibLowToday: 21115,
            cperiodHigh: 21128, cperiodLow: 21116,
            overnightHigh: 21125, overnightLow: 21080);

        var results = RegimeClassifier.Classify(bars);
        var last = results.LastOrDefault();

        Assert.NotNull(last);
        Assert.True(last.Atr14 > 0,               "Atr14 deve ser positivo");
        Assert.False(double.IsNaN(last.IbFullToday), "IbFullToday não deve ser NaN");
        Assert.False(double.IsNaN(last.IbFullYest),  "IbFullYest não deve ser NaN");
        Assert.False(double.IsNaN(last.IbHighYest),  "IbHighYest não deve ser NaN");
        Assert.False(double.IsNaN(last.IbLowYest),   "IbLowYest não deve ser NaN");
    }

    [Fact]
    public void Classify_KerFieldDoesNotExist()
    {
        // Garante que o campo Ker foi removido do DayRegime
        var props = typeof(DayRegime).GetProperties()
            .Select(p => p.Name)
            .ToList();
        Assert.DoesNotContain("Ker", props);
    }
}
```

---

## FASE 7 — Verificação e diagnóstico

```bash
# 1. Build — deve compilar sem referências a Ker ou ComputeDailyKer
dotnet build ./TradingBrain.slnx

# 2. Testes — 84 existentes + 7 novos = 91 esperado
dotnet test ./TradingBrain.slnx

# 3. Novo relatório de regime — comparar com o baseline
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --regime-report outputs/tv-bars/mnq_5m_12mo.csv outputs/regime-report-ib/ \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 4. Grid IbBreakout — deve encontrar trades agora com mais dias Breakout+Trend
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --grid-search outputs/tv-bars/mnq_5m_12mo.csv outputs/grid-ib-ib/ IbBreakout \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 5. Grid SchoolRun
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --grid-search outputs/tv-bars/mnq_5m_12mo.csv outputs/grid-school-ib/ SchoolRun \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 6. Novo relatório de regime após grid — ver distribuição
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --regime-report outputs/tv-bars/mnq_5m_12mo.csv outputs/regime-report-ib/ \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
```

### Distribuição esperada após o classificador IB puro

| Regime | Antes (KER) | Esperado (IB puro) |
|---|---|---|
| Trend | 32 (10.7%) | ~140-150 (47-50%) |
| Breakout | 49 (16.3%) | ~52 (17%) |
| Range | 30 (10.0%) | ~38-42 (13%) |
| HighVolatility | 12 (4.0%) | ~12 (4%) |
| NonTrend | 20 (6.7%) | ~20 (7%) |
| **Undefined** | **157 (52.3%)** | **~20-30 (7-10%)** |

### Se Undefined ainda > 50 após o fix

Os thresholds são ajustáveis. Experimente na ordem:
- Regra Trend: aumentar `ibFullToday <= 0.75` para `<= 0.90`
- Regra Breakout: reduzir `overnightRatio > 1.0` para `> 0.85`
- Regra Range: remover o requisito de `CperiodInside` e usar apenas `!openOutside`

Regenere o relatório após cada ajuste e compare a seção de clusters Undefined.

---

## Restrições

- **`ComputeDailyKer` é deletado** — não deve existir nenhuma referência a KER no código após este commit
- **`DayRegime` é substituído** — qualquer teste que constrói `DayRegime` manualmente deve ser atualizado com os novos campos nomeados
- **ATR14 diário permanece** — `ComputeDailyAtr14` não muda (ou usa a versão corrigida acima)
- **84 testes existentes** devem continuar passando
- **Não altere a lógica de nenhuma strategy** — apenas o classificador e o mapeamento de regime

---

## Arquivos a criar/modificar

| Arquivo | Ação |
|---|---|
| `src/TradingBrain.Core/MarketRegime.cs` | Substituir `DayRegime` — novos campos IB, remover `Ker`, `RangeRatio`, `ClosePosition` |
| `src/TradingBrain.Core/RegimeClassifier.cs` | Reescrever completo — KER removido, 5 regras IB puras |
| `src/TradingBrain.Core/StrategyRegimeMap.cs` | Atualizar: `Trend → [Trend, Breakout]`, `OrbBreakout → [Breakout, Trend]` |
| `src/TradingBrain.Console/RegimeReportWriter.cs` | Ler campos IB de `DayRegime` em vez de recomputar |
| `tests/TradingBrain.Tests/RegimeClassifierIbTests.cs` | Atualizar construtores de `DayRegime` para novos campos |
| `tests/TradingBrain.Tests/RegimeClassifierPureIbTests.cs` | Criar — 7 testes das 5 regras IB puras |
| `ChatMD/CLAUDE.md` | Atualizar: KER removido, descrição do classificador, contagem de testes |

---

*Gerado em: 2026-05-17 | Base empírica: 300 dias MNQ mar/2025–mai/2026 | Undefined esperado cair de 52.3% para ~7-10%*

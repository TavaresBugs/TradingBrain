# TradingBrain — Prompt de Implementação: Refinamento do RegimeClassifier com IB Empírico

> Repositório: https://github.com/TavaresBugs/TradingBrain  
> Branch alvo: `feat/ib-classifier-refinement` (criar a partir de `main`)  
> Runtime: .NET 10 | Build: `dotnet build ./TradingBrain.slnx` | Testes: `dotnet test ./TradingBrain.slnx`

---

## Contexto e motivação

O `RegimeClassifier` atual usa `ibToday30MinRatio` — o range apenas do A-period (9:30-10:00) dividido pelo ATR14. Isso é incompleto: o Initial Balance canônico (usado em Market Profile e validado empiricamente) usa o range da **primeira hora completa** (A+B periods, 9:30-10:30 ET).

Um estudo empírico de 5.519 dias de ES e NQ (janeiro 2015 – dezembro 2025) confirmou:
- **IB Tier** (Narrow/Normal/Wide/Extreme relativo ao ATR14 do IB completo de 60min) é o preditor mais forte de comportamento de breakout
- **C-period** (10:30-11:00): quando fecha **dentro** do IB → 37.9% dos dias fecham dentro do IB; quando fecha **fora** → 45.5% dos dias atingem 100% de extensão
- **IB Formation** (qual extremo formou primeiro no IB de 60min) prediz a direção preferencial do breakout

Os thresholds citados no estudo (Narrow < 0.5×, Normal 0.5-1.0×, Wide 1.0-1.5×, Extreme > 1.5×) referem-se ao IB de **60 minutos**, não ao de 30 minutos que usamos hoje. Os thresholds do classificador atual precisam ser revisados para o IB completo.

### O que NÃO mudar
- A lógica de Breakout está com resultados satisfatórios (PF 2.18, 69% win rate) — **não alterar os critérios de Breakout**
- A estratégia IbBreakout opera em regimes `Breakout|Trend` — qualquer mudança no Trend precisa manter essa sobreposição
- Os 54+ testes existentes devem continuar passando

### O que mudar
1. Adicionar o IB completo de 60min (`ibTodayFullRatio`) como sinal primário de tier
2. Adicionar C-period close como segundo filtro — especialmente para confirmar/rejeitar Range
3. Adicionar IB Formation (qual extremo formou primeiro) como campo diagnóstico no `DayRegime`
4. Ajustar os thresholds do `ClassifyByIB` para usar o IB completo onde aplicável

---

## FASE 1 — Novos campos em `DayRegime`

### 1.1 — Modificar `src/TradingBrain.Core/MarketRegime.cs`

Adicionar quatro campos ao `DayRegime` (todos com defaults para não quebrar código existente):

```csharp
public sealed record DayRegime(
    DateOnly Date,
    MarketRegime Regime,
    double RangeRatio,
    double ClosePosition,
    double OvernightRatio,
    double GapRatio,
    double Ker,
    string Reason,
    double IbYestHigh = double.NaN,
    double IbYestLow = double.NaN,
    double IbToday30MinRatio = double.NaN,    // mantido — A-period apenas
    bool OpenOutsideIbYest = false,
    bool OneTimeFramingUp = false,
    bool OneTimeFramingDown = false,
    // NOVOS CAMPOS:
    double IbTodayFullRatio = double.NaN,     // IB 60min completo (A+B) / ATR14
    double IbTodayFullHigh = double.NaN,      // IB high 9:30-10:30
    double IbTodayFullLow = double.NaN,       // IB low 9:30-10:30
    bool CperiodAboveIb = false,              // C-period (10:30-11:00) fechou acima do IB high
    bool CperiodBelowIb = false,              // C-period (10:30-11:00) fechou abaixo do IB low
    bool CperiodInsideIb = false,             // C-period fechou dentro do IB (= Range signal)
    bool IbHighFormedFirst = false,           // IB high formou antes do IB low no A+B period
    bool IbLowFormedFirst = false);           // IB low formou antes do IB high no A+B period
```

---

## FASE 2 — Cálculo dos novos sinais em `RegimeClassifier.cs`

### 2.1 — Calcular o IB completo de 60min (A+B periods)

No loop principal do método `Classify`, após o cálculo do `ibToday30MinRatio` existente, adicionar:

```csharp
// IB completo: 9:30-10:30 (A+B periods)
var (ibTodayFullHigh, ibTodayFullLow) = GetIbWindow(todayBars, SessionOpenHHmm, IbEndHHmm);
var ibTodayFullRange = !double.IsNaN(ibTodayFullHigh) && !double.IsNaN(ibTodayFullLow)
    ? ibTodayFullHigh - ibTodayFullLow
    : 0;
var ibTodayFullRatio = atr14 > 0 ? ibTodayFullRange / atr14 : 0;
```

> `IbEndHHmm = 1030` já está definido como constante — use direto.

### 2.2 — Calcular C-period close (10:30-11:00)

```csharp
const int CperiodEndHHmm = 1100;

var cPeriodBars = todayBars
    .Where(b => ToHHmm(b.Time) >= IbEndHHmm && ToHHmm(b.Time) < CperiodEndHHmm)
    .OrderBy(b => b.Time)
    .ToList();

var cPeriodClose = cPeriodBars.Count > 0 ? cPeriodBars.Last().Close : double.NaN;

var cperiodAboveIb = !double.IsNaN(cPeriodClose) && !double.IsNaN(ibTodayFullHigh)
    && cPeriodClose > ibTodayFullHigh;
var cperiodBelowIb = !double.IsNaN(cPeriodClose) && !double.IsNaN(ibTodayFullLow)
    && cPeriodClose < ibTodayFullLow;
var cperiodInsideIb = !double.IsNaN(cPeriodClose)
    && !cperiodAboveIb && !cperiodBelowIb
    && !double.IsNaN(ibTodayFullHigh);
```

### 2.3 — Calcular IB Formation (qual extremo formou primeiro)

```csharp
var ibFormationBars = todayBars
    .Where(b => ToHHmm(b.Time) >= SessionOpenHHmm && ToHHmm(b.Time) < IbEndHHmm)
    .OrderBy(b => b.Time)
    .ToList();

bool ibHighFormedFirst = false;
bool ibLowFormedFirst = false;

if (ibFormationBars.Count > 0 && !double.IsNaN(ibTodayFullHigh) && !double.IsNaN(ibTodayFullLow))
{
    // Encontra o primeiro bar que tocou o IB high e o IB low
    var firstHighBar = ibFormationBars.FirstOrDefault(b => b.High >= ibTodayFullHigh);
    var firstLowBar  = ibFormationBars.FirstOrDefault(b => b.Low  <= ibTodayFullLow);

    if (firstHighBar is not null && firstLowBar is not null)
    {
        ibHighFormedFirst = firstHighBar.Time <= firstLowBar.Time;
        ibLowFormedFirst  = firstLowBar.Time  <  firstHighBar.Time;
    }
    else if (firstHighBar is not null)
    {
        ibHighFormedFirst = true;
    }
    else if (firstLowBar is not null)
    {
        ibLowFormedFirst = true;
    }
}
```

### 2.4 — Passar os novos campos para `ClassifyByIB` e para o `DayRegime`

**Assinatura atualizada de `ClassifyByIB`:**

```csharp
private static MarketRegime ClassifyByIB(
    double ibYestHigh,
    double ibYestLow,
    double openToday,
    double ibToday30MinRatio,
    double ibTodayFullRatio,        // NOVO — usar como sinal primário de tier
    double overnightRatio,
    double gapRatio,
    int otfDirection,
    bool cperiodAboveIb,            // NOVO
    bool cperiodBelowIb,            // NOVO
    bool cperiodInsideIb,           // NOVO
    bool atr14IsFallback,
    out string reason)
```

**Atualizar o `result.Add(...)` no final do loop para incluir os novos campos:**

```csharp
result.Add(new DayRegime(
    DateOnly.FromDateTime(today),
    regime,
    rangeRatio,
    closePosition,
    overnightRatio,
    gapRatio,
    ker,
    reason,
    IbYestHigh: ibYestHigh,
    IbYestLow: ibYestLow,
    IbToday30MinRatio: ibToday30MinRatio,
    OpenOutsideIbYest: openOutside,
    OneTimeFramingUp: otfDirection == 1,
    OneTimeFramingDown: otfDirection == -1,
    IbTodayFullRatio: ibTodayFullRatio,
    IbTodayFullHigh: ibTodayFullHigh,
    IbTodayFullLow: ibTodayFullLow,
    CperiodAboveIb: cperiodAboveIb,
    CperiodBelowIb: cperiodBelowIb,
    CperiodInsideIb: cperiodInsideIb,
    IbHighFormedFirst: ibHighFormedFirst,
    IbLowFormedFirst: ibLowFormedFirst));
```

---

## FASE 3 — Ajustar `ClassifyByIB` com os novos thresholds

### Premissa dos thresholds empíricos (IB de 60min / ATR14)

| Tier   | Ratio       | Comportamento empírico                         |
|--------|-------------|------------------------------------------------|
| Narrow | < 0.50×     | Quebra 98.7% das vezes, extensão mediana 74.8% |
| Normal | 0.50–1.00×  | Comportamento típico                           |
| Wide   | 1.00–1.50×  | Expansão reduzida, mais reversão               |
| Extreme| > 1.50×     | Quebra apenas 66.7%, extensão mediana 22.3%    |

### Lógica atualizada do `ClassifyByIB` (path normal, sem fallback)

```csharp
// 1. HighVolatility — não muda (funcionando)
if (ibTodayFullRatio > 2.0 || overnightRatio > 2.0)
{
    reason = $"HighVol: ibFull={ibTodayFullRatio:F2} overnight={overnightRatio:F2}";
    return MarketRegime.HighVolatility;
}

// 2. NonTrend — não muda (critério conservador correto)
if (ibTodayFullRatio < 0.15 && gapRatio < 0.05 && overnightRatio < 0.80)
{
    reason = $"NonTrend: ibFull={ibTodayFullRatio:F2} gap={gapRatio:F2}";
    return MarketRegime.NonTrend;
}

var ibYestValid = !double.IsNaN(ibYestHigh) && !double.IsNaN(ibYestLow);
var openOutside = ibYestValid && (openToday > ibYestHigh || openToday < ibYestLow);

// 3. Breakout — não muda (resultados satisfatórios, não tocar)
if (overnightRatio > 1.20 || gapRatio > 1.00)
{
    reason = $"Breakout: gap={gapRatio:F2} overnight={overnightRatio:F2}";
    return MarketRegime.Breakout;
}

// 4. Trend — usa IB completo + openOutside ou OTF (antes usava só 30min sem confirmação)
//    Narrow IB (< 0.50× ATR no 60min) + contexto direcional = Trend potencial
if (ibTodayFullRatio < 0.50 && (openOutside || otfDirection != 0))
{
    var otfNote = otfDirection != 0 ? $" otf={otfDirection:+0;-0}" : " otf=0";
    reason = $"Trend: narrowIB ibFull={ibTodayFullRatio:F2} openOut={openOutside}{otfNote}";
    return MarketRegime.Trend;
}

// Trend também com IB normal mas forte confirmação direcional (openOutside + OTF juntos)
if (ibTodayFullRatio is >= 0.50 and < 0.75 && openOutside && otfDirection != 0)
{
    reason = $"Trend: normalIB+confirm ibFull={ibTodayFullRatio:F2} otf={otfDirection:+0;-0}";
    return MarketRegime.Trend;
}

// 5. Range — IB normal/wide + C-period fecha DENTRO do IB (segundo filtro empírico)
//    Empírico: C-period inside → 37.9% fecham dentro do IB (vs 18.8% incondicional)
if (ibTodayFullRatio is >= 0.50 and <= 1.50 && !openOutside && cperiodInsideIb)
{
    reason = $"Range: ibFull={ibTodayFullRatio:F2} cperiodInside openInsideIB";
    return MarketRegime.Range;
}

// Range fallback sem C-period (IB muito comprimido sem catalisador)
if (ibTodayFullRatio is >= 0.24 and < 0.50 && !openOutside && !cperiodAboveIb && !cperiodBelowIb)
{
    reason = $"Range: compressedIB ibFull={ibTodayFullRatio:F2} noCperiodBreakout";
    return MarketRegime.Range;
}

reason = $"Undefined: openOut={openOutside} ibFull={ibTodayFullRatio:F2} gap={gapRatio:F2} cperiodInside={cperiodInsideIb}";
return MarketRegime.Undefined;
```

> **Nota sobre o path `atr14IsFallback`:** mantenha a lógica atual sem alterações. O fallback é usado em dias sem ATR14 calculado (primeiros 14 dias do dataset) — são poucos dias e não afetam os resultados.

---

## FASE 4 — Exportar os novos campos no CSV de regime

No comando `--classify-regime` (em `Program.cs` ou `BacktestReports.cs`), adicionar os novos campos ao CSV de saída:

```
Date,Regime,RangeRatio,OvernightRatio,GapRatio,IbToday30MinRatio,IbTodayFullRatio,CperiodAboveIb,CperiodBelowIb,CperiodInsideIb,IbHighFormedFirst,IbLowFormedFirst,OpenOutsideIbYest,OTFUp,OTFDown,Reason
```

Isso permite validar a distribuição dos novos sinais antes de confiar neles para o backtest.

---

## FASE 5 — Testes

### 5.1 — Criar `tests/TradingBrain.Tests/RegimeClassifierIbRefinementTests.cs`

```csharp
using TradingBrain.Core;

namespace TradingBrain.Tests;

public class RegimeClassifierIbRefinementTests
{
    // Helper: cria barras de um dia com controle sobre IB e C-period
    private static List<MarketBar> MakeDayBars(
        DateTime date,
        double basePrice,
        double ibRange,        // range total do IB de 60min
        double cperiodClose,   // close do C-period (10:30-11:00)
        bool ibHighFirst = true)
    {
        var bars = new List<MarketBar>();
        // A-period 9:30-10:00: forma metade do IB
        var aPeriodHigh = ibHighFirst ? basePrice + ibRange * 0.6 : basePrice + ibRange * 0.4;
        var aPeriodLow  = ibHighFirst ? basePrice - ibRange * 0.4 : basePrice - ibRange * 0.6;
        for (var m = 930; m < 1000; m += 5)
        {
            var t = new DateTime(date.Year, date.Month, date.Day, m / 100, m % 100, 0);
            bars.Add(new MarketBar(t, basePrice, aPeriodHigh, aPeriodLow, basePrice, 1000));
        }
        // B-period 10:00-10:30: completa o IB
        var ibHigh = basePrice + ibRange / 2;
        var ibLow  = basePrice - ibRange / 2;
        for (var m = 1000; m < 1030; m += 5)
        {
            var t = new DateTime(date.Year, date.Month, date.Day, m / 100, m % 100, 0);
            bars.Add(new MarketBar(t, basePrice, ibHigh, ibLow, basePrice, 1000));
        }
        // C-period 10:30-11:00: fecha em cperiodClose
        for (var m = 1030; m < 1100; m += 5)
        {
            var t = new DateTime(date.Year, date.Month, date.Day, m / 100, m % 100, 0);
            bars.Add(new MarketBar(t, cperiodClose, cperiodClose, cperiodClose, cperiodClose, 500));
        }
        // Resto do dia: preço flat em cperiodClose
        for (var m = 1100; m <= 1600; m += 5)
        {
            var t = new DateTime(date.Year, date.Month, date.Day, m / 100, m % 100, 0);
            bars.Add(new MarketBar(t, cperiodClose, cperiodClose + 1, cperiodClose - 1, cperiodClose, 300));
        }
        return bars;
    }

    [Fact]
    public void DayRegime_HasIbTodayFullRatio_Field()
    {
        // Garante que o record DayRegime tem os novos campos
        var regime = new DayRegime(
            DateOnly.FromDateTime(DateTime.Today),
            MarketRegime.Range,
            1.0, 0.5, 0.3, 0.1, double.NaN, "test",
            IbTodayFullRatio: 0.75,
            CperiodInsideIb: true,
            IbHighFormedFirst: true);

        Assert.Equal(0.75, regime.IbTodayFullRatio);
        Assert.True(regime.CperiodInsideIb);
        Assert.True(regime.IbHighFormedFirst);
    }

    [Fact]
    public void Classify_NarrowIbWithOtf_ShouldBeTrend()
    {
        // IB estreito (< 0.5× ATR) com one-timeframing → Trend
        // Constrói 20 dias para ter ATR14 disponível
        var bars = new List<MarketBar>();
        var start = new DateTime(2026, 1, 2);
        double price = 21000;
        double atr = 200; // ATR14 sintético

        for (var d = 0; d < 20; d++)
        {
            var date = start.AddDays(d);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            // Dias anteriores: IB normal (0.8× ATR = 160 pts)
            bars.AddRange(MakeDayBars(date, price, atr * 0.8, price));
        }

        // Último dia: IB estreito (0.35× ATR = 70 pts) com C-period acima do IB
        var lastDate = start.AddDays(20);
        while (lastDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            lastDate = lastDate.AddDays(1);

        var narrowIbRange = atr * 0.35; // < 0.5× ATR → Narrow tier
        var cperiodCloseAboveIb = price + narrowIbRange / 2 + 10; // acima do IB high
        bars.AddRange(MakeDayBars(lastDate, price, narrowIbRange, cperiodCloseAboveIb));

        var regimes = RegimeClassifier.Classify(bars);
        var lastRegime = regimes.Last();

        // IB estreito deve classificar como Trend (com OTF ou openOutside)
        // Aceita Trend ou Breakout — ambos são válidos com IB estreito
        Assert.True(
            lastRegime.Regime is MarketRegime.Trend or MarketRegime.Breakout,
            $"Esperado Trend ou Breakout, obtido: {lastRegime.Regime} — {lastRegime.Reason}");
        Assert.True(lastRegime.IbTodayFullRatio < 0.50,
            $"IbTodayFullRatio deveria ser < 0.5, foi: {lastRegime.IbTodayFullRatio:F3}");
    }

    [Fact]
    public void Classify_NormalIbWithCperiodInside_ShouldBeRange()
    {
        // IB normal (0.7× ATR) + C-period fecha DENTRO do IB → Range
        var bars = new List<MarketBar>();
        var start = new DateTime(2026, 1, 2);
        double price = 21000;
        double atr = 200;

        for (var d = 0; d < 20; d++)
        {
            var date = start.AddDays(d);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            bars.AddRange(MakeDayBars(date, price, atr * 0.7, price));
        }

        var lastDate = start.AddDays(20);
        while (lastDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            lastDate = lastDate.AddDays(1);

        // IB normal, C-period fecha dentro do IB (em price = midpoint)
        bars.AddRange(MakeDayBars(lastDate, price, atr * 0.7, price)); // cperiodClose = price = dentro do IB

        var regimes = RegimeClassifier.Classify(bars);
        var lastRegime = regimes.Last();

        Assert.True(lastRegime.CperiodInsideIb,
            "C-period deveria estar dentro do IB");
        Assert.Equal(MarketRegime.Range, lastRegime.Regime);
    }

    [Fact]
    public void Classify_PopulatesCperiodFields_Correctly()
    {
        // Garante que os campos C-period são preenchidos no DayRegime
        var bars = new List<MarketBar>();
        var start = new DateTime(2026, 1, 2);
        double price = 21000;
        double atr = 200;

        for (var d = 0; d < 20; d++)
        {
            var date = start.AddDays(d);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            bars.AddRange(MakeDayBars(date, price, atr * 0.8, price));
        }

        var regimes = RegimeClassifier.Classify(bars);

        // Todos os regimes válidos devem ter IbTodayFullRatio preenchido
        foreach (var r in regimes)
        {
            Assert.False(double.IsNaN(r.IbTodayFullRatio),
                $"IbTodayFullRatio não deveria ser NaN para {r.Date}");
        }
    }

    [Fact]
    public void Classify_IbFormation_HighFirstDetected()
    {
        // Quando o IB high forma antes do IB low, IbHighFormedFirst = true
        var bars = new List<MarketBar>();
        var start = new DateTime(2026, 1, 2);
        double price = 21000;
        double atr = 200;

        for (var d = 0; d < 20; d++)
        {
            var date = start.AddDays(d);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            bars.AddRange(MakeDayBars(date, price, atr * 0.8, price, ibHighFirst: true));
        }

        var lastDate = start.AddDays(20);
        while (lastDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            lastDate = lastDate.AddDays(1);

        bars.AddRange(MakeDayBars(lastDate, price, atr * 0.8, price, ibHighFirst: true));

        var regimes = RegimeClassifier.Classify(bars);
        var lastRegime = regimes.Last();

        Assert.True(lastRegime.IbHighFormedFirst,
            "IbHighFormedFirst deveria ser true quando o high forma primeiro");
        Assert.False(lastRegime.IbLowFormedFirst,
            "IbLowFormedFirst deveria ser false quando o high forma primeiro");
    }
}
```

---

## FASE 6 — Verificação

Execute na ordem:

```bash
# 1. Build
dotnet build ./TradingBrain.slnx

# 2. Todos os testes
dotnet test ./TradingBrain.slnx

# 3. Classificar regime com novos sinais — inspecionar CSV de saída
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --classify-regime outputs/tv-bars/mnq_5m_12mo.csv outputs/regime-refined/ \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 4. Backtest Trend com regime refinado
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --backtest-regime outputs/tv-bars/mnq_5m_12mo.csv ./outputs/regime-trend-v2 Trend \
  --params-from outputs/grid-all-regimes/trend.grid.csv \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 5. Backtest IbBreakout com regime refinado
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --backtest-regime outputs/tv-bars/mnq_5m_12mo.csv ./outputs/regime-ib-v2 IbBreakout \
  --params-from outputs/grid-all-regimes/ibbreakout.grid.csv \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
```

### O que comparar após os comandos 4 e 5

Criar tabela comparativa antes/depois:

| Métrica | Trend v1 | Trend v2 | IbBreakout v1 | IbBreakout v2 |
|---|---|---|---|---|
| Dias no regime | 47 | ? | 84 | ? |
| Trades fechados | 33 | ? | 71 | ? |
| Win rate | 57.6% | ? | 69.0% | ? |
| Profit Factor | 3.47 | ? | 2.18 | ? |
| Net PnL pts | 2744 | ? | 2932 | ? |
| Max Drawdown | 548.5 | ? | 550.5 | ? |

**Critérios de aceite do refinamento:**
- Profit Factor mantém ou melhora
- Número de trades cai (esperado — filtro mais restritivo), mas não abaixo de 20
- Meses ruins (Abr-Mai 2026 para Trend, Jan 2026 para ambos) têm menos trades perdedores

**Critério de rejeição:**
- Se o número de dias no regime cair abaixo de 30 para Trend → thresholds muito restritivos, revisar
- Se o PF cair abaixo de 2.0 para Trend → o filtro está excluindo dias bons junto com os ruins

---

## Resumo dos arquivos a modificar

| Arquivo | Ação |
|---|---|
| `src/TradingBrain.Core/MarketRegime.cs` | Adicionar 7 campos ao `DayRegime` |
| `src/TradingBrain.Core/RegimeClassifier.cs` | Calcular IbFull, C-period, IbFormation; atualizar `ClassifyByIB` |
| `tests/TradingBrain.Tests/RegimeClassifierIbRefinementTests.cs` | Criar — 4 testes novos |

Não modificar: `StrategyRules.cs`, `StrategyBacktester.cs`, `GridSearchRunner.cs`, `IbBreakoutStrategyTests.cs`.

---

*Gerado em: 2026-05-17 | Base empírica: tradingstats.net — 5.519 dias ES/NQ (2015-2025)*
*Próximo passo após validação: avaliar segundo sinal independente (além do IB) para confluência de regimes*

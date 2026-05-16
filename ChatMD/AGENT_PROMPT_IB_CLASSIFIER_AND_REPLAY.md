# TradingBrain — Prompt de Implementação para Agente

> Repositório: https://github.com/TavaresBugs/TradingBrain  
> Branch alvo: `main` (ou crie `feat/ib-classifier-and-replay`)  
> Runtime: .NET 10 | Build: `dotnet build ./TradingBrain.slnx` | Testes: `dotnet test ./TradingBrain.slnx`

---

## Contexto rápido

TradingBrain é um engine de backtest em C# para MNQ (Micro Nasdaq Futures). Ele processa barras de 5min de CSV e roda strategies. O classificador de regime atual usa KER (Kaufman Efficiency Ratio) e foi validado como **preditor ruim** — dias classificados como "Range" têm directionality maior que dias "Trend", o que é incoerente.

O objetivo desta sessão é:
1. **Substituir o RegimeClassifier por um baseado em Initial Balance (IB)**
2. **Adicionar o comando `--replay` para auditoria visual de trades**
3. **Garantir que todos os 54 testes existentes continuem passando** após as mudanças

Não remova nenhuma funcionalidade existente. Apenas adicione e substitua o que está especificado.

---

## FASE 1 — IB-based RegimeClassifier

### 1.1 — Expandir `DayRegime` em `MarketRegime.cs`

Arquivo: `src/TradingBrain.Core/MarketRegime.cs`

Adicione os novos campos ao record `DayRegime` sem quebrar a assinatura existente (adicione ao final com valores default para compatibilidade):

```csharp
public enum MarketRegime
{
    Undefined,
    Trend,
    Breakout,
    Range,
    HighVolatility,
    NonTrend   // NOVO: IB muito estreito sem catalisador — ficar fora
}

public sealed record DayRegime(
    DateOnly Date,
    MarketRegime Regime,
    double RangeRatio,
    double ClosePosition,
    double OvernightRatio,
    double GapRatio,
    double Ker,
    string Reason,
    // NOVOS campos IB:
    double IbYestHigh = double.NaN,
    double IbYestLow = double.NaN,
    double IbToday30MinRatio = double.NaN,  // ibRange30min / ATR14
    bool OpenOutsideIbYest = false,
    bool OneTimeFramingUp = false,
    bool OneTimeFramingDown = false);
```

### 1.2 — Reescrever `RegimeClassifier.cs`

Arquivo: `src/TradingBrain.Core/RegimeClassifier.cs`

Mantenha a assinatura pública `public static IReadOnlyList<DayRegime> Classify(IReadOnlyList<MarketBar> bars)` intacta.

Substitua **apenas** a lógica interna de classificação pelo IB classifier abaixo. O cálculo de ATR14 e KER existentes devem ser mantidos (KER continua sendo exportado no campo `Ker` para diagnóstico, mas não é mais o sinal primário).

**Constantes de tempo necessárias** (adicionar/ajustar nas constantes da classe):
```csharp
private const int SessionOpenHHmm   = 930;
private const int SessionCloseHHmm  = 1600;
private const int IbEndHHmm         = 1030;  // NOVO: fim do Initial Balance
private const int IbMid30HHmm       = 1000;  // NOVO: IB parcial (primeiros 30min)
private const int OvernightStartHHmm = 1800;
```

**Novo método privado de extração do IB de ontem:**
```csharp
private static (double High, double Low) GetIbWindow(
    IEnumerable<MarketBar> bars, int startHHmm, int endHHmm)
{
    var window = bars
        .Where(b => ToHHmm(b.Time) >= startHHmm && ToHHmm(b.Time) < endHHmm)
        .ToList();
    if (window.Count == 0) return (double.NaN, double.NaN);
    return (window.Max(b => b.High), window.Min(b => b.Low));
}
```

**Novo método privado de verificação de One-Timeframing:**
```csharp
// Verifica se os fechamentos de 5min entre 9:30-10:30 são monotônicos
// Retorna: +1 = OTF de alta, -1 = OTF de baixa, 0 = sem OTF
private static int CheckOneTimeFraming(IEnumerable<MarketBar> bars930to1030)
{
    var closes = bars930to1030
        .Where(b => ToHHmm(b.Time) >= SessionOpenHHmm && ToHHmm(b.Time) <= IbEndHHmm)
        .OrderBy(b => b.Time)
        .Select(b => b.Close)
        .ToList();

    if (closes.Count < 4) return 0;

    // Usa os últimos 6 fechamentos (ou todos se < 6)
    var sample = closes.TakeLast(Math.Min(closes.Count, 6)).ToList();
    var allUp   = sample.Zip(sample.Skip(1)).All(p => p.Second > p.First);
    var allDown = sample.Zip(sample.Skip(1)).All(p => p.Second < p.First);

    return allUp ? 1 : allDown ? -1 : 0;
}
```

**Nova função de classificação por IB** (substitui o método `Classify` privado atual):
```csharp
private static MarketRegime ClassifyByIB(
    double ibYestHigh,
    double ibYestLow,
    double openToday,
    double ibToday30MinRatio,   // ibRange30min / ATR14
    double overnightRatio,
    double gapRatio,
    int otfDirection,           // +1, -1, 0
    out string reason)
{
    // 1. Alta volatilidade — range extremo (mantém critério anterior)
    if (ibToday30MinRatio > 2.0 || overnightRatio > 2.0)
    {
        reason = $"HighVol: ib30={ibToday30MinRatio:F2} overnight={overnightRatio:F2}";
        return MarketRegime.HighVolatility;
    }

    // 2. Non-Trend — IB estreitíssimo sem catalisador (ficar fora)
    if (ibToday30MinRatio < 0.30 && gapRatio < 0.20 && overnightRatio < 0.80)
    {
        reason = $"NonTrend: ib30={ibToday30MinRatio:F2} gap={gapRatio:F2}";
        return MarketRegime.NonTrend;
    }

    // 3. Breakout — gap forte + IB estreito que segura
    if (gapRatio > 0.50 && ibToday30MinRatio < 0.55)
    {
        reason = $"Breakout: gap={gapRatio:F2} ib30={ibToday30MinRatio:F2}";
        return MarketRegime.Breakout;
    }

    // Abertura fora do IB de ontem?
    var ibYestValid = !double.IsNaN(ibYestHigh) && !double.IsNaN(ibYestLow);
    var openOutside = ibYestValid && (openToday > ibYestHigh || openToday < ibYestLow);

    // 4. Trend Day — abertura fora do IB de ontem + IB estreito + OTF confirma
    if (openOutside && ibToday30MinRatio < 0.60)
    {
        // OTF confirma a direção? Eleva confiança mas não é obrigatório
        var otfNote = otfDirection != 0 ? $" otf={otfDirection:+0;-0}" : " otf=0(unconfirmed)";
        reason = $"Trend: openOutsideIB ib30={ibToday30MinRatio:F2}{otfNote}";
        return MarketRegime.Trend;
    }

    // 5. Range — abertura dentro do IB de ontem + IB de largura normal
    if (!openOutside && ibToday30MinRatio is >= 0.60 and <= 2.00)
    {
        reason = $"Range: openInsideIB ib30={ibToday30MinRatio:F2}";
        return MarketRegime.Range;
    }

    // 6. Abertura dentro do IB mas IB estreito — possível breakout tardio
    if (!openOutside && ibToday30MinRatio < 0.60 && gapRatio > 0.20)
    {
        reason = $"Breakout(lateGap): openInsideIB ib30={ibToday30MinRatio:F2} gap={gapRatio:F2}";
        return MarketRegime.Breakout;
    }

    reason = $"Undefined: openOut={openOutside} ib30={ibToday30MinRatio:F2} gap={gapRatio:F2} overnight={overnightRatio:F2}";
    return MarketRegime.Undefined;
}
```

**Integração no método `Classify` público** — dentro do loop `for (var d = 1; d < byDate.Count; d++)`, após calcular as variáveis existentes, adicione:

```csharp
// Calcular IB de ontem (9:30-10:30)
var (ibYestHigh, ibYestLow) = GetIbWindow(prevBars, SessionOpenHHmm, IbEndHHmm);

// Calcular IB parcial de hoje (9:30-10:00) — 30 primeiros minutos
var todayIb30Bars = todayBars.Where(b => ToHHmm(b.Time) >= SessionOpenHHmm && ToHHmm(b.Time) < IbMid30HHmm).ToList();
var ibToday30High = todayIb30Bars.Count > 0 ? todayIb30Bars.Max(b => b.High) : double.NaN;
var ibToday30Low  = todayIb30Bars.Count > 0 ? todayIb30Bars.Min(b => b.Low)  : double.NaN;
var ibToday30Range = (!double.IsNaN(ibToday30High) && !double.IsNaN(ibToday30Low))
    ? ibToday30High - ibToday30Low : 0;
var ibToday30MinRatio = atr14 > 0 ? ibToday30Range / atr14 : 0;

// One-timeframing usando barras até 10:30
var otfDirection = CheckOneTimeFraming(todayBars);
var openOutside = !double.IsNaN(ibYestHigh) && !double.IsNaN(ibYestLow)
    && firstBar930 is not null
    && (firstBar930.Open > ibYestHigh || firstBar930.Open < ibYestLow);

// Classificar usando IB
var regime = ClassifyByIB(
    ibYestHigh, ibYestLow,
    firstBar930?.Open ?? double.NaN,
    ibToday30MinRatio,
    overnightRatio,
    gapRatio,
    otfDirection,
    out var reason);

// Manter KER para diagnóstico
var ker = kerByDate.TryGetValue(prevDate, out var kerVal) ? kerVal : double.NaN;
```

E ao construir o `DayRegime`, passe os novos campos:
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
    OneTimeFramingDown: otfDirection == -1));
```

### 1.3 — Atualizar exportação do `--classify-regime` em `BacktestReports.cs`

No método que exporta o CSV de regime (procure por "classify" ou "regime" no arquivo), adicione as novas colunas ao cabeçalho e às linhas:
```
IbYestHigh, IbYestLow, IbToday30MinRatio, OpenOutsideIbYest, OTF_Up, OTF_Down
```

### 1.4 — Adicionar `NonTrend` ao `StrategyKind` e `GridSearchRunner` (se necessário)

No `GridSearchRunner.cs`, onde a filtragem de regime acontece (se já existir), adicione `NonTrend` como regime que nunca ativa nenhuma strategy. Se ainda não existir filtro de regime no grid, ignore esta etapa — ela será implementada na Fase 3.

---

## FASE 2 — Comando `--replay`

### 2.1 — Criar `ReplayViewer.cs` em `TradingBrain.Console`

Arquivo: `src/TradingBrain.Console/ReplayViewer.cs`

```csharp
using System.Globalization;
using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

/// <summary>
/// Lê um arquivo signals.csv já gerado e imprime barra a barra
/// o que o sistema viu e decidiu. Útil para auditoria manual de trades.
/// </summary>
public static class ReplayViewer
{
    public static int Run(string signalsPath, int delayMs = 0)
    {
        if (!File.Exists(signalsPath))
        {
            Console.Error.WriteLine($"Arquivo não encontrado: {signalsPath}");
            return 1;
        }

        var lines = File.ReadAllLines(signalsPath);
        if (lines.Length < 2)
        {
            Console.Error.WriteLine("Arquivo vazio ou sem dados.");
            return 1;
        }

        // Parse cabeçalho
        var headers = lines[0].Split(',');
        var idx = BuildIndex(headers);

        Console.Clear();
        Console.WriteLine("=== TradingBrain Replay ===");
        Console.WriteLine($"Arquivo: {Path.GetFileName(signalsPath)}");
        Console.WriteLine($"Total de barras: {lines.Length - 1}");
        Console.WriteLine(new string('─', 70));
        Console.WriteLine();

        var tradeCount = 0;
        var lastPosition = 0;

        for (var i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split(',');
            if (cols.Length < 5) continue;

            var time     = GetStr(cols, idx, "Time");
            var open     = GetDbl(cols, idx, "Open");
            var high     = GetDbl(cols, idx, "High");
            var low      = GetDbl(cols, idx, "Low");
            var close    = GetDbl(cols, idx, "Close");
            var signal   = GetStr(cols, idx, "Signal");
            var reason   = GetStr(cols, idx, "Reason");
            var position = GetInt(cols, idx, "Position");
            var entry    = GetDbl(cols, idx, "EntryPrice");
            var openPnl  = GetDbl(cols, idx, "OpenProfit");
            var equity   = GetDbl(cols, idx, "Equity");
            var atr      = GetDbl(cols, idx, "ATR");
            var rsi      = GetDbl(cols, idx, "RSI");

            // Detecta abertura/fechamento de trade
            var tradeEvent = "";
            if (position != 0 && lastPosition == 0)
            {
                tradeCount++;
                tradeEvent = position > 0
                    ? $"  ◄ ENTRY LONG #{tradeCount} @ {entry:F2}"
                    : $"  ◄ ENTRY SHORT #{tradeCount} @ {entry:F2}";
            }
            else if (position == 0 && lastPosition != 0)
            {
                tradeEvent = $"  ► EXIT | PnL da barra: {openPnl:+0.##;-0.##;0}";
            }

            lastPosition = position;

            // Linha de barra
            var posStr = position switch
            {
                1  => "[LONG ]",
                -1 => "[SHORT]",
                _  => "[FLAT ]"
            };
            var signalStr = signal switch
            {
                "Buy"  => "▲ BUY ",
                "Sell" => "▼ SELL",
                "Exit" => "◆ EXIT",
                _      => "· ----"
            };
            var equityStr = equity >= 0 ? $"+{equity:F2}" : $"{equity:F2}";

            Console.Write($"[{time,-19}] {posStr} {signalStr}");
            Console.Write($"  O={open:F2} H={high:F2} L={low:F2} C={close:F2}");
            Console.Write($"  ATR={atr:F1} RSI={rsi:F1}");
            Console.Write($"  Equity={equityStr}");

            if (!string.IsNullOrWhiteSpace(tradeEvent))
            {
                Console.ForegroundColor = position != 0 ? ConsoleColor.Cyan : ConsoleColor.Yellow;
                Console.Write(tradeEvent);
                Console.ResetColor();
            }

            if (!string.IsNullOrWhiteSpace(reason) && signal != "None")
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [{reason}]");
                Console.ResetColor();
            }

            Console.WriteLine();

            if (delayMs > 0)
            {
                Thread.Sleep(delayMs);
            }
        }

        Console.WriteLine();
        Console.WriteLine(new string('─', 70));
        Console.WriteLine($"Replay concluído. Total de trades abertos: {tradeCount}");
        return 0;
    }

    private static Dictionary<string, int> BuildIndex(string[] headers)
    {
        var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
            idx[headers[i].Trim()] = i;
        return idx;
    }

    private static string GetStr(string[] cols, Dictionary<string, int> idx, string name)
        => idx.TryGetValue(name, out var i) && i < cols.Length ? cols[i].Trim() : "";

    private static double GetDbl(string[] cols, Dictionary<string, int> idx, string name)
        => idx.TryGetValue(name, out var i) && i < cols.Length
            && double.TryParse(cols[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : double.NaN;

    private static int GetInt(string[] cols, Dictionary<string, int> idx, string name)
        => idx.TryGetValue(name, out var i) && i < cols.Length
            && int.TryParse(cols[i], out var v) ? v : 0;
}
```

### 2.2 — Adicionar parsing do `--replay` em `Program.cs`

No `Program.cs`, adicione antes do bloco de `--grid-search`:

```csharp
// --- Replay ---
var replayRequest = ReadReplayRequest(args);
if (replayRequest is not null)
{
    return ReplayViewer.Run(
        replayRequest.Value.SignalsPath,
        replayRequest.Value.DelayMs);
}
```

Adicione a função de parsing no final do arquivo junto às outras funções `Read*`:

```csharp
static (string SignalsPath, int DelayMs)? ReadReplayRequest(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals("--replay", StringComparison.OrdinalIgnoreCase))
            continue;

        if (i + 1 >= args.Length)
            throw new ArgumentException("Use --replay <signals.csv> [--delay <ms>].");

        var signalsPath = args[i + 1];
        var delayMs = ReadIntOption(args, "--delay", 0);
        return (signalsPath, delayMs);
    }
    return null;
}
```

Adicione o uso no `PrintUsage()`:

```csharp
Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --replay <signals.csv> [--delay 200]");
```

---

## FASE 3 — Testes de validação do IB Classifier

### 3.1 — Adicionar testes em `TradingBrain.Tests`

Crie o arquivo `tests/TradingBrain.Tests/RegimeClassifierIbTests.cs`:

```csharp
using TradingBrain.Core;

namespace TradingBrain.Tests;

public class RegimeClassifierIbTests
{
    /// <summary>
    /// Fabrica barras de 5min para um dia inteiro com valores simples.
    /// </summary>
    private static List<MarketBar> MakeDayBars(
        DateTime date,
        double open, double high, double low, double close,
        int startHHmm = 930, int endHHmm = 1600, int stepMin = 5)
    {
        var bars = new List<MarketBar>();
        var current = new DateTime(date.Year, date.Month, date.Day,
            startHHmm / 100, startHHmm % 100, 0);
        var end = new DateTime(date.Year, date.Month, date.Day,
            endHHmm / 100, endHHmm % 100, 0);

        while (current <= end)
        {
            bars.Add(new MarketBar(current, open, high, low, close, 1000));
            current = current.AddMinutes(stepMin);
        }
        return bars;
    }

    [Fact]
    public void Classify_WhenOpenOutsideIBYestAndNarrowIB_ReturnsTrend()
    {
        // Dia 1: IB de ontem com high=21000, low=20900
        var day1 = MakeDayBars(new DateTime(2026, 1, 2), 20950, 21000, 20900, 20980);
        // Dia 2: abre acima do high de ontem (21010), IB estreito hoje
        var day2Open = MakeDayBars(
            new DateTime(2026, 1, 3), 21010, 21020, 21005, 21015,
            startHHmm: 930, endHHmm: 1000);
        var day2Rest = MakeDayBars(
            new DateTime(2026, 1, 3), 21015, 21050, 21010, 21045,
            startHHmm: 1005, endHHmm: 1600);

        var allBars = day1.Concat(day2Open).Concat(day2Rest).ToList();
        var result = RegimeClassifier.Classify(allBars);

        Assert.NotEmpty(result);
        // Com IB estreito e abertura fora do IB, esperamos Trend
        var day2Regime = result.First(r => r.Date == DateOnly.FromDateTime(new DateTime(2026, 1, 3)));
        Assert.Equal(MarketRegime.Trend, day2Regime.Regime);
        Assert.True(day2Regime.OpenOutsideIbYest);
    }

    [Fact]
    public void Classify_WhenOpenInsideIBAndNormalWidth_ReturnsRange()
    {
        // Dia 1: IB wide, high=21100, low=20900
        var day1 = MakeDayBars(new DateTime(2026, 1, 2), 21000, 21100, 20900, 21000);
        // Dia 2: abre dentro do IB (21000), IB de largura normal
        var day2 = MakeDayBars(new DateTime(2026, 1, 3), 21000, 21060, 20940, 21010);

        var allBars = day1.Concat(day2).ToList();
        var result = RegimeClassifier.Classify(allBars);

        Assert.NotEmpty(result);
        var day2Regime = result.First(r => r.Date == DateOnly.FromDateTime(new DateTime(2026, 1, 3)));
        Assert.Equal(MarketRegime.Range, day2Regime.Regime);
        Assert.False(day2Regime.OpenOutsideIbYest);
    }

    [Fact]
    public void Classify_WhenGapLargeAndNarrowIB_ReturnsBreakout()
    {
        // Dia 1: close at 21000
        var day1 = MakeDayBars(new DateTime(2026, 1, 2), 20950, 21050, 20950, 21000);
        // Dia 2: gap de abertura grande (21100 = +100 pts), IB estreito
        var day2 = MakeDayBars(new DateTime(2026, 1, 3), 21100, 21110, 21090, 21105);

        var allBars = day1.Concat(day2).ToList();
        var result = RegimeClassifier.Classify(allBars);

        Assert.NotEmpty(result);
        var day2Regime = result.First(r => r.Date == DateOnly.FromDateTime(new DateTime(2026, 1, 3)));
        Assert.Equal(MarketRegime.Breakout, day2Regime.Regime);
    }

    [Fact]
    public void Classify_NewFields_ArePopulated()
    {
        var day1 = MakeDayBars(new DateTime(2026, 1, 2), 20950, 21050, 20900, 21000);
        var day2 = MakeDayBars(new DateTime(2026, 1, 3), 21000, 21060, 20950, 21010);

        var allBars = day1.Concat(day2).ToList();
        var result = RegimeClassifier.Classify(allBars);

        Assert.NotEmpty(result);
        var day2Regime = result.First(r => r.Date == DateOnly.FromDateTime(new DateTime(2026, 1, 3)));

        // IB de ontem deve ser a janela 9:30-10:30 do dia 1
        Assert.False(double.IsNaN(day2Regime.IbYestHigh));
        Assert.False(double.IsNaN(day2Regime.IbYestLow));
        Assert.True(day2Regime.IbYestHigh >= day2Regime.IbYestLow);

        // IB de hoje (30min) deve ser calculado
        Assert.False(double.IsNaN(day2Regime.IbToday30MinRatio));
        Assert.True(day2Regime.IbToday30MinRatio >= 0);
    }
}
```

---

## FASE 4 — Verificação e sanidade

### Checklist de verificação após implementação

Execute cada passo abaixo e confirme que passa:

```bash
# 1. Build limpo
dotnet build ./TradingBrain.slnx

# 2. Todos os testes passam (incluindo os 54 existentes + novos)
dotnet test ./TradingBrain.slnx

# 3. Classificar regime com o novo classifier
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --classify-regime outputs/tv-bars/mnq_5m_12mo.csv outputs/ib-regime/ \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 4. Checar distribuição no CSV gerado — colunas esperadas:
# Date, Regime, IbYestHigh, IbYestLow, IbToday30MinRatio, OpenOutsideIbYest, OTF_Up, OTF_Down, ...

# 5. Rodar backtest normal para gerar signals.csv
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  outputs/tv-bars/mnq_5m_12mo.csv outputs/replay-test/ --strategy SchoolRun \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# 6. Testar o --replay
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --replay outputs/replay-test/schoolrun.signals.csv --delay 50
```

### Validação de sanidade do IB classifier

Após rodar `--classify-regime`, abra o CSV gerado e calcule:
- Distribuição de regimes (quantidade de dias por tipo)
- `directionality` médio por regime (coluna se existir, ou calcule manualmente)

**Resultado esperado vs. atual (KER):**

| Regime    | KER (atual, errado) | IB (esperado) |
|-----------|---------------------|---------------|
| Trend     | directionality=0.431 (MENOR) | directionality > 0.55 |
| Range     | directionality=0.488 (MAIOR) | directionality < 0.45 |
| Breakout  | directionality=0.465 | directionality > 0.50 |

Se o IB classifier ainda mostrar Range com directionality maior que Trend, os thresholds (0.30, 0.50, 0.55, 0.60) precisam ser ajustados empiricamente nos dados reais.

---

## Restrições importantes

- **Não quebre nenhuma assinatura pública** de `RegimeClassifier.Classify()`, `DayRegime`, `MarketRegime`, `PrecomputedSeries.From()` ou `StrategyBacktester.Run()`
- **Não remova** nenhum valor do enum `MarketRegime` existente — apenas adicione `NonTrend`
- **O campo `Ker`** deve continuar sendo calculado e exportado (diagnóstico)
- **Os 54 testes existentes** devem continuar passando — se quebrarem, corrija sem alterar a lógica de negócio
- **Novos testes** devem ser adicionados em arquivo separado (`RegimeClassifierIbTests.cs`)
- **Não use lookahead** — a classificação de cada dia deve usar apenas dados disponíveis antes das 10:30 ET daquele dia. O IB de hoje usa barras de 9:30 a 10:00 (30min), nunca barras após 10:30.

---

## Arquivos a criar/modificar (resumo)

| Arquivo | Ação |
|---------|------|
| `src/TradingBrain.Core/MarketRegime.cs` | Modificar — adicionar `NonTrend` e campos ao `DayRegime` |
| `src/TradingBrain.Core/RegimeClassifier.cs` | Modificar — substituir lógica interna por IB classifier |
| `src/TradingBrain.Console/BacktestReports.cs` | Modificar — adicionar novas colunas IB no CSV de regime |
| `src/TradingBrain.Console/ReplayViewer.cs` | Criar — viewer barra a barra |
| `src/TradingBrain.Console/Program.cs` | Modificar — adicionar `--replay` parsing e chamada |
| `tests/TradingBrain.Tests/RegimeClassifierIbTests.cs` | Criar — testes do novo classifier |

---

*Gerado em: 2026-05-16 | Próxima sessão deve verificar a sanidade estatística do IB classifier nos dados reais de 82k barras antes de implementar RegimeFilter no GridSearchRunner.*

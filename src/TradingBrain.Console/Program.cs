using System.Globalization;
using TradingBrain.ConsoleApp;
using TradingBrain.Core;

var generateDir = ReadGenerateNinjaDirectory(args);
if (!string.IsNullOrWhiteSpace(generateDir))
{
    CleanNinjaScriptGenerator.GenerateAll(generateDir);
    Console.WriteLine($"Strategies limpas geradas em: {generateDir}");
    return 0;
}

var inspectRequest = ReadInspectDllRequest(args);
if (inspectRequest is not null)
{
    DllMetadataInspector.Inspect(inspectRequest.Value.DllPath, inspectRequest.Value.ReportPath);
    Console.WriteLine($"Relatorio estatico gerado em: {inspectRequest.Value.ReportPath}");
    return 0;
}

if (args.Any(arg => arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase)))
{
    PrintUsage();
    return 0;
}

var classifyRegimeIndex = Array.FindIndex(args, arg => arg.Equals("--classify-regime", StringComparison.OrdinalIgnoreCase));
if (classifyRegimeIndex >= 0)
{
    if (classifyRegimeIndex + 2 >= args.Length)
    {
        throw new ArgumentException("Use --classify-regime <input.csv|txt> <pasta-de-saida>.");
    }

    var inputPath = args[classifyRegimeIndex + 1];
    var outputDir = args[classifyRegimeIndex + 2];
    if (!TryReadBars(inputPath, out var regimeBars))
    {
        return 1;
    }

    var regimes = RegimeClassifier.Classify(regimeBars);
    Directory.CreateDirectory(outputDir);

    var regimeCsvPath = Path.Combine(outputDir, "regime_distribution.csv");
    using var writer = new StreamWriter(regimeCsvPath);
    writer.WriteLine("Date,Regime,Reason,IbHighYest,IbLowYest,IbFullYest,IbFullToday,OpenOutside,CperiodInside,OvernightRatio,GapRatio,Atr14,DayRangeAtr,CloseLocation,DirectionalEfficiency,IbExtensionAtr,CloseOutsideIb,BrokeBothIbSides,VwapCrossCount");
    foreach (var r in regimes)
    {
        writer.WriteLine(string.Join(",",
            r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.Regime,
            "\"" + r.Reason.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"",
            r.IbHighYest.ToString("0.####", CultureInfo.InvariantCulture),
            r.IbLowYest.ToString("0.####", CultureInfo.InvariantCulture),
            r.IbFullYest.ToString("0.####", CultureInfo.InvariantCulture),
            r.IbFullToday.ToString("0.####", CultureInfo.InvariantCulture),
            r.OpenOutside,
            r.CperiodInside,
            r.OvernightRatio.ToString("0.####", CultureInfo.InvariantCulture),
            r.GapRatio.ToString("0.####", CultureInfo.InvariantCulture),
            r.Atr14.ToString("0.####", CultureInfo.InvariantCulture),
            r.DayRangeAtr.ToString("0.####", CultureInfo.InvariantCulture),
            r.CloseLocation.ToString("0.####", CultureInfo.InvariantCulture),
            r.DirectionalEfficiency.ToString("0.####", CultureInfo.InvariantCulture),
            r.IbExtensionAtr.ToString("0.####", CultureInfo.InvariantCulture),
            r.CloseOutsideIb,
            r.BrokeBothIbSides,
            r.VwapCrossCount.ToString(CultureInfo.InvariantCulture)));
    }

    var counts = regimes
        .GroupBy(r => r.Regime)
        .OrderByDescending(g => g.Count())
        .ToList();

    Console.WriteLine($"Dias classificados: {regimes.Count}");
    Console.WriteLine($"CSV: {regimeCsvPath}");
    Console.WriteLine();
    foreach (var g in counts)
    {
        Console.WriteLine($"  {g.Key,-15} {g.Count(),4} dias  ({g.Count() * 100.0 / regimes.Count:F1}%)");
    }

    return 0;
}

var regimeReportIndex = Array.FindIndex(args, a =>
    a.Equals("--regime-report", StringComparison.OrdinalIgnoreCase));
if (regimeReportIndex >= 0)
{
    if (regimeReportIndex + 2 >= args.Length)
    {
        Console.WriteLine("Uso: --regime-report <barras.csv> <output_dir>");
        return 1;
    }

    var rrBarsPath = args[regimeReportIndex + 1];
    var rrOutputDir = args[regimeReportIndex + 2];
    var rrSettings = ReadExecutionSettings(args);
    if (!TryReadBars(rrBarsPath, out var rrBars))
    {
        return 1;
    }

    Directory.CreateDirectory(rrOutputDir);
    var rrHtml = RegimeReportWriter.Build(rrBars, rrSettings);
    var rrPath = Path.Combine(rrOutputDir, "regime_report.html");
    await File.WriteAllTextAsync(rrPath, rrHtml);
    Console.WriteLine($"Relatorio gerado: {rrPath}");
    return 0;
}

var executionSettings = ReadExecutionSettings(args);

var backtestRegimeRequest = ReadBacktestRegimeRequest(args);
if (backtestRegimeRequest is not null)
{
    return RunBacktestRegime(
        backtestRegimeRequest.Value.InputPath,
        backtestRegimeRequest.Value.OutputDirectory,
        backtestRegimeRequest.Value.Strategy,
        backtestRegimeRequest.Value.ParamsFromPath,
        executionSettings);
}

var analyzeTradesRequest = ReadAnalyzeTradesRequest(args);
if (analyzeTradesRequest is not null)
{
    TradeAnalyzer.Analyze(analyzeTradesRequest.Value.InputPath, analyzeTradesRequest.Value.OutputDirectory);
    return 0;
}

var runAllRequest = ReadRunAllRequest(args);
if (runAllRequest is not null)
{
    return RunAllStrategies(runAllRequest.Value.InputPath, runAllRequest.Value.OutputDirectory, executionSettings);
}

var fullReportRequest = ReadFullReportRequest(args);
if (fullReportRequest is not null)
{
    return RunFullReport(fullReportRequest.Value.InputPath, fullReportRequest.Value.OutputDirectory, executionSettings);
}

var compareFullReportsRequest = ReadCompareFullReportsRequest(args);
if (compareFullReportsRequest is not null)
{
    ReportComparisonWriter.Export(
        compareFullReportsRequest.Value.OldReportDirectory,
        compareFullReportsRequest.Value.NewReportDirectory,
        compareFullReportsRequest.Value.OutputPath);
    Console.WriteLine($"Comparativo gerado em: {compareFullReportsRequest.Value.OutputPath}");
    return 0;
}

var walkForwardRequest = ReadWalkForwardRequest(args);
if (walkForwardRequest is not null)
{
    return RunWalkForward(
        walkForwardRequest.Value.InputPath,
        walkForwardRequest.Value.OutputDirectory,
        walkForwardRequest.Value.Strategy,
        walkForwardRequest.Value.Windows,
        executionSettings,
        !args.Any(a => a.Equals("--no-regime-filter", StringComparison.OrdinalIgnoreCase)));
}

var replayRequest = ReadReplayRequest(args);
if (replayRequest is not null)
{
    return ReplayViewer.Run(
        replayRequest.Value.SignalsPath,
        replayRequest.Value.DelayMs);
}

var gridSearchRequest = ReadGridSearchRequest(args);
if (gridSearchRequest is not null)
{
    var applyRegimeFilter = !args.Any(a => a.Equals("--no-regime-filter", StringComparison.OrdinalIgnoreCase));
    return RunGridSearch(
        gridSearchRequest.Value.InputPath,
        gridSearchRequest.Value.OutputDirectory,
        gridSearchRequest.Value.Strategy,
        executionSettings,
        applyRegimeFilter);
}

var csvPath = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal)
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "sample-bars.csv");
var requestedOutputPath = args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal)
    ? args[1]
    : Path.Combine(Environment.CurrentDirectory, "volatility-signals-output.csv");
var strategy = ReadStrategy(args);
var outputPath = ResolveSingleBacktestOutputPath(requestedOutputPath, strategy);

if (!File.Exists(csvPath))
{
    Console.Error.WriteLine($"CSV nao encontrado: {csvPath}");
    Console.Error.WriteLine("Uso: dotnet run --project TradingBrain.Console -- caminho\\dados.csv caminho\\saida.csv");
    return 1;
}

var bars = CsvBarReader.Read(csvPath).ToList();
if (bars.Count == 0)
{
    Console.Error.WriteLine("Nenhuma barra valida foi carregada.");
    return 1;
}

var backtester = new StrategyBacktester(strategy);
var rows = backtester.Run(bars);
var trades = StrategyBacktester.ExtractTrades(rows, executionSettings);
var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
if (!string.IsNullOrWhiteSpace(outputDirectory))
{
    Directory.CreateDirectory(outputDirectory);
}

StrategyBacktester.ExportCsv(rows, outputPath);
var tradeOutputPath = Path.Combine(
    outputDirectory ?? Environment.CurrentDirectory,
    Path.GetFileNameWithoutExtension(outputPath) + ".trades.csv");
StrategyBacktester.ExportTradesCsv(trades, tradeOutputPath);
var summary = StrategyBacktester.Summarize(rows, executionSettings);
var manifestPath = RunManifestWriter.Write(
    RunManifestWriter.Create(
        "single-backtest",
        csvPath,
        bars.Count,
        new[] { strategy },
        executionSettings,
        new[] { outputPath, tradeOutputPath },
        StrategyTuningParams.RefinedDefault),
    outputDirectory ?? Environment.CurrentDirectory);

Console.WriteLine($"Barras carregadas: {bars.Count}");
Console.WriteLine($"Strategy testada: {summary.StrategyName}");
Console.WriteLine($"Sinais gerados: {summary.Signals}");
Console.WriteLine($"Trades fechados: {summary.ClosedTrades}");
Console.WriteLine($"Win rate: {summary.WinRate:0.##}%");
Console.WriteLine($"Profit factor: {summary.ProfitFactor:0.##}");
Console.WriteLine($"Expectancy/trade: {summary.Expectancy:0.####}");
Console.WriteLine($"Gross PnL points: {summary.GrossPnL:0.##}");
Console.WriteLine($"Net PnL points: {summary.NetPnL:0.##}");
Console.WriteLine($"Total costs currency: {summary.TotalCosts:0.##}");
Console.WriteLine($"Net currency: {summary.NetCurrency:0.##}");
Console.WriteLine($"Max drawdown: {summary.MaxDrawdown:0.##}");
Console.WriteLine($"Relatorio CSV: {outputPath}");
Console.WriteLine($"Trades CSV: {tradeOutputPath}");
Console.WriteLine($"Manifesto: {manifestPath}");
return 0;

static int RunAllStrategies(string inputPath, string outputDirectory, ExecutionSettings executionSettings)
{
    if (!File.Exists(inputPath))
    {
        Console.Error.WriteLine($"CSV nao encontrado: {inputPath}");
        return 1;
    }

    var bars = CsvBarReader.Read(inputPath).ToList();
    if (bars.Count == 0)
    {
        Console.Error.WriteLine("Nenhuma barra valida foi carregada.");
        return 1;
    }

    Directory.CreateDirectory(outputDirectory);
    var summaries = new List<BacktestSummary>();
    var outputFiles = new List<string>();

    foreach (var strategy in Enum.GetValues<StrategyKind>())
    {
        var backtester = new StrategyBacktester(strategy);
        var rows = backtester.Run(bars);
        var trades = StrategyBacktester.ExtractTrades(rows, executionSettings);
        var summary = StrategyBacktester.Summarize(rows, executionSettings);
        summaries.Add(summary);

        var slug = strategy.ToString().ToLowerInvariant();
        var signalsPath = Path.Combine(outputDirectory, slug + ".signals.csv");
        var tradesPath = Path.Combine(outputDirectory, slug + ".trades.csv");
        StrategyBacktester.ExportCsv(rows, signalsPath);
        StrategyBacktester.ExportTradesCsv(trades, tradesPath);
        outputFiles.Add(signalsPath);
        outputFiles.Add(tradesPath);

        Console.WriteLine(string.Join(" | ",
            summary.StrategyName,
            "Trades=" + summary.ClosedTrades.ToString(CultureInfo.InvariantCulture),
            "WinRate=" + summary.WinRate.ToString("0.##", CultureInfo.InvariantCulture) + "%",
            "PF=" + FormatNumber(summary.ProfitFactor),
            "NetPF=" + FormatNumber(summary.NetProfitFactor),
            "Exp=" + FormatNumber(summary.Expectancy),
            "NetExp=" + FormatNumber(summary.NetExpectancy),
            "GrossPts=" + FormatNumber(summary.GrossPnL),
            "NetPts=" + FormatNumber(summary.NetPnL),
            "Costs=" + FormatNumber(summary.TotalCosts),
            "DD=" + FormatNumber(summary.MaxDrawdown)));
    }

    var summaryPath = Path.Combine(outputDirectory, "strategy-summary.csv");
    StrategyBacktester.ExportSummaryCsv(
        summaries.OrderByDescending(s => s.NetProfitFactor).ThenByDescending(s => s.NetExpectancy).ToList(),
        summaryPath);
    outputFiles.Add(summaryPath);
    var manifestPath = RunManifestWriter.Write(
        RunManifestWriter.Create(
            "all-strategies",
            inputPath,
            bars.Count,
            Enum.GetValues<StrategyKind>(),
            executionSettings,
            outputFiles,
            StrategyTuningParams.RefinedDefault),
        outputDirectory);

    Console.WriteLine($"Barras carregadas: {bars.Count}");
    Console.WriteLine($"Saidas geradas em: {outputDirectory}");
    Console.WriteLine($"Manifesto: {manifestPath}");
    return 0;
}

static int RunFullReport(string inputPath, string outputDirectory, ExecutionSettings executionSettings)
{
    if (!TryReadBars(inputPath, out var bars))
        return 1;

    Directory.CreateDirectory(outputDirectory);
    Console.WriteLine($"=== Full Report: {bars.Count} barras → {outputDirectory} ===");

    var allStrategies = new[]
    {
        StrategyKind.Trend, StrategyKind.Momentum, StrategyKind.Ema,
        StrategyKind.SchoolRun, StrategyKind.OrbBreakout, StrategyKind.IbBreakout,
        StrategyKind.VwapReversion, StrategyKind.BollingerFade,
        StrategyKind.Volatility,
    };

    // 1. Run all strategies with regime filter → collect trades + summaries
    var allTrades = new List<TradeResult>();
    var allSummaries = new List<BacktestSummary>();
    var regimes = RegimeClassifier.Classify(bars);
    var regimeByDate = regimes.ToDictionary(r => r.Date);

    foreach (var strategy in allStrategies)
    {
        var allowed = StrategyRegimeMap.For(strategy);
        var filteredBars = allowed.Count == 0
            ? bars
            : RegimeFilter.Apply(bars, allowed);

        var backtester = new StrategyBacktester(strategy, StrategyTuningParams.RefinedDefault);
        var rows = backtester.Run(filteredBars);
        var trades = StrategyBacktester.ExtractTrades(rows, executionSettings);
        var summary = StrategyBacktester.Summarize(rows, executionSettings);
        allTrades.AddRange(trades);
        allSummaries.Add(summary);
        Console.WriteLine($"  {strategy}: {trades.Count} trades, NetPnL={summary.NetPnL:0.#}");
    }

    // 2. Write CSVs
    var tradesCsvPath = Path.Combine(outputDirectory, "trades_combined.csv");
    var summaryCsvPath = Path.Combine(outputDirectory, "summary_combined.csv");
    StrategyBacktester.ExportTradesCsv(allTrades, tradesCsvPath);
    StrategyBacktester.ExportSummaryCsv(allSummaries, summaryCsvPath);

    // 3. grid_search.html (HtmlReportWriter) with equity curve / scatter / trade list
    var split = DataSplit.SplitChronological(bars, 0.65);
    var comparisonRows = new List<GridSearchResult>();
    foreach (var strategy in allStrategies)
        comparisonRows.AddRange(RunGridSearchForStrategy(strategy, split, outputDirectory, executionSettings, new List<string>(), true));

    var regimeDistribution = RegimeFilter.CountDaysByRegime(bars);
    var totalDays = bars.Select(b => b.Time.Date).Distinct().Count();
    var filteredDays = split.InSample.Select(b => b.Time.Date).Distinct().Count();

    var gridHtmlPath = Path.Combine(outputDirectory, "grid_search.html");
    HtmlReportWriter.ExportGridSearchHtml(
        comparisonRows.Where(r => r.Summary.IsLabel == "IS").ToList(),
        comparisonRows.Where(r => r.Summary.IsLabel == "OOS").ToList(),
        regimeDistribution,
        allStrategies[0],
        filteredDays,
        totalDays,
        executionSettings,
        gridHtmlPath,
        allTrades);

    // 4. trade_analysis.html (TradeAnalyzer HTML)
    var taPath = Path.Combine(outputDirectory, "trade_analysis.html");
    TradeAnalyzer.Analyze(tradesCsvPath, outputDirectory);

    // 5. regime_report.html (RegimeReportWriter with matrix)
    var rrHtml = RegimeReportWriter.Build(bars, executionSettings);
    var rrPath = Path.Combine(outputDirectory, "regime_report.html");
    File.WriteAllText(rrPath, rrHtml, System.Text.Encoding.UTF8);

    Console.WriteLine($"grid_search.html    → {gridHtmlPath}");
    Console.WriteLine($"trade_analysis.html → {taPath}");
    Console.WriteLine($"regime_report.html  → {rrPath}");
    Console.WriteLine($"trades_combined.csv → {tradesCsvPath}");
    Console.WriteLine($"summary_combined.csv → {summaryCsvPath}");
    return 0;
}

static (string InputPath, string OutputDirectory)? ReadFullReportRequest(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals("--full-report", StringComparison.OrdinalIgnoreCase))
            continue;

        if (i + 2 >= args.Length)
            throw new ArgumentException("Use --full-report <input.csv> <output_dir> [execution flags].");

        return (args[i + 1], args[i + 2]);
    }
    return null;
}

static (string OldReportDirectory, string NewReportDirectory, string OutputPath)? ReadCompareFullReportsRequest(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals("--compare-full-reports", StringComparison.OrdinalIgnoreCase))
            continue;

        if (i + 3 >= args.Length)
            throw new ArgumentException("Use --compare-full-reports <old_dir> <new_dir> <output.html>.");

        return (args[i + 1], args[i + 2], args[i + 3]);
    }
    return null;
}

static int RunGridSearch(
    string inputPath,
    string outputDirectory,
    StrategyKind? requestedStrategy,
    ExecutionSettings executionSettings,
    bool applyRegimeFilter)
{
    if (!TryReadBars(inputPath, out var bars))
        return 1;

    PrintRegimeDistribution(bars);

    const double splitRatio = 0.65;
    var split = DataSplit.SplitChronological(bars, splitRatio);
    if (split.InSample.Count == 0 || split.OutSample.Count == 0)
        return FailExit("Split 65/35 sem barras suficientes para IS/OOS.");

    Directory.CreateDirectory(outputDirectory);
    var strategies = GridSearchStrategies(requestedStrategy);
    var outputFiles = new List<string>();
    var comparisonRows = new List<GridSearchResult>();

    foreach (var strategy in strategies)
        comparisonRows.AddRange(RunGridSearchForStrategy(
            strategy,
            split,
            outputDirectory,
            executionSettings,
            outputFiles,
            applyRegimeFilter));

    var isVsOosPath = Path.Combine(outputDirectory, "is_vs_oos.csv");
    GridSearchRunner.ExportIsVsOosCsv(comparisonRows, isVsOosPath);
    outputFiles.Add(isVsOosPath);

    var regimeDistribution = RegimeFilter.CountDaysByRegime(bars);
    var totalDays = bars.Select(b => b.Time.Date).Distinct().Count();
    var filteredDays = split.InSample.Select(b => b.Time.Date).Distinct().Count();
    if (requestedStrategy is not null && applyRegimeFilter && StrategyRegimeMap.HasFilter(requestedStrategy.Value))
    {
        filteredDays = RegimeFilter.Apply(split.InSample, StrategyRegimeMap.For(requestedStrategy.Value))
            .Select(b => b.Time.Date)
            .Distinct()
            .Count();
    }

    var dashboardPath = Path.Combine(outputDirectory, "dashboard.html");
    HtmlReportWriter.ExportGridSearchHtml(
        comparisonRows.Where(r => r.Summary.IsLabel == "IS").ToList(),
        comparisonRows.Where(r => r.Summary.IsLabel == "OOS").ToList(),
        regimeDistribution,
        requestedStrategy ?? strategies[0],
        filteredDays,
        totalDays,
        executionSettings,
        dashboardPath);
    outputFiles.Add(dashboardPath);

    var manifestPath = WriteGridSearchManifest(inputPath, outputDirectory, bars.Count, strategies, executionSettings, outputFiles, split, splitRatio, requestedStrategy, applyRegimeFilter);
    Console.WriteLine($"Split IS/OOS: {split.InSample.Count}/{split.OutSample.Count}");
    Console.WriteLine($"IS vs OOS CSV: {isVsOosPath}");
    Console.WriteLine($"Dashboard HTML: {dashboardPath}");
    Console.WriteLine($"Manifesto: {manifestPath}");
    return 0;
}

static int RunWalkForward(
    string inputPath,
    string outputDirectory,
    StrategyKind strategy,
    int windows,
    ExecutionSettings executionSettings,
    bool applyRegimeFilter)
{
    if (!TryReadBars(inputPath, out var bars))
        return 1;

    Directory.CreateDirectory(outputDirectory);
    var summary = WalkForwardValidator.Run(bars, strategy, windows, executionSettings, applyRegimeFilter);
    var csvPath = Path.Combine(outputDirectory, "walk_forward.csv");
    ExportWalkForwardCsv(summary, csvPath);
    var manifestPath = WriteWalkForwardManifest(inputPath, outputDirectory, bars.Count, strategy, executionSettings, csvPath, summary);

    Console.WriteLine($"Walk-forward windows: {summary.Windows.Count}");
    Console.WriteLine($"Median OOS score: {FormatNumber(summary.MedianOosScore)}");
    Console.WriteLine($"ConsistencyRatio: {FormatNumber(summary.ConsistencyRatio)}");
    Console.WriteLine($"Walk-forward CSV: {csvPath}");
    Console.WriteLine($"Manifesto: {manifestPath}");
    return 0;
}

static int RunBacktestRegime(
    string inputPath,
    string outputDirectory,
    StrategyKind strategy,
    string? paramsFromPath,
    ExecutionSettings executionSettings)
{
    if (!TryReadBars(inputPath, out var allBars))
        return 1;

    var regimes = RegimeClassifier.Classify(allBars);
    var allowedRegimes = StrategyRegimeMap.For(strategy);
    if (allowedRegimes.Count == 0)
        return FailExit($"Strategy sem regime alvo definido: {strategy}");

    var allowedSet = allowedRegimes.ToHashSet();
    var regimesByDate = regimes.ToDictionary(r => r.Date);
    var filteredBars = allBars
        .Where(b =>
        {
            var date = DateOnly.FromDateTime(b.Time);
            return regimesByDate.TryGetValue(date, out var dayRegime)
                && allowedSet.Contains(dayRegime.Regime);
        })
        .ToList();
    var filteredDays = regimes.Count(r => allowedSet.Contains(r.Regime));
    var regimeLabel = string.Join("|", allowedRegimes);

    Console.WriteLine($"[BacktestRegime] {strategy}: {filteredDays} dias no regime {regimeLabel}");
    Console.WriteLine($"[BacktestRegime] Barras filtradas: {filteredBars.Count} de {allBars.Count}");

    if (filteredBars.Count == 0)
        return FailExit("[BacktestRegime] Nenhuma barra restante apos filtro de regime.");

    var tuningParams = StrategyTuningParams.RefinedDefault;
    if (!string.IsNullOrWhiteSpace(paramsFromPath))
    {
        tuningParams = ReadBestParamsFromGrid(paramsFromPath);
        Console.WriteLine($"[BacktestRegime] Parametros carregados de: {paramsFromPath}");
    }

    Directory.CreateDirectory(outputDirectory);
    var backtester = new StrategyBacktester(strategy, tuningParams);
    var rows = backtester.Run(filteredBars);
    var summary = StrategyBacktester.Summarize(rows, executionSettings);
    var trades = StrategyBacktester.ExtractTrades(rows, executionSettings);
    var slug = strategy.ToString().ToLowerInvariant();
    var signalsPath = Path.Combine(outputDirectory, $"{slug}.signals.csv");
    var tradesPath = Path.Combine(outputDirectory, $"{slug}.trades.csv");
    var monthlyPath = Path.Combine(outputDirectory, "monthly_equity.csv");

    StrategyBacktester.ExportCsv(rows, signalsPath);
    StrategyBacktester.ExportTradesCsv(trades, tradesPath);
    ExportMonthlyEquityCsv(trades, monthlyPath);
    PrintBacktestSummary(summary, strategy);

    Console.WriteLine($"Signals CSV: {signalsPath}");
    Console.WriteLine($"Trades CSV: {tradesPath}");
    Console.WriteLine($"Monthly equity: {monthlyPath}");
    return 0;
}

static void ExportWalkForwardCsv(WalkForwardSummary summary, string path)
{
    using var writer = new StreamWriter(path);

    foreach (var window in summary.Windows)
        writer.WriteLine(BuildWalkForwardWindowCsv(window));

    writer.WriteLine(BuildWalkForwardSummaryCsv(summary));
}

static void ExportMonthlyEquityCsv(IReadOnlyList<TradeResult> trades, string path)
{
    var byMonth = trades
        .GroupBy(t => t.EntryTime.ToString("yyyy-MM", CultureInfo.InvariantCulture))
        .OrderBy(g => g.Key)
        .ToList();

    using var writer = new StreamWriter(path);
    writer.WriteLine("Month,Trades,Wins,WinRate,GrossPts,NetPts,CumulativeNetPts");

    var cumulative = 0.0;
    foreach (var g in byMonth)
    {
        var count = g.Count();
        var wins = g.Count(t => t.GrossPoints > 0);
        var winRate = count == 0 ? 0 : wins * 100.0 / count;
        var grossPts = g.Sum(t => t.GrossPoints);
        var netPts = g.Sum(t => t.NetPoints);
        cumulative += netPts;

        writer.WriteLine(string.Join(",",
            g.Key,
            count.ToString(CultureInfo.InvariantCulture),
            wins.ToString(CultureInfo.InvariantCulture),
            winRate.ToString("0.##", CultureInfo.InvariantCulture),
            grossPts.ToString("0.##", CultureInfo.InvariantCulture),
            netPts.ToString("0.##", CultureInfo.InvariantCulture),
            cumulative.ToString("0.##", CultureInfo.InvariantCulture)));
    }
}

static string BuildWalkForwardWindowCsv(WalkForwardWindow window)
{
    var isSummary = window.IsWinner.Summary;
    var oosSummary = window.RawOosResult?.Summary;
    return string.Join(",",
        "WINDOW",
        window.WindowIndex.ToString(CultureInfo.InvariantCulture),
        window.IsBars.ToString(CultureInfo.InvariantCulture),
        window.OosBars.ToString(CultureInfo.InvariantCulture),
        window.IsWinner.Strategy,
        FormatCsvNumber(GridSearchRunner.Score(isSummary)),
        isSummary.ClosedTrades.ToString(CultureInfo.InvariantCulture),
        FormatCsvNumber(isSummary.NetPnL),
        FormatCsvNumber(OosScore(oosSummary)),
        FormatCsvInt(oosSummary?.ClosedTrades),
        FormatCsvNumber(oosSummary?.NetPnL),
        window.FilteredDaysTotal.ToString(CultureInfo.InvariantCulture),
        window.TotalDaysTotal.ToString(CultureInfo.InvariantCulture),
        "",
        "",
        "");
}

static string BuildWalkForwardSummaryCsv(WalkForwardSummary summary)
{
    return string.Join(",",
        "SUMMARY",
        summary.Windows.Count.ToString(CultureInfo.InvariantCulture),
        summary.Windows.Sum(w => w.IsBars).ToString(CultureInfo.InvariantCulture),
        summary.Windows.Sum(w => w.OosBars).ToString(CultureInfo.InvariantCulture),
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        FormatCsvNumber(summary.MedianOosScore),
        FormatCsvNumber(summary.WinRate),
        FormatCsvNumber(summary.MedianOosTrades),
        FormatCsvNumber(summary.ConsistencyRatio));
}

static bool TryReadBars(string inputPath, out IReadOnlyList<MarketBar> bars)
{
    bars = Array.Empty<MarketBar>();
    if (!File.Exists(inputPath))
        return Fail($"CSV nao encontrado: {inputPath}");

    var loaded = CsvBarReader.Read(inputPath).ToList();
    if (loaded.Count == 0)
        return Fail("Nenhuma barra valida foi carregada.");

    bars = loaded;
    return true;
}

static IReadOnlyList<StrategyKind> GridSearchStrategies(StrategyKind? requestedStrategy)
{
    return requestedStrategy is null
        ? new[]
        {
            StrategyKind.Momentum,
            StrategyKind.Ema,
            StrategyKind.Trend,
            StrategyKind.SchoolRun,
            StrategyKind.OrbBreakout,
            StrategyKind.IbBreakout,
            StrategyKind.VwapReversion,
            StrategyKind.BollingerFade,
            StrategyKind.Volatility
        }
        : new[] { requestedStrategy.Value };
}

static string ResolveSingleBacktestOutputPath(string outputPath, StrategyKind strategy)
{
    var isDirectory = outputPath.EndsWith(Path.DirectorySeparatorChar) ||
                      outputPath.EndsWith(Path.AltDirectorySeparatorChar) ||
                      Directory.Exists(outputPath) ||
                      string.IsNullOrWhiteSpace(Path.GetExtension(outputPath));

    if (!isDirectory)
    {
        return outputPath;
    }

    var slug = strategy.ToString().ToLowerInvariant();
    return Path.Combine(outputPath, slug + ".signals.csv");
}

static IReadOnlyList<GridSearchResult> RunGridSearchForStrategy(
    StrategyKind strategy,
    DataSplit split,
    string outputDirectory,
    ExecutionSettings executionSettings,
    List<string> outputFiles,
    bool applyRegimeFilter)
{
    var results = GridSearchRunner.Label(GridSearchRunner.Run(split.InSample, strategy, executionSettings, applyRegimeFilter), "IS");
    var outputPath = Path.Combine(outputDirectory, strategy.ToString().ToLowerInvariant() + ".grid.csv");
    GridSearchRunner.ExportCsv(results, outputPath);
    outputFiles.Add(outputPath);

    if (results.Count == 0)
    {
        Console.WriteLine($"{strategy}: nenhum resultado com trades fechados.");
        return Array.Empty<GridSearchResult>();
    }

    var top = results.Take(3).ToList();
    var oos = GridSearchRunner.ValidateOutOfSample(split.OutSample, top, executionSettings, applyRegimeFilter);
    PrintGridSearchResult(strategy, results, outputPath, oos.Count);
    return GridSearchRunner.BuildIsVsOosRows(top, oos);
}

static void PrintRegimeDistribution(IReadOnlyList<MarketBar> bars)
{
    var regimeCounts = RegimeFilter.CountDaysByRegime(bars);
    var totalDays = regimeCounts.Values.Sum();

    Console.WriteLine("=== Distribuicao de regimes no dataset ===");
    foreach (var (regime, count) in regimeCounts.OrderByDescending(kv => kv.Value))
    {
        var pct = totalDays > 0 ? 100.0 * count / totalDays : 0;
        Console.WriteLine($"  {regime,-16} {count,4} dias  ({pct:F1}%)");
    }

    Console.WriteLine($"  {"TOTAL",-16} {totalDays,4} dias");
    Console.WriteLine();
}

static string WriteGridSearchManifest(
    string inputPath,
    string outputDirectory,
    int barCount,
    IReadOnlyList<StrategyKind> strategies,
    ExecutionSettings executionSettings,
    IReadOnlyList<string> outputFiles,
    DataSplit split,
    double splitRatio,
    StrategyKind? requestedStrategy,
    bool applyRegimeFilter)
{
    return RunManifestWriter.Write(RunManifestWriter.Create(
        "grid-search",
        inputPath,
        barCount,
        strategies,
        executionSettings,
        outputFiles,
        new
        {
            RequestedStrategy = requestedStrategy?.ToString(),
            Grid = "default",
            SplitRatio = splitRatio,
            InSampleBars = split.InSample.Count,
            OutSampleBars = split.OutSample.Count,
            ApplyRegimeFilter = applyRegimeFilter,
            MinTradesOos = GridSearchRunner.MinTradesOos,
            MinTradesIsScore = GridSearchRunner.MinTradesIsScore
        }), outputDirectory);
}

static string WriteWalkForwardManifest(
    string inputPath,
    string outputDirectory,
    int barCount,
    StrategyKind strategy,
    ExecutionSettings executionSettings,
    string csvPath,
    WalkForwardSummary summary)
{
    return RunManifestWriter.Write(RunManifestWriter.Create(
        "walk-forward",
        inputPath,
        barCount,
        new[] { strategy },
        executionSettings,
        new[] { csvPath },
        new
        {
            Windows = summary.Windows.Count,
            SplitRatio = WalkForwardValidator.SplitRatio,
            MedianOosScore = FiniteOrNull(summary.MedianOosScore),
            WinRate = summary.WinRate,
            MedianOosTrades = summary.MedianOosTrades,
            ConsistencyRatio = summary.ConsistencyRatio
        }), outputDirectory);
}

static void PrintGridSearchResult(
    StrategyKind strategy,
    IReadOnlyList<GridSearchResult> results,
    string outputPath,
    int oosValidated)
{
    var best = results[0];
    Console.WriteLine(string.Join(" | ",
        strategy.ToString(),
        "Combos=" + results.Count.ToString(CultureInfo.InvariantCulture),
        "BestTrades=" + best.Summary.ClosedTrades.ToString(CultureInfo.InvariantCulture),
        "BestNetPF=" + FormatNumber(best.Summary.NetProfitFactor),
        "BestNetExp=" + FormatNumber(best.Summary.NetExpectancy),
        "BestNetPts=" + FormatNumber(best.Summary.NetPnL),
        "OOSValidated=" + oosValidated.ToString(CultureInfo.InvariantCulture),
        "CSV=" + outputPath));
}

static bool Fail(string message)
{
    Console.Error.WriteLine(message);
    return false;
}

static int FailExit(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

static void PrintBacktestSummary(BacktestSummary summary, StrategyKind strategy)
{
    Console.WriteLine($"Strategy testada: {strategy}");
    Console.WriteLine($"Sinais gerados: {summary.Signals}");
    Console.WriteLine($"Trades fechados: {summary.ClosedTrades}");
    Console.WriteLine($"Win rate: {summary.WinRate:0.##}%");
    Console.WriteLine($"Profit factor: {summary.ProfitFactor:0.##}");
    Console.WriteLine($"Expectancy/trade: {summary.Expectancy:0.####}");
    Console.WriteLine($"Gross PnL points: {summary.GrossPnL:0.##}");
    Console.WriteLine($"Net PnL points: {summary.NetPnL:0.##}");
    Console.WriteLine($"Total costs currency: {summary.TotalCosts:0.##}");
    Console.WriteLine($"Net currency: {summary.NetCurrency:0.##}");
    Console.WriteLine($"Max drawdown: {summary.MaxDrawdown:0.##}");
}

static StrategyKind ReadStrategy(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals("--strategy", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 1 >= args.Length)
        {
            throw new ArgumentException("Use --strategy Volatility.");
        }

        if (Enum.TryParse<StrategyKind>(args[i + 1], ignoreCase: true, out var strategy))
        {
            return strategy;
        }

        throw new ArgumentException("Strategy invalida: " + args[i + 1]);
    }

    return StrategyKind.Volatility;
}

static string? ReadGenerateNinjaDirectory(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals("--generate-ninja", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 1 >= args.Length)
        {
            throw new ArgumentException("Use --generate-ninja <pasta-de-saida>.");
        }

        return args[i + 1];
    }

    return null;
}

static (string DllPath, string ReportPath)? ReadInspectDllRequest(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals("--inspect-dll", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 2 >= args.Length)
        {
            throw new ArgumentException("Use --inspect-dll <dll> <relatorio.md>.");
        }

        return (args[i + 1], args[i + 2]);
    }

    return null;
}

static (string InputPath, string OutputDirectory)? ReadRunAllRequest(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals("--run-all", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 2 >= args.Length)
        {
            throw new ArgumentException("Use --run-all <input.csv|txt> <pasta-de-saida>.");
        }

        return (args[i + 1], args[i + 2]);
    }

    return null;
}

static (string InputPath, string OutputDirectory, StrategyKind Strategy, string? ParamsFromPath)? ReadBacktestRegimeRequest(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals("--backtest-regime", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 3 >= args.Length)
        {
            throw new ArgumentException("Use --backtest-regime <input.csv|txt> <pasta-de-saida> <Strategy> [--params-from <grid.csv>].");
        }

        if (!Enum.TryParse<StrategyKind>(args[i + 3], ignoreCase: true, out var strategy))
        {
            throw new ArgumentException("Strategy invalida: " + args[i + 3]);
        }

        return (args[i + 1], args[i + 2], strategy, ReadStringOption(args, "--params-from"));
    }

    return null;
}

static (string InputPath, string OutputDirectory)? ReadAnalyzeTradesRequest(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals("--analyze-trades", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 2 >= args.Length)
        {
            throw new ArgumentException("Use --analyze-trades <trades.csv|pasta> <pasta-de-saida>.");
        }

        return (args[i + 1], args[i + 2]);
    }

    return null;
}

static (string SignalsPath, int DelayMs)? ReadReplayRequest(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals("--replay", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 1 >= args.Length)
        {
            throw new ArgumentException("Use --replay <signals.csv> [--delay <ms>].");
        }

        var signalsPath = args[i + 1];
        var delayMs = ReadIntOption(args, "--delay", 0);
        return (signalsPath, delayMs);
    }

    return null;
}

static StrategyTuningParams ReadBestParamsFromGrid(string path)
{
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"[BacktestRegime] Aviso: grid CSV nao encontrado: {path}. Usando RefinedDefault.");
        return StrategyTuningParams.RefinedDefault;
    }

    try
    {
        using var reader = new StreamReader(path);
        var headerLine = reader.ReadLine();
        var valueLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine) || string.IsNullOrWhiteSpace(valueLine))
        {
            Console.Error.WriteLine($"[BacktestRegime] Aviso: grid CSV sem primeira linha de dados: {path}. Usando RefinedDefault.");
            return StrategyTuningParams.RefinedDefault;
        }

        var headers = SplitCsvLine(headerLine);
        var values = SplitCsvLine(valueLine);
        if (headers.Count != values.Count)
        {
            Console.Error.WriteLine($"[BacktestRegime] Aviso: grid CSV invalido: {path}. Usando RefinedDefault.");
            return StrategyTuningParams.RefinedDefault;
        }

        var row = headers
            .Select((header, index) => new { header, value = values[index] })
            .ToDictionary(x => x.header, x => x.value, StringComparer.OrdinalIgnoreCase);
        var defaults = StrategyTuningParams.RefinedDefault;

        return new StrategyTuningParams(
            VolatilityMinAtrRatio: ReadGridDouble(row, defaults.VolatilityMinAtrRatio, "VolatilityMinAtrRatio", "VolMinAtr"),
            VolatilityMinVolumeRatio: ReadGridDouble(row, defaults.VolatilityMinVolumeRatio, "VolatilityMinVolumeRatio", "VolMinVolume"),
            UseSqueezeFilter: ReadGridBool(row, defaults.UseSqueezeFilter, "UseSqueezeFilter", "UseSqueeze"),
            VolatilitySqueezeRatio: ReadGridDouble(row, defaults.VolatilitySqueezeRatio, "VolatilitySqueezeRatio", "SqueezeRatio"),
            VolatilityRangeMultiplier: ReadGridDouble(row, defaults.VolatilityRangeMultiplier, "VolatilityRangeMultiplier", "VolRangeMultiplier"),
            VolatilityExpansionMode: ReadGridEnum(row, defaults.VolatilityExpansionMode, "VolatilityExpansionMode", "VolExpansionMode"),
            VwapMinDistance: ReadGridDouble(row, defaults.VwapMinDistance, "VwapMinDistance"),
            RsiLongMax: ReadGridDouble(row, defaults.RsiLongMax, "RsiLongMax"),
            RsiShortMin: ReadGridDouble(row, defaults.RsiShortMin, "RsiShortMin"),
            VolatilityTrailingMode: ReadGridEnum(row, defaults.VolatilityTrailingMode, "VolatilityTrailingMode", "VolTrailingMode"),
            AtrChandelierMultiplier: ReadGridDouble(row, defaults.AtrChandelierMultiplier, "AtrChandelierMultiplier", "AtrChandelier"),
            MaxBarsWithoutProfit: ReadGridInt(row, defaults.MaxBarsWithoutProfit, "MaxBarsWithoutProfit"),
            MinProfitAtrRatio: ReadGridDouble(row, defaults.MinProfitAtrRatio, "MinProfitAtrRatio"),
            RangeCompressionRatio: ReadGridDouble(row, defaults.RangeCompressionRatio, "RangeCompressionRatio", "RangeCompression"),
            MomentumMinMacdAtrRatio: ReadGridDouble(row, defaults.MomentumMinMacdAtrRatio, "MomentumMinMacdAtrRatio", "MomentumMacdAtr"),
            MomentumVolumeRatio: ReadGridDouble(row, defaults.MomentumVolumeRatio, "MomentumVolumeRatio", "MomentumVolume"),
            EmaVolumeRatio: ReadGridDouble(row, defaults.EmaVolumeRatio, "EmaVolumeRatio", "EmaVolume"),
            AtrStopMultiplier: ReadGridDouble(row, defaults.AtrStopMultiplier, "AtrStopMultiplier", "AtrStop"),
            TrailingActivationBars: ReadGridInt(row, defaults.TrailingActivationBars, "TrailingActivationBars", "TrailingBars"),
            EmaTrailingAtrOffset: ReadGridDouble(row, defaults.EmaTrailingAtrOffset, "EmaTrailingAtrOffset", "EmaTrailingOffset"),
            TrendAtrStopMultiplier: ReadGridDouble(row, defaults.TrendAtrStopMultiplier, "TrendAtrStopMultiplier", "TrendAtrStop"),
            OrbAtrStopMultiplier: ReadGridDouble(row, defaults.OrbAtrStopMultiplier, "OrbAtrStopMultiplier", "OrbAtrStop"),
            VwapReversionBand: ReadGridDouble(row, defaults.VwapReversionBand, "VwapReversionBand"),
            RsiOversold: ReadGridInt(row, defaults.RsiOversold, "RsiOversold"),
            RsiOverbought: ReadGridInt(row, defaults.RsiOverbought, "RsiOverbought"),
            VwapReversionVolumeRatio: ReadGridDouble(row, defaults.VwapReversionVolumeRatio, "VwapReversionVolumeRatio"),
            BbStdDev: ReadGridDouble(row, defaults.BbStdDev, "BbStdDev"),
            BbFadeRsiOversold: ReadGridInt(row, defaults.BbFadeRsiOversold, "BbFadeRsiOversold"),
            BbFadeRsiOverbought: ReadGridInt(row, defaults.BbFadeRsiOverbought, "BbFadeRsiOverbought"),
            SessionBreakoutAtrBuffer: ReadGridDouble(row, defaults.SessionBreakoutAtrBuffer, "SessionBreakoutAtrBuffer"),
            SessionMinRangeAtrRatio: ReadGridDouble(row, defaults.SessionMinRangeAtrRatio, "SessionMinRangeAtrRatio"),
            SrsReferenceCandle: ReadGridInt(row, defaults.SrsReferenceCandle, "SrsReferenceCandle", "SrsRefCandle"),
            OvernightRangeStartHHmmss: ReadGridInt(row, defaults.OvernightRangeStartHHmmss, "OvernightRangeStartHHmmss"),
            OvernightRangeEndHHmmss: ReadGridInt(row, defaults.OvernightRangeEndHHmmss, "OvernightRangeEndHHmmss"),
            SrsAtrBuffer: ReadGridDouble(row, defaults.SrsAtrBuffer, "SrsAtrBuffer", "SrsBuffer"),
            SrsAtrStopMultiplier: ReadGridDouble(row, defaults.SrsAtrStopMultiplier, "SrsAtrStopMultiplier", "SrsStop"),
            SrsAtrTargetMultiplier: ReadGridDouble(row, defaults.SrsAtrTargetMultiplier, "SrsAtrTargetMultiplier", "SrsTarget"),
            OrbRangeStartHHmmss: ReadGridInt(row, defaults.OrbRangeStartHHmmss, "OrbRangeStartHHmmss", "OrbRangeStart"),
            OrbRangeEndHHmmss: ReadGridInt(row, defaults.OrbRangeEndHHmmss, "OrbRangeEnd", "OrbRangeEndHHmmss"),
            OrbMinWindowBars: ReadGridInt(row, defaults.OrbMinWindowBars, "OrbMinWindowBars"),
            OrbMinRangeAtrRatio: ReadGridDouble(row, defaults.OrbMinRangeAtrRatio, "OrbMinRangeAtrRatio"),
            OrbBreakoutBuffer: ReadGridDouble(row, defaults.OrbBreakoutBuffer, "OrbBreakoutBuffer"),
            OrbRequireVolume: ReadGridBool(row, defaults.OrbRequireVolume, "OrbRequireVolume"),
            OrbVolumeRatio: ReadGridDouble(row, defaults.OrbVolumeRatio, "OrbVolumeRatio"),
            IbTargetMultiplier: ReadGridDouble(row, defaults.IbTargetMultiplier, "IbTargetMultiplier"),
            IbUseHalfRangeStop: ReadGridBool(row, defaults.IbUseHalfRangeStop, "IbUseHalfRangeStop"),
            IbMinRangeRatio: ReadGridDouble(row, defaults.IbMinRangeRatio, "IbMinRangeRatio"),
            IbMaxRangeRatio: ReadGridDouble(row, defaults.IbMaxRangeRatio, "IbMaxRangeRatio"),
            IbRequireVolume: ReadGridBool(row, defaults.IbRequireVolume, "IbRequireVolume"),
            TrendTimeExitBars: ReadGridInt(row, defaults.TrendTimeExitBars, "TrendTimeExitBars"),
            BeActivationRMultiple: ReadGridDouble(row, defaults.BeActivationRMultiple, "BeActivationRMultiple", "BeActivationR"),
            ChandelierActivationRMultiple: ReadGridDouble(row, defaults.ChandelierActivationRMultiple, "ChandelierActivationRMultiple", "ChandelierActivationR"),
            ChandelierTrailMultiplier: ReadGridDouble(row, defaults.ChandelierTrailMultiplier, "ChandelierTrailMultiplier"),
            RangeTargetRatio: ReadGridDouble(row, defaults.RangeTargetRatio, "RangeTargetRatio"),
            BbFadeTargetRatio: ReadGridDouble(row, defaults.BbFadeTargetRatio, "BbFadeTargetRatio"),
            SrsMinRangeAtrRatio: ReadGridDouble(row, defaults.SrsMinRangeAtrRatio, "SrsMinRangeAtrRatio", "SrsMinRange"),
            SrsUseRefCandleStop: ReadGridBool(row, defaults.SrsUseRefCandleStop, "SrsUseRefCandleStop", "SrsUseRefStop"));
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
    {
        Console.Error.WriteLine($"[BacktestRegime] Aviso: falha ao ler grid CSV ({ex.Message}). Usando RefinedDefault.");
        return StrategyTuningParams.RefinedDefault;
    }
}

static (string InputPath, string OutputDirectory, StrategyKind? Strategy)? ReadGridSearchRequest(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals("--grid-search", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 2 >= args.Length)
        {
            throw new ArgumentException("Use --grid-search <input.csv|txt> <pasta-de-saida> [Strategy].");
        }

        StrategyKind? strategy = null;
        if (i + 3 < args.Length && !args[i + 3].StartsWith("--", StringComparison.Ordinal))
        {
            if (!Enum.TryParse<StrategyKind>(args[i + 3], ignoreCase: true, out var parsed))
            {
                throw new ArgumentException("Strategy invalida: " + args[i + 3]);
            }

            strategy = parsed;
        }

        return (args[i + 1], args[i + 2], strategy);
    }

    return null;
}

static (string InputPath, string OutputDirectory, StrategyKind Strategy, int Windows)? ReadWalkForwardRequest(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals("--walk-forward", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 2 >= args.Length)
        {
            throw new ArgumentException("Use --walk-forward <input.csv|txt> <pasta-de-saida> [Strategy] [--windows N].");
        }

        var strategy = StrategyKind.Volatility;
        if (i + 3 < args.Length && !args[i + 3].StartsWith("--", StringComparison.Ordinal))
        {
            if (!Enum.TryParse<StrategyKind>(args[i + 3], ignoreCase: true, out strategy))
            {
                throw new ArgumentException("Strategy invalida: " + args[i + 3]);
            }
        }

        var windows = ReadIntOption(args, "--windows", WalkForwardValidator.DefaultWindows);
        return (args[i + 1], args[i + 2], strategy, windows);
    }

    return null;
}

static string? ReadStringOption(string[] args, string name)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Use {name} <valor>.");
        }

        return args[i + 1];
    }

    return null;
}

static ExecutionSettings ReadExecutionSettings(string[] args)
{
    var settings = ExecutionSettings.MnqDefault;
    var tickSize = ReadDoubleOption(args, "--tick-size", settings.TickSize);
    var tickValue = ReadDoubleOption(args, "--tick-value", settings.TickValue);
    var slippageTicks = ReadDoubleOption(args, "--slippage-ticks", settings.SlippageTicks);
    var spreadTicks = ReadDoubleOption(args, "--spread-ticks", settings.SpreadTicks);
    var commissionPerSide = ReadDoubleOption(args, "--commission-per-side", settings.CommissionPerSide);
    var quantity = ReadIntOption(args, "--quantity", settings.Quantity);

    return new ExecutionSettings(
        tickSize,
        tickValue,
        slippageTicks,
        spreadTicks,
        commissionPerSide,
        quantity);
}

static double ReadDoubleOption(string[] args, string name, double defaultValue)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Use {name} <valor>.");
        }

        if (double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new ArgumentException($"Valor invalido para {name}: {args[i + 1]}");
    }

    return defaultValue;
}

static int ReadIntOption(string[] args, string name, int defaultValue)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Use {name} <valor>.");
        }

        if (int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new ArgumentException($"Valor invalido para {name}: {args[i + 1]}");
    }

    return defaultValue;
}

static IReadOnlyList<string> SplitCsvLine(string line)
{
    var values = new List<string>();
    var current = new System.Text.StringBuilder();
    var inQuotes = false;

    for (var i = 0; i < line.Length; i++)
    {
        var ch = line[i];
        if (ch == '"')
        {
            if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');
                i++;
                continue;
            }

            inQuotes = !inQuotes;
            continue;
        }

        if (ch == ',' && !inQuotes)
        {
            values.Add(current.ToString());
            current.Clear();
            continue;
        }

        current.Append(ch);
    }

    values.Add(current.ToString());
    return values;
}

static double ReadGridDouble(IReadOnlyDictionary<string, string> row, double defaultValue, params string[] names)
{
    if (!TryGetGridValue(row, out var raw, names))
        return defaultValue;

    if (raw.Equals("Infinity", StringComparison.OrdinalIgnoreCase))
        return double.PositiveInfinity;

    if (raw.Equals("-Infinity", StringComparison.OrdinalIgnoreCase))
        return double.NegativeInfinity;

    return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
        ? value
        : defaultValue;
}

static int ReadGridInt(IReadOnlyDictionary<string, string> row, int defaultValue, params string[] names)
{
    return TryGetGridValue(row, out var raw, names)
        && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
}

static bool ReadGridBool(IReadOnlyDictionary<string, string> row, bool defaultValue, params string[] names)
{
    return TryGetGridValue(row, out var raw, names)
        && bool.TryParse(raw, out var value)
            ? value
            : defaultValue;
}

static TEnum ReadGridEnum<TEnum>(IReadOnlyDictionary<string, string> row, TEnum defaultValue, params string[] names)
    where TEnum : struct
{
    return TryGetGridValue(row, out var raw, names)
        && Enum.TryParse<TEnum>(raw, ignoreCase: true, out var value)
            ? value
            : defaultValue;
}

static bool TryGetGridValue(IReadOnlyDictionary<string, string> row, out string value, params string[] names)
{
    foreach (var name in names)
    {
        if (row.TryGetValue(name, out value!) && !string.IsNullOrWhiteSpace(value))
            return true;
    }

    value = "";
    return false;
}

static void PrintUsage()
{
    Console.WriteLine("Uso:");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- <input.csv|txt> <output.csv> --strategy Volatility");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --run-all <input.csv|txt> <pasta>");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --backtest-regime <input.csv|txt> <pasta> <Strategy> [--params-from <grid.csv>]");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --grid-search <input.csv|txt> <pasta> [Strategy]");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --analyze-trades <trades.csv|pasta> <pasta>");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --walk-forward <input.csv|txt> <pasta> [Strategy] [--windows N]");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --classify-regime <input.csv|txt> <pasta>");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --regime-report <input.csv|txt> <pasta>");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --compare-full-reports <old_dir> <new_dir> <output.html>");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --replay <signals.csv> [--delay 200]");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --generate-ninja <pasta>");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --inspect-dll <dll> <relatorio.md>");
    Console.WriteLine();
    Console.WriteLine("Flags opcionais de execucao:");
    Console.WriteLine("  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0 --quantity 1");
    Console.WriteLine("  --no-regime-filter    Desativa filtro de regime no grid search/walk-forward (roda em todos os dias)");
    Console.WriteLine();
    Console.WriteLine("Formato NinjaTrader aceito:");
    Console.WriteLine("  yyyyMMdd HHmmss;open;high;low;close;volume");
    Console.WriteLine();
    Console.WriteLine("Formato CSV aceito:");
    Console.WriteLine("  time,open,high,low,close,volume");
}

static double OosScore(BacktestSummary? summary)
{
    return summary is null ? double.NaN : GridSearchRunner.Score(summary);
}

static string FormatCsvNumber(double? value)
{
    return value is null || double.IsNaN(value.Value) ? "" : FormatNumber(value.Value);
}

static string FormatCsvInt(int? value)
{
    return value is null ? "" : value.Value.ToString(CultureInfo.InvariantCulture);
}

static double? FiniteOrNull(double value)
{
    return double.IsNaN(value) || double.IsInfinity(value) ? null : value;
}

static string FormatNumber(double value)
{
    if (double.IsPositiveInfinity(value))
    {
        return "Infinity";
    }

    if (double.IsNegativeInfinity(value))
    {
        return "-Infinity";
    }

    return value.ToString("0.####", CultureInfo.InvariantCulture);
}

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
    writer.WriteLine("Date,Regime,RangeRatio,ClosePosition,OvernightRatio,GapRatio,Ker,Reason");
    foreach (var r in regimes)
    {
        writer.WriteLine(string.Join(",",
            r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.Regime,
            r.RangeRatio.ToString("0.####", CultureInfo.InvariantCulture),
            r.ClosePosition.ToString("0.####", CultureInfo.InvariantCulture),
            r.OvernightRatio.ToString("0.####", CultureInfo.InvariantCulture),
            r.GapRatio.ToString("0.####", CultureInfo.InvariantCulture),
            r.Ker.ToString("0.####", CultureInfo.InvariantCulture),
            "\"" + r.Reason.Replace("\"", "\"\"") + "\""));
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

var executionSettings = ReadExecutionSettings(args);

var runAllRequest = ReadRunAllRequest(args);
if (runAllRequest is not null)
{
    return RunAllStrategies(runAllRequest.Value.InputPath, runAllRequest.Value.OutputDirectory, executionSettings);
}

var walkForwardRequest = ReadWalkForwardRequest(args);
if (walkForwardRequest is not null)
{
    return RunWalkForward(
        walkForwardRequest.Value.InputPath,
        walkForwardRequest.Value.OutputDirectory,
        walkForwardRequest.Value.Strategy,
        walkForwardRequest.Value.Windows,
        executionSettings);
}

var gridSearchRequest = ReadGridSearchRequest(args);
if (gridSearchRequest is not null)
{
    return RunGridSearch(gridSearchRequest.Value.InputPath, gridSearchRequest.Value.OutputDirectory, gridSearchRequest.Value.Strategy, executionSettings);
}

var csvPath = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal)
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "sample-bars.csv");
var outputPath = args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal)
    ? args[1]
    : Path.Combine(Environment.CurrentDirectory, "volatility-signals-output.csv");
var strategy = ReadStrategy(args);

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

static int RunGridSearch(string inputPath, string outputDirectory, StrategyKind? requestedStrategy, ExecutionSettings executionSettings)
{
    if (!TryReadBars(inputPath, out var bars))
        return 1;

    const double splitRatio = 0.65;
    var split = DataSplit.SplitChronological(bars, splitRatio);
    if (split.InSample.Count == 0 || split.OutSample.Count == 0)
        return FailExit("Split 65/35 sem barras suficientes para IS/OOS.");

    Directory.CreateDirectory(outputDirectory);
    var strategies = GridSearchStrategies(requestedStrategy);
    var outputFiles = new List<string>();
    var comparisonRows = new List<GridSearchResult>();

    foreach (var strategy in strategies)
        comparisonRows.AddRange(RunGridSearchForStrategy(strategy, split, outputDirectory, executionSettings, outputFiles));

    var isVsOosPath = Path.Combine(outputDirectory, "is_vs_oos.csv");
    GridSearchRunner.ExportIsVsOosCsv(comparisonRows, isVsOosPath);
    outputFiles.Add(isVsOosPath);

    var manifestPath = WriteGridSearchManifest(inputPath, outputDirectory, bars.Count, strategies, executionSettings, outputFiles, split, splitRatio, requestedStrategy);
    Console.WriteLine($"Split IS/OOS: {split.InSample.Count}/{split.OutSample.Count}");
    Console.WriteLine($"IS vs OOS CSV: {isVsOosPath}");
    Console.WriteLine($"Manifesto: {manifestPath}");
    return 0;
}

static int RunWalkForward(
    string inputPath,
    string outputDirectory,
    StrategyKind strategy,
    int windows,
    ExecutionSettings executionSettings)
{
    if (!TryReadBars(inputPath, out var bars))
        return 1;

    Directory.CreateDirectory(outputDirectory);
    var summary = WalkForwardValidator.Run(bars, strategy, windows, executionSettings);
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

static void ExportWalkForwardCsv(WalkForwardSummary summary, string path)
{
    using var writer = new StreamWriter(path);

    foreach (var window in summary.Windows)
        writer.WriteLine(BuildWalkForwardWindowCsv(window));

    writer.WriteLine(BuildWalkForwardSummaryCsv(summary));
}

static string BuildWalkForwardWindowCsv(WalkForwardWindow window)
{
    var isSummary = window.IsWinner.Summary;
    var oosSummary = window.OosResult?.Summary;
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
        "",
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
        ? new[] { StrategyKind.Momentum, StrategyKind.OrbBreakout, StrategyKind.Ema, StrategyKind.Range, StrategyKind.VwapReversion, StrategyKind.BollingerFade, StrategyKind.SessionBreakout, StrategyKind.SchoolRun }
        : new[] { requestedStrategy.Value };
}

static IReadOnlyList<GridSearchResult> RunGridSearchForStrategy(
    StrategyKind strategy,
    DataSplit split,
    string outputDirectory,
    ExecutionSettings executionSettings,
    List<string> outputFiles)
{
    var results = GridSearchRunner.Label(GridSearchRunner.Run(split.InSample, strategy, executionSettings), "IS");
    var outputPath = Path.Combine(outputDirectory, strategy.ToString().ToLowerInvariant() + ".grid.csv");
    GridSearchRunner.ExportCsv(results, outputPath);
    outputFiles.Add(outputPath);

    if (results.Count == 0)
    {
        Console.WriteLine($"{strategy}: nenhum resultado com trades fechados.");
        return Array.Empty<GridSearchResult>();
    }

    var top = results.Take(3).ToList();
    var oos = GridSearchRunner.ValidateOutOfSample(split.OutSample, top, executionSettings);
    PrintGridSearchResult(strategy, results, outputPath, oos.Count);
    return GridSearchRunner.BuildIsVsOosRows(top, oos);
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
    StrategyKind? requestedStrategy)
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
            MinTradesOos = GridSearchRunner.MinTradesOos
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

static void PrintUsage()
{
    Console.WriteLine("Uso:");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- <input.csv|txt> <output.csv> --strategy Volatility");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --run-all <input.csv|txt> <pasta>");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --grid-search <input.csv|txt> <pasta> [Strategy]");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --walk-forward <input.csv|txt> <pasta> [Strategy] [--windows N]");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --classify-regime <input.csv|txt> <pasta>");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --generate-ninja <pasta>");
    Console.WriteLine("  dotnet run --project .\\TradingBrain.Console\\TradingBrain.Console.csproj -- --inspect-dll <dll> <relatorio.md>");
    Console.WriteLine();
    Console.WriteLine("Flags opcionais de execucao:");
    Console.WriteLine("  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0 --quantity 1");
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

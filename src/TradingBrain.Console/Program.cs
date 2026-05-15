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

var executionSettings = ReadExecutionSettings(args);

var runAllRequest = ReadRunAllRequest(args);
if (runAllRequest is not null)
{
    return RunAllStrategies(runAllRequest.Value.InputPath, runAllRequest.Value.OutputDirectory, executionSettings);
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

    foreach (var strategy in Enum.GetValues<StrategyKind>())
    {
        var backtester = new StrategyBacktester(strategy);
        var rows = backtester.Run(bars);
        var trades = StrategyBacktester.ExtractTrades(rows, executionSettings);
        var summary = StrategyBacktester.Summarize(rows, executionSettings);
        summaries.Add(summary);

        var slug = strategy.ToString().ToLowerInvariant();
        StrategyBacktester.ExportCsv(rows, Path.Combine(outputDirectory, slug + ".signals.csv"));
        StrategyBacktester.ExportTradesCsv(trades, Path.Combine(outputDirectory, slug + ".trades.csv"));

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

    StrategyBacktester.ExportSummaryCsv(
        summaries.OrderByDescending(s => s.NetProfitFactor).ThenByDescending(s => s.NetExpectancy).ToList(),
        Path.Combine(outputDirectory, "strategy-summary.csv"));

    Console.WriteLine($"Barras carregadas: {bars.Count}");
    Console.WriteLine($"Saidas geradas em: {outputDirectory}");
    return 0;
}

static int RunGridSearch(string inputPath, string outputDirectory, StrategyKind? requestedStrategy, ExecutionSettings executionSettings)
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
    var strategies = requestedStrategy is null
        ? new[] { StrategyKind.Momentum, StrategyKind.Ema, StrategyKind.Volatility, StrategyKind.Range }
        : new[] { requestedStrategy.Value };

    foreach (var strategy in strategies)
    {
        var results = GridSearchRunner.Run(bars, strategy, executionSettings);
        var outputPath = Path.Combine(outputDirectory, strategy.ToString().ToLowerInvariant() + ".grid.csv");
        GridSearchRunner.ExportCsv(results, outputPath);

        var best = results.FirstOrDefault();
        if (best is null)
        {
            Console.WriteLine($"{strategy}: nenhum resultado com trades fechados.");
            continue;
        }

        Console.WriteLine(string.Join(" | ",
            strategy.ToString(),
            "Combos=" + results.Count.ToString(CultureInfo.InvariantCulture),
            "BestTrades=" + best.Summary.ClosedTrades.ToString(CultureInfo.InvariantCulture),
            "BestNetPF=" + FormatNumber(best.Summary.NetProfitFactor),
            "BestNetExp=" + FormatNumber(best.Summary.NetExpectancy),
            "BestNetPts=" + FormatNumber(best.Summary.NetPnL),
            "CSV=" + outputPath));
    }

    return 0;
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

static ExecutionSettings ReadExecutionSettings(string[] args)
{
    var settings = ExecutionSettings.MnqDefault;
    var tickSize = ReadDoubleOption(args, "--tick-size", settings.TickSize);
    var tickValue = ReadDoubleOption(args, "--tick-value", settings.TickValue);
    var slippageTicks = ReadDoubleOption(args, "--slippage-ticks", settings.SlippageTicks);
    var spreadTicks = ReadDoubleOption(args, "--spread-ticks", settings.SpreadTicks);
    var commissionPerSide = ReadDoubleOption(args, "--commission-per-side", settings.CommissionPerSide);
    var quantity = ReadIntOption(args, "--quantity", settings.Quantity);

    if (tickSize <= 0) throw new ArgumentException("--tick-size deve ser maior que zero.");
    if (tickValue <= 0) throw new ArgumentException("--tick-value deve ser maior que zero.");
    if (quantity <= 0) throw new ArgumentException("--quantity deve ser maior que zero.");
    if (slippageTicks < 0) throw new ArgumentException("--slippage-ticks nao pode ser negativo.");
    if (spreadTicks < 0) throw new ArgumentException("--spread-ticks nao pode ser negativo.");
    if (commissionPerSide < 0) throw new ArgumentException("--commission-per-side nao pode ser negativo.");

    return settings with
    {
        TickSize = tickSize,
        TickValue = tickValue,
        SlippageTicks = slippageTicks,
        SpreadTicks = spreadTicks,
        CommissionPerSide = commissionPerSide,
        Quantity = quantity
    };
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

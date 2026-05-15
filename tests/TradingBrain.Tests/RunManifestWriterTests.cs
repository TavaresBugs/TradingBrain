using System.Text.Json;
using TradingBrain.ConsoleApp;
using TradingBrain.Core;

namespace TradingBrain.Tests;

public sealed class RunManifestWriterTests
{
    [Fact]
    public void Write_CreatesRunManifestJson()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"tradingbrain-manifest-{Guid.NewGuid():N}");
        var outputFile = Path.Combine(outputDirectory, "volatility.signals.csv");
        var manifest = RunManifestWriter.Create(
            "single-backtest",
            "input.csv",
            bars: 10,
            new[] { StrategyKind.Volatility },
            ExecutionSettings.MnqDefault,
            new[] { outputFile });

        var path = RunManifestWriter.Write(manifest, outputDirectory);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Assert.Equal("single-backtest", root.GetProperty("Mode").GetString());
        Assert.Equal(10, root.GetProperty("Bars").GetInt32());
        Assert.Equal("Volatility", root.GetProperty("Strategies")[0].GetString());
        Assert.Equal(0.25, root.GetProperty("Execution").GetProperty("TickSize").GetDouble());
    }
}

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

public static class RunManifestWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Write(RunManifest manifest, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "run-manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));
        return path;
    }

    public static RunManifest Create(
        string mode,
        string inputPath,
        int bars,
        IReadOnlyList<StrategyKind> strategies,
        ExecutionSettings executionSettings,
        IReadOnlyList<string> outputFiles,
        object? parameters = null)
    {
        return new RunManifest(
            RunName: DeriveRunName(outputFiles),
            Mode: mode,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Dataset: Path.GetFullPath(inputPath),
            Bars: bars,
            Strategies: strategies.Select(s => s.ToString()).ToArray(),
            Parameters: parameters ?? StrategyTuningParams.RefinedDefault,
            Execution: executionSettings,
            CodeVersion: ResolveCodeVersion(),
            OutputFiles: outputFiles.Select(Path.GetFullPath).ToArray());
    }

    private static string DeriveRunName(IReadOnlyList<string> outputFiles)
    {
        var firstOutput = outputFiles.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstOutput))
        {
            return "run";
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(firstOutput));
        return string.IsNullOrWhiteSpace(directory)
            ? "run"
            : Path.GetFileName(directory);
    }

    private static string ResolveCodeVersion()
    {
        var githubSha = Environment.GetEnvironmentVariable("GITHUB_SHA");
        if (!string.IsNullOrWhiteSpace(githubSha))
        {
            return githubSha;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --short HEAD",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null || !process.WaitForExit(1000))
            {
                return "unknown";
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(output) ? "unknown" : output;
        }
        catch
        {
            return "unknown";
        }
    }
}

public sealed record RunManifest(
    string RunName,
    string Mode,
    DateTimeOffset GeneratedAtUtc,
    string Dataset,
    int Bars,
    IReadOnlyList<string> Strategies,
    object Parameters,
    ExecutionSettings Execution,
    string CodeVersion,
    IReadOnlyList<string> OutputFiles);

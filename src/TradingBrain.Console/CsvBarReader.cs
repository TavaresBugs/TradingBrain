using System.Globalization;
using TradingBrain.Core;

namespace TradingBrain.ConsoleApp;

public static class CsvBarReader
{
    public static IEnumerable<MarketBar> Read(string path)
    {
        var lineNumber = 0;

        foreach (var rawLine in File.ReadLines(path))
        {
            lineNumber++;
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (lineNumber == 1 && line.StartsWith("time", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var delimiter = line.Contains(';') ? ';' : ',';
            var parts = line.Split(delimiter);
            if (parts.Length < 6)
            {
                throw new InvalidDataException($"Linha {lineNumber}: esperado time,open,high,low,close,volume.");
            }

            yield return new MarketBar(
                ParseTime(parts[0]),
                ParseDouble(parts[1]),
                ParseDouble(parts[2]),
                ParseDouble(parts[3]),
                ParseDouble(parts[4]),
                long.Parse(parts[5], CultureInfo.InvariantCulture));
        }
    }

    private static double ParseDouble(string value) =>
        double.Parse(value, CultureInfo.InvariantCulture);

    private static DateTime ParseTime(string value)
    {
        var text = value.Trim();
        var ninjaFormats = new[] { "yyyyMMdd HHmmss", "yyyyMMdd HHmm", "yyyyMMdd" };

        if (DateTime.TryParseExact(
                text,
                ninjaFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var ninjaTime))
        {
            return ninjaTime;
        }

        return DateTime.Parse(text, CultureInfo.InvariantCulture);
    }
}

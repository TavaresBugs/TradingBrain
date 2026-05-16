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
            if (cols.Length < 5)
            {
                continue;
            }

            var time = GetStr(cols, idx, "Time");
            var open = GetDbl(cols, idx, "Open");
            var high = GetDbl(cols, idx, "High");
            var low = GetDbl(cols, idx, "Low");
            var close = GetDbl(cols, idx, "Close");
            var signal = GetStr(cols, idx, "Signal");
            var reason = GetStr(cols, idx, "Reason");
            var position = GetInt(cols, idx, "Position");
            var entry = GetDbl(cols, idx, "EntryPrice");
            var openPnl = GetDbl(cols, idx, "OpenProfit");
            var equity = GetDbl(cols, idx, "Equity");
            var atr = GetDbl(cols, idx, "ATR");
            var rsi = GetDbl(cols, idx, "RSI");

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

            var posStr = position switch
            {
                1 => "[LONG ]",
                -1 => "[SHORT]",
                _ => "[FLAT ]"
            };
            var signalStr = signal switch
            {
                "Buy" => "▲ BUY ",
                "Sell" => "▼ SELL",
                "Exit" => "◆ EXIT",
                _ => "· ----"
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
        {
            idx[headers[i].Trim()] = i;
        }

        return idx;
    }

    private static string GetStr(string[] cols, Dictionary<string, int> idx, string name) =>
        idx.TryGetValue(name, out var i) && i < cols.Length ? cols[i].Trim() : "";

    private static double GetDbl(string[] cols, Dictionary<string, int> idx, string name) =>
        idx.TryGetValue(name, out var i) && i < cols.Length &&
        double.TryParse(cols[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : double.NaN;

    private static int GetInt(string[] cols, Dictionary<string, int> idx, string name) =>
        idx.TryGetValue(name, out var i) && i < cols.Length &&
        int.TryParse(cols[i], out var v)
            ? v
            : 0;
}

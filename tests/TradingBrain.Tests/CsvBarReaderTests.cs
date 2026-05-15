using TradingBrain.ConsoleApp;

namespace TradingBrain.Tests;

public sealed class CsvBarReaderTests
{
    [Fact]
    public void Read_ParsesStandardCommaSeparatedCsvWithHeader()
    {
        var path = WriteTempCsv("""
            time,open,high,low,close,volume
            2026-01-02 09:00:00,100.00,101.00,99.50,100.25,1200
            """);

        var bar = CsvBarReader.Read(path).Single();

        Assert.Equal(new DateTime(2026, 1, 2, 9, 0, 0), bar.Time);
        Assert.Equal(100.00, bar.Open);
        Assert.Equal(101.00, bar.High);
        Assert.Equal(99.50, bar.Low);
        Assert.Equal(100.25, bar.Close);
        Assert.Equal(1200, bar.Volume);
    }

    [Fact]
    public void Read_ParsesNinjaTraderSemicolonSeparatedCsv()
    {
        var path = WriteTempCsv("20260102 090000;100.00;101.00;99.50;100.25;1200");

        var bar = CsvBarReader.Read(path).Single();

        Assert.Equal(new DateTime(2026, 1, 2, 9, 0, 0), bar.Time);
        Assert.Equal(100.25, bar.Close);
    }

    [Fact]
    public void Read_SkipsBlankLinesAndComments()
    {
        var path = WriteTempCsv("""
            # comment

            20260102;100;101;99;100.25;1200
            """);

        var bars = CsvBarReader.Read(path).ToList();

        Assert.Single(bars);
    }

    private static string WriteTempCsv(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"tradingbrain-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content);
        return path;
    }
}

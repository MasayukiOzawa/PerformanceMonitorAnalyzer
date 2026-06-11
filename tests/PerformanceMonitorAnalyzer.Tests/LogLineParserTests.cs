using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class LogLineParserTests
{
    [Theory]
    [InlineData("[2026-01-02 03:04:05] ERROR failed", LogLevel.Error)]
    [InlineData("[2026-01-02 03:04:05] エラーが発生しました", LogLevel.Error)]
    [InlineData("[2026-01-02 03:04:05] 処理に失敗しました", LogLevel.Error)]
    [InlineData("[2026-01-02 03:04:05] WARNING check", LogLevel.Warning)]
    [InlineData("[2026-01-02 03:04:05] 警告です", LogLevel.Warning)]
    [InlineData("[2026-01-02 03:04:05] INFO started", LogLevel.Info)]
    public void Parse_InfersLevelFromMessage(string line, LogLevel expectedLevel)
    {
        var entry = Assert.IsType<LogEntry>(LogLineParser.Parse(line));

        Assert.Equal(expectedLevel, entry.Level);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), entry.Timestamp);
    }

    [Fact]
    public void Parse_WithoutTimestamp_UsesCurrentTimeAndInfoLevel()
    {
        var before = DateTime.Now;

        var entry = Assert.IsType<LogEntry>(LogLineParser.Parse("plain message"));

        Assert.Equal(LogLevel.Info, entry.Level);
        Assert.Equal("plain message", entry.Message);
        Assert.InRange(entry.Timestamp, before, DateTime.Now);
    }
}

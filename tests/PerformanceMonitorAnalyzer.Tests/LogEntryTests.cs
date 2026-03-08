using System.Windows.Media;
using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class LogEntryTests
{
    [Fact]
    public void DerivedProperties_FormatTimestampAndDisplayLevel()
    {
        var entry = new LogEntry
        {
            Timestamp = new DateTime(2024, 5, 6, 7, 8, 9),
            Level = LogLevel.Warning,
            Message = "threshold exceeded"
        };

        Assert.Equal("2024-05-06 07:08:09", entry.FormattedTimestamp);
        Assert.Equal(nameof(LogLevel.Warning), entry.LevelDisplay);
        Assert.Equal("threshold exceeded", entry.Message);
    }

    [Theory]
    [InlineData(LogLevel.Error, 255, 0, 0)]
    [InlineData(LogLevel.Warning, 255, 165, 0)]
    [InlineData(LogLevel.Info, 0, 0, 255)]
    [InlineData(LogLevel.Success, 0, 128, 0)]
    public void TextColor_ReturnsExpectedBrushForKnownLogLevels(LogLevel level, byte expectedRed, byte expectedGreen, byte expectedBlue)
    {
        var entry = new LogEntry { Level = level };

        Assert.Equal(Color.FromRgb(expectedRed, expectedGreen, expectedBlue), GetBrushColor(entry.TextColor));
    }

    private static Color GetBrushColor(Brush brush)
    {
        return Assert.IsType<SolidColorBrush>(brush).Color;
    }
}

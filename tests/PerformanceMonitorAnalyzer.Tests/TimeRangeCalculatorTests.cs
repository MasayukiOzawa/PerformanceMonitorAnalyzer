using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class TimeRangeCalculatorTests
{
    [Fact]
    public void CalculateRange_WithZeroAndHundredPercent_ReturnsFullRange()
    {
        var start = new DateTime(2026, 1, 1);
        var end = start.AddHours(2);

        var result = TimeRangeCalculator.CalculateRange(start, end, 0, 100);

        Assert.Equal(start, result.StartTime);
        Assert.Equal(end, result.EndTime);
    }

    [Fact]
    public void CalculateRange_WithMiddlePercent_ReturnsInterpolatedRange()
    {
        var start = new DateTime(2026, 1, 1);
        var end = start.AddHours(4);

        var result = TimeRangeCalculator.CalculateRange(start, end, 25, 75);

        Assert.Equal(start.AddHours(1), result.StartTime);
        Assert.Equal(start.AddHours(3), result.EndTime);
    }
}

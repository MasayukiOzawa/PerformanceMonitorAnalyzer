using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class CounterValueModeConverterTests
{
    [Fact]
    public void ToDisplayDataPoints_RawMode_ReturnsOriginalList()
    {
        var rawData = new List<PerformanceDataPoint> { Point(1), Point(2) };

        var result = CounterValueModeConverter.ToDisplayDataPoints("counter", rawData, CounterValueMode.RawValue);

        Assert.Same(rawData, result);
    }

    [Fact]
    public void ToDisplayDataPoints_DeltaMode_ReturnsDifferences()
    {
        var rawData = new List<PerformanceDataPoint>
        {
            Point(10, 0),
            Point(15, 1),
            Point(12, 2)
        };

        var result = CounterValueModeConverter.ToDisplayDataPoints(@"\Counter\Count", rawData, CounterValueMode.DeltaFromPrevious);

        Assert.Equal(2, result.Count);
        Assert.Equal(5, result[0].Value);
        Assert.Equal(-3, result[1].Value);
        Assert.Equal(rawData[1].Timestamp, result[0].Timestamp);
    }

    [Fact]
    public void ToDisplayDataPoints_DeltaModeWithLessThanTwoPoints_ReturnsEmpty()
    {
        Assert.Empty(CounterValueModeConverter.ToDisplayDataPoints("counter", [Point(1)], CounterValueMode.DeltaFromPrevious));
    }

    [Fact]
    public void TryGetMaximumAbsoluteValue_SkipsNonFiniteValues()
    {
        Assert.True(CounterValueModeConverter.TryGetMaximumAbsoluteValue(
            [Point(double.NaN), Point(-3), Point(2)],
            out var value));
        Assert.Equal(3, value);
    }

    private static PerformanceDataPoint Point(double value, int seconds = 0)
    {
        return new PerformanceDataPoint
        {
            Timestamp = new DateTime(2026, 1, 1).AddSeconds(seconds),
            Value = value
        };
    }
}

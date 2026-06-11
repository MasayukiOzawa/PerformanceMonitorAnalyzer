using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class CounterStatisticsCalculatorTests
{
    [Fact]
    public void Compute_WithEmptyData_ReturnsZeroStatisticsAndUnit()
    {
        var result = CounterStatisticsCalculator.Compute("counter", [], "%");

        Assert.Equal("counter", result.CounterName);
        Assert.Equal(0, result.DataPointCount);
        Assert.Equal("%", result.Unit);
    }

    [Fact]
    public void Compute_WithKnownData_UsesPopulationStandardDeviation()
    {
        var start = new DateTime(2026, 1, 1);
        var result = CounterStatisticsCalculator.Compute(
            "counter",
            [(start, 2), (start.AddSeconds(1), 4), (start.AddSeconds(2), 4), (start.AddSeconds(3), 4), (start.AddSeconds(4), 5), (start.AddSeconds(5), 5), (start.AddSeconds(6), 7), (start.AddSeconds(7), 9)],
            "%");

        Assert.Equal(8, result.DataPointCount);
        Assert.Equal(5, result.Average);
        Assert.Equal(2, result.Minimum);
        Assert.Equal(9, result.Maximum);
        Assert.Equal(2, result.StandardDeviation);
        Assert.Equal(start, result.FirstTimestamp);
        Assert.Equal(start.AddSeconds(7), result.LastTimestamp);
    }

    [Fact]
    public void Calculate_WithSinglePoint_ReturnsZeroDeviationAndTimestamps()
    {
        var timestamp = new DateTime(2026, 1, 1);
        var result = CounterStatisticsCalculator.Calculate(
            "counter",
            [new PerformanceDataPoint { Timestamp = timestamp, Value = 10 }],
            "MB");

        Assert.Equal(1, result.DataPointCount);
        Assert.Equal(10, result.Average);
        Assert.Equal(0, result.StandardDeviation);
        Assert.Equal(timestamp, result.FirstTimestamp);
        Assert.Equal(timestamp, result.LastTimestamp);
        Assert.Equal("MB", result.Unit);
    }
}

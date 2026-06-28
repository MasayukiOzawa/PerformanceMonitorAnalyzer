using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class StackedAreaSeriesBuilderTests
{
    [Fact]
    public void InterpolateValue_HandlesBoundariesAndMiddle()
    {
        var start = new DateTime(2026, 1, 1);
        var data = new List<PerformanceDataPoint>
        {
            Point(start.AddSeconds(10), 10),
            Point(start.AddSeconds(20), 20)
        };

        Assert.Equal(10, StackedAreaSeriesBuilder.InterpolateValue(data, start, 1));
        Assert.Equal(15, StackedAreaSeriesBuilder.InterpolateValue(data, start.AddSeconds(15), 1));
        Assert.Equal(40, StackedAreaSeriesBuilder.InterpolateValue(data, start.AddSeconds(30), 2));
    }

    [Fact]
    public void Build_AccumulatesBaselineInCounterOrderAndAppliesScale()
    {
        var start = new DateTime(2026, 1, 1);
        var result = StackedAreaSeriesBuilder.Build(
            ["first", "second"],
            new Dictionary<string, List<PerformanceDataPoint>>
            {
                ["first"] = [Point(start, 1), Point(start.AddSeconds(1), 2)],
                ["second"] = [Point(start, 10), Point(start.AddSeconds(1), 20)]
            },
            new Dictionary<string, double>
            {
                ["second"] = 0.5
            });

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 0d, 0d }, result[0].Baseline);
        Assert.Equal(new[] { 1d, 2d }, result[0].Top);
        Assert.Equal(new[] { 1d, 2d }, result[1].Baseline);
        Assert.Equal(new[] { 6d, 12d }, result[1].Top);
    }

    [Fact]
    public void Build_InterpolatesUnsortedDataPointsByTimestamp()
    {
        var start = new DateTime(2026, 1, 1);
        var result = StackedAreaSeriesBuilder.Build(
            ["anchor", "unsorted"],
            new Dictionary<string, List<PerformanceDataPoint>>
            {
                ["anchor"] = [Point(start.AddSeconds(25), 0)],
                ["unsorted"] =
                [
                    Point(start.AddSeconds(20), 20),
                    Point(start.AddSeconds(10), 100),
                    Point(start.AddSeconds(30), 30)
                ]
            },
            new Dictionary<string, double>());

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 100d, 20d, 25d, 30d }, result[1].Top);
    }

    private static PerformanceDataPoint Point(DateTime timestamp, double value)
    {
        return new PerformanceDataPoint
        {
            Timestamp = timestamp,
            Value = value
        };
    }
}

using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class LineSeriesDataBuilderTests
{
    [Fact]
    public void Build_ConvertsTimestampsAndAppliesScale()
    {
        var timestamp = new DateTime(2026, 1, 1);

        var result = LineSeriesDataBuilder.Build(
            [new PerformanceDataPoint { Timestamp = timestamp, Value = 10 }],
            0.5);

        Assert.Equal([timestamp.ToOADate()], result.XValues);
        Assert.Equal([5d], result.YValues);
    }
}

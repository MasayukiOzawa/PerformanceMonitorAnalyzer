using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class SmokeTests
{
    [Fact]
    public void CanReferenceProductionProjectTypes()
    {
        var point = new PerformanceDataPoint
        {
            Counter = @"\Processor(_Total)\% Processor Time",
            Value = 42.0,
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            FormattedValue = "42.0",
            Unit = "%"
        };

        Assert.Equal("PerformanceMonitorAnalyzer", typeof(PerformanceDataPoint).Assembly.GetName().Name);
        Assert.Equal(@"\Processor(_Total)\% Processor Time", point.Counter);
        Assert.Equal(42.0, point.Value);
        Assert.Equal("%", point.Unit);
    }
}

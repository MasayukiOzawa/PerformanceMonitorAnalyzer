using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class PerformanceDataPointTests
{
    [Fact]
    public void Defaults_AreInitializedToEmptyOrZeroValues()
    {
        var point = new PerformanceDataPoint();

        Assert.Equal(string.Empty, point.Counter);
        Assert.Equal(0d, point.Value);
        Assert.Equal(default, point.Timestamp);
        Assert.Equal(string.Empty, point.FormattedValue);
        Assert.Equal(string.Empty, point.Unit);
    }
}

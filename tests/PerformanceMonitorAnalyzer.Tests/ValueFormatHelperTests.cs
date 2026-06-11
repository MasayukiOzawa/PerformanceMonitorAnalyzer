using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class ValueFormatHelperTests
{
    [Theory]
    [InlineData(@"\Processor(_Total)\% Processor Time", "%")]
    [InlineData(@"\Processor(_Total)\% Idle Time", "%")]
    [InlineData(@"\Memory\Available MBytes", "MB")]
    [InlineData(@"\Process(_Total)\IO Read Bytes/sec", "Bytes")]
    [InlineData(@"\System\File Read Operations/sec", "/sec")]
    [InlineData(@"\System\Processor Queue Length Count", "count")]
    [InlineData(@"\System\Processor Queue Length", "")]
    public void EstimateUnit_ReturnsExpectedUnit(string counter, string expected)
    {
        Assert.Equal(expected, ValueFormatHelper.EstimateUnit(counter));
    }

    [Fact]
    public void FormatValueWithUnit_ConvertsByteThresholds()
    {
        using var _ = new TestCultureScope("en-US");

        Assert.Equal("1.00 KB", ValueFormatHelper.FormatValueWithUnit(1024, "Bytes"));
        Assert.Equal("1.00 MB", ValueFormatHelper.FormatValueWithUnit(1048576, "Bytes"));
        Assert.Equal("1.00 GB", ValueFormatHelper.FormatValueWithUnit(1073741824, "Bytes"));
    }

    [Theory]
    [InlineData(3600000, "1.0時間")]
    [InlineData(60000, "1.0分")]
    [InlineData(1000, "1.0秒")]
    [InlineData(1, "1ミリ秒")]
    [InlineData(0, "不明")]
    public void FormatSamplingInterval_UsesLargestMatchingUnit(int milliseconds, string expected)
    {
        Assert.Equal(expected, ValueFormatHelper.FormatSamplingInterval(TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Fact]
    public void ExtractUnit_ReturnsFirstNonNumericUnitText()
    {
        Assert.Equal("MB", ValueFormatHelper.ExtractUnit("1,234.56 MB"));
    }
}

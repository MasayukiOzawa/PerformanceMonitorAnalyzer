using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public sealed class ScaleCatalogTests
{
    [Fact]
    public void SupportedScaleCollections_PreserveExpectedOrderAndLabels()
    {
        var expectedValues = new[]
        {
            1000000000.0, 100000000.0, 10000000.0, 1000000.0, 100000.0,
            10000.0, 1000.0, 100.0, 10.0, 1.0, 0.1, 0.01, 0.001,
            0.0001, 0.00001, 0.000001, 0.0000001, 0.00000001, 0.000000001
        };
        var expectedLabels = new[]
        {
            "1000000000", "100000000", "10000000", "1000000", "100000",
            "10000", "1000", "100", "10", "1.0", "0.1", "0.01", "0.001",
            "0.0001", "0.00001", "0.000001", "0.0000001", "0.00000001", "0.000000001"
        };

        Assert.Equal(expectedValues, ScaleCatalog.SupportedValues);
        Assert.Equal(expectedLabels, ScaleCatalog.SupportedLabels);
        Assert.Equal(expectedLabels, ScaleCatalog.SupportedOptions.Select(option => option.Label));
    }

    [Theory]
    [InlineData(1.0, "1.0")]
    [InlineData(0.000001, "0.000001")]
    [InlineData(1000.0, "1000")]
    [InlineData(0.25, "0.25")]
    public void GetLabel_ReturnsExpectedDisplayText(double scale, string expectedLabel)
    {
        Assert.Equal(expectedLabel, ScaleCatalog.GetLabel(scale));
    }
}

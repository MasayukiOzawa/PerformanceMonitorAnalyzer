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
    [InlineData(0.0000000000000001, "1E-16")]
    public void GetLabel_ReturnsExpectedDisplayText(double scale, string expectedLabel)
    {
        Assert.Equal(expectedLabel, ScaleCatalog.GetLabel(scale));
    }

    [Theory]
    [InlineData(50.0, 2.0)]
    [InlineData(200.0, 0.5)]
    [InlineData(100.0, 1.0)]
    [InlineData(4700.0, 0.02)]
    [InlineData(2800.0, 0.05)]
    [InlineData(3000000000.0, 0.00000002)]
    public void TryCalculateScaleToTarget_ReturnsNiceScaleForMaximumAbsoluteValue(double maximumAbsoluteValue, double expectedScale)
    {
        var result = ScaleCatalog.TryCalculateScaleToTarget(maximumAbsoluteValue, out var scale);

        Assert.True(result);
        Assert.Equal(expectedScale, scale, precision: 12);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void TryCalculateScaleToTarget_ReturnsFalseForInvalidMaximumAbsoluteValue(double maximumAbsoluteValue)
    {
        var result = ScaleCatalog.TryCalculateScaleToTarget(maximumAbsoluteValue, out var scale);

        Assert.False(result);
        Assert.Equal(0, scale);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-100.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void TryCalculateScaleToTarget_ReturnsFalseForInvalidTargetValue(double targetValue)
    {
        var result = ScaleCatalog.TryCalculateScaleToTarget(50.0, out var scale, targetValue);

        Assert.False(result);
        Assert.Equal(0, scale);
    }

    [Theory]
    [InlineData(0.0149, 0.01)]
    [InlineData(0.015, 0.02)]
    [InlineData(0.0349, 0.02)]
    [InlineData(0.035, 0.05)]
    [InlineData(0.0749, 0.05)]
    [InlineData(0.075, 0.1)]
    [InlineData(149.0, 100.0)]
    [InlineData(150.0, 200.0)]
    public void RoundToNiceScale_RoundsToOneTwoFiveTimesPowerOfTen(double scale, double expectedScale)
    {
        Assert.Equal(expectedScale, ScaleCatalog.RoundToNiceScale(scale), precision: 12);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void RoundToNiceScale_ReturnsZeroForInvalidScale(double scale)
    {
        Assert.Equal(0, ScaleCatalog.RoundToNiceScale(scale));
    }
}

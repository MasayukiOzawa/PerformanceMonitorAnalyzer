namespace PerformanceMonitorAnalyzer.Tests;

public class YAxisRangeStateTests
{
    [Fact]
    public void SetManualAndReset_KeepIndependentRangeState()
    {
        var primaryState = new YAxisRangeState();
        var secondaryState = new YAxisRangeState();

        secondaryState.SetManual(10, 250);

        Assert.False(primaryState.IsManual);
        Assert.Equal(0, primaryState.Minimum);
        Assert.Equal(100, primaryState.Maximum);
        Assert.True(secondaryState.IsManual);
        Assert.Equal(10, secondaryState.Minimum);
        Assert.Equal(250, secondaryState.Maximum);

        secondaryState.Reset();

        Assert.False(secondaryState.IsManual);
        Assert.Equal(0, secondaryState.Minimum);
        Assert.Equal(100, secondaryState.Maximum);
    }

    [Fact]
    public void CalculateAutomaticRange_UsesLargestLineValue()
    {
        var range = YAxisRangeCalculator.CalculateAutomaticRange(
            [[10, 20], [50, 200]],
            stacked: false);

        Assert.Equal(0, range.Minimum);
        Assert.Equal(221, range.Maximum);
    }

    [Fact]
    public void CalculateAutomaticRange_UsesStackedUpperBound()
    {
        var range = YAxisRangeCalculator.CalculateAutomaticRange(
            [[80], [70]],
            stacked: true);

        Assert.Equal(0, range.Minimum);
        Assert.Equal(165, range.Maximum);
    }

    [Fact]
    public void CalculateAutomaticRange_ReturnsDefaultForNoFiniteValues()
    {
        var range = YAxisRangeCalculator.CalculateAutomaticRange(
            [[double.NaN], [double.PositiveInfinity]],
            stacked: false);

        Assert.Equal(0, range.Minimum);
        Assert.Equal(100, range.Maximum);
    }

    [Fact]
    public void CalculateDetailChartRange_AddsTenPercentPadding()
    {
        var range = YAxisRangeCalculator.CalculateDetailChartRange([10, 200, 50]);

        Assert.Equal(0, range.Minimum);
        Assert.Equal(220, range.Maximum, precision: 10);
    }

    [Theory]
    [InlineData(new double[0])]
    [InlineData(new[] { double.NaN, double.PositiveInfinity })]
    [InlineData(new[] { -10d, -1d })]
    public void CalculateDetailChartRange_UsesUsableFallbackForNonPositiveData(double[] values)
    {
        var range = YAxisRangeCalculator.CalculateDetailChartRange(values);

        Assert.Equal(0, range.Minimum);
        Assert.Equal(1, range.Maximum);
    }
}

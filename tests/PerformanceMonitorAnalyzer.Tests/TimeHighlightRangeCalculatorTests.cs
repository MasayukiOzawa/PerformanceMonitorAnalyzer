namespace PerformanceMonitorAnalyzer.Tests;

public class TimeHighlightRangeCalculatorTests
{
    [Fact]
    public void NormalizePercentRange_WhenStartCrossesEnd_MovesEndForward()
    {
        var result = TimeHighlightRangeCalculator.NormalizePercentRange(
            90,
            80,
            85,
            TimeHighlightSliderRole.Start);

        Assert.Equal(90, result.StartPercent);
        Assert.Equal(91, result.EndPercent);
        Assert.Equal(90, result.FocusPercent);
    }

    [Fact]
    public void NormalizePercentRange_WhenEndCrossesStart_MovesStartBackward()
    {
        var result = TimeHighlightRangeCalculator.NormalizePercentRange(
            80,
            20,
            85,
            TimeHighlightSliderRole.End);

        Assert.Equal(19, result.StartPercent);
        Assert.Equal(20, result.EndPercent);
        Assert.Equal(20, result.FocusPercent);
    }

    [Fact]
    public void NormalizePercentRange_ClampsFocusInsideRange()
    {
        var result = TimeHighlightRangeCalculator.NormalizePercentRange(
            25,
            75,
            90,
            TimeHighlightSliderRole.Focus);

        Assert.Equal(25, result.StartPercent);
        Assert.Equal(75, result.EndPercent);
        Assert.Equal(75, result.FocusPercent);
    }

    [Fact]
    public void CalculateRange_ConvertsPercentagesToTimes()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0);
        var end = start.AddHours(4);

        var result = TimeHighlightRangeCalculator.CalculateRange(start, end, 25, 75, 50);

        Assert.Equal(start.AddHours(1), result.StartTime);
        Assert.Equal(start.AddHours(3), result.EndTime);
        Assert.Equal(start.AddHours(2), result.FocusTime);
    }
}

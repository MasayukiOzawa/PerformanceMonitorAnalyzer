using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class ChartHoverContextBuilderTests
{
    [Fact]
    public void Build_SelectsNearestTimestampAndVisibleCounterValues()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0);
        var context = ChartHoverContextBuilder.Build(
            start.AddSeconds(8),
            ["counter-a", "counter-b"],
            new Dictionary<string, List<PerformanceDataPoint>>
            {
                ["counter-a"] = [Point(start, 10), Point(start.AddSeconds(10), 20)],
                ["counter-b"] = [Point(start, 100), Point(start.AddSeconds(10), 200)]
            },
            new Dictionary<string, bool>(),
            new Dictionary<string, double>(),
            CounterValueMode.RawValue);

        Assert.NotNull(context);
        Assert.Equal(start.AddSeconds(10), context.Timestamp);
        Assert.Equal(["counter-a", "counter-b"], context.Items.Select(static item => item.Counter).ToArray());
        Assert.Equal([20d, 200d], context.Items.Select(static item => item.Value).ToArray());
    }

    [Fact]
    public void Build_ExcludesHiddenCounters()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0);
        var context = ChartHoverContextBuilder.Build(
            start,
            ["visible", "hidden"],
            new Dictionary<string, List<PerformanceDataPoint>>
            {
                ["visible"] = [Point(start, 1)],
                ["hidden"] = [Point(start, 2)]
            },
            new Dictionary<string, bool>
            {
                ["hidden"] = false
            },
            new Dictionary<string, double>(),
            CounterValueMode.RawValue);

        Assert.NotNull(context);
        var item = Assert.Single(context.Items);
        Assert.Equal("visible", item.Counter);
        Assert.Equal(1, item.Value);
    }

    [Fact]
    public void Build_CurrentValuesChangeWithTargetTimestamp()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0);
        var counterData = new Dictionary<string, List<PerformanceDataPoint>>
        {
            ["counter"] =
            [
                Point(start, 10),
                Point(start.AddSeconds(10), 20)
            ]
        };

        var firstContext = ChartHoverContextBuilder.Build(
            start,
            ["counter"],
            counterData,
            new Dictionary<string, bool>(),
            new Dictionary<string, double>(),
            CounterValueMode.RawValue);
        var secondContext = ChartHoverContextBuilder.Build(
            start.AddSeconds(10),
            ["counter"],
            counterData,
            new Dictionary<string, bool>(),
            new Dictionary<string, double>(),
            CounterValueMode.RawValue);

        Assert.NotNull(firstContext);
        Assert.NotNull(secondContext);
        Assert.Equal(10, Assert.Single(firstContext.Items).DisplayValue);
        Assert.Equal(20, Assert.Single(secondContext.Items).DisplayValue);
    }

    [Fact]
    public void Build_UsesDeltaFromPreviousValues()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0);
        var context = ChartHoverContextBuilder.Build(
            start.AddSeconds(2),
            ["delta"],
            new Dictionary<string, List<PerformanceDataPoint>>
            {
                ["delta"] =
                [
                    Point(start, 10),
                    Point(start.AddSeconds(1), 15),
                    Point(start.AddSeconds(2), 30)
                ]
            },
            new Dictionary<string, bool>(),
            new Dictionary<string, double>(),
            CounterValueMode.DeltaFromPrevious);

        Assert.NotNull(context);
        Assert.Equal(start.AddSeconds(2), context.Timestamp);
        var item = Assert.Single(context.Items);
        Assert.Equal(15, item.Value);
    }

    [Fact]
    public void Build_AppliesScaleToDisplayValue()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0);
        var context = ChartHoverContextBuilder.Build(
            start,
            ["scaled"],
            new Dictionary<string, List<PerformanceDataPoint>>
            {
                ["scaled"] = [Point(start, 20)]
            },
            new Dictionary<string, bool>(),
            new Dictionary<string, double>
            {
                ["scaled"] = 0.5
            },
            CounterValueMode.RawValue);

        Assert.NotNull(context);
        var item = Assert.Single(context.Items);
        Assert.True(item.HasScale);
        Assert.Equal(20, item.Value);
        Assert.Equal(10, item.DisplayValue);
        Assert.Equal(0.5, item.Scale);
        Assert.Equal("20.00", item.FormattedValue);
        Assert.Equal("10.00", item.FormattedDisplayValue);
    }

    [Fact]
    public void Build_FormattedDisplayValue_DoesNotInferUnitsAndUsesTwoDecimalPlaces()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0);
        var context = ChartHoverContextBuilder.Build(
            start,
            [@"\Memory\Available Bytes"],
            new Dictionary<string, List<PerformanceDataPoint>>
            {
                [@"\Memory\Available Bytes"] = [Point(start, 1048576)]
            },
            new Dictionary<string, bool>(),
            new Dictionary<string, double>(),
            CounterValueMode.RawValue);

        Assert.NotNull(context);
        var item = Assert.Single(context.Items);
        Assert.Equal("1,048,576.00", item.FormattedDisplayValue);
    }

    [Fact]
    public void Build_FormattedValue_RoundsToTwoDecimalPlaces()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0);
        var context = ChartHoverContextBuilder.Build(
            start,
            ["counter"],
            new Dictionary<string, List<PerformanceDataPoint>>
            {
                ["counter"] = [Point(start, 12.345)]
            },
            new Dictionary<string, bool>(),
            new Dictionary<string, double>(),
            CounterValueMode.RawValue);

        Assert.NotNull(context);
        var item = Assert.Single(context.Items);
        Assert.Equal("12.35", item.FormattedValue);
    }

    [Fact]
    public void Build_ReturnsNullWhenNoVisibleData()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0);
        var context = ChartHoverContextBuilder.Build(
            start,
            ["missing"],
            new Dictionary<string, List<PerformanceDataPoint>>(),
            new Dictionary<string, bool>(),
            new Dictionary<string, double>(),
            CounterValueMode.RawValue);

        Assert.Null(context);
    }

    [Fact]
    public void Build_ReturnsNullWhenTargetIsOutsideDataRange()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0);
        var context = ChartHoverContextBuilder.Build(
            start.AddSeconds(-1),
            ["counter"],
            new Dictionary<string, List<PerformanceDataPoint>>
            {
                ["counter"] = [Point(start, 1), Point(start.AddSeconds(1), 2)]
            },
            new Dictionary<string, bool>(),
            new Dictionary<string, double>(),
            CounterValueMode.RawValue);

        Assert.Null(context);
    }

    [Fact]
    public void Build_ReturnsNullWhenNearestValuesAreNotFinite()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0);
        var context = ChartHoverContextBuilder.Build(
            start,
            ["counter-a", "counter-b"],
            new Dictionary<string, List<PerformanceDataPoint>>
            {
                ["counter-a"] = [Point(start, double.NaN)],
                ["counter-b"] = [Point(start, double.PositiveInfinity)]
            },
            new Dictionary<string, bool>(),
            new Dictionary<string, double>(),
            CounterValueMode.RawValue);

        Assert.Null(context);
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

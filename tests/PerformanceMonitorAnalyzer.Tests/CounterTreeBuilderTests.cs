namespace PerformanceMonitorAnalyzer.Tests;

public class CounterTreeBuilderTests
{
    [Fact]
    public void Build_UsesSummaryNodeForCountersWithoutInstance()
    {
        var counters = new[]
        {
            @"\\PC\Memory\Available Bytes",
            @"\\PC\Memory\Committed Bytes"
        };

        var result = CounterTreeBuilder.Build(counters);

        var objectNode = Assert.Single(result);
        Assert.Equal("Memory", objectNode.DisplayName);
        Assert.Single(objectNode.Children);

        var summaryNode = Assert.Single(objectNode.Children);
        Assert.Equal("(総合)", summaryNode.DisplayName);
        Assert.False(summaryNode.IsWildCard);
        Assert.Collection(
            summaryNode.Children,
            child => Assert.Equal("Available Bytes", child.DisplayName),
            child => Assert.Equal("Committed Bytes", child.DisplayName));
    }

    [Fact]
    public void Build_AddsWildcardNodeForCountersWithNamedInstances()
    {
        var counters = new[]
        {
            @"\\PC\Processor(1)\% Processor Time",
            @"\\PC\Processor(_Total)\% Processor Time"
        };

        var result = CounterTreeBuilder.Build(counters);

        var objectNode = Assert.Single(result);
        var wildcardNode = Assert.Single(objectNode.Children, child => child.IsWildCard);
        Assert.Equal("*", wildcardNode.DisplayName);
        Assert.Single(wildcardNode.Children);
        Assert.Equal("% Processor Time", wildcardNode.Children[0].DisplayName);

        var instanceNames = objectNode.Children
            .Where(child => !child.IsWildCard)
            .Select(child => child.DisplayName)
            .ToArray();

        Assert.Equal(new[] { "1", "_Total" }, instanceNames);
    }

    [Fact]
    public void Build_SortsObjectsAndCountersAlphabetically()
    {
        var counters = new[]
        {
            @"\\PC\ZObject\B Counter",
            @"\\PC\AObject\C Counter",
            @"\\PC\AObject\A Counter"
        };

        var result = CounterTreeBuilder.Build(counters);

        Assert.Equal(new[] { "AObject", "ZObject" }, result.Select(node => node.DisplayName).ToArray());

        var aObjectSummary = Assert.Single(result[0].Children);
        Assert.Equal("(総合)", aObjectSummary.DisplayName);
        Assert.Equal(new[] { "A Counter", "C Counter" }, aObjectSummary.Children.Select(node => node.DisplayName).ToArray());
    }
}

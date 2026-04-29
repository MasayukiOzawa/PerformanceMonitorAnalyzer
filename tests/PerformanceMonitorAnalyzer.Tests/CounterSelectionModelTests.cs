namespace PerformanceMonitorAnalyzer.Tests;

public class CounterSelectionModelTests
{
    [Fact]
    public void CreateInstanceItems_HidesBlankInstanceAndAddsAllInstancesChoice()
    {
        var objectNode = CounterTreeBuilder.Build(new[]
        {
            @"\\PC\Process(_Total)\% Processor Time",
            @"\\PC\Process(sqlservr)\% Processor Time"
        }).Single();

        var items = CounterSelectionModel.CreateInstanceItems(objectNode);

        Assert.Equal(new[] { "<すべてのインスタンス>", "_Total", "sqlservr" }, items.Select(item => item.DisplayName).ToArray());
        Assert.True(items[0].IsAllInstances);
    }

    [Fact]
    public void CreateInstanceItems_ReturnsEmptyListForCountersWithoutInstance()
    {
        var objectNode = CounterTreeBuilder.Build(new[]
        {
            @"\\PC\Memory\Available Bytes"
        }).Single();

        var items = CounterSelectionModel.CreateInstanceItems(objectNode);

        Assert.Empty(items);
        Assert.False(CounterSelectionModel.HasSelectableInstances(objectNode));
    }

    [Fact]
    public void CreateSelectedCounterPaths_DoesNotRequireInstanceForCountersWithoutInstance()
    {
        var objectNode = CounterTreeBuilder.Build(new[]
        {
            @"\\PC\Memory\Available Bytes"
        }).Single();
        var counterItem = CounterSelectionModel.CreateCounterItems(objectNode).Single();

        var paths = CounterSelectionModel.CreateSelectedCounterPaths(objectNode, [counterItem], []);

        Assert.Equal(new[] { @"\\PC\Memory\Available Bytes" }, paths);
    }

    [Fact]
    public void CreateSelectedCounterPaths_ExpandsAllInstancesChoice()
    {
        var objectNode = CounterTreeBuilder.Build(new[]
        {
            @"\\PC\Process(_Total)\% Processor Time",
            @"\\PC\Process(sqlservr)\% Processor Time"
        }).Single();
        var counterItem = CounterSelectionModel.CreateCounterItems(objectNode).Single();
        var allInstancesItem = CounterSelectionModel.CreateInstanceItems(objectNode).Single(item => item.IsAllInstances);

        var paths = CounterSelectionModel.CreateSelectedCounterPaths(objectNode, [counterItem], [allInstancesItem]);

        Assert.Equal(
            new[]
            {
                @"\\PC\Process(_Total)\% Processor Time",
                @"\\PC\Process(sqlservr)\% Processor Time"
            },
            paths);
    }
}

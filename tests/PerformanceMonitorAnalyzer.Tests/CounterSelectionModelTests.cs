namespace PerformanceMonitorAnalyzer.Tests;

public class CounterSelectionModelTests
{
    [Fact]
    public void CreateCounterItems_AddsAllCountersChoiceForMultipleCounters()
    {
        var objectNode = CounterTreeBuilder.Build(new[]
        {
            @"\\PC\Process(_Total)\% Processor Time",
            @"\\PC\Process(_Total)\Working Set"
        }).Single();

        var items = CounterSelectionModel.CreateCounterItems(objectNode);

        Assert.Equal(new[] { "<すべてのカウンター>", "% Processor Time", "Working Set" }, items.Select(item => item.DisplayName).ToArray());
        Assert.True(items[0].IsAllCounters);
        Assert.All(items, item => Assert.False(item.IsChecked));
    }

    [Fact]
    public void CreateCounterItems_DoesNotAddAllCountersChoiceForSingleCounter()
    {
        var objectNode = CounterTreeBuilder.Build(new[]
        {
            @"\\PC\Memory\Available Bytes"
        }).Single();

        var item = Assert.Single(CounterSelectionModel.CreateCounterItems(objectNode));

        Assert.Equal("Available Bytes", item.DisplayName);
        Assert.False(item.IsAllCounters);
    }

    [Fact]
    public void SetAllItemsChecked_UpdatesBulkSelectorAndAllConcreteItems()
    {
        var objectNode = CounterTreeBuilder.Build(new[]
        {
            @"\\PC\Memory\Available Bytes",
            @"\\PC\Memory\Pages/sec"
        }).Single();
        var items = CounterSelectionModel.CreateCounterItems(objectNode);

        CounterSelectionModel.SetAllItemsChecked(items, true);

        Assert.All(items, item => Assert.True(item.IsChecked));
        Assert.True(CounterSelectionModel.GetBulkSelectionState(items));

        CounterSelectionModel.SetAllItemsChecked(items, false);

        Assert.All(items, item => Assert.False(item.IsChecked));
        Assert.False(CounterSelectionModel.GetBulkSelectionState(items));
    }

    [Fact]
    public void GetBulkSelectionState_ReturnsNullForPartialSelection()
    {
        var objectNode = CounterTreeBuilder.Build(new[]
        {
            @"\\PC\Memory\Available Bytes",
            @"\\PC\Memory\Pages/sec"
        }).Single();
        var items = CounterSelectionModel.CreateCounterItems(objectNode);
        items.Single(item => item.CounterName == "Available Bytes").IsChecked = true;

        var state = CounterSelectionModel.GetBulkSelectionState(items);

        Assert.Null(state);
    }

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
        Assert.All(items, item => Assert.False(item.IsChecked));
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

    [Fact]
    public void CreateSelectedCounterPaths_ExpandsAllCountersAndAllInstancesChoices()
    {
        var objectNode = CounterTreeBuilder.Build(new[]
        {
            @"\\PC\Process(_Total)\% Processor Time",
            @"\\PC\Process(sqlservr)\% Processor Time",
            @"\\PC\Process(_Total)\Working Set"
        }).Single();
        var counterItems = CounterSelectionModel.CreateCounterItems(objectNode);
        var allCountersItem = counterItems.Single(item => item.IsAllCounters);
        var allInstancesItem = CounterSelectionModel.CreateInstanceItems(objectNode).Single(item => item.IsAllInstances);

        var paths = CounterSelectionModel.CreateSelectedCounterPaths(
            objectNode,
            [allCountersItem, counterItems.Single(item => item.CounterName == "% Processor Time")],
            [allInstancesItem]);

        Assert.Equal(
            new[]
            {
                @"\\PC\Process(_Total)\% Processor Time",
                @"\\PC\Process(sqlservr)\% Processor Time",
                @"\\PC\Process(_Total)\Working Set"
            },
            paths);
    }

    [Fact]
    public void CreateSelectedCounterPaths_ExpandsAllCountersWithoutInstances()
    {
        var objectNode = CounterTreeBuilder.Build(new[]
        {
            @"\\PC\Memory\Available Bytes",
            @"\\PC\Memory\Pages/sec"
        }).Single();
        var allCountersItem = CounterSelectionModel.CreateCounterItems(objectNode).Single(item => item.IsAllCounters);

        var paths = CounterSelectionModel.CreateSelectedCounterPaths(objectNode, [allCountersItem], []);

        Assert.Equal(
            new[]
            {
                @"\\PC\Memory\Available Bytes",
                @"\\PC\Memory\Pages/sec"
            },
            paths);
    }
}

namespace PerformanceMonitorAnalyzer.Tests;

public class DataTableSelectionStateTests
{
    [Fact]
    public void Synchronize_OrdersVisibleCountersByDisplayName()
    {
        using var _ = new TestCultureScope("ja-JP");
        var snapshot = DataTableSelectionState.Synchronize(
            previousCounters: Array.Empty<string>(),
            nextCounters:
            [
                @"\\A-PC\System\Processor Queue Length",
                @"\\Z-PC\Memory\Available MBytes",
                @"\\M-PC\Processor(_Total)\% Processor Time"
            ],
            selectedCounter: null);

        Assert.Equal(
            [
                @"\\Z-PC\Memory\Available MBytes",
                @"\\M-PC\Processor(_Total)\% Processor Time",
                @"\\A-PC\System\Processor Queue Length"
            ],
            snapshot.Counters);
    }

    [Fact]
    public void Synchronize_WhenSelectedCounterStillExists_PreservesSelection()
    {
        var snapshot = DataTableSelectionState.Synchronize(
            previousCounters: new[] { "counter-a", "counter-b" },
            nextCounters: new[] { "counter-a", "counter-b", "counter-c" },
            selectedCounter: "counter-b");

        Assert.Equal(new[] { "counter-a", "counter-b", "counter-c" }, snapshot.Counters);
        Assert.Equal("counter-b", snapshot.SelectedCounter);
    }

    [Fact]
    public void Synchronize_WhenSelectedCounterIsRemoved_PicksNextCounterFromPreviousOrder()
    {
        var snapshot = DataTableSelectionState.Synchronize(
            previousCounters: new[] { "counter-a", "counter-b", "counter-c" },
            nextCounters: new[] { "counter-a", "counter-c" },
            selectedCounter: "counter-b");

        Assert.Equal(new[] { "counter-a", "counter-c" }, snapshot.Counters);
        Assert.Equal("counter-c", snapshot.SelectedCounter);
    }

    [Fact]
    public void Synchronize_WhenSelectedCounterIsRemovedAtEnd_PicksPreviousCounter()
    {
        var snapshot = DataTableSelectionState.Synchronize(
            previousCounters: new[] { "counter-a", "counter-b", "counter-c" },
            nextCounters: new[] { "counter-a", "counter-b" },
            selectedCounter: "counter-c");

        Assert.Equal(new[] { "counter-a", "counter-b" }, snapshot.Counters);
        Assert.Equal("counter-b", snapshot.SelectedCounter);
    }

    [Fact]
    public void Synchronize_WhenAllCountersAreClosed_ClearsSelection()
    {
        var snapshot = DataTableSelectionState.Synchronize(
            previousCounters: new[] { "counter-a", "counter-b" },
            nextCounters: Array.Empty<string>(),
            selectedCounter: "counter-a");

        Assert.Empty(snapshot.Counters);
        Assert.Null(snapshot.SelectedCounter);
    }
}

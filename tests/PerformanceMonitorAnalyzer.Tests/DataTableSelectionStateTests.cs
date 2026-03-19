namespace PerformanceMonitorAnalyzer.Tests;

public class DataTableSelectionStateTests
{
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

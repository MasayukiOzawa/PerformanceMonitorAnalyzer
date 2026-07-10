namespace PerformanceMonitorAnalyzer.Tests;

public class YAxisAssignmentTests
{
    [Fact]
    public void GetAssignment_DefaultsToPrimary()
    {
        var state = new YAxisAssignmentState();

        Assert.Equal(YAxisAssignment.Primary, state.GetAssignment("counter"));
        Assert.False(state.ContainsSecondaryAssignment(["counter"]));
    }

    [Fact]
    public void GetEffectiveValueMode_SecondaryAlwaysUsesRaw()
    {
        var state = new YAxisAssignmentState();
        state.SetAssignment("secondary", YAxisAssignment.Secondary);

        Assert.Equal(
            CounterValueMode.DeltaFromPrevious,
            state.GetEffectiveValueMode("primary", CounterValueMode.DeltaFromPrevious));
        Assert.Equal(
            CounterValueMode.RawValue,
            state.GetEffectiveValueMode("secondary", CounterValueMode.DeltaFromPrevious));
    }

    [Fact]
    public void CreateSeriesPlan_PreservesOrderAndSeparatesAxes()
    {
        var state = new YAxisAssignmentState();
        state.SetAssignment("counter-b", YAxisAssignment.Secondary);

        var plan = state.CreateSeriesPlan(["counter-a", "counter-b", "counter-c", "counter-b"]);

        Assert.Equal(["counter-a", "counter-c"], plan.PrimaryCounters);
        Assert.Equal(["counter-b"], plan.SecondaryCounters);
    }

    [Fact]
    public void SetAssignment_PrimaryRemovesSecondaryAssignment()
    {
        var state = new YAxisAssignmentState();
        state.SetAssignment("counter", YAxisAssignment.Secondary);

        state.SetAssignment("counter", YAxisAssignment.Primary);

        Assert.Equal(YAxisAssignment.Primary, state.GetAssignment("counter"));
        Assert.False(state.ContainsSecondaryAssignment(["counter"]));
    }

    [Fact]
    public void RemoveAndClear_DiscardStoredAssignments()
    {
        var state = new YAxisAssignmentState();
        state.SetAssignment("counter-a", YAxisAssignment.Secondary);
        state.SetAssignment("counter-b", YAxisAssignment.Secondary);

        state.Remove("counter-a");

        Assert.Equal(YAxisAssignment.Primary, state.GetAssignment("counter-a"));
        Assert.True(state.ContainsSecondaryAssignment(["counter-a", "counter-b"]));

        state.Clear();

        Assert.Equal(YAxisAssignment.Primary, state.GetAssignment("counter-b"));
        Assert.False(state.ContainsSecondaryAssignment(["counter-a", "counter-b"]));
    }

    [Fact]
    public void ContainsSecondaryAssignment_UsesOnlyProvidedCounters()
    {
        var state = new YAxisAssignmentState();
        state.SetAssignment("secondary", YAxisAssignment.Secondary);

        Assert.True(state.ContainsSecondaryAssignment(["primary", "secondary"]));
        Assert.False(state.ContainsSecondaryAssignment(["primary"]));
        Assert.False(state.ContainsSecondaryAssignment([]));
    }
}

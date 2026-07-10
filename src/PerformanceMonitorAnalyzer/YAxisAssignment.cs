namespace PerformanceMonitorAnalyzer;

internal enum YAxisAssignment
{
    Primary,
    Secondary
}

internal sealed record ChartSeriesPlan(
    IReadOnlyList<string> PrimaryCounters,
    IReadOnlyList<string> SecondaryCounters);

internal sealed class YAxisAssignmentState
{
    private readonly Dictionary<string, YAxisAssignment> _assignments = new(StringComparer.Ordinal);

    public bool ContainsSecondaryAssignment(IEnumerable<string> counters)
    {
        return counters.Any(_assignments.ContainsKey);
    }

    public YAxisAssignment GetAssignment(string counter)
    {
        return _assignments.GetValueOrDefault(counter, YAxisAssignment.Primary);
    }

    public CounterValueMode GetEffectiveValueMode(string counter, CounterValueMode primaryValueMode)
    {
        return GetAssignment(counter) == YAxisAssignment.Secondary
            ? CounterValueMode.RawValue
            : primaryValueMode;
    }

    public void SetAssignment(string counter, YAxisAssignment assignment)
    {
        if (assignment == YAxisAssignment.Secondary)
        {
            _assignments[counter] = assignment;
        }
        else
        {
            _assignments.Remove(counter);
        }
    }

    public void Remove(string counter)
    {
        _assignments.Remove(counter);
    }

    public void Clear()
    {
        _assignments.Clear();
    }

    public ChartSeriesPlan CreateSeriesPlan(IEnumerable<string> counters)
    {
        var primaryCounters = new List<string>();
        var secondaryCounters = new List<string>();

        foreach (var counter in counters.Distinct(StringComparer.Ordinal))
        {
            if (GetAssignment(counter) == YAxisAssignment.Secondary)
            {
                secondaryCounters.Add(counter);
            }
            else
            {
                primaryCounters.Add(counter);
            }
        }

        return new ChartSeriesPlan(primaryCounters, secondaryCounters);
    }
}

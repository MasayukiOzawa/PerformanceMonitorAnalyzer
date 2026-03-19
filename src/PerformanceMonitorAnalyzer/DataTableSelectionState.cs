namespace PerformanceMonitorAnalyzer;

internal sealed record DataTableSelectionSnapshot(IReadOnlyList<string> Counters, string? SelectedCounter);

internal static class DataTableSelectionState
{
    public static DataTableSelectionSnapshot Synchronize(
        IEnumerable<string> previousCounters,
        IEnumerable<string> nextCounters,
        string? selectedCounter)
    {
        var previous = Normalize(previousCounters);
        var current = Normalize(nextCounters);

        if (current.Count == 0)
        {
            return new DataTableSelectionSnapshot(current, null);
        }

        if (!string.IsNullOrWhiteSpace(selectedCounter) &&
            current.Contains(selectedCounter, StringComparer.Ordinal))
        {
            return new DataTableSelectionSnapshot(current, selectedCounter);
        }

        if (!string.IsNullOrWhiteSpace(selectedCounter))
        {
            var previousIndex = previous.FindIndex(counter =>
                string.Equals(counter, selectedCounter, StringComparison.Ordinal));

            if (previousIndex >= 0)
            {
                for (var index = previousIndex + 1; index < previous.Count; index++)
                {
                    if (current.Contains(previous[index], StringComparer.Ordinal))
                    {
                        return new DataTableSelectionSnapshot(current, previous[index]);
                    }
                }

                for (var index = previousIndex - 1; index >= 0; index--)
                {
                    if (current.Contains(previous[index], StringComparer.Ordinal))
                    {
                        return new DataTableSelectionSnapshot(current, previous[index]);
                    }
                }
            }
        }

        return new DataTableSelectionSnapshot(current, current[0]);
    }

    private static List<string> Normalize(IEnumerable<string> counters)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var counter in counters)
        {
            if (string.IsNullOrWhiteSpace(counter) || !seen.Add(counter))
            {
                continue;
            }

            normalized.Add(counter);
        }

        return normalized;
    }
}

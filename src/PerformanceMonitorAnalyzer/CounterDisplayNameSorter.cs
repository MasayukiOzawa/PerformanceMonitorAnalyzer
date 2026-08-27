namespace PerformanceMonitorAnalyzer;

internal static class CounterDisplayNameSorter
{
    public static List<string> Sort(IEnumerable<string> counterPaths)
    {
        ArgumentNullException.ThrowIfNull(counterPaths);

        return counterPaths
            .OrderBy(CounterPathFormatter.GetDisplayName, StringComparer.CurrentCulture)
            .ThenBy(static counterPath => counterPath, StringComparer.CurrentCulture)
            .ToList();
    }
}

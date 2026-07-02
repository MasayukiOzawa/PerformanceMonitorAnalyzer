using System.Globalization;

namespace PerformanceMonitorAnalyzer;

internal static class CounterPatternClipboardBuilder
{
    public static string BuildCounterDefinitionYaml(string counterPath, double scale)
    {
        var localCounterPath = NormalizeCounterPatternName(counterPath);

        return string.Join(
            Environment.NewLine,
            $"      - name: {localCounterPath}",
            "        enabled: true",
            $"        scale: {FormatScale(scale)}");
    }

    public static string BuildCounterDefinitionsYaml(IEnumerable<(string CounterPath, double Scale)> counters)
    {
        return string.Join(
            Environment.NewLine,
            counters.Select(counter => BuildCounterDefinitionYaml(counter.CounterPath, counter.Scale)));
    }

    private static string NormalizeCounterPatternName(string counterPath)
    {
        var trimmedPath = counterPath.Trim();
        if (!trimmedPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return @"\" + trimmedPath.TrimStart('\\');
        }

        var parts = trimmedPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3
            ? @"\" + string.Join(@"\", parts.Skip(1))
            : @"\" + trimmedPath.TrimStart('\\');
    }

    private static string FormatScale(double scale)
    {
        if (!double.IsFinite(scale) || scale <= 0)
        {
            scale = 1.0;
        }

        var decimalLabel = scale.ToString("0.###############", CultureInfo.InvariantCulture);
        return decimalLabel == "0" && scale != 0
            ? scale.ToString("0.###############E+0", CultureInfo.InvariantCulture)
            : decimalLabel;
    }
}

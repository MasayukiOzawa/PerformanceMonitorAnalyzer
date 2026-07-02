using System.Globalization;

namespace PerformanceMonitorAnalyzer;

internal sealed record ChartHoverContext(
    DateTime Timestamp,
    IReadOnlyList<ChartHoverContextItem> Items);

internal sealed record ChartHoverContextItem(
    string Counter,
    string CounterName,
    double Value,
    double DisplayValue,
    double Scale,
    string FormattedValue,
    string FormattedDisplayValue)
{
    public bool HasScale => Math.Abs(Scale - 1.0) > 1e-12;
}

internal static class ChartHoverContextBuilder
{
    public static ChartHoverContext? Build(
        DateTime targetTimestamp,
        IEnumerable<string> counters,
        IReadOnlyDictionary<string, List<PerformanceDataPoint>> counterData,
        IReadOnlyDictionary<string, bool> seriesVisibility,
        IReadOnlyDictionary<string, double> counterScales,
        CounterValueMode valueMode)
    {
        var visibleCounters = counters
            .Where(counter => seriesVisibility.GetValueOrDefault(counter, true))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!visibleCounters.Any())
        {
            return null;
        }

        DateTime? minTimestamp = null;
        DateTime? maxTimestamp = null;
        var nearestByCounter = new List<HoverSourcePoint>();

        foreach (var counter in visibleCounters)
        {
            if (!counterData.TryGetValue(counter, out var rawData) ||
                !TryGetTimestampBounds(rawData, valueMode, out var firstTimestamp, out var lastTimestamp))
            {
                continue;
            }

            minTimestamp = minTimestamp.HasValue && minTimestamp.Value <= firstTimestamp
                ? minTimestamp
                : firstTimestamp;
            maxTimestamp = maxTimestamp.HasValue && maxTimestamp.Value >= lastTimestamp
                ? maxTimestamp
                : lastTimestamp;

            if (TryGetClosestFinitePoint(counter, rawData, valueMode, targetTimestamp, out var sourcePoint))
            {
                nearestByCounter.Add(sourcePoint);
            }
        }

        if (!minTimestamp.HasValue ||
            !maxTimestamp.HasValue ||
            targetTimestamp < minTimestamp.Value ||
            targetTimestamp > maxTimestamp.Value ||
            !nearestByCounter.Any())
        {
            return null;
        }

        var selectedTimestamp = nearestByCounter
            .OrderBy(point => AbsTicks(point.Timestamp - targetTimestamp))
            .ThenBy(point => point.Timestamp)
            .First()
            .Timestamp;

        var items = new List<ChartHoverContextItem>();
        foreach (var counter in visibleCounters)
        {
            if (!counterData.TryGetValue(counter, out var rawData) ||
                !TryGetClosestFinitePoint(counter, rawData, valueMode, selectedTimestamp, out var sourcePoint))
            {
                continue;
            }

            var scale = counterScales.GetValueOrDefault(counter, 1.0);
            var displayValue = sourcePoint.Value * scale;
            if (!double.IsFinite(displayValue))
            {
                continue;
            }

            items.Add(new ChartHoverContextItem(
                counter,
                CounterPathFormatter.GetDisplayName(counter),
                sourcePoint.Value,
                displayValue,
                scale,
                FormatHoverValue(sourcePoint.Value),
                FormatHoverValue(displayValue)));
        }

        return items.Any()
            ? new ChartHoverContext(selectedTimestamp, items)
            : null;
    }

    private static bool TryGetTimestampBounds(
        List<PerformanceDataPoint> rawData,
        CounterValueMode valueMode,
        out DateTime firstTimestamp,
        out DateTime lastTimestamp)
    {
        firstTimestamp = default;
        lastTimestamp = default;

        var startIndex = valueMode == CounterValueMode.DeltaFromPrevious ? 1 : 0;
        if (rawData.Count <= startIndex)
        {
            return false;
        }

        firstTimestamp = rawData[startIndex].Timestamp;
        lastTimestamp = rawData[^1].Timestamp;
        return firstTimestamp <= lastTimestamp;
    }

    private static bool TryGetClosestFinitePoint(
        string counter,
        List<PerformanceDataPoint> rawData,
        CounterValueMode valueMode,
        DateTime targetTimestamp,
        out HoverSourcePoint sourcePoint)
    {
        sourcePoint = default;

        var startIndex = valueMode == CounterValueMode.DeltaFromPrevious ? 1 : 0;
        if (rawData.Count <= startIndex)
        {
            return false;
        }

        var insertionIndex = FindFirstIndexAtOrAfter(rawData, startIndex, targetTimestamp);
        var candidateIndexes = new[] { insertionIndex - 1, insertionIndex }
            .Where(index => index >= startIndex && index < rawData.Count)
            .Distinct()
            .OrderBy(index => AbsTicks(rawData[index].Timestamp - targetTimestamp))
            .ThenBy(index => rawData[index].Timestamp);

        foreach (var index in candidateIndexes)
        {
            var value = GetDisplayValue(rawData, index, valueMode);
            if (!double.IsFinite(value))
            {
                continue;
            }

            sourcePoint = new HoverSourcePoint(counter, rawData[index].Timestamp, value);
            return true;
        }

        return false;
    }

    private static int FindFirstIndexAtOrAfter(
        IReadOnlyList<PerformanceDataPoint> rawData,
        int startIndex,
        DateTime targetTimestamp)
    {
        var left = startIndex;
        var right = rawData.Count;

        while (left < right)
        {
            var middle = left + ((right - left) / 2);
            if (rawData[middle].Timestamp < targetTimestamp)
            {
                left = middle + 1;
            }
            else
            {
                right = middle;
            }
        }

        return left;
    }

    private static double GetDisplayValue(
        IReadOnlyList<PerformanceDataPoint> rawData,
        int index,
        CounterValueMode valueMode)
    {
        return valueMode == CounterValueMode.RawValue
            ? rawData[index].Value
            : rawData[index].Value - rawData[index - 1].Value;
    }

    private static long AbsTicks(TimeSpan value)
    {
        return value.Ticks == long.MinValue
            ? long.MaxValue
            : Math.Abs(value.Ticks);
    }

    private static string FormatHoverValue(double value)
    {
        return value.ToString("#,0.00", CultureInfo.CurrentCulture);
    }

    private readonly record struct HoverSourcePoint(
        string Counter,
        DateTime Timestamp,
        double Value);
}

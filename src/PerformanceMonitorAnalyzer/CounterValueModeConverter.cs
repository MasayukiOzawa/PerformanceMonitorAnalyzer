namespace PerformanceMonitorAnalyzer;

public static class CounterValueModeConverter
{
    public static List<PerformanceDataPoint> ToDisplayDataPoints(
        string counter,
        List<PerformanceDataPoint> rawData,
        CounterValueMode mode)
    {
        if (rawData.Count == 0)
        {
            return new List<PerformanceDataPoint>();
        }

        if (mode == CounterValueMode.RawValue)
        {
            return rawData;
        }

        if (rawData.Count < 2)
        {
            return new List<PerformanceDataPoint>();
        }

        var unit = ValueFormatHelper.EstimateUnit(counter);
        var deltaDataPoints = new List<PerformanceDataPoint>(rawData.Count - 1);

        for (int i = 1; i < rawData.Count; i++)
        {
            var current = rawData[i];
            var previous = rawData[i - 1];
            var deltaValue = current.Value - previous.Value;

            deltaDataPoints.Add(new PerformanceDataPoint
            {
                Counter = counter,
                Value = deltaValue,
                Timestamp = current.Timestamp,
                FormattedValue = ValueFormatHelper.FormatValueWithUnit(deltaValue, unit),
                Unit = unit
            });
        }

        return deltaDataPoints;
    }

    public static bool TryGetLatestValue(
        List<PerformanceDataPoint> rawData,
        CounterValueMode mode,
        out double latestValue)
    {
        latestValue = 0;

        if (rawData.Count == 0)
        {
            return false;
        }

        if (mode == CounterValueMode.RawValue)
        {
            latestValue = rawData[^1].Value;
            return true;
        }

        if (rawData.Count < 2)
        {
            return false;
        }

        latestValue = rawData[^1].Value - rawData[^2].Value;
        return true;
    }

    public static bool TryGetMaximumAbsoluteValue(
        IEnumerable<PerformanceDataPoint> dataPoints,
        out double maximumAbsoluteValue)
    {
        maximumAbsoluteValue = 0;
        var hasValue = false;

        foreach (var dataPoint in dataPoints)
        {
            if (!double.IsFinite(dataPoint.Value))
            {
                continue;
            }

            maximumAbsoluteValue = Math.Max(maximumAbsoluteValue, Math.Abs(dataPoint.Value));
            hasValue = true;
        }

        return hasValue && maximumAbsoluteValue > 0;
    }
}

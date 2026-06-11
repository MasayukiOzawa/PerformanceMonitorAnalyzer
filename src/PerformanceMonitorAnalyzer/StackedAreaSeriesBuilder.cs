namespace PerformanceMonitorAnalyzer;

public static class StackedAreaSeriesBuilder
{
    public sealed record StackedAreaSeries(
        string Counter,
        double[] XValues,
        double[] Baseline,
        double[] Top);

    public static List<StackedAreaSeries> Build(
        IReadOnlyList<string> visibleCounters,
        IReadOnlyDictionary<string, List<PerformanceDataPoint>> displayedCounterData,
        IReadOnlyDictionary<string, double> counterScales)
    {
        var allTimestamps = new SortedSet<DateTime>();
        foreach (var counter in visibleCounters)
        {
            if (!displayedCounterData.TryGetValue(counter, out var dataPoints) || !dataPoints.Any())
            {
                continue;
            }

            foreach (var dataPoint in dataPoints)
            {
                allTimestamps.Add(dataPoint.Timestamp);
            }
        }

        if (!allTimestamps.Any())
        {
            return new List<StackedAreaSeries>();
        }

        var timeArray = allTimestamps.ToArray();
        var xValues = timeArray.Select(static timestamp => timestamp.ToOADate()).ToArray();
        var baseline = new double[timeArray.Length];
        var series = new List<StackedAreaSeries>();

        foreach (var counter in visibleCounters)
        {
            if (!displayedCounterData.TryGetValue(counter, out var dataPoints))
            {
                continue;
            }

            var finalScale = counterScales.GetValueOrDefault(counter, 1.0);
            var dataDict = dataPoints.ToDictionary(dp => dp.Timestamp, dp => dp.Value * finalScale);
            var yValues = new double[timeArray.Length];

            for (int i = 0; i < timeArray.Length; i++)
            {
                var timestamp = timeArray[i];
                yValues[i] = dataDict.TryGetValue(timestamp, out var value)
                    ? value
                    : InterpolateValue(dataPoints, timestamp, finalScale);
            }

            var currentBaseline = baseline.ToArray();
            var topValues = new double[timeArray.Length];
            for (int i = 0; i < timeArray.Length; i++)
            {
                topValues[i] = currentBaseline[i] + yValues[i];
            }

            series.Add(new StackedAreaSeries(
                counter,
                xValues,
                currentBaseline,
                topValues));

            baseline = topValues;
        }

        return series;
    }

    public static double InterpolateValue(
        List<PerformanceDataPoint> dataPoints,
        DateTime targetTime,
        double scale)
    {
        if (!dataPoints.Any())
        {
            return 0;
        }

        var before = dataPoints.Where(dp => dp.Timestamp <= targetTime).LastOrDefault();
        var after = dataPoints.Where(dp => dp.Timestamp >= targetTime).FirstOrDefault();

        if (before == null && after == null)
        {
            return 0;
        }

        if (before == null)
        {
            return after!.Value * scale;
        }

        if (after == null)
        {
            return before.Value * scale;
        }

        if (before.Timestamp == after.Timestamp)
        {
            return before.Value * scale;
        }

        var totalTicks = (after.Timestamp - before.Timestamp).Ticks;
        var targetTicks = (targetTime - before.Timestamp).Ticks;
        var ratio = (double)targetTicks / totalTicks;

        return (before.Value + (after.Value - before.Value) * ratio) * scale;
    }
}

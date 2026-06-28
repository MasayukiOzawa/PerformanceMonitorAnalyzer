namespace PerformanceMonitorAnalyzer;

public static class CounterStatisticsCalculator
{
    public static CounterStatistics Compute(
        string counterName,
        List<(DateTime Timestamp, double Value)> dataPoints,
        string unit)
    {
        if (!dataPoints.Any())
        {
            return new CounterStatistics
            {
                CounterName = counterName,
                DataPointCount = 0,
                Average = 0,
                Maximum = 0,
                Minimum = 0,
                StandardDeviation = 0,
                Unit = unit
            };
        }

        var values = dataPoints.Select(static dp => dp.Value).ToArray();
        var count = (uint)values.Length;
        var sum = values.Sum();
        var mean = sum / count;
        var min = values.Min();
        var max = values.Max();
        var variance = values.Select(v => Math.Pow(v - mean, 2)).Sum() / count;
        var standardDeviation = Math.Sqrt(variance);

        return new CounterStatistics
        {
            CounterName = counterName,
            DataPointCount = dataPoints.Count,
            Average = mean,
            Maximum = max,
            Minimum = min,
            StandardDeviation = standardDeviation,
            FirstTimestamp = dataPoints.Min(static dp => dp.Timestamp),
            LastTimestamp = dataPoints.Max(static dp => dp.Timestamp),
            Unit = unit
        };
    }

    public static CounterStatistics Calculate(
        string counter,
        List<PerformanceDataPoint> dataPoints,
        string unit)
    {
        if (!dataPoints.Any())
        {
            return new CounterStatistics
            {
                CounterName = counter,
                DataPointCount = 0
            };
        }

        var values = dataPoints.Select(static dp => dp.Value).ToList();
        var average = values.Average();
        var variance = values.Select(v => Math.Pow(v - average, 2)).Average();
        var standardDeviation = Math.Sqrt(variance);

        return new CounterStatistics
        {
            CounterName = counter,
            DataPointCount = dataPoints.Count,
            Average = average,
            Maximum = values.Max(),
            Minimum = values.Min(),
            StandardDeviation = standardDeviation,
            FirstTimestamp = dataPoints.Min(static dp => dp.Timestamp),
            LastTimestamp = dataPoints.Max(static dp => dp.Timestamp),
            Unit = unit
        };
    }
}

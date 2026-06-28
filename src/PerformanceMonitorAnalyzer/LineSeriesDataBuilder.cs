namespace PerformanceMonitorAnalyzer;

public static class LineSeriesDataBuilder
{
    public sealed record LineSeriesData(double[] XValues, double[] YValues);

    public static LineSeriesData Build(IEnumerable<PerformanceDataPoint> dataPoints, double scale)
    {
        var points = dataPoints.ToList();
        return new LineSeriesData(
            points.Select(static dp => dp.Timestamp.ToOADate()).ToArray(),
            points.Select(dp => dp.Value * scale).ToArray());
    }
}

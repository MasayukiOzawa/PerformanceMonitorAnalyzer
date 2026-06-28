namespace PerformanceMonitorAnalyzer;

public static class TimeRangeCalculator
{
    public static (DateTime StartTime, DateTime EndTime) CalculateRange(
        DateTime fileStart,
        DateTime fileEnd,
        double startPercent,
        double endPercent)
    {
        var totalDuration = fileEnd - fileStart;
        var startTime = fileStart.AddMilliseconds(totalDuration.TotalMilliseconds * startPercent / 100);
        var endTime = fileStart.AddMilliseconds(totalDuration.TotalMilliseconds * endPercent / 100);

        return (startTime, endTime);
    }
}

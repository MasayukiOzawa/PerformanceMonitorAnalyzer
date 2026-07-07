namespace PerformanceMonitorAnalyzer;

internal enum TimeHighlightSliderRole
{
    None,
    Start,
    End,
    Focus
}

internal static class TimeHighlightRangeCalculator
{
    public const double MinimumPercentGap = 1;

    public static (double StartPercent, double EndPercent, double FocusPercent) NormalizePercentRange(
        double startPercent,
        double endPercent,
        double focusPercent,
        TimeHighlightSliderRole changedRole)
    {
        var start = Math.Clamp(startPercent, 0, 100);
        var end = Math.Clamp(endPercent, 0, 100);

        if (start >= end)
        {
            if (changedRole == TimeHighlightSliderRole.Start)
            {
                end = Math.Min(100, start + MinimumPercentGap);
                start = Math.Min(start, end - MinimumPercentGap);
            }
            else
            {
                start = Math.Max(0, end - MinimumPercentGap);
                end = Math.Max(end, start + MinimumPercentGap);
            }
        }

        var focus = Math.Clamp(focusPercent, start, end);
        return (start, end, focus);
    }

    public static (DateTime StartTime, DateTime EndTime, DateTime FocusTime) CalculateRange(
        DateTime fileStart,
        DateTime fileEnd,
        double startPercent,
        double endPercent,
        double focusPercent)
    {
        var normalized = NormalizePercentRange(
            startPercent,
            endPercent,
            focusPercent,
            TimeHighlightSliderRole.None);

        var range = TimeRangeCalculator.CalculateRange(
            fileStart,
            fileEnd,
            normalized.StartPercent,
            normalized.EndPercent);

        var focusRange = TimeRangeCalculator.CalculateRange(
            fileStart,
            fileEnd,
            normalized.FocusPercent,
            normalized.FocusPercent);

        return (range.StartTime, range.EndTime, focusRange.StartTime);
    }
}

namespace PerformanceMonitorAnalyzer;

internal sealed class YAxisRangeState
{
    public const double DefaultMinimum = 0;
    public const double DefaultMaximum = 100;

    public bool IsManual { get; private set; }
    public double Minimum { get; private set; } = DefaultMinimum;
    public double Maximum { get; private set; } = DefaultMaximum;

    public void SetManual(double minimum, double maximum)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || maximum <= minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        Minimum = minimum;
        Maximum = maximum;
        IsManual = true;
    }

    public void Reset()
    {
        Minimum = DefaultMinimum;
        Maximum = DefaultMaximum;
        IsManual = false;
    }
}

internal static class YAxisRangeCalculator
{
    public static (double Minimum, double Maximum) CalculateAutomaticRange(
        IEnumerable<IEnumerable<double>> seriesValues,
        bool stacked)
    {
        var seriesMaximums = seriesValues
            .Select(values => values.Where(double.IsFinite).DefaultIfEmpty(double.NaN).Max())
            .Where(double.IsFinite)
            .ToList();

        if (seriesMaximums.Count == 0)
        {
            return (YAxisRangeState.DefaultMinimum, YAxisRangeState.DefaultMaximum);
        }

        var displayMaximum = seriesMaximums.Max();
        if (stacked)
        {
            var stackedUpperBound = seriesMaximums.Where(static value => value > 0).Sum();
            displayMaximum = Math.Max(displayMaximum, stackedUpperBound);
        }

        var maximum = displayMaximum > YAxisRangeState.DefaultMaximum
            ? Math.Ceiling(displayMaximum * 1.1)
            : YAxisRangeState.DefaultMaximum;

        return (YAxisRangeState.DefaultMinimum, maximum);
    }

    public static (double Minimum, double Maximum) CalculateDetailChartRange(IEnumerable<double> values)
    {
        var maximum = values
            .Where(double.IsFinite)
            .DefaultIfEmpty(0)
            .Max();

        return (YAxisRangeState.DefaultMinimum, maximum > 0 ? maximum * 1.1 : 1);
    }
}

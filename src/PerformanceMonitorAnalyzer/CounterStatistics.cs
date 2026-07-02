namespace PerformanceMonitorAnalyzer;

/// <summary>
/// カウンター統計情報
/// </summary>
public class CounterStatistics
{
    public string CounterName { get; set; } = string.Empty;
    public int DataPointCount { get; set; }
    public double Average { get; set; }
    public double Maximum { get; set; }
    public double Minimum { get; set; }
    public double StandardDeviation { get; set; }
    public DateTime FirstTimestamp { get; set; }
    public DateTime LastTimestamp { get; set; }
    public string Unit { get; set; } = string.Empty;

    public string FormattedAverage => $"{Average:N2}";
    public string FormattedMaximum => $"{Maximum:N2}";
    public string FormattedMinimum => $"{Minimum:N2}";
    public string FormattedStandardDeviation => $"{StandardDeviation:N2}";
}

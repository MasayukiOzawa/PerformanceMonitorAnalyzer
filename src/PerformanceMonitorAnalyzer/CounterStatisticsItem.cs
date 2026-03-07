namespace PerformanceMonitorAnalyzer;

/// <summary>
/// 統計情報表示用のデータクラス
/// </summary>
public class CounterStatisticsItem
{
    public string CounterName { get; set; } = string.Empty;
    public string Average { get; set; } = string.Empty;
    public string Maximum { get; set; } = string.Empty;
    public string Minimum { get; set; } = string.Empty;
}

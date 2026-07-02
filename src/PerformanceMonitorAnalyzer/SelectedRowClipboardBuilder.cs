using System.Globalization;
using System.Text;

namespace PerformanceMonitorAnalyzer;

internal static class SelectedRowClipboardBuilder
{
    public static string BuildPerformanceDataPointTsv(PerformanceDataPoint dataPoint)
    {
        return BuildPerformanceDataPointsTsv([dataPoint]);
    }

    public static string BuildPerformanceDataPointsTsv(IEnumerable<PerformanceDataPoint> dataPoints)
    {
        return BuildTsv(
            ["時間", "値", "カウンター"],
            dataPoints.Select(static dataPoint => new[]
            {
                dataPoint.Timestamp.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.CurrentCulture),
                dataPoint.Value.ToString("N2", CultureInfo.CurrentCulture),
                dataPoint.Counter
            }));
    }

    public static string BuildCounterStatisticsItemTsv(CounterStatisticsItem statisticsItem)
    {
        return BuildCounterStatisticsItemsTsv([statisticsItem]);
    }

    public static string BuildCounterStatisticsItemsTsv(IEnumerable<CounterStatisticsItem> statisticsItems)
    {
        return BuildTsv(
            ["カウンター名", "平均", "最大", "最小"],
            statisticsItems.Select(static statisticsItem => new[]
            {
                statisticsItem.CounterName,
                statisticsItem.Average,
                statisticsItem.Maximum,
                statisticsItem.Minimum
            }));
    }

    private static string BuildTsv(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var tsv = new StringBuilder();
        tsv.AppendLine(string.Join('\t', headers.Select(EscapeTsvField)));

        foreach (var row in rows)
        {
            tsv.AppendLine(string.Join('\t', row.Select(EscapeTsvField)));
        }

        if (tsv.Length >= Environment.NewLine.Length)
        {
            tsv.Length -= Environment.NewLine.Length;
        }

        return tsv.ToString();
    }

    private static string EscapeTsvField(string value)
    {
        return value
            .Replace('\t', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }
}

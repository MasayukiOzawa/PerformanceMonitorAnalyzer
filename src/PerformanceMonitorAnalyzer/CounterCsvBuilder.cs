using System.Text;

namespace PerformanceMonitorAnalyzer;

public static class CounterCsvBuilder
{
    public static string BuildSingleCounterCsv(IEnumerable<PerformanceDataPoint> dataPoints)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,Value,FormattedValue,Unit,Counter");

        foreach (var dataPoint in dataPoints.OrderBy(static dp => dp.Timestamp))
        {
            csv.AppendLine($"{dataPoint.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                           $"{dataPoint.Value}," +
                           $"\"{dataPoint.FormattedValue}\"," +
                           $"\"{dataPoint.Unit}\"," +
                           $"\"{dataPoint.Counter}\"");
        }

        return csv.ToString();
    }

    public static string BuildAllCountersCsv(
        IEnumerable<(string Counter, IEnumerable<PerformanceDataPoint> DataPoints)> counterData,
        Func<string, string> displayNameSelector)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,CounterName,Value,FormattedValue,Unit");

        var allData = counterData
            .SelectMany(counterPair => counterPair.DataPoints.Select(dataPoint => (
                Timestamp: dataPoint.Timestamp,
                Counter: counterPair.Counter,
                DataPoint: dataPoint)))
            .OrderBy(static item => item.Timestamp)
            .ThenBy(static item => item.Counter);

        foreach (var item in allData)
        {
            var dp = item.DataPoint;
            csv.AppendLine($"{dp.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                           $"\"{displayNameSelector(item.Counter)}\"," +
                           $"{dp.Value}," +
                           $"\"{dp.FormattedValue}\"," +
                           $"\"{dp.Unit}\"");
        }

        return csv.ToString();
    }

    public static string BuildDefaultFileName(string counterPath)
    {
        return $"{CounterPathFormatter.GetDisplayName(counterPath).Replace(" - ", "_").Replace(" ", "_")}.csv";
    }
}

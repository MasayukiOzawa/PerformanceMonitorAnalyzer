using System.IO;
using System.Text;

namespace PerformanceMonitorAnalyzer;

public static class CounterCsvBuilder
{
    public static string BuildSingleCounterCsv(IEnumerable<PerformanceDataPoint> dataPoints)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,Value,Counter");

        foreach (var dataPoint in dataPoints.OrderBy(static dp => dp.Timestamp))
        {
            csv.AppendLine($"{dataPoint.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                           $"{dataPoint.Value}," +
                           $"\"{dataPoint.Counter}\"");
        }

        return csv.ToString();
    }

    public static string BuildSingleCounterTsv(IEnumerable<PerformanceDataPoint> dataPoints)
    {
        var tsv = new StringBuilder();
        tsv.AppendLine("Timestamp\tValue\tCounter");

        foreach (var dataPoint in dataPoints.OrderBy(static dp => dp.Timestamp))
        {
            tsv.AppendLine($"{dataPoint.Timestamp:yyyy-MM-dd HH:mm:ss}\t" +
                           $"{dataPoint.Value}\t" +
                           $"{EscapeTsvField(dataPoint.Counter)}");
        }

        return tsv.ToString();
    }

    public static string BuildDefaultFileName(string counterPath)
    {
        var fileName = $"{CounterPathFormatter.GetDisplayName(counterPath).Replace(" - ", "_").Replace(" ", "_")}.csv";
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedFileName = new StringBuilder(fileName.Length);

        foreach (var character in fileName)
        {
            sanitizedFileName.Append(invalidChars.Contains(character) ? '_' : character);
        }

        return sanitizedFileName.ToString();
    }

    private static string EscapeTsvField(string value)
    {
        return value
            .Replace('\t', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }
}

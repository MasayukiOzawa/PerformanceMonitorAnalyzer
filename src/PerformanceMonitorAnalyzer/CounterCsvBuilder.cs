using System.IO;
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

    public static string BuildSingleCounterTsv(IEnumerable<PerformanceDataPoint> dataPoints)
    {
        var tsv = new StringBuilder();
        tsv.AppendLine("Timestamp\tValue\tFormattedValue\tUnit\tCounter");

        foreach (var dataPoint in dataPoints.OrderBy(static dp => dp.Timestamp))
        {
            tsv.AppendLine($"{dataPoint.Timestamp:yyyy-MM-dd HH:mm:ss}\t" +
                           $"{dataPoint.Value}\t" +
                           $"{EscapeTsvField(dataPoint.FormattedValue)}\t" +
                           $"{EscapeTsvField(dataPoint.Unit)}\t" +
                           $"{EscapeTsvField(dataPoint.Counter)}");
        }

        return tsv.ToString();
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

    public static string BuildAllCountersTsv(
        IEnumerable<(string Counter, IEnumerable<PerformanceDataPoint> DataPoints)> counterData,
        Func<string, string> displayNameSelector)
    {
        var tsv = new StringBuilder();
        tsv.AppendLine("Timestamp\tCounterName\tValue\tFormattedValue\tUnit");

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
            tsv.AppendLine($"{dp.Timestamp:yyyy-MM-dd HH:mm:ss}\t" +
                           $"{EscapeTsvField(displayNameSelector(item.Counter))}\t" +
                           $"{dp.Value}\t" +
                           $"{EscapeTsvField(dp.FormattedValue)}\t" +
                           $"{EscapeTsvField(dp.Unit)}");
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

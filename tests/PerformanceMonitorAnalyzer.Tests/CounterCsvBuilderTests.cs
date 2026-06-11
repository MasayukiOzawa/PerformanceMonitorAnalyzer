using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class CounterCsvBuilderTests
{
    [Fact]
    public void BuildSingleCounterCsv_AddsHeaderAndSortsByTimestamp()
    {
        var later = new DateTime(2026, 1, 1, 0, 0, 2);
        var earlier = new DateTime(2026, 1, 1, 0, 0, 1);
        var csv = CounterCsvBuilder.BuildSingleCounterCsv(new[]
        {
            Point(later, 2),
            Point(earlier, 1)
        });

        var lines = csv.TrimEnd().Split(Environment.NewLine);
        Assert.Equal("Timestamp,Value,FormattedValue,Unit,Counter", lines[0]);
        Assert.StartsWith("2026-01-01 00:00:01,1", lines[1]);
        Assert.StartsWith("2026-01-01 00:00:02,2", lines[2]);
        Assert.Contains("\"1.00\"", lines[1]);
    }

    [Fact]
    public void BuildAllCountersCsv_UsesDisplayNameAndTimestampCounterSort()
    {
        var csv = CounterCsvBuilder.BuildAllCountersCsv(
            new[]
            {
                (@"\B\Counter", (IEnumerable<PerformanceDataPoint>)new[] { Point(new DateTime(2026, 1, 1), 2, @"\B\Counter") }),
                (@"\A\Counter", new[] { Point(new DateTime(2026, 1, 1), 1, @"\A\Counter") })
            },
            counter => $"display:{counter}");

        var lines = csv.TrimEnd().Split(Environment.NewLine);
        Assert.Equal("Timestamp,CounterName,Value,FormattedValue,Unit", lines[0]);
        Assert.Contains("\"display:\\A\\Counter\"", lines[1]);
        Assert.Contains("\"display:\\B\\Counter\"", lines[2]);
    }

    [Fact]
    public void BuildDefaultFileName_UsesDisplayName()
    {
        Assert.Equal("Processor(_Total)_%_Processor_Time.csv", CounterCsvBuilder.BuildDefaultFileName(@"\Processor(_Total)\% Processor Time"));
    }

    private static PerformanceDataPoint Point(DateTime timestamp, double value, string counter = @"\Counter")
    {
        return new PerformanceDataPoint
        {
            Counter = counter,
            Timestamp = timestamp,
            Value = value,
            FormattedValue = $"{value:N2}",
            Unit = "%"
        };
    }
}

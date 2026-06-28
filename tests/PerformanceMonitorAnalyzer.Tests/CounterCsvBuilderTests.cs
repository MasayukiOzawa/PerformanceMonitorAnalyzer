using System.IO;
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
    public void BuildSingleCounterTsv_AddsHeaderSortsByTimestampAndEscapesTabs()
    {
        var later = new DateTime(2026, 1, 1, 0, 0, 2);
        var earlier = new DateTime(2026, 1, 1, 0, 0, 1);
        var tsv = CounterCsvBuilder.BuildSingleCounterTsv(new[]
        {
            Point(later, 2),
            Point(earlier, 1, "\\Counter\tWithTab")
        });

        var lines = tsv.TrimEnd().Split(Environment.NewLine);
        Assert.Equal("Timestamp\tValue\tFormattedValue\tUnit\tCounter", lines[0]);
        Assert.StartsWith("2026-01-01 00:00:01\t1\t1.00\t%\t", lines[1]);
        Assert.EndsWith(@"\Counter WithTab", lines[1]);
        Assert.StartsWith("2026-01-01 00:00:02\t2\t2.00\t%", lines[2]);
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
    public void BuildAllCountersTsv_UsesDisplayNameAndTimestampCounterSort()
    {
        var tsv = CounterCsvBuilder.BuildAllCountersTsv(
            new[]
            {
                (@"\B\Counter", (IEnumerable<PerformanceDataPoint>)new[] { Point(new DateTime(2026, 1, 1), 2, @"\B\Counter") }),
                (@"\A\Counter", new[] { Point(new DateTime(2026, 1, 1), 1, @"\A\Counter") })
            },
            counter => $"display:{counter}");

        var lines = tsv.TrimEnd().Split(Environment.NewLine);
        Assert.Equal("Timestamp\tCounterName\tValue\tFormattedValue\tUnit", lines[0]);
        Assert.Contains("display:\\A\\Counter", lines[1]);
        Assert.Contains("display:\\B\\Counter", lines[2]);
    }

    [Fact]
    public void BuildDefaultFileName_UsesDisplayName()
    {
        Assert.Equal("Processor(_Total)_%_Processor_Time.csv", CounterCsvBuilder.BuildDefaultFileName(@"\Processor(_Total)\% Processor Time"));
    }

    [Fact]
    public void BuildDefaultFileName_ReplacesInvalidFileNameCharacters()
    {
        var fileName = CounterCsvBuilder.BuildDefaultFileName(@"\SQLServer:Locks\Lock Waits/sec");
        var invalidChars = Path.GetInvalidFileNameChars();

        Assert.Equal("SQLServer_Locks_Lock_Waits_sec.csv", fileName);
        Assert.DoesNotContain(fileName, character => invalidChars.Contains(character));
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

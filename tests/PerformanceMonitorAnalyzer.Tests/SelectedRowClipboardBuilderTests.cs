using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public sealed class SelectedRowClipboardBuilderTests
{
    [Fact]
    public void BuildPerformanceDataPointTsv_UsesDisplayedColumnsAndEscapesFields()
    {
        using var culture = new TestCultureScope("en-US");
        var tsv = SelectedRowClipboardBuilder.BuildPerformanceDataPointTsv(new PerformanceDataPoint
        {
            Timestamp = new DateTime(2026, 7, 1, 11, 39, 0),
            Value = 1234.567,
            Counter = "\\Counter\tWithTab"
        });

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "時間\t値\tカウンター",
                "2026/07/01 11:39:00\t1,234.57\t\\Counter WithTab"),
            tsv);
    }

    [Fact]
    public void BuildPerformanceDataPointsTsv_CopiesMultipleRowsWithSingleHeader()
    {
        using var culture = new TestCultureScope("en-US");
        var tsv = SelectedRowClipboardBuilder.BuildPerformanceDataPointsTsv(
        [
            new PerformanceDataPoint
            {
                Timestamp = new DateTime(2026, 7, 1, 11, 39, 0),
                Value = 1,
                Counter = "CounterA"
            },
            new PerformanceDataPoint
            {
                Timestamp = new DateTime(2026, 7, 1, 11, 40, 0),
                Value = 2.5,
                Counter = "CounterB"
            }
        ]);

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "時間\t値\tカウンター",
                "2026/07/01 11:39:00\t1.00\tCounterA",
                "2026/07/01 11:40:00\t2.50\tCounterB"),
            tsv);
    }

    [Fact]
    public void BuildCounterStatisticsItemTsv_UsesDisplayedColumnsAndEscapesFields()
    {
        var tsv = SelectedRowClipboardBuilder.BuildCounterStatisticsItemTsv(new CounterStatisticsItem
        {
            CounterName = "Counter\nName",
            Average = "1.00",
            Maximum = "2.00",
            Minimum = "0.00"
        });

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "カウンター名\t平均\t最大\t最小",
                "Counter Name\t1.00\t2.00\t0.00"),
            tsv);
    }

    [Fact]
    public void BuildCounterStatisticsItemsTsv_CopiesMultipleRowsWithSingleHeader()
    {
        var tsv = SelectedRowClipboardBuilder.BuildCounterStatisticsItemsTsv(
        [
            new CounterStatisticsItem
            {
                CounterName = "CounterA",
                Average = "1.00",
                Maximum = "2.00",
                Minimum = "0.00"
            },
            new CounterStatisticsItem
            {
                CounterName = "CounterB",
                Average = "3.00",
                Maximum = "4.00",
                Minimum = "2.00"
            }
        ]);

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "カウンター名\t平均\t最大\t最小",
                "CounterA\t1.00\t2.00\t0.00",
                "CounterB\t3.00\t4.00\t2.00"),
            tsv);
    }
}

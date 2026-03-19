using System.ComponentModel;
using System.Globalization;
using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class StatisticsGridSorterTests
{
    [Fact]
    public void SortItems_AverageAscending_OrdersByNumericAverageValue()
    {
        using var _ = new CurrentCultureScope("ja-JP");
        var items = new[]
        {
            CreateItem("Counter-B", average: 10, maximum: 30, minimum: 1),
            CreateItem("Counter-A", average: 2, maximum: 20, minimum: 3),
            CreateItem("Counter-C", average: 100, maximum: 10, minimum: 2)
        };

        var sorted = StatisticsGridSorter.SortItems(
            items,
            nameof(CounterStatisticsItem.AverageValue),
            ListSortDirection.Ascending);

        Assert.Equal(["Counter-A", "Counter-B", "Counter-C"], sorted.Select(item => item.CounterName).ToArray());
    }

    [Fact]
    public void SortItems_MaximumDescending_OrdersByNumericMaximumValue()
    {
        using var _ = new CurrentCultureScope("ja-JP");
        var items = new[]
        {
            CreateItem("Counter-B", average: 10, maximum: 30, minimum: 1),
            CreateItem("Counter-A", average: 2, maximum: 20, minimum: 3),
            CreateItem("Counter-C", average: 100, maximum: 10, minimum: 2)
        };

        var sorted = StatisticsGridSorter.SortItems(
            items,
            nameof(CounterStatisticsItem.MaximumValue),
            ListSortDirection.Descending);

        Assert.Equal(["Counter-B", "Counter-A", "Counter-C"], sorted.Select(item => item.CounterName).ToArray());
    }

    [Fact]
    public void SortItems_EqualValues_FallsBackToCounterName()
    {
        using var _ = new CurrentCultureScope("ja-JP");
        var items = new[]
        {
            CreateItem("Counter-B", average: 10, maximum: 30, minimum: 5),
            CreateItem("Counter-A", average: 20, maximum: 40, minimum: 5),
            CreateItem("Counter-C", average: 30, maximum: 50, minimum: 5)
        };

        var sorted = StatisticsGridSorter.SortItems(
            items,
            nameof(CounterStatisticsItem.MinimumValue),
            ListSortDirection.Ascending);

        Assert.Equal(["Counter-A", "Counter-B", "Counter-C"], sorted.Select(item => item.CounterName).ToArray());
    }

    [Fact]
    public void SortItems_CounterNameDescending_OrdersByCounterName()
    {
        using var _ = new CurrentCultureScope("ja-JP");
        var items = new[]
        {
            CreateItem("Counter-B", average: 10, maximum: 30, minimum: 1),
            CreateItem("Counter-A", average: 2, maximum: 20, minimum: 3),
            CreateItem("Counter-C", average: 100, maximum: 10, minimum: 2)
        };

        var sorted = StatisticsGridSorter.SortItems(
            items,
            nameof(CounterStatisticsItem.CounterName),
            ListSortDirection.Descending);

        Assert.Equal(["Counter-C", "Counter-B", "Counter-A"], sorted.Select(item => item.CounterName).ToArray());
    }

    [Fact]
    public void GetNextSortDirection_SameColumnAfterAscending_ReturnsDescending()
    {
        var nextDirection = StatisticsGridSorter.GetNextSortDirection(
            nameof(CounterStatisticsItem.AverageValue),
            ListSortDirection.Ascending,
            nameof(CounterStatisticsItem.AverageValue));

        Assert.Equal(ListSortDirection.Descending, nextDirection);
    }

    [Fact]
    public void GetNextSortDirection_DifferentColumn_StartsAscending()
    {
        var nextDirection = StatisticsGridSorter.GetNextSortDirection(
            nameof(CounterStatisticsItem.AverageValue),
            ListSortDirection.Descending,
            nameof(CounterStatisticsItem.MaximumValue));

        Assert.Equal(ListSortDirection.Ascending, nextDirection);
    }

    private static CounterStatisticsItem CreateItem(string counterName, double average, double maximum, double minimum)
    {
        return new CounterStatisticsItem
        {
            CounterName = counterName,
            Average = average.ToString("N2", CultureInfo.CurrentCulture),
            AverageValue = average,
            Maximum = maximum.ToString("N2", CultureInfo.CurrentCulture),
            MaximumValue = maximum,
            Minimum = minimum.ToString("N2", CultureInfo.CurrentCulture),
            MinimumValue = minimum
        };
    }

    private sealed class CurrentCultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture;
        private readonly CultureInfo _previousUiCulture;

        public CurrentCultureScope(string cultureName)
        {
            _previousCulture = CultureInfo.CurrentCulture;
            _previousUiCulture = CultureInfo.CurrentUICulture;
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUiCulture;
        }
    }
}

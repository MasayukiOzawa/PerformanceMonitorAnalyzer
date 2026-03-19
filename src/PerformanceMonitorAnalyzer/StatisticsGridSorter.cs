using System.ComponentModel;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// 統計情報グリッドの並び替えを行うヘルパー
/// </summary>
public static class StatisticsGridSorter
{
    public static ListSortDirection GetNextSortDirection(
        string? currentSortMemberPath,
        ListSortDirection? currentSortDirection,
        string requestedSortMemberPath)
    {
        return currentSortMemberPath == requestedSortMemberPath &&
               currentSortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
    }

    public static List<CounterStatisticsItem> SortItems(
        IEnumerable<CounterStatisticsItem> items,
        string? sortMemberPath,
        ListSortDirection? sortDirection)
    {
        var itemList = items.ToList();

        if (string.IsNullOrWhiteSpace(sortMemberPath) || sortDirection is null)
        {
            return itemList;
        }

        return (sortMemberPath, sortDirection.Value) switch
        {
            (nameof(CounterStatisticsItem.CounterName), ListSortDirection.Ascending) => itemList
                .OrderBy(item => item.CounterName, StringComparer.CurrentCulture)
                .ToList(),
            (nameof(CounterStatisticsItem.CounterName), ListSortDirection.Descending) => itemList
                .OrderByDescending(item => item.CounterName, StringComparer.CurrentCulture)
                .ToList(),
            (nameof(CounterStatisticsItem.AverageValue), ListSortDirection.Ascending) => itemList
                .OrderBy(item => item.AverageValue)
                .ThenBy(item => item.CounterName, StringComparer.CurrentCulture)
                .ToList(),
            (nameof(CounterStatisticsItem.AverageValue), ListSortDirection.Descending) => itemList
                .OrderByDescending(item => item.AverageValue)
                .ThenBy(item => item.CounterName, StringComparer.CurrentCulture)
                .ToList(),
            (nameof(CounterStatisticsItem.MaximumValue), ListSortDirection.Ascending) => itemList
                .OrderBy(item => item.MaximumValue)
                .ThenBy(item => item.CounterName, StringComparer.CurrentCulture)
                .ToList(),
            (nameof(CounterStatisticsItem.MaximumValue), ListSortDirection.Descending) => itemList
                .OrderByDescending(item => item.MaximumValue)
                .ThenBy(item => item.CounterName, StringComparer.CurrentCulture)
                .ToList(),
            (nameof(CounterStatisticsItem.MinimumValue), ListSortDirection.Ascending) => itemList
                .OrderBy(item => item.MinimumValue)
                .ThenBy(item => item.CounterName, StringComparer.CurrentCulture)
                .ToList(),
            (nameof(CounterStatisticsItem.MinimumValue), ListSortDirection.Descending) => itemList
                .OrderByDescending(item => item.MinimumValue)
                .ThenBy(item => item.CounterName, StringComparer.CurrentCulture)
                .ToList(),
            _ => itemList
        };
    }
}

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace PerformanceMonitorAnalyzer;

public partial class CounterAddDialog : Window
{
    private readonly IReadOnlyList<CounterTreeNode> _counterTreeNodes;
    private readonly Func<IReadOnlyList<string>, int> _addCounters;
    private readonly ObservableCollection<string> _counterObjectNames = new();
    private readonly ObservableCollection<CounterSelectorItem> _availableCounterItems = new();
    private readonly ObservableCollection<CounterSelectorItem> _availableInstanceItems = new();
    private bool _isRefreshingCounterSelector;

    public CounterAddDialog(
        IReadOnlyList<CounterTreeNode> counterTreeNodes,
        Func<IReadOnlyList<string>, int> addCounters)
    {
        _counterTreeNodes = counterTreeNodes;
        _addCounters = addCounters;

        InitializeComponent();

        CounterObjectListBox.ItemsSource = _counterObjectNames;
        AvailableCountersListBox.ItemsSource = _availableCounterItems;
        AvailableInstancesListBox.ItemsSource = _availableInstanceItems;

        RefreshCounterSelector();
    }

    private void RefreshCounterSelector()
    {
        _isRefreshingCounterSelector = true;
        try
        {
            _counterObjectNames.Clear();
            _availableCounterItems.Clear();
            _availableInstanceItems.Clear();

            foreach (var objectName in CounterSelectionModel.GetObjectNames(_counterTreeNodes))
            {
                _counterObjectNames.Add(objectName);
            }

            CounterObjectListBox.SelectedIndex = _counterObjectNames.Count > 0 ? 0 : -1;
            AddCountersButton.IsEnabled = _counterObjectNames.Count > 0;
            StatusText.Text = _counterObjectNames.Count > 0
                ? "追加するカウンターを選択してください。"
                : "追加できるカウンターがありません。";
        }
        finally
        {
            _isRefreshingCounterSelector = false;
        }

        RefreshAvailableCounterSelectorItems();
    }

    private void CounterObjectListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingCounterSelector)
        {
            return;
        }

        RefreshAvailableCounterSelectorItems();
    }

    private void AvailableCountersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingCounterSelector)
        {
            return;
        }

        UpdateInstanceSelectionAvailability();
    }

    private void RefreshAvailableCounterSelectorItems()
    {
        _isRefreshingCounterSelector = true;
        try
        {
            _availableCounterItems.Clear();
            _availableInstanceItems.Clear();

            var objectNode = GetSelectedCounterObjectNode();
            if (objectNode is null)
            {
                return;
            }

            foreach (var counterItem in CounterSelectionModel.CreateCounterItems(objectNode))
            {
                _availableCounterItems.Add(counterItem);
            }

            var instanceItems = CounterSelectionModel.CreateInstanceItems(objectNode);
            foreach (var instanceItem in instanceItems)
            {
                _availableInstanceItems.Add(instanceItem);
            }

            ResetCounterSelectorRows();
        }
        finally
        {
            _isRefreshingCounterSelector = false;
        }

        UpdateInstanceSelectionAvailability();
    }

    private void ResetCounterSelectorRows()
    {
        AvailableCountersListBox.SelectedIndex = -1;
        AvailableInstancesListBox.SelectedIndex = -1;
    }

    private void SelectDefaultInstanceRow()
    {
        var totalInstance = _availableInstanceItems
            .FirstOrDefault(static item => !item.IsAllInstances && string.Equals(item.DisplayName, "_Total", StringComparison.Ordinal));
        var defaultInstance = totalInstance ?? _availableInstanceItems.FirstOrDefault(static item => !item.IsAllInstances);
        if (defaultInstance is not null)
        {
            AvailableInstancesListBox.SelectedItem = defaultInstance;
        }
    }

    private void UpdateInstanceSelectionAvailability()
    {
        var hasInstanceItems = _availableInstanceItems.Count > 0;
        var canSelectInstances = !hasInstanceItems || HasSelectedCounterItems();

        AvailableInstancesListBox.IsEnabled = canSelectInstances;
        AvailableInstancesListBox.ToolTip = hasInstanceItems && !canSelectInstances
            ? "カウンターを選択するとインスタンスを選択できます。"
            : null;

        if (!hasInstanceItems)
        {
            AvailableInstancesListBox.SelectedIndex = -1;
            return;
        }

        if (!canSelectInstances)
        {
            ClearInstanceSelection();
            return;
        }

        if (!HasSelectedInstanceItems())
        {
            SelectDefaultInstanceRow();
        }
    }

    private bool HasSelectedCounterItems()
    {
        return _availableCounterItems.Any(static item => item.IsChecked) ||
               AvailableCountersListBox.SelectedItems.OfType<CounterSelectorItem>().Any();
    }

    private bool HasSelectedInstanceItems()
    {
        return _availableInstanceItems.Any(static item => item.IsChecked) ||
               AvailableInstancesListBox.SelectedItems.OfType<CounterSelectorItem>().Any();
    }

    private void ClearInstanceSelection()
    {
        AvailableInstancesListBox.SelectedIndex = -1;
        foreach (var instanceItem in _availableInstanceItems)
        {
            instanceItem.IsChecked = false;
        }
    }

    private CounterTreeNode? GetSelectedCounterObjectNode()
    {
        return CounterObjectListBox.SelectedItem is string objectName
            ? CounterSelectionModel.FindObjectNode(_counterTreeNodes, objectName)
            : null;
    }

    private void CounterSelectorCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isRefreshingCounterSelector)
        {
            return;
        }

        if (sender is CheckBox checkBox && checkBox.Tag is CounterSelectorItem item)
        {
            item.IsChecked = checkBox.IsChecked == true;
        }

        if (sender is CheckBox { Tag: CounterSelectorItem itemForList } &&
            _availableCounterItems.Contains(itemForList))
        {
            UpdateInstanceSelectionAvailability();
        }
    }

    private void AddCounters_Click(object sender, RoutedEventArgs e)
    {
        var objectNode = GetSelectedCounterObjectNode();
        if (objectNode is null)
        {
            StatusText.Text = "追加するオブジェクトが選択されていません。";
            return;
        }

        var selectedCounters = GetCheckedOrSelectedCounterItems(AvailableCountersListBox, _availableCounterItems);
        var selectedInstances = GetCheckedOrSelectedCounterItems(AvailableInstancesListBox, _availableInstanceItems);
        var requiresInstanceSelection = CounterSelectionModel.HasSelectableInstances(objectNode);

        if (selectedCounters.Count == 0 || (requiresInstanceSelection && selectedInstances.Count == 0))
        {
            StatusText.Text = "追加するカウンターまたはインスタンスを選択してください。";
            return;
        }

        var counterPaths = CounterSelectionModel.CreateSelectedCounterPaths(objectNode, selectedCounters, selectedInstances);
        if (counterPaths.Count == 0)
        {
            StatusText.Text = "追加できるカウンターが見つかりませんでした。";
            return;
        }

        var addedCount = _addCounters(counterPaths);
        StatusText.Text = addedCount > 0
            ? $"{addedCount} 個のカウンターを追加しました。続けて選択できます。"
            : "選択したカウンターはすでに追加済みです。";
    }

    private static List<CounterSelectorItem> GetCheckedOrSelectedCounterItems(
        ListBox listBox,
        ObservableCollection<CounterSelectorItem> items)
    {
        var checkedItems = items.Where(static item => item.IsChecked).ToList();
        if (checkedItems.Count > 0)
        {
            return checkedItems;
        }

        return listBox.SelectedItems
            .OfType<CounterSelectorItem>()
            .ToList();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

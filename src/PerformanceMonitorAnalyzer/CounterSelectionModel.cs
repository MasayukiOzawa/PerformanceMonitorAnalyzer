namespace PerformanceMonitorAnalyzer;

public static class CounterSelectionModel
{
    public static List<string> GetObjectNames(IEnumerable<CounterTreeNode> rootNodes)
    {
        return rootNodes
            .Select(static node => node.DisplayName)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();
    }

    public static CounterTreeNode? FindObjectNode(IEnumerable<CounterTreeNode> rootNodes, string objectName)
    {
        return rootNodes.FirstOrDefault(node => string.Equals(node.DisplayName, objectName, StringComparison.Ordinal));
    }

    public static List<CounterSelectorItem> CreateCounterItems(CounterTreeNode objectNode)
    {
        return GetActualInstanceNodes(objectNode, sortByDisplayName: true)
            .SelectMany(static instanceNode => instanceNode.Children)
            .Where(static counterNode => !counterNode.IsWildCard)
            .Select(static counterNode => counterNode.DisplayName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .Select(counterName => new CounterSelectorItem
            {
                DisplayName = counterName,
                ObjectName = objectNode.DisplayName,
                CounterName = counterName
            })
            .ToList();
    }

    public static List<CounterSelectorItem> CreateInstanceItems(CounterTreeNode objectNode)
    {
        var visibleInstanceNodes = GetVisibleInstanceNodes(GetActualInstanceNodes(objectNode, sortByDisplayName: true));
        var items = new List<CounterSelectorItem>();

        if (visibleInstanceNodes.Count > 1)
        {
            items.Add(new CounterSelectorItem
            {
                DisplayName = "<すべてのインスタンス>",
                ObjectName = objectNode.DisplayName,
                IsAllInstances = true
            });
        }

        items.AddRange(visibleInstanceNodes.Select(instanceNode => new CounterSelectorItem
        {
            DisplayName = instanceNode.DisplayName,
            ObjectName = objectNode.DisplayName,
            InstanceName = instanceNode.DisplayName
        }));

        return items;
    }

    public static bool HasSelectableInstances(CounterTreeNode objectNode)
    {
        return GetVisibleInstanceNodes(GetActualInstanceNodes(objectNode)).Count > 0;
    }

    public static List<CounterTreeNode> ResolveSelectedInstanceNodes(
        CounterTreeNode objectNode,
        IReadOnlyCollection<CounterSelectorItem> selectedInstances)
    {
        var actualInstanceNodes = GetActualInstanceNodes(objectNode);
        var visibleInstanceNodes = GetVisibleInstanceNodes(actualInstanceNodes);

        if (visibleInstanceNodes.Count == 0)
        {
            return actualInstanceNodes;
        }

        if (selectedInstances.Any(static item => item.IsAllInstances))
        {
            return visibleInstanceNodes;
        }

        var selectedInstanceNames = selectedInstances
            .Where(static item => !item.IsAllInstances)
            .Select(static item => item.InstanceName)
            .ToHashSet(StringComparer.Ordinal);

        return actualInstanceNodes
            .Where(node => selectedInstanceNames.Contains(node.DisplayName))
            .ToList();
    }

    public static List<string> CreateSelectedCounterPaths(
        CounterTreeNode objectNode,
        IReadOnlyCollection<CounterSelectorItem> selectedCounters,
        IReadOnlyCollection<CounterSelectorItem> selectedInstances)
    {
        if (selectedCounters.Count == 0 ||
            (HasSelectableInstances(objectNode) && selectedInstances.Count == 0))
        {
            return [];
        }

        var selectedPaths = new List<string>();
        var addedPaths = new HashSet<string>(StringComparer.Ordinal);
        var instanceNodes = ResolveSelectedInstanceNodes(objectNode, selectedInstances);

        foreach (var counterItem in selectedCounters)
        {
            foreach (var instanceNode in instanceNodes)
            {
                var counterNode = instanceNode.Children.FirstOrDefault(node =>
                    !node.IsWildCard &&
                    string.Equals(node.DisplayName, counterItem.CounterName, StringComparison.Ordinal));
                if (counterNode is not null && addedPaths.Add(counterNode.FullPath))
                {
                    selectedPaths.Add(counterNode.FullPath);
                }
            }
        }

        return selectedPaths;
    }

    public static List<CounterTreeNode> GetActualInstanceNodes(CounterTreeNode objectNode, bool sortByDisplayName = false)
    {
        var instanceNodes = objectNode.Children.Where(static node => !node.IsWildCard);
        return sortByDisplayName
            ? instanceNodes.OrderBy(static node => node.DisplayName, StringComparer.Ordinal).ToList()
            : instanceNodes.ToList();
    }

    public static List<CounterTreeNode> GetVisibleInstanceNodes(IEnumerable<CounterTreeNode> instanceNodes)
    {
        return instanceNodes
            .Where(static node => !string.IsNullOrWhiteSpace(node.DisplayName))
            .ToList();
    }
}

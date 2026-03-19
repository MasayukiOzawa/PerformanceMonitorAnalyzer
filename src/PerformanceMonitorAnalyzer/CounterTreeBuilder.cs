namespace PerformanceMonitorAnalyzer;

public static class CounterTreeBuilder
{
    public static List<CounterTreeNode> Build(IEnumerable<string> counters)
    {
        ArgumentNullException.ThrowIfNull(counters);

        var objectGroups = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);

        foreach (var counter in counters)
        {
            if (!TryParseCounterPath(counter, out var objectName, out var instanceName))
            {
                continue;
            }

            if (!objectGroups.TryGetValue(objectName, out var instanceGroups))
            {
                instanceGroups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                objectGroups[objectName] = instanceGroups;
            }

            if (!instanceGroups.TryGetValue(instanceName, out var counterPaths))
            {
                counterPaths = new List<string>();
                instanceGroups[instanceName] = counterPaths;
            }

            counterPaths.Add(counter);
        }

        var rootNodes = new List<CounterTreeNode>();

        foreach (var objectGroup in objectGroups.OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            var objectNode = new CounterTreeNode
            {
                DisplayName = objectGroup.Key,
                FullPath = string.Empty,
                Type = NodeType.Object
            };

            if (HasWildcardInstance(objectGroup.Value))
            {
                var wildcardInstanceNode = BuildWildcardInstanceNode(objectGroup.Key, objectGroup.Value);
                wildcardInstanceNode.Parent = objectNode;
                objectNode.Children.Add(wildcardInstanceNode);
            }

            foreach (var instanceGroup in objectGroup.Value.OrderBy(static group => group.Key, StringComparer.Ordinal))
            {
                var instanceNode = new CounterTreeNode
                {
                    DisplayName = instanceGroup.Key == NoInstanceName ? SummaryInstanceDisplayName : instanceGroup.Key,
                    FullPath = string.Empty,
                    Type = NodeType.Instance,
                    Parent = objectNode
                };

                foreach (var counterPath in instanceGroup.Value.OrderBy(static path => path, StringComparer.Ordinal))
                {
                    instanceNode.Children.Add(new CounterTreeNode
                    {
                        DisplayName = ExtractCounterName(counterPath),
                        FullPath = counterPath,
                        Type = NodeType.Counter,
                        Parent = instanceNode
                    });
                }

                objectNode.Children.Add(instanceNode);
            }

            rootNodes.Add(objectNode);
        }

        return rootNodes;
    }

    private const string NoInstanceName = "(なし)";
    private const string SummaryInstanceDisplayName = "(総合)";

    private static bool TryParseCounterPath(string counterPath, out string objectName, out string instanceName)
    {
        objectName = string.Empty;
        instanceName = NoInstanceName;

        if (string.IsNullOrWhiteSpace(counterPath))
        {
            return false;
        }

        var parts = counterPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
        {
            objectName = parts[1];
        }
        else if (parts.Length >= 2)
        {
            objectName = parts[0];
        }
        else
        {
            return false;
        }

        if (!TryExtractObjectAndInstance(objectName, out objectName, out instanceName))
        {
            instanceName = NoInstanceName;
        }

        return true;
    }

    private static bool TryExtractObjectAndInstance(string rawObjectName, out string objectName, out string instanceName)
    {
        objectName = rawObjectName;
        instanceName = NoInstanceName;

        var startIndex = rawObjectName.IndexOf('(');
        var endIndex = rawObjectName.LastIndexOf(')');
        if (startIndex < 0 || endIndex <= startIndex)
        {
            return false;
        }

        objectName = rawObjectName[..startIndex];
        instanceName = rawObjectName.Substring(startIndex + 1, endIndex - startIndex - 1);
        return true;
    }

    private static bool HasWildcardInstance(Dictionary<string, List<string>> instanceGroups)
    {
        if (instanceGroups.Count > 1)
        {
            return true;
        }

        return instanceGroups.Count == 1 && !string.Equals(instanceGroups.Keys.First(), NoInstanceName, StringComparison.Ordinal);
    }

    private static CounterTreeNode BuildWildcardInstanceNode(
        string objectName,
        Dictionary<string, List<string>> instanceGroups)
    {
        var allCounterNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var counterPath in instanceGroups.Values.SelectMany(static paths => paths))
        {
            allCounterNames.Add(ExtractCounterName(counterPath));
        }

        var wildcardInstanceNode = new CounterTreeNode
        {
            DisplayName = "*",
            FullPath = string.Empty,
            Type = NodeType.Instance,
            IsWildCard = true
        };

        foreach (var counterName in allCounterNames.OrderBy(static name => name, StringComparer.Ordinal))
        {
            wildcardInstanceNode.Children.Add(new CounterTreeNode
            {
                DisplayName = counterName,
                FullPath = $"WILDCARD:{objectName}:*:{counterName}",
                Type = NodeType.Counter,
                Parent = wildcardInstanceNode,
                IsWildCard = true
            });
        }

        return wildcardInstanceNode;
    }

    private static string ExtractCounterName(string counterPath)
    {
        var parts = counterPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            >= 3 => parts[2],
            >= 2 => parts[1],
            _ => counterPath
        };
    }
}

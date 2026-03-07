using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class CounterTreeNodeTests
{
    [Fact]
    public void ParentSelection_SelectsAllLeafCounters()
    {
        var root = CreateTree();

        root.IsChecked = true;

        Assert.True(root.IsChecked);
        Assert.All(root.Children, child => Assert.True(child.IsChecked));
        Assert.Equal(2, root.GetSelectedCounters().Count());
    }

    [Fact]
    public void ChildSelection_MakesAncestorsIndeterminate()
    {
        var root = CreateTree();
        var firstLeaf = root.Children[0].Children[0];

        firstLeaf.IsChecked = true;

        Assert.Null(root.IsChecked);
        Assert.Null(root.Children[0].IsChecked);
        Assert.Single(root.GetSelectedCounters());
    }

    [Fact]
    public void ToggleFromUserInteraction_OnPartialParent_SelectsAllChildren()
    {
        var root = CreateTree();
        var firstLeaf = root.Children[0].Children[0];
        firstLeaf.IsChecked = true;

        root.ToggleFromUserInteraction();

        Assert.True(root.IsChecked);
        Assert.All(root.Children, child => Assert.True(child.IsChecked));
        Assert.Equal(2, root.GetSelectedCounters().Count());
    }

    [Fact]
    public void ToggleFromUserInteraction_OnCheckedParent_ClearsAllChildren()
    {
        var root = CreateTree();
        root.IsChecked = true;

        root.ToggleFromUserInteraction();

        Assert.False(root.IsChecked);
        Assert.All(root.Children, child => Assert.False(child.IsChecked));
        Assert.Empty(root.GetSelectedCounters());
    }

    [Fact]
    public void GetSelectedCounters_ExcludesWildcardLeafNodes()
    {
        var root = CreateTree();
        var wildcardLeaf = new CounterTreeNode
        {
            DisplayName = "*",
            FullPath = "WILDCARD:Processor:*:% Processor Time",
            Type = NodeType.Counter,
            IsWildCard = true,
            Parent = root.Children[0],
            IsChecked = true
        };

        root.Children[0].Children.Add(wildcardLeaf);
        root.Children[0].UpdateParentStateFromChild();
        root.UpdateParentStateFromChild();

        Assert.Empty(root.GetSelectedCounters());
    }

    private static CounterTreeNode CreateTree()
    {
        var objectNode = new CounterTreeNode
        {
            DisplayName = "Processor",
            Type = NodeType.Object
        };

        var instanceNode = new CounterTreeNode
        {
            DisplayName = "_Total",
            Type = NodeType.Instance,
            Parent = objectNode
        };

        var cpuCounter = new CounterTreeNode
        {
            DisplayName = "% Processor Time",
            FullPath = @"\Processor(_Total)\% Processor Time",
            Type = NodeType.Counter,
            Parent = instanceNode
        };

        var userCounter = new CounterTreeNode
        {
            DisplayName = "% User Time",
            FullPath = @"\Processor(_Total)\% User Time",
            Type = NodeType.Counter,
            Parent = instanceNode
        };

        instanceNode.Children.Add(cpuCounter);
        instanceNode.Children.Add(userCounter);
        objectNode.Children.Add(instanceNode);

        return objectNode;
    }
}

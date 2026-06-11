using System.Collections.ObjectModel;
using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class CounterTreeNodeHierarchyTests
{
    [Fact]
    public void ObjectLevelSelection_SelectsAllCounters()
    {
        var nodes = CreateTestTree();
        var processorObject = nodes[0];

        processorObject.IsChecked = true;

        var selectedCounters = processorObject.GetSelectedCounters().ToList();
        Assert.Equal(2, selectedCounters.Count);
        Assert.True(processorObject.IsChecked);
        Assert.All(processorObject.Children, child => Assert.True(child.IsChecked));
        Assert.Equal(
            new[]
            {
                @"\Processor(_Total)\% Processor Time",
                @"\Processor(_Total)\% User Time"
            },
            selectedCounters.Select(counter => counter.FullPath).ToArray());
    }

    [Fact]
    public void InstanceLevelSelection_SelectsAllCountersAndChecksObject()
    {
        var nodes = CreateTestTree();
        var processorObject = nodes[0];
        var totalInstance = processorObject.Children[0];

        totalInstance.IsChecked = true;

        var selectedCounters = processorObject.GetSelectedCounters().ToList();
        Assert.Equal(2, selectedCounters.Count);
        Assert.True(totalInstance.IsChecked);
        Assert.True(processorObject.IsChecked);
    }

    [Fact]
    public void CounterLevelSelection_MakesAncestorsIndeterminate()
    {
        var nodes = CreateTestTree();
        var processorObject = nodes[0];
        var totalInstance = processorObject.Children[0];
        var cpuTimeCounter = totalInstance.Children[0];

        cpuTimeCounter.IsChecked = true;

        var selectedCounters = processorObject.GetSelectedCounters().ToList();
        var selectedCounter = Assert.Single(selectedCounters);
        Assert.Equal(@"\Processor(_Total)\% Processor Time", selectedCounter.FullPath);
        Assert.True(cpuTimeCounter.IsChecked);
        Assert.Null(totalInstance.IsChecked);
        Assert.Null(processorObject.IsChecked);
    }

    [Fact]
    public void PartialSelection_KeepsUnselectedCounterUnchecked()
    {
        var nodes = CreateTestTree();
        var processorObject = nodes[0];
        var totalInstance = processorObject.Children[0];
        var cpuTimeCounter = totalInstance.Children[0];
        var userTimeCounter = totalInstance.Children[1];

        cpuTimeCounter.IsChecked = true;

        var selectedCounters = processorObject.GetSelectedCounters().ToList();
        var selectedCounter = Assert.Single(selectedCounters);
        Assert.Equal(@"\Processor(_Total)\% Processor Time", selectedCounter.FullPath);
        Assert.True(cpuTimeCounter.IsChecked);
        Assert.False(userTimeCounter.IsChecked);
        Assert.Null(totalInstance.IsChecked);
        Assert.Null(processorObject.IsChecked);
    }

    private static ObservableCollection<CounterTreeNode> CreateTestTree()
    {
        var nodes = new ObservableCollection<CounterTreeNode>();

        var processorObject = new CounterTreeNode
        {
            DisplayName = "Processor",
            Type = NodeType.Object
        };

        var totalInstance = new CounterTreeNode
        {
            DisplayName = "_Total",
            Type = NodeType.Instance,
            Parent = processorObject
        };

        var cpuTimeCounter = new CounterTreeNode
        {
            DisplayName = "% Processor Time",
            FullPath = @"\Processor(_Total)\% Processor Time",
            Type = NodeType.Counter,
            Parent = totalInstance
        };

        var userTimeCounter = new CounterTreeNode
        {
            DisplayName = "% User Time",
            FullPath = @"\Processor(_Total)\% User Time",
            Type = NodeType.Counter,
            Parent = totalInstance
        };

        totalInstance.Children.Add(cpuTimeCounter);
        totalInstance.Children.Add(userTimeCounter);
        processorObject.Children.Add(totalInstance);
        nodes.Add(processorObject);

        return nodes;
    }
}

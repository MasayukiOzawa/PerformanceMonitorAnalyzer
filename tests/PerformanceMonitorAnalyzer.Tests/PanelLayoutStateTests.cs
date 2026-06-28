using System.Windows;
using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class PanelLayoutStateTests
{
    [Fact]
    public void CounterPanelToggle_RestoresPreviousWidth()
    {
        var state = new PanelLayoutState();
        var hiddenWidth = state.HideCounterPanel(new GridLength(420));

        Assert.False(state.IsCounterPanelVisible);
        Assert.Equal(0, hiddenWidth.Value);

        var restoredWidth = state.ShowCounterPanel();

        Assert.True(state.IsCounterPanelVisible);
        Assert.Equal(420, restoredWidth.Value);
    }

    [Fact]
    public void SetAllTogglePanelsCollapsed_UpdatesAllPanelFlags()
    {
        var state = new PanelLayoutState();

        state.SetAllTogglePanelsCollapsed(true);

        Assert.False(state.IsCounterPanelVisible);
        Assert.True(state.IsLegendPanelCollapsed);
        Assert.True(state.IsStatisticsPanelCollapsed);
        Assert.True(state.IsScalePanelCollapsed);
        Assert.True(state.IsBottomPanelCollapsed);

        state.SetAllTogglePanelsCollapsed(false);

        Assert.True(state.IsCounterPanelVisible);
        Assert.False(state.IsLegendPanelCollapsed);
        Assert.False(state.IsStatisticsPanelCollapsed);
        Assert.False(state.IsScalePanelCollapsed);
        Assert.False(state.IsBottomPanelCollapsed);
    }

    [Fact]
    public void InitialValues_MatchMainWindowDefaults()
    {
        var state = new PanelLayoutState();

        Assert.True(state.IsCounterPanelVisible);
        Assert.Equal(350, state.LastCounterPanelWidth.Value);
        Assert.Equal(250, state.LastLegendPanelWidth.Value);
        Assert.Equal(170, state.LastStatisticsPanelHeight.Value);
        Assert.Equal(400, state.LastBottomPanelHeight.Value);
        Assert.Equal(230, state.LastScalePanelWidth.Value);
    }
}

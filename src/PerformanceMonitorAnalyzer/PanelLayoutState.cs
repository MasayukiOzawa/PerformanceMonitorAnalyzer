using System.Windows;

namespace PerformanceMonitorAnalyzer;

public sealed class PanelLayoutState
{
    public const double StatisticsPanelMinHeight = 80;
    public const double StatisticsPanelMaxHeight = 450;
    public const double BottomPanelMinHeight = 100;
    public const double BottomPanelMaxHeight = 750;

    public bool IsCounterPanelVisible { get; private set; } = true;
    public GridLength LastCounterPanelWidth { get; private set; } = new(350);
    public bool IsLegendPanelCollapsed { get; private set; }
    public GridLength LastLegendPanelWidth { get; private set; } = new(250);
    public bool IsStatisticsPanelCollapsed { get; private set; }
    public GridLength LastStatisticsPanelHeight { get; private set; } = new(170);
    public bool IsBottomPanelCollapsed { get; private set; }
    public GridLength LastBottomPanelHeight { get; private set; } = new(400);
    public GridLength LastScalePanelWidth { get; private set; } = new(230);
    public bool IsScalePanelCollapsed { get; private set; }

    public void InitializeCounterPanel(bool isVisible, GridLength currentWidth)
    {
        IsCounterPanelVisible = isVisible;
        RememberCounterPanelWidth(currentWidth);
    }

    public void RememberCounterPanelWidth(GridLength currentWidth)
    {
        if (currentWidth.Value > 0)
        {
            LastCounterPanelWidth = currentWidth;
        }
    }

    public GridLength HideCounterPanel(GridLength currentWidth)
    {
        RememberCounterPanelWidth(currentWidth);
        IsCounterPanelVisible = false;
        return new GridLength(0);
    }

    public GridLength ShowCounterPanel()
    {
        IsCounterPanelVisible = true;
        return LastCounterPanelWidth.Value > 0 ? LastCounterPanelWidth : new GridLength(350);
    }

    public void ToggleLegendPanel()
    {
        IsLegendPanelCollapsed = !IsLegendPanelCollapsed;
    }

    public void SetLegendPanelCollapsed(bool collapsed)
    {
        IsLegendPanelCollapsed = collapsed;
    }

    public void RememberLegendPanelWidth(GridLength currentWidth)
    {
        if (currentWidth.Value > 0)
        {
            LastLegendPanelWidth = currentWidth;
        }
    }

    public GridLength GetLegendPanelWidth()
    {
        return LastLegendPanelWidth.Value > 0 ? LastLegendPanelWidth : new GridLength(250);
    }

    public void ToggleStatisticsPanel()
    {
        IsStatisticsPanelCollapsed = !IsStatisticsPanelCollapsed;
    }

    public void SetStatisticsPanelCollapsed(bool collapsed)
    {
        IsStatisticsPanelCollapsed = collapsed;
    }

    public void RememberStatisticsPanelHeight(GridLength currentHeight)
    {
        if (currentHeight.Value > 0)
        {
            LastStatisticsPanelHeight = currentHeight;
        }
    }

    public GridLength GetStatisticsPanelHeight()
    {
        return LastStatisticsPanelHeight.Value > 0 ? LastStatisticsPanelHeight : new GridLength(170);
    }

    public void ToggleBottomPanel()
    {
        IsBottomPanelCollapsed = !IsBottomPanelCollapsed;
    }

    public void SetBottomPanelCollapsed(bool collapsed)
    {
        IsBottomPanelCollapsed = collapsed;
    }

    public void RememberBottomPanelHeight(GridLength currentHeight)
    {
        if (currentHeight.Value > 0)
        {
            LastBottomPanelHeight = currentHeight;
        }
    }

    public GridLength GetBottomPanelHeight()
    {
        return LastBottomPanelHeight.Value > 0 ? LastBottomPanelHeight : new GridLength(400);
    }

    public void ToggleScalePanel()
    {
        IsScalePanelCollapsed = !IsScalePanelCollapsed;
    }

    public void SetScalePanelCollapsed(bool collapsed)
    {
        IsScalePanelCollapsed = collapsed;
    }

    public void RememberScalePanelWidth(GridLength currentWidth)
    {
        if (currentWidth.Value > 0)
        {
            LastScalePanelWidth = currentWidth;
        }
    }

    public GridLength GetScalePanelWidth()
    {
        return LastScalePanelWidth.Value > 0 ? LastScalePanelWidth : new GridLength(230);
    }

    public void SetAllTogglePanelsCollapsed(bool collapsed)
    {
        IsCounterPanelVisible = !collapsed;
        IsLegendPanelCollapsed = collapsed;
        IsStatisticsPanelCollapsed = collapsed;
        IsScalePanelCollapsed = collapsed;
        IsBottomPanelCollapsed = collapsed;
    }
}

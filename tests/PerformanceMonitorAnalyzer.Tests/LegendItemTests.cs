using System.Windows;
using System.Windows.Media;
using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class LegendItemTests
{
    [Fact]
    public void Defaults_ExposeVisibleNonHighlightedPresentation()
    {
        var item = new LegendItem();

        Assert.True(item.IsVisible);
        Assert.False(item.IsHighlighted);
        Assert.False(item.IsSecondaryAxis);
        Assert.Equal(string.Empty, item.CurrentValue);
        Assert.Equal(Colors.Blue, item.Color);
        Assert.Equal(Colors.Blue, GetBrushColor(item.ColorBrush));
        Assert.Equal(Colors.Transparent, GetBrushColor(item.BackgroundBrush));
        Assert.Equal(Colors.Black, GetBrushColor(item.TextBrush));
        Assert.Equal(FontWeights.Normal, item.CounterFontWeight);
        Assert.Equal("☆", item.HighlightMark);
        Assert.Equal(Colors.Gray, GetBrushColor(item.HighlightBrush));
    }

    [Fact]
    public void IsVisible_WhenChanged_RaisesDependentNotificationsAndUpdatesBrushes()
    {
        var item = new LegendItem();
        var changedProperties = TrackPropertyChanges(item);

        item.IsVisible = false;

        Assert.Equal(
            new[] { nameof(LegendItem.IsVisible), nameof(LegendItem.BackgroundBrush), nameof(LegendItem.TextBrush) },
            changedProperties);
        Assert.Equal(Color.FromArgb(50, 200, 200, 200), GetBrushColor(item.BackgroundBrush));
        Assert.Equal(Colors.Gray, GetBrushColor(item.TextBrush));
    }

    [Fact]
    public void IsVisible_WhenAssignedSameValue_DoesNotRaisePropertyChanged()
    {
        var item = new LegendItem();
        var changedProperties = TrackPropertyChanges(item);

        item.IsVisible = true;

        Assert.Empty(changedProperties);
    }

    [Fact]
    public void IsHighlighted_WhenChanged_RaisesDependentNotificationsAndUpdatesPresentation()
    {
        var item = new LegendItem();
        var changedProperties = TrackPropertyChanges(item);

        item.IsHighlighted = true;

        Assert.Equal(
            new[]
            {
                nameof(LegendItem.IsHighlighted),
                nameof(LegendItem.BackgroundBrush),
                nameof(LegendItem.CounterFontWeight),
                nameof(LegendItem.HighlightMark),
                nameof(LegendItem.HighlightBrush)
            },
            changedProperties);
        Assert.Equal(Color.FromArgb(60, 255, 242, 204), GetBrushColor(item.BackgroundBrush));
        Assert.Equal(FontWeights.Bold, item.CounterFontWeight);
        Assert.Equal("★", item.HighlightMark);
        Assert.Equal(Colors.Goldenrod, GetBrushColor(item.HighlightBrush));
    }

    [Fact]
    public void CurrentValue_WhenChanged_RaisesOnlyCurrentValueNotification()
    {
        var item = new LegendItem();
        var changedProperties = TrackPropertyChanges(item);

        item.CurrentValue = "42.00 %";

        Assert.Equal(new[] { nameof(LegendItem.CurrentValue) }, changedProperties);
        Assert.Equal("42.00 %", item.CurrentValue);
    }

    [Fact]
    public void IsSecondaryAxis_WhenChanged_RaisesOnlyAxisNotification()
    {
        var item = new LegendItem();
        var changedProperties = TrackPropertyChanges(item);

        item.IsSecondaryAxis = true;

        Assert.True(item.IsSecondaryAxis);
        Assert.Equal(new[] { nameof(LegendItem.IsSecondaryAxis) }, changedProperties);
    }

    [Fact]
    public void Color_WhenChanged_RaisesColorNotificationsAndUpdatesBrush()
    {
        var item = new LegendItem();
        var changedProperties = TrackPropertyChanges(item);

        item.Color = Colors.Red;

        Assert.Equal(new[] { nameof(LegendItem.Color), nameof(LegendItem.ColorBrush) }, changedProperties);
        Assert.Equal(Colors.Red, item.Color);
        Assert.Equal(Colors.Red, GetBrushColor(item.ColorBrush));
    }

    private static List<string?> TrackPropertyChanges(LegendItem item)
    {
        var changedProperties = new List<string?>();
        item.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        return changedProperties;
    }

    private static Color GetBrushColor(Brush brush)
    {
        return Assert.IsType<SolidColorBrush>(brush).Color;
    }
}

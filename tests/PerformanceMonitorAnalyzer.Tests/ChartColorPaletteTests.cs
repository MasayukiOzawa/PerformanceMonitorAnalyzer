using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class ChartColorPaletteTests
{
    [Fact]
    public void GetNextColor_CyclesAfterPaletteLength()
    {
        var first = ChartColorPalette.GetNextColor(0);
        var cycled = ChartColorPalette.GetNextColor(10);

        Assert.Equal(first.R, cycled.R);
        Assert.Equal(first.G, cycled.G);
        Assert.Equal(first.B, cycled.B);
        Assert.Equal(first.A, cycled.A);
    }

    [Fact]
    public void ConvertToMediaColor_CopiesChannels()
    {
        var scottColor = ChartColorPalette.GetNextColor(1);
        var mediaColor = ChartColorPalette.ConvertToMediaColor(scottColor);

        Assert.Equal(scottColor.R, mediaColor.R);
        Assert.Equal(scottColor.G, mediaColor.G);
        Assert.Equal(scottColor.B, mediaColor.B);
        Assert.Equal(scottColor.A, mediaColor.A);
    }
}

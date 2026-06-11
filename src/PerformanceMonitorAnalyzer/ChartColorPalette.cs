namespace PerformanceMonitorAnalyzer;

public static class ChartColorPalette
{
    public static ScottPlot.Color GetNextColor(int index)
    {
        var colors = new[]
        {
            ScottPlot.Colors.Blue,
            ScottPlot.Colors.Red,
            ScottPlot.Colors.Green,
            ScottPlot.Colors.Orange,
            ScottPlot.Colors.Purple,
            ScottPlot.Colors.Brown,
            ScottPlot.Colors.Pink,
            ScottPlot.Colors.Gray,
            ScottPlot.Colors.Olive,
            ScottPlot.Colors.Cyan
        };

        return colors[index % colors.Length];
    }

    public static System.Windows.Media.Color ConvertToMediaColor(ScottPlot.Color scottPlotColor)
    {
        return System.Windows.Media.Color.FromArgb(
            scottPlotColor.A,
            scottPlotColor.R,
            scottPlotColor.G,
            scottPlotColor.B);
    }
}

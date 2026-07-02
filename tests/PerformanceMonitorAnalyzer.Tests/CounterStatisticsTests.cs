using System.Globalization;
using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class CounterStatisticsTests
{
    [Fact]
    public void FormattedValues_WithUnit_UseTwoDecimalPlacesWithoutAppendingUnit()
    {
        using var _ = new CurrentCultureScope("en-US");
        var statistics = new CounterStatistics
        {
            Average = 1234.5,
            Maximum = 2000,
            Minimum = 12.345,
            StandardDeviation = 0.567,
            Unit = "ms"
        };

        Assert.Equal("1,234.50", statistics.FormattedAverage);
        Assert.Equal("2,000.00", statistics.FormattedMaximum);
        Assert.Equal("12.35", statistics.FormattedMinimum);
        Assert.Equal("0.57", statistics.FormattedStandardDeviation);
    }

    [Fact]
    public void FormattedValues_WithoutUnit_DoNotLeaveTrailingWhitespace()
    {
        using var _ = new CurrentCultureScope("en-US");
        var statistics = new CounterStatistics
        {
            Average = 1.2,
            Maximum = 3.4,
            Minimum = 5.6,
            StandardDeviation = 7.8
        };

        Assert.Equal("1.20", statistics.FormattedAverage);
        Assert.Equal("3.40", statistics.FormattedMaximum);
        Assert.Equal("5.60", statistics.FormattedMinimum);
        Assert.Equal("7.80", statistics.FormattedStandardDeviation);
    }

    private sealed class CurrentCultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture;
        private readonly CultureInfo _previousUiCulture;

        public CurrentCultureScope(string cultureName)
        {
            _previousCulture = CultureInfo.CurrentCulture;
            _previousUiCulture = CultureInfo.CurrentUICulture;
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUiCulture;
        }
    }
}

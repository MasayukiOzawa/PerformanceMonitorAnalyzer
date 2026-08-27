using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class CounterDisplayNameSorterTests
{
    [Fact]
    public void Sort_OrdersByDisplayedCounterNameInsteadOfFullPath()
    {
        using var _ = new TestCultureScope("ja-JP");
        var counters = new[]
        {
            @"\\A-PC\System\Processor Queue Length",
            @"\\Z-PC\Memory\Available MBytes",
            @"\\M-PC\Processor(_Total)\% Processor Time"
        };

        var sorted = CounterDisplayNameSorter.Sort(counters);

        Assert.Equal(
            [
                @"\\Z-PC\Memory\Available MBytes",
                @"\\M-PC\Processor(_Total)\% Processor Time",
                @"\\A-PC\System\Processor Queue Length"
            ],
            sorted);
    }

    [Fact]
    public void Sort_WhenDisplayNamesMatch_UsesOrdinalCounterPathTieBreaker()
    {
        using var _ = new TestCultureScope("ja-JP");
        var counters = new[]
        {
            @"\\ä-PC\Memory\Available MBytes",
            @"\\z-PC\Memory\Available MBytes"
        };

        var sorted = CounterDisplayNameSorter.Sort(counters);

        Assert.Equal(
            [
                @"\\z-PC\Memory\Available MBytes",
                @"\\ä-PC\Memory\Available MBytes"
            ],
            sorted);
    }
}

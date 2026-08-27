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
}

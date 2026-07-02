using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class RelogCommandBuilderTests
{
    [Fact]
    public void Build_UsesBacktickContinuationAndExpectedDateFormat()
    {
        var command = RelogCommandBuilder.Build(
            @"logs\sample.blg",
            new DateTime(2026, 1, 2, 3, 4, 5),
            new DateTime(2026, 1, 2, 4, 5, 6));

        Assert.Contains("relog.exe `", command);
        Assert.Contains("  \"logs\\sample.blg\" `", command);
        Assert.Contains("  -o \"sample_output.blg\" `", command);
        Assert.Contains("  -b \"2026/01/02 03:04:05\" `", command);
        Assert.EndsWith("  -e \"2026/01/02 04:05:06\"", command);
    }
}

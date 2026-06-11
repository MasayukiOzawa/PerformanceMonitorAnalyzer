using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class CounterPathFormatterTests
{
    [Fact]
    public void GetDisplayName_WithComputerAndInstance_FormatsObjectInstanceAndCounter()
    {
        var displayName = CounterPathFormatter.GetDisplayName(@"\\PC\Processor(_Total)\% Processor Time");

        Assert.Equal("Processor(_Total) - % Processor Time", displayName);
    }

    [Fact]
    public void GetDisplayName_WithLocalTwoPartPath_FormatsObjectAndCounter()
    {
        var displayName = CounterPathFormatter.GetDisplayName(@"\Memory\Available MBytes");

        Assert.Equal("Memory - Available MBytes", displayName);
    }

    [Fact]
    public void GetDisplayName_WithoutInstance_FormatsObjectAndCounter()
    {
        var displayName = CounterPathFormatter.GetDisplayName(@"\\PC\System\Processor Queue Length");

        Assert.Equal("System - Processor Queue Length", displayName);
    }

    [Fact]
    public void GetDisplayName_WithInvalidPath_ReturnsOriginal()
    {
        Assert.Equal("invalid", CounterPathFormatter.GetDisplayName("invalid"));
    }

    [Fact]
    public void GetComputerName_UsesPathComputerNameOrActualLocalName()
    {
        Assert.Equal("PC", CounterPathFormatter.GetComputerName(@"\\PC\Memory\Available MBytes", null));
        Assert.Equal("SERVER01", CounterPathFormatter.GetComputerName(@"\Memory\Available MBytes", "SERVER01"));
        Assert.Equal("ローカルコンピューター", CounterPathFormatter.GetComputerName(@"\Memory\Available MBytes", null));
    }

    [Fact]
    public void Normalize_RemovesQuotesAndUsesLowerSlashPath()
    {
        Assert.Equal("/processor(_total)/% processor time", CounterPathFormatter.Normalize(@" ""\Processor(_Total)\% Processor Time"" "));
    }
}

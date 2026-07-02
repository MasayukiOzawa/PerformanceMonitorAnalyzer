using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public sealed class CounterPatternClipboardBuilderTests
{
    [Fact]
    public void BuildCounterDefinitionYaml_ReturnsCounterPatternsSnippet()
    {
        var yaml = CounterPatternClipboardBuilder.BuildCounterDefinitionYaml(
            @"\SQLServer:Locks(Page)\Lock Requests/sec",
            1.0);

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                @"      - name: \SQLServer:Locks(Page)\Lock Requests/sec",
                "        enabled: true",
                "        scale: 1"),
            yaml);
    }

    [Fact]
    public void BuildCounterDefinitionYaml_RemovesComputerName()
    {
        var yaml = CounterPatternClipboardBuilder.BuildCounterDefinitionYaml(
            @"\\DB-MAIN31\SQLServer:Access Methods\Table Lock Escalations/sec",
            1.0);

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                @"      - name: \SQLServer:Access Methods\Table Lock Escalations/sec",
                "        enabled: true",
                "        scale: 1"),
            yaml);
    }

    [Fact]
    public void BuildCounterDefinitionsYaml_CopiesMultipleCounters()
    {
        var yaml = CounterPatternClipboardBuilder.BuildCounterDefinitionsYaml(
        [
            (@"\\DB-MAIN31\SQLServer:General Statistics\User Connections", 1.0),
            (@"\SQLServer:Locks(Page)\Lock Requests/sec", 0.1)
        ]);

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                @"      - name: \SQLServer:General Statistics\User Connections",
                "        enabled: true",
                "        scale: 1",
                @"      - name: \SQLServer:Locks(Page)\Lock Requests/sec",
                "        enabled: true",
                "        scale: 0.1"),
            yaml);
    }

    [Theory]
    [InlineData(0.00001, "0.00001")]
    [InlineData(0.0000000000000001, "1E-16")]
    [InlineData(double.NaN, "1")]
    [InlineData(double.PositiveInfinity, "1")]
    [InlineData(0.0, "1")]
    public void BuildCounterDefinitionYaml_FormatsScaleForYaml(double scale, string expectedScale)
    {
        var yaml = CounterPatternClipboardBuilder.BuildCounterDefinitionYaml(
            @"\Processor(_Total)\% Processor Time",
            scale);

        Assert.EndsWith($"scale: {expectedScale}", yaml);
    }
}

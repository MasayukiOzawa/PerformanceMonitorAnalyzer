using System.IO;
using System.Linq;
using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public sealed class CounterPatternTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "PerformanceMonitorAnalyzer.Tests",
        Guid.NewGuid().ToString("N"));

    public CounterPatternTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task LoadConfigAsync_WithExplicitScaleAndDisplayModes_LoadsConfiguredValues()
    {
        var configPath = GetConfigPath();
        var yaml = """
            patterns:
              - name: スケール指定あり
                description: 明示的なスケールを検証する
                graphType: stackedAreaChart
                valueMode: deltaFromPrevious
                counters:
                  - name: \Processor(_Total)\% Processor Time
                    enabled: true
                    scale: 0.25
            """;

        await File.WriteAllTextAsync(configPath, yaml);

        var manager = new CounterPatternManager(configPath);

        var loaded = await manager.LoadConfigAsync();

        Assert.True(loaded);
        var pattern = Assert.Single(manager.GetAvailablePatterns());
        var counter = Assert.Single(pattern.Counters);
        Assert.Equal("スケール指定あり", pattern.Name);
        Assert.Equal(ChartType.StackedAreaChart, pattern.GraphType);
        Assert.Equal(CounterValueMode.DeltaFromPrevious, pattern.ValueMode);
        Assert.Equal(0.25, counter.Scale, precision: 6);
    }

    [Fact]
    public async Task LoadConfigAsync_WithoutScale_UsesDefaultScaleValue()
    {
        var configPath = GetConfigPath();
        var yaml = """
            patterns:
              - name: スケール指定なし
                description: scale未指定時の既定値を検証する
                counters:
                  - name: \Memory\Available MBytes
                    enabled: false
            """;

        await File.WriteAllTextAsync(configPath, yaml);

        var manager = new CounterPatternManager(configPath);

        var loaded = await manager.LoadConfigAsync();

        Assert.True(loaded);
        var pattern = Assert.Single(manager.GetAvailablePatterns());
        var counter = Assert.Single(pattern.Counters);
        Assert.Equal("スケール指定なし", pattern.Name);
        Assert.Equal(ChartType.LineChart, pattern.GraphType);
        Assert.Equal(CounterValueMode.RawValue, pattern.ValueMode);
        Assert.False(counter.Enabled);
        Assert.Equal(CounterDefinition.DefaultScale, counter.Scale);
    }

    [Fact]
    public async Task LoadConfigAsync_WithInvalidScale_UsesDefaultScaleValue()
    {
        var configPath = GetConfigPath();
        var yaml = """
            patterns:
              - name: 無効スケール
                description: 0以下の scale は既定値へ補正する
                counters:
                  - name: \Memory\Available MBytes
                    enabled: true
                    scale: 0
            """;

        await File.WriteAllTextAsync(configPath, yaml);

        var manager = new CounterPatternManager(configPath);

        var loaded = await manager.LoadConfigAsync();

        Assert.True(loaded);
        var pattern = Assert.Single(manager.GetAvailablePatterns());
        var counter = Assert.Single(pattern.Counters);
        Assert.Equal(CounterDefinition.DefaultScale, counter.Scale);
    }

    [Fact]
    public async Task LoadConfigAsync_WhenCreatingDefaultConfig_WritesOptionalScaleEntriesAndKeepsDefaults()
    {
        var configPath = GetConfigPath();
        var manager = new CounterPatternManager(configPath);

        var loaded = await manager.LoadConfigAsync();

        Assert.True(loaded);
        Assert.True(File.Exists(configPath));

        var generatedYaml = await File.ReadAllTextAsync(configPath);
        Assert.Contains("patterns:", generatedYaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scale:", generatedYaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("graphType:", generatedYaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("valueMode:", generatedYaml, StringComparison.OrdinalIgnoreCase);

        var patterns = manager.GetAvailablePatterns().ToList();
        Assert.NotEmpty(patterns);
        Assert.All(patterns, pattern => Assert.NotEmpty(pattern.Counters));

        var counters = patterns.SelectMany(pattern => pattern.Counters).ToList();
        Assert.Contains(counters, counter => Math.Abs(counter.Scale - CounterDefinition.DefaultScale) < 1e-12);
        Assert.Contains(counters, counter => Math.Abs(counter.Scale - 0.000001d) < 1e-12);

        var detailedPattern = Assert.Single(patterns, pattern => pattern.Name == "詳細システム監視");
        var workingSet = Assert.Single(detailedPattern.Counters, counter => counter.Name == @"\Process(_Total)\Working Set");
        var systemCalls = Assert.Single(detailedPattern.Counters, counter => counter.Name == @"\System\System Calls/sec");
        Assert.Equal(ChartType.LineChart, detailedPattern.GraphType);
        Assert.Equal(CounterValueMode.RawValue, detailedPattern.ValueMode);
        Assert.Equal(0.000001d, workingSet.Scale, precision: 12);
        Assert.Equal(CounterDefinition.DefaultScale, systemCalls.Scale);

        var reloadedManager = new CounterPatternManager(configPath);
        var reloaded = await reloadedManager.LoadConfigAsync();

        Assert.True(reloaded);
        var reloadedPatterns = reloadedManager.GetAvailablePatterns().ToList();
        var reloadedCounters = reloadedPatterns.SelectMany(pattern => pattern.Counters).ToList();
        Assert.All(reloadedPatterns, pattern => Assert.Equal(ChartType.LineChart, pattern.GraphType));
        Assert.All(reloadedPatterns, pattern => Assert.Equal(CounterValueMode.RawValue, pattern.ValueMode));
        Assert.Contains(reloadedCounters, counter => Math.Abs(counter.Scale - CounterDefinition.DefaultScale) < 1e-12);
        Assert.Contains(reloadedCounters, counter => Math.Abs(counter.Scale - 0.000001d) < 1e-12);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private string GetConfigPath()
    {
        var configDirectory = Path.Combine(_tempDirectory, "config");
        Directory.CreateDirectory(configDirectory);
        return Path.Combine(configDirectory, "counter-patterns.yaml");
    }
}

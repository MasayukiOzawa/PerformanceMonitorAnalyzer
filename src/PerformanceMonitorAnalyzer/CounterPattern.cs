using System.ComponentModel;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// カウンターパターン設定のメインクラス
/// </summary>
public class CounterPatternConfig
{
    [YamlMember(Alias = "patterns")]
    public List<CounterPattern> Patterns { get; set; } = new();
}

/// <summary>
/// 個別のパターン定義
/// </summary>
public class CounterPattern
{
    public const string DefaultGraphTypeConfigValue = "lineChart";
    public const string DefaultValueModeConfigValue = "rawValue";

    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;
    
    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;

    [YamlIgnore]
    public ChartType GraphType { get; set; } = ChartType.LineChart;

    [YamlIgnore]
    public CounterValueMode ValueMode { get; set; } = CounterValueMode.RawValue;

    [DefaultValue(DefaultGraphTypeConfigValue)]
    [YamlMember(Alias = "graphType")]
    public string GraphTypeName
    {
        get => GetGraphTypeConfigValue(GraphType);
        set => GraphType = ParseGraphType(value);
    }

    [DefaultValue(DefaultValueModeConfigValue)]
    [YamlMember(Alias = "valueMode")]
    public string ValueModeName
    {
        get => GetValueModeConfigValue(ValueMode);
        set => ValueMode = ParseValueMode(value);
    }
    
    [YamlMember(Alias = "counters")]
    public List<CounterDefinition> Counters { get; set; } = new();

    public void Normalize()
    {
        Name ??= string.Empty;
        Description ??= string.Empty;
        Counters ??= new List<CounterDefinition>();

        GraphType = GraphType switch
        {
            ChartType.StackedAreaChart => ChartType.StackedAreaChart,
            _ => ChartType.LineChart
        };

        ValueMode = ValueMode switch
        {
            CounterValueMode.DeltaFromPrevious => CounterValueMode.DeltaFromPrevious,
            _ => CounterValueMode.RawValue
        };
    }

    private static ChartType ParseGraphType(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "line" => ChartType.LineChart,
            "linechart" => ChartType.LineChart,
            "line-chart" => ChartType.LineChart,
            "stackedarea" => ChartType.StackedAreaChart,
            "stacked-area" => ChartType.StackedAreaChart,
            "stackedareachart" => ChartType.StackedAreaChart,
            "stacked-area-chart" => ChartType.StackedAreaChart,
            "area" => ChartType.StackedAreaChart,
            _ => ChartType.LineChart
        };
    }

    private static CounterValueMode ParseValueMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "delta" => CounterValueMode.DeltaFromPrevious,
            "deltafromprevious" => CounterValueMode.DeltaFromPrevious,
            "delta-from-previous" => CounterValueMode.DeltaFromPrevious,
            "deltafrompreviousvalue" => CounterValueMode.DeltaFromPrevious,
            "raw" => CounterValueMode.RawValue,
            "rawvalue" => CounterValueMode.RawValue,
            "raw-value" => CounterValueMode.RawValue,
            _ => CounterValueMode.RawValue
        };
    }

    private static string GetGraphTypeConfigValue(ChartType chartType)
    {
        return chartType == ChartType.StackedAreaChart ? "stackedAreaChart" : DefaultGraphTypeConfigValue;
    }

    private static string GetValueModeConfigValue(CounterValueMode valueMode)
    {
        return valueMode == CounterValueMode.DeltaFromPrevious ? "deltaFromPrevious" : DefaultValueModeConfigValue;
    }
}

/// <summary>
/// 個別カウンターの定義
/// </summary>
public class CounterDefinition
{
    public const double DefaultScale = 1.0;

    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;
    
    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// YAML の scale キー。省略時は 1.0 を使用する。
    /// </summary>
    [DefaultValue(DefaultScale)]
    [YamlMember(Alias = "scale", DefaultValuesHandling = DefaultValuesHandling.OmitDefaults)]
    public double Scale { get; set; } = DefaultScale;

    public void Normalize()
    {
        if (Scale <= 0 || double.IsNaN(Scale) || double.IsInfinity(Scale))
        {
            Scale = DefaultScale;
        }
    }
}

/// <summary>
/// カウンターパターンの管理クラス
/// </summary>
public class CounterPatternManager
{
    private readonly string _configFilePath;
    private CounterPatternConfig? _config;
    
    public CounterPatternManager(string? configFilePath = null)
    {
        _configFilePath = configFilePath ?? Path.Combine(
            AppContext.BaseDirectory,
            "config",
            "counter-patterns.yaml"
        );
    }
    
    /// <summary>
    /// YAMLファイルからパターン設定を読み込み
    /// </summary>
    public async Task<bool> LoadConfigAsync()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                // デフォルト設定ファイルを作成
                await CreateDefaultConfigAsync();
                return true;
            }
            
            var yaml = await File.ReadAllTextAsync(_configFilePath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            
            _config = deserializer.Deserialize<CounterPatternConfig>(yaml) ?? new CounterPatternConfig();
            NormalizeConfig(_config);
            return true;
        }
        catch (Exception ex)
        {
            await LogErrorAsync($"パターン設定の読み込みエラー: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// デフォルトの設定ファイルを作成
    /// </summary>
    private async Task CreateDefaultConfigAsync()
    {
        var defaultConfig = new CounterPatternConfig
        {
            Patterns = new List<CounterPattern>
            {
                new CounterPattern
                {
                    Name = "基本システム監視",
                    Description = "CPU、メモリ、ディスクの基本的な監視項目",
                    Counters = new List<CounterDefinition>
                    {
                        new() { Name = @"\Processor(_Total)\% Processor Time" },
                        new() { Name = @"\Memory\Available MBytes" },
                        new() { Name = @"\PhysicalDisk(_Total)\Disk Reads/sec" },
                        new() { Name = @"\PhysicalDisk(_Total)\Disk Writes/sec" }
                    }
                },
                new CounterPattern
                {
                    Name = "詳細システム監視",
                    Description = "詳細なシステム分析のための監視項目",
                    Counters = new List<CounterDefinition>
                    {
                        new() { Name = @"\System\Context Switches/sec" },
                        new() { Name = @"\System\System Calls/sec" },
                        new() { Name = @"\Process(_Total)\Working Set", Scale = 0.000001 },
                        new() { Name = @"\Process(_Total)\Private Bytes", Scale = 0.000001 },
                        new() { Name = @"\Paging File(_Total)\% Usage" }
                    }
                },
                new CounterPattern
                {
                    Name = "SQLサーバー監視",
                    Description = "SQL Serverパフォーマンスの監視項目",
                    Counters = new List<CounterDefinition>
                    {
                        new() { Name = @"\SQLServer:General Statistics\User Connections" },
                        new() { Name = @"\SQLServer:Buffer Manager\Buffer cache hit ratio" },
                        new() { Name = @"\SQLServer:SQL Statistics\Batch Requests/sec" },
                        new() { Name = @"\SQLServer:SQL Statistics\SQL Compilations/sec" },
                        new() { Name = @"\SQLServer:Locks(_Total)\Lock Waits/sec" }
                    }
                }
            }
        };

        NormalizeConfig(defaultConfig);
        
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        
        var yaml = serializer.Serialize(defaultConfig);
        
        // ディレクトリが存在しない場合は作成
        var directory = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        await File.WriteAllTextAsync(_configFilePath, yaml);
        _config = defaultConfig;
    }
    
    /// <summary>
    /// 利用可能なパターン一覧を取得
    /// </summary>
    public IEnumerable<CounterPattern> GetAvailablePatterns()
    {
        return _config?.Patterns ?? Enumerable.Empty<CounterPattern>();
    }
    
    /// <summary>
    /// 指定されたパターン名のパターンを取得
    /// </summary>
    public CounterPattern? GetPattern(string patternName)
    {
        return _config?.Patterns.FirstOrDefault(p => p.Name == patternName);
    }
    
    /// <summary>
    /// 設定ファイルのパスを取得
    /// </summary>
    public string ConfigFilePath => _configFilePath;

    private static void NormalizeConfig(CounterPatternConfig config)
    {
        config.Patterns ??= new List<CounterPattern>();

        foreach (var pattern in config.Patterns)
        {
            pattern.Normalize();

            foreach (var counter in pattern.Counters)
            {
                counter.Name ??= string.Empty;
                counter.Normalize();
            }
        }
    }
    
    /// <summary>
    /// エラーログの出力
    /// </summary>
    private static async Task LogErrorAsync(string message)
    {
        try
        {
            var logPath = Path.Combine(
                AppContext.BaseDirectory,
                "error.log"
            );
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(logPath, logEntry);
        }
        catch
        {
            // ログ出力に失敗した場合は何もしない
        }
    }
}

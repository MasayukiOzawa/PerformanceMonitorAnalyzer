using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// カウンターパターン設定のメインクラス
/// </summary>
public class CounterPatternConfig
{
    [YamlMember(Alias = "patterns")]
    public Dictionary<string, CounterPattern> Patterns { get; set; } = new();
}

/// <summary>
/// 個別のパターン定義
/// </summary>
public class CounterPattern
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;
    
    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;
    
    [YamlMember(Alias = "counters")]
    public List<CounterDefinition> Counters { get; set; } = new();
}

/// <summary>
/// 個別カウンターの定義
/// </summary>
public class CounterDefinition
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;
    
    [YamlMember(Alias = "scale")]
    public double Scale { get; set; } = 1.0;
    
    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; set; } = true;
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
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
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
            
            _config = deserializer.Deserialize<CounterPatternConfig>(yaml);
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
            Patterns = new Dictionary<string, CounterPattern>
            {
                ["基本システム監視"] = new CounterPattern
                {
                    Name = "基本システム監視",
                    Description = "CPU、メモリ、ディスクの基本的な監視項目",
                    Counters = new List<CounterDefinition>
                    {
                        new() { Name = @"\Processor(_Total)\% Processor Time", Scale = 1.0 },
                        new() { Name = @"\Memory\Available MBytes", Scale = 1.0 },
                        new() { Name = @"\PhysicalDisk(_Total)\Disk Reads/sec", Scale = 1.0 },
                        new() { Name = @"\PhysicalDisk(_Total)\Disk Writes/sec", Scale = 1.0 }
                    }
                },
                ["ネットワーク監視"] = new CounterPattern
                {
                    Name = "ネットワーク監視",
                    Description = "ネットワークトラフィックの監視項目",
                    Counters = new List<CounterDefinition>
                    {
                        new() { Name = @"\Network Interface(*)\Bytes Total/sec", Scale = 1.0 },
                        new() { Name = @"\Network Interface(*)\Packets Total/sec", Scale = 1.0 },
                        new() { Name = @"\Network Interface(*)\Current Bandwidth", Scale = 1.0 }
                    }
                },
                ["詳細システム監視"] = new CounterPattern
                {
                    Name = "詳細システム監視",
                    Description = "詳細なシステム分析のための監視項目",
                    Counters = new List<CounterDefinition>
                    {
                        new() { Name = @"\System\Context Switches/sec", Scale = 1.0 },
                        new() { Name = @"\System\System Calls/sec", Scale = 1.0 },
                        new() { Name = @"\Process(_Total)\Working Set", Scale = 1.0 },
                        new() { Name = @"\Process(_Total)\Private Bytes", Scale = 1.0 },
                        new() { Name = @"\Paging File(_Total)\% Usage", Scale = 1.0 }
                    }
                }
            }
        };
        
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
        return _config?.Patterns.Values ?? Enumerable.Empty<CounterPattern>();
    }
    
    /// <summary>
    /// 指定されたパターン名のパターンを取得
    /// </summary>
    public CounterPattern? GetPattern(string patternName)
    {
        return _config?.Patterns.TryGetValue(patternName, out var pattern) == true ? pattern : null;
    }
    
    /// <summary>
    /// 設定ファイルのパスを取得
    /// </summary>
    public string ConfigFilePath => _configFilePath;
    
    /// <summary>
    /// エラーログの出力
    /// </summary>
    private static async Task LogErrorAsync(string message)
    {
        try
        {
            var logPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
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
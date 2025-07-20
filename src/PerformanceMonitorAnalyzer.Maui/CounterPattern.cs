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
    
    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; set; } = true;
    
    // Scaleプロパティは削除しました（既存のスケール管理システムを使用）
    public double Scale { get; set; } = 1.0;
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
                        new() { Name = @"\Process(_Total)\Working Set" },
                        new() { Name = @"\Process(_Total)\Private Bytes" },
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
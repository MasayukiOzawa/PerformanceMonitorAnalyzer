using System.Diagnostics;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// Performance Monitor BLGファイル解析ツール - コンソール版
/// WPF版はWindows環境で実行してください
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Performance Monitor BLG File Analyzer");
        Console.WriteLine("=====================================");
        Console.WriteLine();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Windows環境が検出されました。");
            Console.WriteLine("WPF版のGUIアプリケーションを使用する場合は、以下のコマンドでビルドしてください:");
            Console.WriteLine("dotnet build -p:BuildWindowsWpf=true -f net8.0-windows");
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("Linux/macOS環境が検出されました。");
            Console.WriteLine("BLGファイルの解析機能はWindows環境でのみ利用可能です。");
            Console.WriteLine("ここではサンプルデータを使用して機能をデモンストレーションします。");
            Console.WriteLine();
        }

        if (args.Length > 0 && File.Exists(args[0]))
        {
            await AnalyzeBlgFile(args[0]);
        }
        else
        {
            await DemonstrateWithSampleData();
        }

        Console.WriteLine();
        Console.WriteLine("エラーログは 'error.log' に出力されます。");
        Console.WriteLine("処理が完了しました。何かキーを押して終了してください...");
        Console.ReadKey();
    }

    static async Task AnalyzeBlgFile(string filePath)
    {
        Console.WriteLine($"BLGファイルを解析中: {filePath}");
        
        try
        {
            // Windows環境でのみ実際のBLG解析を実行
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var analyzer = new BlgFileAnalyzer();
                var counters = await analyzer.ParseBlgFileAsync(filePath);
                
                Console.WriteLine($"発見されたカウンター数: {counters.Count}");
                Console.WriteLine();
                
                DisplayCounters(counters);
                await ExportToJson(counters, Path.ChangeExtension(filePath, ".json"));
            }
            else
            {
                Console.WriteLine("Linux/macOS環境ではBLGファイルの解析はサポートされていません。");
                await DemonstrateWithSampleData();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"エラー: {ex.Message}");
            LogError($"BLG file analysis error: {ex}");
        }
    }

    static async Task DemonstrateWithSampleData()
    {
        Console.WriteLine("サンプルデータを使用してデモンストレーション中...");
        Console.WriteLine();

        var sampleCounters = GenerateSampleData();
        
        Console.WriteLine($"サンプルカウンター数: {sampleCounters.Count}");
        Console.WriteLine();
        
        DisplayCounters(sampleCounters);
        
        // サンプルデータをJSONに出力
        await ExportToJson(sampleCounters, "sample_performance_data.json");
        
        // 簡単な統計情報の表示
        DisplayStatistics(sampleCounters);
    }

    static void DisplayCounters(Dictionary<string, List<PerformanceDataPoint>> counters)
    {
        Console.WriteLine("パフォーマンスカウンター一覧:");
        Console.WriteLine("".PadRight(50, '-'));
        
        foreach (var counter in counters.Keys)
        {
            var dataPoints = counters[counter];
            var avg = dataPoints.Average(p => p.Value);
            var min = dataPoints.Min(p => p.Value);
            var max = dataPoints.Max(p => p.Value);
            
            Console.WriteLine($"カウンター: {GetCounterDisplayName(counter)}");
            Console.WriteLine($"  データ点数: {dataPoints.Count}");
            Console.WriteLine($"  平均値: {avg:F2}");
            Console.WriteLine($"  最小値: {min:F2}");
            Console.WriteLine($"  最大値: {max:F2}");
            Console.WriteLine();
        }
    }

    static void DisplayStatistics(Dictionary<string, List<PerformanceDataPoint>> counters)
    {
        Console.WriteLine();
        Console.WriteLine("統計情報:");
        Console.WriteLine("".PadRight(50, '='));
        
        foreach (var kvp in counters)
        {
            var counterName = GetCounterDisplayName(kvp.Key);
            var dataPoints = kvp.Value;
            
            if (dataPoints.Count > 0)
            {
                var timeSpan = dataPoints.Last().Timestamp - dataPoints.First().Timestamp;
                Console.WriteLine($"{counterName}:");
                Console.WriteLine($"  期間: {timeSpan.TotalMinutes:F1} 分");
                Console.WriteLine($"  サンプリング間隔: {timeSpan.TotalSeconds / dataPoints.Count:F1} 秒");
                Console.WriteLine();
            }
        }
    }

    static async Task ExportToJson(Dictionary<string, List<PerformanceDataPoint>> counters, string outputPath)
    {
        try
        {
            var json = JsonConvert.SerializeObject(counters, Formatting.Indented);
            await File.WriteAllTextAsync(outputPath, json);
            Console.WriteLine($"データをJSONファイルに出力しました: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"JSON出力エラー: {ex.Message}");
            LogError($"JSON export error: {ex}");
        }
    }

    static Dictionary<string, List<PerformanceDataPoint>> GenerateSampleData()
    {
        var counters = new Dictionary<string, List<PerformanceDataPoint>>();
        var counterNames = new[]
        {
            "\\Processor(_Total)\\% Processor Time",
            "\\Memory\\Available MBytes",
            "\\PhysicalDisk(_Total)\\Disk Reads/sec",
            "\\PhysicalDisk(_Total)\\Disk Writes/sec",
            "\\Network Interface(*)\\Bytes Total/sec",
            "\\System\\Context Switches/sec",
            "\\Process(_Total)\\Working Set"
        };

        var random = new Random();
        var startTime = DateTime.Now.AddMinutes(-30);

        foreach (var counterName in counterNames)
        {
            var dataPoints = new List<PerformanceDataPoint>();
            
            for (int i = 0; i < 180; i++) // 30分間、10秒間隔
            {
                var timestamp = startTime.AddSeconds(i * 10);
                var value = GenerateSampleValue(counterName, random, i);
                
                dataPoints.Add(new PerformanceDataPoint
                {
                    Timestamp = timestamp,
                    Value = value,
                    Counter = counterName
                });
            }
            
            counters[counterName] = dataPoints;
        }

        return counters;
    }

    static double GenerateSampleValue(string counter, Random random, int index)
    {
        return counter switch
        {
            var c when c.Contains("% Processor Time") => Math.Max(0, Math.Min(100, 
                20 + 30 * Math.Sin(index * 0.1) + random.NextDouble() * 10)),
            var c when c.Contains("Available MBytes") => Math.Max(1000, 
                4000 + 1000 * Math.Sin(index * 0.05) + random.NextDouble() * 500),
            var c when c.Contains("Disk Reads/sec") => Math.Max(0, 
                50 + 20 * Math.Sin(index * 0.2) + random.NextDouble() * 30),
            var c when c.Contains("Disk Writes/sec") => Math.Max(0, 
                30 + 15 * Math.Sin(index * 0.15) + random.NextDouble() * 20),
            var c when c.Contains("Bytes Total/sec") => Math.Max(0, 
                1000000 + 500000 * Math.Sin(index * 0.1) + random.NextDouble() * 200000),
            var c when c.Contains("Context Switches/sec") => Math.Max(0, 
                5000 + 2000 * Math.Sin(index * 0.3) + random.NextDouble() * 1000),
            var c when c.Contains("Working Set") => Math.Max(100000000, 
                2000000000 + 500000000 * Math.Sin(index * 0.05) + random.NextDouble() * 100000000),
            _ => random.NextDouble() * 100
        };
    }

    static string GetCounterDisplayName(string counter)
    {
        var parts = counter.Split('\\');
        if (parts.Length >= 3)
        {
            return $"{parts[1]} - {parts[2]}";
        }
        return counter;
    }

    static void LogError(string message)
    {
        try
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch
        {
            // ログ出力に失敗した場合は何もしない
        }
    }
}

/// <summary>
/// パフォーマンスデータポイント
/// </summary>
public class PerformanceDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string Counter { get; set; } = string.Empty;
}

/// <summary>
/// BLGファイル解析クラス (Windows環境でのみ動作)
/// </summary>
public class BlgFileAnalyzer
{
    public async Task<Dictionary<string, List<PerformanceDataPoint>>> ParseBlgFileAsync(string filePath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("BLG file analysis is only supported on Windows.");
        }

        // 実際の実装では、PDH APIまたはWMIを使用してBLGファイルを解析
        // ここでは簡単なサンプル実装を提供
        var counters = new Dictionary<string, List<PerformanceDataPoint>>();
        
        // TODO: 実際のBLGファイル解析ロジックを実装
        // 現在はサンプルデータを返す
        await Task.Delay(1000); // 解析のシミュレーション
        
        throw new NotImplementedException("Actual BLG file parsing will be implemented with PDH API.");
    }
}
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// BLGファイル解析を行うクラス（PDH APIを使用）
/// DevelopersCommunity/PerformanceCounters リポジトリの実装を参考にした改良版
/// </summary>
public class BlgFileAnalyzer : IDisposable
{
    private string? _filePath = null;
    private bool _disposed = false;

    public class CounterInfo
    {
        public string FullPath { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
        public string CounterName { get; set; } = string.Empty;
        public string InstanceName { get; set; } = string.Empty;
        public List<CounterDataPoint> DataPoints { get; set; } = new();
    }

    public class CounterDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public uint Status { get; set; }
    }

    /// <summary>
    /// BLGファイルを開く
    /// </summary>
    public async Task<bool> OpenBlgFileAsync(string filePath, IProgress<string>? progress = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"BLGファイルが見つかりません: {filePath}");
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("PDH APIはWindows環境でのみ利用可能です。");
        }

        progress?.Report("BLGファイルを検証しています...");

        return await Task.Run(() =>
        {
            try
            {
                _filePath = filePath;
                
                // テスト用のクエリを開いて、ファイルが有効か確認
                uint result = PdhApi.PdhOpenQuery(_filePath, IntPtr.Zero, out IntPtr testQuery);
                if (result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"BLGファイルを開けませんでした: {PdhApi.GetErrorMessage(result)}");
                }
                
                // すぐにクエリを閉じる
                PdhApi.PdhCloseQuery(testQuery);
                
                progress?.Report("BLGファイルが正常に開かれました");
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"エラー: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// データソースの時間範囲を取得
    /// </summary>
    public async Task<(DateTime startTime, DateTime endTime)> GetTimeRangeAsync(IProgress<string>? progress = null)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            throw new InvalidOperationException("BLGファイルが開かれていません。");
        }

        progress?.Report("時間範囲を取得中...");

        return await Task.Run(() =>
        {
            try
            {
                uint bufferSize = (uint)Marshal.SizeOf<PdhApi.PDH_TIME_INFO>();
                uint result = PdhApi.PdhGetDataSourceTimeRange(_filePath, out uint numEntries, out PdhApi.PDH_TIME_INFO timeInfo, ref bufferSize);
                
                PdhApi.CheckPdhStatus(result);

                var startTime = PdhApi.DateTimeFromFileTime(timeInfo.StartTime);
                var endTime = PdhApi.DateTimeFromFileTime(timeInfo.EndTime);

                progress?.Report($"時間範囲: {startTime:yyyy-MM-dd HH:mm:ss} - {endTime:yyyy-MM-dd HH:mm:ss} (サンプル数: {numEntries})");

                return (startTime, endTime);
            }
            catch (Exception ex)
            {
                progress?.Report($"時間範囲取得エラー: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// 利用可能なマシン名を取得
    /// </summary>
    public async Task<List<string>> GetMachineNamesAsync(IProgress<string>? progress = null)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            throw new InvalidOperationException("BLGファイルが開かれていません。");
        }

        progress?.Report("マシン名を取得中...");

        return await Task.Run(() =>
        {
            try
            {
                uint bufferSize = 0;
                uint result = PdhApi.PdhEnumMachines(_filePath, null, ref bufferSize);
                
                var machines = new List<string>();
                
                if (result == PdhApi.PDH_MORE_DATA && bufferSize > 0)
                {
                    var buffer = new char[bufferSize];
                    result = PdhApi.PdhEnumMachines(_filePath, buffer, ref bufferSize);
                    PdhApi.CheckPdhStatus(result);
                    
                    machines = PdhApi.MultipleStringsToList(buffer);
                }

                progress?.Report($"{machines.Count}個のマシンが見つかりました");
                return machines;
            }
            catch (Exception ex)
            {
                progress?.Report($"マシン名取得エラー: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// ワイルドカードパスを展開してカウンターリストを取得
    /// </summary>
    public async Task<List<string>> ExpandWildCardPathAsync(string wildCardPath, IProgress<string>? progress = null)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            throw new InvalidOperationException("BLGファイルが開かれていません。");
        }

        progress?.Report($"ワイルドカードパス '{wildCardPath}' を展開中...");

        return await Task.Run(() =>
        {
            try
            {
                uint bufferSize = 0;
                uint result = PdhApi.PdhExpandWildCardPath(_filePath, wildCardPath, null, ref bufferSize, 0);
                
                if (result == PdhApi.PDH_ENTRY_NOT_IN_LOG_FILE || result == PdhApi.PDH_CSTATUS_NO_OBJECT)
                {
                    progress?.Report($"ワイルドカードパス '{wildCardPath}' に一致するカウンターが見つかりません");
                    return new List<string>();
                }
                
                if (result != PdhApi.PDH_MORE_DATA)
                {
                    PdhApi.CheckPdhStatus(result);
                }

                var buffer = new char[bufferSize];
                result = PdhApi.PdhExpandWildCardPath(_filePath, wildCardPath, buffer, ref bufferSize, 0);
                PdhApi.CheckPdhStatus(result);

                var expandedPaths = PdhApi.MultipleStringsToList(buffer);
                progress?.Report($"ワイルドカードパス '{wildCardPath}' から {expandedPaths.Count} 個のカウンターパスを展開しました");

                return expandedPaths;
            }
            catch (Exception ex)
            {
                progress?.Report($"ワイルドカード展開エラー: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// 指定されたカウンターのデータを読み込み
    /// PerformanceCounters リポジトリの PCReaderEnumerator パターンを使用
    /// </summary>
    public async Task<CounterInfo> LoadCounterDataAsync(string counterPath, IProgress<string>? progress = null)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            throw new InvalidOperationException("BLGファイルが開かれていません。");
        }

        progress?.Report($"カウンターデータを読み込み中: {counterPath}");

        return await Task.Run(() =>
        {
            IntPtr query = IntPtr.Zero;
            IntPtr counter = IntPtr.Zero;
            var counterInfo = new CounterInfo
            {
                FullPath = counterPath,
                ObjectName = ExtractObjectName(counterPath),
                CounterName = ExtractCounterName(counterPath),
                InstanceName = ExtractInstanceName(counterPath)
            };

            try
            {
                // BLGファイルを指定してクエリを開く
                uint result = PdhApi.PdhOpenQuery(_filePath, IntPtr.Zero, out query);
                PdhApi.CheckPdhStatus(result);

                // カウンターをクエリに追加
                result = PdhApi.PdhAddCounter(query, counterPath, IntPtr.Zero, out counter);
                PdhApi.CheckPdhStatus(result);

                progress?.Report($"カウンター '{counterPath}' をクエリに追加しました");

                // 最初のデータ収集（これによりデータの読み込みが初期化される）
                result = PdhApi.PdhCollectQueryData(query);
                if (result != PdhApi.PDH_NO_MORE_DATA && result != PdhApi.PDH_NO_DATA)
                {
                    PdhApi.CheckPdhStatus(result);
                }

                var dataPoints = new List<CounterDataPoint>();
                int sampleCount = 0;
                const int maxSamples = 100000; // 安全のため最大100,000サンプル

                // BLGファイル内の全データポイントを反復処理
                while (sampleCount < maxSamples)
                {
                    result = PdhApi.PdhCollectQueryDataWithTime(query, out long timestamp);
                    
                    if (result == PdhApi.PDH_NO_MORE_DATA || result == PdhApi.PDH_NO_DATA)
                    {
                        break; // データ終了
                    }
                    
                    PdhApi.CheckPdhStatus(result);

                    // フォーマットされた値を取得
                    result = PdhApi.PdhGetFormattedCounterValue(
                        counter,
                        PdhApi.PDH_FMT_DOUBLE,
                        IntPtr.Zero,
                        out PdhApi.PDH_FMT_COUNTERVALUE value);

                    double formattedValue;
                    if (result == PdhApi.PDH_CALC_NEGATIVE_DENOMINATOR ||
                        result == PdhApi.PDH_CALC_NEGATIVE_VALUE ||
                        result == PdhApi.PDH_CALC_NEGATIVE_TIMEBASE ||
                        result == PdhApi.PDH_INVALID_DATA)
                    {
                        formattedValue = double.NaN;
                    }
                    else
                    {
                        PdhApi.CheckPdhStatus(result);
                        formattedValue = value.doubleValue;
                    }

                    var dataPoint = new CounterDataPoint
                    {
                        Timestamp = PdhApi.DateTimeFromFileTime(timestamp),
                        Value = formattedValue,
                        Status = value.CStatus
                    };
                    
                    dataPoints.Add(dataPoint);
                    sampleCount++;

                    // 進行状況を定期的に報告
                    if (sampleCount % 1000 == 0)
                    {
                        progress?.Report($"カウンター '{counterPath}': {sampleCount} データポイントを読み込み中...");
                    }
                }

                counterInfo.DataPoints = dataPoints;
                progress?.Report($"カウンター '{counterPath}' から {dataPoints.Count} データポイントを読み込みました");

                return counterInfo;
            }
            catch (Exception ex)
            {
                progress?.Report($"カウンターデータ読み込みエラー: {ex.Message}");
                throw;
            }
            finally
            {
                // リソースをクリーンアップ
                if (counter != IntPtr.Zero)
                {
                    PdhApi.PdhRemoveCounter(counter);
                }
                if (query != IntPtr.Zero)
                {
                    PdhApi.PdhCloseQuery(query);
                }
            }
        });
    }

    /// <summary>
    /// 時間制約付きでカウンターデータを読み込み
    /// </summary>
    public async Task<CounterInfo> LoadCounterDataAsync(string counterPath, DateTime? startTime, DateTime? endTime, IProgress<string>? progress = null)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            throw new InvalidOperationException("BLGファイルが開かれていません。");
        }

        progress?.Report($"時間制約付きでカウンターデータを読み込み中: {counterPath}");
        if (startTime.HasValue || endTime.HasValue)
        {
            progress?.Report($"時間範囲: {startTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "開始なし"} - {endTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "終了なし"}");
        }

        return await Task.Run(() =>
        {
            IntPtr query = IntPtr.Zero;
            IntPtr counter = IntPtr.Zero;
            var counterInfo = new CounterInfo
            {
                FullPath = counterPath,
                ObjectName = ExtractObjectName(counterPath),
                CounterName = ExtractCounterName(counterPath),
                InstanceName = ExtractInstanceName(counterPath)
            };

            try
            {
                // BLGファイルを指定してクエリを開く
                uint result = PdhApi.PdhOpenQuery(_filePath, IntPtr.Zero, out query);
                PdhApi.CheckPdhStatus(result);

                // 時間範囲を設定（必要に応じて）
                if (startTime.HasValue || endTime.HasValue)
                {
                    var timeInfo = new PdhApi.PDH_TIME_INFO
                    {
                        StartTime = startTime.HasValue ? PdhApi.FileTimeFromDateTime(startTime.Value) : 0,
                        EndTime = endTime.HasValue ? PdhApi.FileTimeFromDateTime(endTime.Value) : long.MaxValue,
                        SampleCount = 0
                    };
                    
                    result = PdhApi.PdhSetQueryTimeRange(query, ref timeInfo);
                    PdhApi.CheckPdhStatus(result);
                    progress?.Report("時間範囲を設定しました");
                }

                // カウンターをクエリに追加
                result = PdhApi.PdhAddCounter(query, counterPath, IntPtr.Zero, out counter);
                PdhApi.CheckPdhStatus(result);

                progress?.Report($"カウンター '{counterPath}' をクエリに追加しました");

                // 最初のデータ収集
                result = PdhApi.PdhCollectQueryData(query);
                if (result != PdhApi.PDH_NO_MORE_DATA && result != PdhApi.PDH_NO_DATA)
                {
                    PdhApi.CheckPdhStatus(result);
                }

                var dataPoints = new List<CounterDataPoint>();
                int sampleCount = 0;
                const int maxSamples = 100000; // 安全のため最大100,000サンプル

                // BLGファイル内の全データポイントを反復処理
                while (sampleCount < maxSamples)
                {
                    result = PdhApi.PdhCollectQueryDataWithTime(query, out long timestamp);
                    
                    if (result == PdhApi.PDH_NO_MORE_DATA || result == PdhApi.PDH_NO_DATA)
                    {
                        break; // データ終了
                    }
                    
                    PdhApi.CheckPdhStatus(result);

                    var currentTime = PdhApi.DateTimeFromFileTime(timestamp);
                    
                    // 時間制約のチェック（追加の安全策）
                    if (startTime.HasValue && currentTime < startTime.Value)
                    {
                        continue; // スキップ
                    }
                    if (endTime.HasValue && currentTime > endTime.Value)
                    {
                        break; // 終了
                    }

                    // フォーマットされた値を取得
                    result = PdhApi.PdhGetFormattedCounterValue(
                        counter,
                        PdhApi.PDH_FMT_DOUBLE,
                        IntPtr.Zero,
                        out PdhApi.PDH_FMT_COUNTERVALUE value);

                    double formattedValue;
                    if (result == PdhApi.PDH_CALC_NEGATIVE_DENOMINATOR ||
                        result == PdhApi.PDH_CALC_NEGATIVE_VALUE ||
                        result == PdhApi.PDH_CALC_NEGATIVE_TIMEBASE ||
                        result == PdhApi.PDH_INVALID_DATA)
                    {
                        formattedValue = double.NaN;
                    }
                    else
                    {
                        PdhApi.CheckPdhStatus(result);
                        formattedValue = value.doubleValue;
                    }

                    var dataPoint = new CounterDataPoint
                    {
                        Timestamp = currentTime,
                        Value = formattedValue,
                        Status = value.CStatus
                    };
                    
                    dataPoints.Add(dataPoint);
                    sampleCount++;

                    // 進行状況を定期的に報告
                    if (sampleCount % 1000 == 0)
                    {
                        progress?.Report($"カウンター '{counterPath}': {sampleCount} データポイントを読み込み中...");
                    }
                }

                counterInfo.DataPoints = dataPoints;
                progress?.Report($"カウンター '{counterPath}' から {dataPoints.Count} データポイントを読み込みました");

                return counterInfo;
            }
            catch (Exception ex)
            {
                progress?.Report($"カウンターデータ読み込みエラー: {ex.Message}");
                throw;
            }
            finally
            {
                // リソースをクリーンアップ
                if (counter != IntPtr.Zero)
                {
                    PdhApi.PdhRemoveCounter(counter);
                }
                if (query != IntPtr.Zero)
                {
                    PdhApi.PdhCloseQuery(query);
                }
            }
        });
    }

    private static string ExtractObjectName(string counterPath)
    {
        // \\ObjectName(Instance)\\CounterName 形式から ObjectName を抽出
        var parts = counterPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var objectPart = parts[0];
            var parenIndex = objectPart.IndexOf('(');
            return parenIndex > 0 ? objectPart[..parenIndex] : objectPart;
        }
        return string.Empty;
    }

    private static string ExtractCounterName(string counterPath)
    {
        // \\ObjectName(Instance)\\CounterName 形式から CounterName を抽出
        var parts = counterPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[^1] : string.Empty;
    }

    private static string ExtractInstanceName(string counterPath)
    {
        // \\ObjectName(Instance)\\CounterName 形式から Instance を抽出
        var parts = counterPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var objectPart = parts[0];
            var startParen = objectPart.IndexOf('(');
            var endParen = objectPart.IndexOf(')', startParen);
            if (startParen > 0 && endParen > startParen)
            {
                return objectPart.Substring(startParen + 1, endParen - startParen - 1);
            }
        }
        return string.Empty;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    ~BlgFileAnalyzer()
    {
        Dispose(false);
    }
}
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
    private IntPtr _dataSource = IntPtr.Zero;
    private IntPtr _query = IntPtr.Zero;
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
                
                // 方法1: PdhBindInputDataSourceを使用
                progress?.Report("PdhBindInputDataSourceでBLGファイルを開いています...");
                uint result = PdhApi.PdhBindInputDataSource(out _dataSource, filePath);
                
                if (result == PdhApi.ERROR_SUCCESS)
                {
                    progress?.Report("PdhBindInputDataSourceでBLGファイルが正常に開かれました");
                    
                    // クエリをデータソースと関連付けて作成
                    result = PdhApi.PdhOpenQuery(filePath, IntPtr.Zero, out _query);
                    if (result != PdhApi.ERROR_SUCCESS)
                    {
                        // データソースをクリーンアップしてから例外を投げる
                        PdhApi.PdhCloseLog(_dataSource, 0);
                        _dataSource = IntPtr.Zero;
                        throw new Exception($"クエリを開けませんでした: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                    }
                    
                    progress?.Report("PDHクエリが正常に開かれました");
                    return true;
                }
                
                // 方法1が失敗した場合、方法2: 従来のPdhOpenLogを試行
                progress?.Report($"PdhBindInputDataSource失敗 ({PdhApi.GetErrorMessage(result)})、PdhOpenLogを試行中...");
                
                // まずクエリを開く
                result = PdhApi.PdhOpenQuery(null, IntPtr.Zero, out _query);
                if (result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"クエリを開けませんでした: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                }
                
                progress?.Report("PDHクエリが正常に開かれました");
                
                // その後BLGファイルをデータソースとして開く
                result = PdhApi.PdhOpenLog(
                    filePath,
                    PdhApi.GENERIC_READ,
                    out uint logType,
                    _query,  // 作成したクエリハンドルを使用
                    0,
                    null,
                    out _dataSource);

                if (result != PdhApi.ERROR_SUCCESS)
                {
                    // クエリをクリーンアップしてから例外を投げる
                    PdhApi.PdhCloseQuery(_query);
                    _query = IntPtr.Zero;
                    throw new Exception($"BLGファイルを開けませんでした: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                }
                
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
    /// <summary>
    /// BLGファイル内のマシン名を取得（内部実装）
    /// </summary>
    private List<string> GetMachineNames()
    {
        var machines = new List<string>();
        
        try
        {
            if (_dataSource != IntPtr.Zero)
            {
                uint bufferSize = 0;
                uint result = PdhApi.PdhEnumMachinesH(_dataSource, IntPtr.Zero, ref bufferSize);
                
                if (result == PdhApi.PDH_MORE_DATA && bufferSize > 0)
                {
                    IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize * 2); // Unicodeのため2倍
                    
                    try
                    {
                        result = PdhApi.PdhEnumMachinesH(_dataSource, buffer, ref bufferSize);
                        
                        if (result == PdhApi.ERROR_SUCCESS)
                        {
                            var machineList = Marshal.PtrToStringUni(buffer, (int)bufferSize - 1);
                            if (machineList != null)
                            {
                                machines = ParseMultiStringZ(machineList);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // マシン名取得に失敗しても続行
            Console.WriteLine($"マシン名取得エラー: {ex.Message}");
        }
        
        return machines;
    }

    /// <summary>
    /// BLGファイル内のマシン名を取得（公開メソッド）
    /// </summary>
    public List<string> GetMachineNamesFromBlg()
    {
        return GetMachineNames();
    }

    /// <summary>
    /// null区切りの文字列を解析
    /// </summary>
    private static List<string> ParseMultiStringZ(string multiString)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        
        for (int i = 0; i < multiString.Length; i++)
        {
            if (multiString[i] == '\0')
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(multiString[i]);
            }
        }
        
        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }
        
        return result;
    }

    /// <summary>
    /// 利用可能なパフォーマンスオブジェクトを列挙
    /// </summary>
    public async Task<List<string>> EnumerateObjectsAsync(IProgress<string>? progress = null)
    {
        if (_dataSource == IntPtr.Zero)
        {
            throw new InvalidOperationException("BLGファイルが開かれていません。");
        }

        progress?.Report("パフォーマンスオブジェクトを列挙中...");

        return await Task.Run(() =>
        {
            try
            {
                var objects = new List<string>();
                
                // BLGファイル内のマシン名を取得
                var machineNames = GetMachineNames();
                string? machineName = machineNames.FirstOrDefault();
                progress?.Report($"マシン名を検出: {machineName ?? "null"}");

                // 方法1: PdhEnumObjectsH（BLGファイル専用API）を試行
                progress?.Report("方法1: PdhEnumObjectsH（BLGファイル専用API）を試行中...");
                var objectsH = TryEnumerateObjectsH(machineName, progress);
                if (objectsH.Count > 0)
                {
                    progress?.Report($"PdhEnumObjectsHで{objectsH.Count}個のオブジェクトを取得しました");
                    objects.AddRange(objectsH);
                }
                else
                {
                    progress?.Report("PdhEnumObjectsHでオブジェクトを取得できませんでした");
                }

                // 方法2: PdhEnumObjectsA（ANSI版）を試行（フォールバック）
                if (objects.Count == 0)
                {
                    progress?.Report("方法2: PdhEnumObjectsA（ANSI版）をフォールバックとして試行中...");
                    var objectsA = TryEnumerateObjectsA(machineName, progress);
                    if (objectsA.Count > 0)
                    {
                        progress?.Report($"PdhEnumObjectsAで{objectsA.Count}個のオブジェクトを取得しました");
                        objects.AddRange(objectsA);
                    }
                    else
                    {
                        progress?.Report("PdhEnumObjectsAでもオブジェクトを取得できませんでした");
                    }
                }

                progress?.Report($"{objects.Count}個のオブジェクトが見つかりました");
                return objects;
            }
            catch (Exception ex)
            {
                progress?.Report($"オブジェクト列挙エラー: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// PdhEnumObjectsH（BLGファイル専用API）を使用してオブジェクトを列挙
    /// </summary>
    private List<string> TryEnumerateObjectsH(string? machineName, IProgress<string>? progress)
    {
        var objects = new List<string>();
        
        try
        {
            uint bufferSize = 0;
            uint result = PdhApi.PdhEnumObjectsH(_dataSource, machineName, IntPtr.Zero, ref bufferSize, 100, true);
            
            if (result == PdhApi.PDH_MORE_DATA && bufferSize > 0)
            {
                IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize * 2);
                
                try
                {
                    result = PdhApi.PdhEnumObjectsH(_dataSource, machineName, buffer, ref bufferSize, 100, true);
                    
                    if (result == PdhApi.ERROR_SUCCESS)
                    {
                        var objectList = Marshal.PtrToStringUni(buffer, (int)bufferSize - 1);
                        if (objectList != null)
                        {
                            objects = ParseMultiStringZ(objectList);
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"PdhEnumObjectsHエラー: {ex.Message}");
        }
        
        return objects;
    }

    /// <summary>
    /// PdhEnumObjectsA（ANSI版）を使用してオブジェクトを列挙
    /// </summary>
    private List<string> TryEnumerateObjectsA(string? machineName, IProgress<string>? progress)
    {
        var objects = new List<string>();
        
        try
        {
            uint bufferSize = 0;
            uint result = PdhApi.PdhEnumObjectsA(_dataSource, machineName, IntPtr.Zero, ref bufferSize, 100, true);
            
            if (result == PdhApi.PDH_MORE_DATA && bufferSize > 0)
            {
                IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
                
                try
                {
                    result = PdhApi.PdhEnumObjectsA(_dataSource, machineName, buffer, ref bufferSize, 100, true);
                    
                    if (result == PdhApi.ERROR_SUCCESS)
                    {
                        var objectList = Marshal.PtrToStringAnsi(buffer, (int)bufferSize - 1);
                        if (objectList != null)
                        {
                            objects = ParseMultiStringZ(objectList);
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"PdhEnumObjectsAエラー: {ex.Message}");
        }
        
        return objects;
    }

    /// <summary>
    /// 指定されたオブジェクトのカウンターとインスタンスを列挙
    /// </summary>
    public async Task<(List<string> counters, List<string> instances)> EnumerateCountersAndInstancesAsync(
        string objectName, IProgress<string>? progress = null)
    {
        if (_dataSource == IntPtr.Zero)
        {
            throw new InvalidOperationException("BLGファイルが開かれていません。");
        }

        progress?.Report($"{objectName} のカウンターを列挙中...");

        return await Task.Run(() =>
        {
            try
            {
                // BLGファイル内のマシン名を取得
                var machineNames = GetMachineNames();
                string? machineName = machineNames.FirstOrDefault();
                
                uint counterBufferSize = 0;
                uint instanceBufferSize = 0;

                // まずバッファサイズを取得
                uint result = PdhApi.PdhEnumObjectItemsHSB(
                    _dataSource,
                    machineName,
                    objectName,
                    null,
                    ref counterBufferSize,
                    null,
                    ref instanceBufferSize,
                    100, // PERF_DETAIL_NOVICE
                    0);

                progress?.Report($"バッファサイズ取得結果: {PdhApi.GetErrorMessage(result)}, カウンター: {counterBufferSize}, インスタンス: {instanceBufferSize}");

                var counters = new List<string>();
                var instances = new List<string>();

                if (result == PdhApi.PDH_MORE_DATA && (counterBufferSize > 0 || instanceBufferSize > 0))
                {
                    IntPtr counterBuffer = IntPtr.Zero;
                    IntPtr instanceBuffer = IntPtr.Zero;

                    try
                    {
                        // バッファを割り当て
                        if (counterBufferSize > 0)
                        {
                            counterBuffer = Marshal.AllocHGlobal((int)counterBufferSize * 2); // Unicode対応
                        }
                        if (instanceBufferSize > 0)
                        {
                            instanceBuffer = Marshal.AllocHGlobal((int)instanceBufferSize * 2); // Unicode対応
                        }

                        // 実際にデータを取得
                        result = PdhApi.PdhEnumObjectItemsH(
                            _dataSource,
                            machineName,
                            objectName,
                            counterBuffer,
                            ref counterBufferSize,
                            instanceBuffer,
                            ref instanceBufferSize,
                            100, // PERF_DETAIL_NOVICE
                            0);

                        if (result == PdhApi.ERROR_SUCCESS)
                        {
                            // カウンター名を解析
                            if (counterBuffer != IntPtr.Zero && counterBufferSize > 0)
                            {
                                var counterList = Marshal.PtrToStringUni(counterBuffer, (int)counterBufferSize - 1);
                                if (counterList != null)
                                {
                                    counters = ParseMultiStringZ(counterList);
                                }
                            }

                            // インスタンス名を解析
                            if (instanceBuffer != IntPtr.Zero && instanceBufferSize > 0)
                            {
                                var instanceList = Marshal.PtrToStringUni(instanceBuffer, (int)instanceBufferSize - 1);
                                if (instanceList != null)
                                {
                                    instances = ParseMultiStringZ(instanceList);
                                }
                            }
                        }
                        else
                        {
                            throw new Exception($"カウンター列挙に失敗しました: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                        }
                    }
                    finally
                    {
                        if (counterBuffer != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(counterBuffer);
                        }
                        if (instanceBuffer != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(instanceBuffer);
                        }
                    }
                }
                else if (result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"カウンター列挙のバッファサイズ取得に失敗しました: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                }

                progress?.Report($"{objectName}: {counters.Count}個のカウンター, {instances.Count}個のインスタンス");
                return (counters, instances);
            }
            catch (Exception ex)
            {
                progress?.Report($"カウンター列挙エラー: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// 全てのカウンターパスを生成
    /// </summary>
    public async Task<List<string>> GenerateAllCounterPathsAsync(IProgress<string>? progress = null)
    {
        progress?.Report("全カウンターパスを生成中...");

        var allPaths = new List<string>();
        var objects = await EnumerateObjectsAsync(progress);

        progress?.Report($"見つかったオブジェクト数: {objects.Count}");
        
        foreach (var objectName in objects)
        {
            try
            {
                progress?.Report($"処理中のオブジェクト: {objectName}");
                var (counters, instances) = await EnumerateCountersAndInstancesAsync(objectName, progress);

                var pathsForThisObject = 0;
                foreach (var counterName in counters)
                {
                    if (instances.Count > 0)
                    {
                        // インスタンスありの場合
                        foreach (var instanceName in instances)
                        {
                            var path = $"\\{objectName}({instanceName})\\{counterName}";
                            allPaths.Add(path);
                            pathsForThisObject++;
                        }
                    }
                    else
                    {
                        // インスタンスなしの場合
                        var path = $"\\{objectName}\\{counterName}";
                        allPaths.Add(path);
                        pathsForThisObject++;
                    }
                }
                
                progress?.Report($"{objectName}: {counters.Count}カウンター × {Math.Max(1, instances.Count)}インスタンス = {pathsForThisObject}パス");
            }
            catch (Exception ex)
            {
                // 個別のオブジェクトでエラーが発生しても続行
                progress?.Report($"警告: {objectName} の処理でエラー: {ex.Message}");
                continue;
            }
        }

        progress?.Report($"合計 {allPaths.Count} 個のカウンターパスを生成しました");
        
        // 最初のいくつかのパスをログ出力（デバッグ用）
        if (allPaths.Count > 0)
        {
            var samplePaths = allPaths.Take(10).ToList();
            progress?.Report($"サンプルパス（最初の10個）: {string.Join(", ", samplePaths)}");
        }

        return allPaths;
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
            if (_query != IntPtr.Zero)
            {
                PdhApi.PdhCloseQuery(_query);
                _query = IntPtr.Zero;
            }
            
            if (_dataSource != IntPtr.Zero)
            {
                PdhApi.PdhCloseLog(_dataSource, 0);
                _dataSource = IntPtr.Zero;
            }
            
            _disposed = true;
        }
    }

    ~BlgFileAnalyzer()
    {
        Dispose(false);
    }
}
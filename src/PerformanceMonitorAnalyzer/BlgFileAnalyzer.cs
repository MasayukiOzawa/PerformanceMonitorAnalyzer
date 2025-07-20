using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Concurrent;

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
    /// データソースのサンプリング間隔を取得
    /// </summary>
    public async Task<TimeSpan> GetSamplingIntervalAsync(IProgress<string>? progress = null)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            throw new InvalidOperationException("BLGファイルが開かれていません。");
        }

        progress?.Report("サンプリング間隔を取得中...");

        return await Task.Run(() =>
        {
            try
            {
                // 方法1: 実際のデータポイントから間隔を計算（より正確）
                progress?.Report("実際のデータポイントから間隔を計算中...");
                var actualInterval = CalculateIntervalFromActualData(progress);
                
                if (actualInterval != TimeSpan.Zero)
                {
                    progress?.Report($"実際のデータから計算した間隔: {FormatInterval(actualInterval)}");
                    return actualInterval;
                }
                
                // 方法2: フォールバック - PdhGetDataSourceTimeRange を使用
                progress?.Report("フォールバック: PDH APIから時間範囲を取得中...");
                uint bufferSize = (uint)Marshal.SizeOf<PdhApi.PDH_TIME_INFO>();
                uint result = PdhApi.PdhGetDataSourceTimeRange(_filePath, out uint numEntries, out PdhApi.PDH_TIME_INFO timeInfo, ref bufferSize);
                
                PdhApi.CheckPdhStatus(result);

                var startTime = PdhApi.DateTimeFromFileTime(timeInfo.StartTime);
                var endTime = PdhApi.DateTimeFromFileTime(timeInfo.EndTime);
                
                progress?.Report($"PDH情報: サンプル数={numEntries}, 開始時刻={startTime:yyyy-MM-dd HH:mm:ss.fff}, 終了時刻={endTime:yyyy-MM-dd HH:mm:ss.fff}");
                
                // サンプル数が2以上の場合、時間範囲を(サンプル数-1)で割って間隔を計算
                TimeSpan interval = TimeSpan.Zero;
                if (numEntries > 1)
                {
                    var totalDuration = endTime - startTime;
                    interval = TimeSpan.FromTicks(totalDuration.Ticks / (numEntries - 1));
                    progress?.Report($"PDHベース計算: {FormatInterval(interval)} (総時間: {totalDuration}, サンプル数: {numEntries})");
                }
                else if (numEntries == 1)
                {
                    progress?.Report($"警告: サンプル数が1のため間隔計算不可 (単一サンプル)");
                    // 単一サンプルの場合でも最小間隔を返す（例：1秒）
                    interval = TimeSpan.FromSeconds(1);
                }
                else
                {
                    progress?.Report($"警告: サンプル数が0のため間隔計算不可");
                    throw new InvalidOperationException($"BLGファイルにサンプルデータが含まれていません (サンプル数: {numEntries})");
                }

                return interval;
            }
            catch (Exception ex)
            {
                progress?.Report($"サンプリング間隔取得エラー: {ex.Message}");
                throw;
            }
        });
    }
    
    /// <summary>
    /// 実際のデータポイントを使用してサンプリング間隔を計算
    /// </summary>
    private TimeSpan CalculateIntervalFromActualData(IProgress<string>? progress = null)
    {
        IntPtr query = IntPtr.Zero;
        IntPtr counter = IntPtr.Zero;
        
        try
        {
            // BLGファイルを指定してクエリを開く
            uint result = PdhApi.PdhOpenQuery(_filePath, IntPtr.Zero, out query);
            if (result != PdhApi.ERROR_SUCCESS)
            {
                progress?.Report($"実データ検証用クエリオープンに失敗: {PdhApi.GetErrorMessage(result)}");
                return TimeSpan.Zero;
            }

            // 利用可能なカウンターを取得
            var machineNames = GetMachineNames();
            string? machineName = machineNames.FirstOrDefault();
            
            if (string.IsNullOrEmpty(machineName))
            {
                progress?.Report("実データ検証: マシン名を取得できませんでした");
                return TimeSpan.Zero;
            }

            // 確実に存在するカウンターパスのリストを試行
            var testCounterPaths = new string[]
            {
                $"{machineName}\\Processor(_Total)\\% Processor Time",
                $"{machineName}\\Memory\\Available Bytes",
                $"{machineName}\\System\\System Calls/sec",
                $"{machineName}\\Processor Information(_Total)\\% Processor Time"
            };
            
            string? workingCounterPath = null;
            
            // 利用可能なカウンターを見つける
            foreach (var testPath in testCounterPaths)
            {
                result = PdhApi.PdhAddCounter(query, testPath, IntPtr.Zero, out counter);
                if (result == PdhApi.ERROR_SUCCESS)
                {
                    workingCounterPath = testPath;
                    progress?.Report($"実データ検証に使用するカウンター: {testPath}");
                    break;
                }
                else
                {
                    if (counter != IntPtr.Zero)
                    {
                        PdhApi.PdhRemoveCounter(counter);
                        counter = IntPtr.Zero;
                    }
                }
            }
            
            if (string.IsNullOrEmpty(workingCounterPath))
            {
                progress?.Report("実データ検証: 利用可能なテストカウンターが見つかりませんでした");
                return TimeSpan.Zero;
            }

            // 最初の数個のデータポイントのタイムスタンプを取得
            var timestamps = new List<DateTime>();
            
            for (int i = 0; i < 10; i++) // 最初の10個のサンプルを取得
            {
                result = PdhApi.PdhCollectQueryDataWithTime(query, out long timestamp);
                
                if (result == PdhApi.PDH_NO_MORE_DATA || result == PdhApi.PDH_NO_DATA)
                {
                    break;
                }
                
                if (result == PdhApi.ERROR_SUCCESS)
                {
                    timestamps.Add(PdhApi.DateTimeFromFileTime(timestamp));
                }
                else
                {
                    // 最初のコレクションでエラーが出ることがあるので、スキップして続行
                    continue;
                }
            }

            // タイムスタンプ間隔を計算
            if (timestamps.Count >= 3) // 最低3個のサンプルが必要
            {
                var intervals = new List<TimeSpan>();
                for (int i = 1; i < timestamps.Count; i++)
                {
                    var interval = timestamps[i] - timestamps[i - 1];
                    if (interval.TotalSeconds > 0.1) // 0.1秒未満の間隔は無視（ノイズの可能性）
                    {
                        intervals.Add(interval);
                    }
                }
                
                if (intervals.Count >= 2)
                {
                    // 最も一般的な間隔を特定（四捨五入して秒単位でグループ化）
                    var roundedIntervals = intervals
                        .Select(t => Math.Round(t.TotalSeconds, 1))
                        .GroupBy(s => s)
                        .OrderByDescending(g => g.Count())
                        .First();
                    
                    var mostCommonInterval = TimeSpan.FromSeconds(roundedIntervals.Key);
                    
                    progress?.Report($"実データ検証結果: {timestamps.Count}個のタイムスタンプから{intervals.Count}個の有効間隔を抽出");
                    progress?.Report($"最も一般的な間隔: {mostCommonInterval.TotalSeconds:F1}秒 (出現回数: {roundedIntervals.Count()}/{intervals.Count})");
                    
                    return mostCommonInterval;
                }
                else
                {
                    progress?.Report($"実データ検証: 有効な間隔が不足 (有効間隔数: {intervals.Count})");
                    return TimeSpan.Zero;
                }
            }
            else
            {
                progress?.Report($"実データ検証: 十分なタイムスタンプを取得できませんでした (取得数: {timestamps.Count})");
                return TimeSpan.Zero;
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"実データ間隔計算エラー: {ex.Message}");
            return TimeSpan.Zero;
        }
        finally
        {
            if (counter != IntPtr.Zero)
            {
                PdhApi.PdhRemoveCounter(counter);
            }
            if (query != IntPtr.Zero)
            {
                PdhApi.PdhCloseQuery(query);
            }
        }
    }
    
    /// <summary>
    /// 時間間隔を分かりやすい形式にフォーマット
    /// </summary>
    private static string FormatInterval(TimeSpan interval)
    {
        if (interval.TotalHours >= 1)
        {
            return $"{interval.TotalHours:F1}時間";
        }
        else if (interval.TotalMinutes >= 1)
        {
            return $"{interval.TotalMinutes:F1}分";
        }
        else if (interval.TotalSeconds >= 1)
        {
            return $"{interval.TotalSeconds:F1}秒";
        }
        else if (interval.TotalMilliseconds >= 1)
        {
            return $"{interval.TotalMilliseconds:F0}ミリ秒";
        }
        else
        {
            return "不明";
        }
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
    /// 複数のカウンターのデータを2並列で読み込み
    /// </summary>
    public async Task<List<CounterInfo>> LoadMultipleCounterDataAsync(IEnumerable<string> counterPaths, IProgress<string>? progress = null)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            throw new InvalidOperationException("BLGファイルが開かれていません。");
        }

        var counterPathsList = counterPaths.ToList();
        progress?.Report($"複数カウンターデータを2並列で読み込み開始: {counterPathsList.Count}個のカウンター");

        // 2並列に制限するSemaphore
        using var semaphore = new SemaphoreSlim(2, 2);
        var results = new ConcurrentBag<CounterInfo>();

        // 全てのカウンターを並列で処理
        var tasks = counterPathsList.Select(async counterPath =>
        {
            await semaphore.WaitAsync();
            try
            {
                progress?.Report($"カウンター読み込み開始: {counterPath}");
                var counterInfo = await LoadCounterDataAsync(counterPath, progress);
                results.Add(counterInfo);
                progress?.Report($"カウンター読み込み完了: {counterPath}");
                return counterInfo;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        
        var resultList = results.ToList();
        progress?.Report($"複数カウンターデータの2並列読み込み完了: {resultList.Count}個のカウンター");
        
        return resultList;
    }

    /// <summary>
    /// 時間制約付きで複数のカウンターのデータを2並列で読み込み
    /// </summary>
    public async Task<List<CounterInfo>> LoadMultipleCounterDataAsync(IEnumerable<string> counterPaths, DateTime? startTime, DateTime? endTime, IProgress<string>? progress = null)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            throw new InvalidOperationException("BLGファイルが開かれていません。");
        }

        var counterPathsList = counterPaths.ToList();
        progress?.Report($"時間制約付き複数カウンターデータを2並列で読み込み開始: {counterPathsList.Count}個のカウンター");

        // 2並列に制限するSemaphore
        using var semaphore = new SemaphoreSlim(2, 2);
        var results = new ConcurrentBag<CounterInfo>();

        // 全てのカウンターを並列で処理
        var tasks = counterPathsList.Select(async counterPath =>
        {
            await semaphore.WaitAsync();
            try
            {
                progress?.Report($"時間制約付きカウンター読み込み開始: {counterPath}");
                var counterInfo = await LoadCounterDataAsync(counterPath, startTime, endTime, progress);
                results.Add(counterInfo);
                progress?.Report($"時間制約付きカウンター読み込み完了: {counterPath}");
                return counterInfo;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        
        var resultList = results.ToList();
        progress?.Report($"時間制約付き複数カウンターデータの2並列読み込み完了: {resultList.Count}個のカウンター");
        
        return resultList;
    }

    /// <summary>
    /// 指定されたカウンターのデータを読み込み
    /// PerformanceCounters リポジトリの PCReaderEnumerator パターンを使用
    /// SQLServerカウンター等の特殊ケースに対応
    /// </summary>
    public async Task<CounterInfo> LoadCounterDataAsync(string counterPath, IProgress<string>? progress = null)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            throw new InvalidOperationException("BLGファイルが開かれていません。");
        }

        // カウンターパスにマシン名が含まれていない場合は自動で補完
        var fullCounterPath = EnsureMachineNameInPath(counterPath);
        progress?.Report($"カウンターデータを読み込み中: {fullCounterPath}");

        return await Task.Run(() =>
        {
            IntPtr query = IntPtr.Zero;
            IntPtr counter = IntPtr.Zero;
            var counterInfo = new CounterInfo
            {
                FullPath = fullCounterPath,
                ObjectName = ExtractObjectName(fullCounterPath),
                CounterName = ExtractCounterName(fullCounterPath),
                InstanceName = ExtractInstanceName(fullCounterPath)
            };

            // SQLServerカウンターかどうかを判定
            bool isSqlServerCounter = fullCounterPath.Contains("SQLServer", StringComparison.OrdinalIgnoreCase);
            
            try
            {
                // 詳細なデバッグ情報を提供
                progress?.Report($"カウンターパス解析: '{counterPath}' -> '{fullCounterPath}'");
                progress?.Report($"オブジェクト名: '{counterInfo.ObjectName}', カウンター名: '{counterInfo.CounterName}', インスタンス名: '{counterInfo.InstanceName}'");
                
                if (isSqlServerCounter)
                {
                    progress?.Report("SQLServerカウンターを検出しました。特別な処理を適用します。");
                }

                // BLGファイルを指定してクエリを開く
                uint result = PdhApi.PdhOpenQuery(_filePath, IntPtr.Zero, out query);
                if (result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"クエリを開けませんでした: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                }

                // カウンターをクエリに追加
                result = PdhApi.PdhAddCounter(query, fullCounterPath, IntPtr.Zero, out counter);
                if (result != PdhApi.ERROR_SUCCESS)
                {
                    // カウンターの追加に失敗した場合の詳細な診断情報
                    var errorMsg = $"カウンター '{fullCounterPath}' をクエリに追加できませんでした: {PdhApi.GetErrorMessage(result)} (0x{result:X8})";
                    
                    // 特定のエラーコードに対する詳細な説明
                    if (result == PdhApi.PDH_CSTATUS_NO_OBJECT)
                    {
                        errorMsg += "\n原因: 指定されたパフォーマンスオブジェクトが見つかりません。";
                        if (isSqlServerCounter)
                        {
                            errorMsg += "\nSQLServerカウンターの場合、SQLServerサービスが実行されていない可能性があります。";
                        }
                    }
                    else if (result == 0x800007D1) // PDH_CSTATUS_NO_INSTANCE
                    {
                        errorMsg += "\n原因: 指定されたインスタンスが見つかりません。";
                        if (!string.IsNullOrEmpty(counterInfo.InstanceName))
                        {
                            errorMsg += $"\nインスタンス名 '{counterInfo.InstanceName}' が存在しない可能性があります。";
                        }
                    }
                    else if (result == 0xC0000BBF) // PDH_CSTATUS_NO_COUNTER
                    {
                        errorMsg += "\n原因: 指定されたカウンターが見つかりません。";
                        errorMsg += $"\nカウンター名 '{counterInfo.CounterName}' が存在しない可能性があります。";
                    }
                    else if (result == PdhApi.PDH_ENTRY_NOT_IN_LOG_FILE)
                    {
                        errorMsg += "\n原因: このカウンターはBLGファイルに記録されていません。";
                    }
                    
                    // SQLServerカウンターの場合は、より寛容なエラーハンドリング
                    if (isSqlServerCounter)
                    {
                        // SQLServerカウンターの場合は、例外を投げずに空のデータを返す
                        progress?.Report($"警告: {errorMsg}");
                        progress?.Report("SQLServerカウンターのため、空のデータセットを返します。");
                        counterInfo.DataPoints = new List<CounterDataPoint>();
                        return counterInfo;
                    }
                    else
                    {
                        throw new Exception(errorMsg);
                    }
                }

                progress?.Report($"カウンター '{fullCounterPath}' をクエリに追加しました");

                // 最初のデータ収集（これによりデータの読み込みが初期化される）
                result = PdhApi.PdhCollectQueryData(query);
                if (result != PdhApi.PDH_NO_MORE_DATA && result != PdhApi.PDH_NO_DATA && result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"初回データ収集に失敗: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
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

        // カウンターパスにマシン名が含まれていない場合は自動で補完
        var fullCounterPath = EnsureMachineNameInPath(counterPath);
        progress?.Report($"時間制約付きでカウンターデータを読み込み中: {fullCounterPath}");
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
                FullPath = fullCounterPath,
                ObjectName = ExtractObjectName(fullCounterPath),
                CounterName = ExtractCounterName(fullCounterPath),
                InstanceName = ExtractInstanceName(fullCounterPath)
            };

            // SQLServerカウンターかどうかを判定
            bool isSqlServerCounter = fullCounterPath.Contains("SQLServer", StringComparison.OrdinalIgnoreCase);

            try
            {
                // 詳細なデバッグ情報を提供
                progress?.Report($"時間制約付きカウンターパス解析: '{counterPath}' -> '{fullCounterPath}'");
                progress?.Report($"オブジェクト名: '{counterInfo.ObjectName}', カウンター名: '{counterInfo.CounterName}', インスタンス名: '{counterInfo.InstanceName}'");
                
                if (isSqlServerCounter)
                {
                    progress?.Report("SQLServerカウンターを検出しました。特別な処理を適用します。");
                }

                // BLGファイルを指定してクエリを開く
                uint result = PdhApi.PdhOpenQuery(_filePath, IntPtr.Zero, out query);
                if (result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"クエリを開けませんでした: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                }

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
                    if (result != PdhApi.ERROR_SUCCESS)
                    {
                        throw new Exception($"時間範囲設定に失敗: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                    }
                    progress?.Report("時間範囲を設定しました");
                }

                // カウンターをクエリに追加
                result = PdhApi.PdhAddCounter(query, fullCounterPath, IntPtr.Zero, out counter);
                if (result != PdhApi.ERROR_SUCCESS)
                {
                    // カウンターの追加に失敗した場合の詳細な診断情報
                    var errorMsg = $"カウンター '{fullCounterPath}' をクエリに追加できませんでした: {PdhApi.GetErrorMessage(result)} (0x{result:X8})";
                    
                    // 特定のエラーコードに対する詳細な説明
                    if (result == PdhApi.PDH_CSTATUS_NO_OBJECT)
                    {
                        errorMsg += "\n原因: 指定されたパフォーマンスオブジェクトが見つかりません。";
                        if (isSqlServerCounter)
                        {
                            errorMsg += "\nSQLServerカウンターの場合、SQLServerサービスが実行されていない可能性があります。";
                        }
                    }
                    else if (result == 0x800007D1) // PDH_CSTATUS_NO_INSTANCE
                    {
                        errorMsg += "\n原因: 指定されたインスタンスが見つかりません。";
                        if (!string.IsNullOrEmpty(counterInfo.InstanceName))
                        {
                            errorMsg += $"\nインスタンス名 '{counterInfo.InstanceName}' が存在しない可能性があります。";
                        }
                    }
                    else if (result == 0xC0000BBF) // PDH_CSTATUS_NO_COUNTER
                    {
                        errorMsg += "\n原因: 指定されたカウンターが見つかりません。";
                        errorMsg += $"\nカウンター名 '{counterInfo.CounterName}' が存在しない可能性があります。";
                    }
                    else if (result == PdhApi.PDH_ENTRY_NOT_IN_LOG_FILE)
                    {
                        errorMsg += "\n原因: このカウンターはBLGファイルに記録されていません。";
                    }
                    
                    // SQLServerカウンターの場合は、より寛容なエラーハンドリング
                    if (isSqlServerCounter)
                    {
                        // SQLServerカウンターの場合は、例外を投げずに空のデータを返す
                        progress?.Report($"警告: {errorMsg}");
                        progress?.Report("SQLServerカウンターのため、空のデータセットを返します。");
                        counterInfo.DataPoints = new List<CounterDataPoint>();
                        return counterInfo;
                    }
                    else
                    {
                        throw new Exception(errorMsg);
                    }
                }

                progress?.Report($"カウンター '{fullCounterPath}' をクエリに追加しました");

                // 最初のデータ収集
                result = PdhApi.PdhCollectQueryData(query);
                if (result != PdhApi.PDH_NO_MORE_DATA && result != PdhApi.PDH_NO_DATA && result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"初回データ収集に失敗: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
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
        // 入力値チェック
        if (string.IsNullOrEmpty(counterPath))
        {
            return string.Empty;
        }

        // \\MachineName\ObjectName(Instance)\CounterName 形式から ObjectName を抽出
        var parts = counterPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
        {
            // parts[0] = MachineName, parts[1] = ObjectName(Instance), parts[2] = CounterName
            var objectPart = parts[1];
            var parenIndex = objectPart.IndexOf('(');
            return parenIndex > 0 ? objectPart[..parenIndex] : objectPart;
        }
        else if (parts.Length >= 2)
        {
            // マシン名なしの場合: \ObjectName(Instance)\CounterName
            var objectPart = parts[0];
            var parenIndex = objectPart.IndexOf('(');
            return parenIndex > 0 ? objectPart[..parenIndex] : objectPart;
        }
        else if (parts.Length == 1)
        {
            // パーツが1つしかない場合、それがオブジェクト名の可能性
            var objectPart = parts[0];
            var parenIndex = objectPart.IndexOf('(');
            return parenIndex > 0 ? objectPart[..parenIndex] : objectPart;
        }
        return string.Empty;
    }

    private static string ExtractCounterName(string counterPath)
    {
        // 入力値チェック
        if (string.IsNullOrEmpty(counterPath))
        {
            return string.Empty;
        }

        // \\MachineName\ObjectName(Instance)\CounterName 形式から CounterName を抽出
        var parts = counterPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 1 ? parts[^1] : string.Empty;
    }

    private static string ExtractInstanceName(string counterPath)
    {
        // 入力値チェック
        if (string.IsNullOrEmpty(counterPath))
        {
            return string.Empty;
        }

        // \\MachineName\ObjectName(Instance)\CounterName 形式から Instance を抽出
        var parts = counterPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        
        string objectPart;
        if (parts.Length >= 3)
        {
            // parts[0] = MachineName, parts[1] = ObjectName(Instance), parts[2] = CounterName
            objectPart = parts[1];
        }
        else if (parts.Length >= 2)
        {
            // マシン名なしの場合: \ObjectName(Instance)\CounterName
            objectPart = parts[0];
        }
        else if (parts.Length == 1)
        {
            // パーツが1つしかない場合、それがオブジェクト名の可能性
            objectPart = parts[0];
        }
        else
        {
            return string.Empty;
        }
        
        var startParen = objectPart.IndexOf('(');
        var endParen = startParen >= 0 ? objectPart.IndexOf(')', startParen) : -1;
        
        // 境界チェックを強化
        if (startParen >= 0 && endParen > startParen && endParen < objectPart.Length)
        {
            var instanceLength = endParen - startParen - 1;
            if (instanceLength > 0)
            {
                return objectPart.Substring(startParen + 1, instanceLength);
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
        
        // BLGファイル内のマシン名を取得
        var machineNames = GetMachineNames();
        string? machineName = machineNames.FirstOrDefault();
        progress?.Report($"マシン名: {machineName ?? "未検出"}");
        
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
                            // マシン名を含むフルパスを生成
                            var path = $"{machineName}\\{objectName}({instanceName})\\{counterName}";
                            allPaths.Add(path);
                            pathsForThisObject++;
                        }
                    }
                    else
                    {
                        // インスタンスなしの場合
                        // マシン名を含むフルパスを生成
                        var path = $"{machineName}\\{objectName}\\{counterName}";
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

    /// <summary>
    /// カウンターパスにマシン名が含まれていない場合は自動で補完
    /// </summary>
    private string EnsureMachineNameInPath(string counterPath)
    {
        // 入力値のチェック
        if (string.IsNullOrEmpty(counterPath))
        {
            return counterPath;
        }

        // 既にマシン名が含まれている場合（\\MachineName\ で始まっている場合）
        if (counterPath.StartsWith("\\\\"))
        {
            return counterPath; // そのまま返す
        }

        // マシン名を取得
        var machineNames = GetMachineNames();
        string? machineName = machineNames.FirstOrDefault();
        
        if (string.IsNullOrEmpty(machineName))
        {
            // マシン名が取得できない場合はそのまま返す
            return counterPath;
        }

        // \ で始まっている場合は先頭の \ を削除（境界チェック強化）
        var pathWithoutLeadingSlash = counterPath;
        if (counterPath.StartsWith("\\") && counterPath.Length > 1)
        {
            pathWithoutLeadingSlash = counterPath.Substring(1);
        }
        else if (counterPath == "\\")
        {
            // 単独の \ の場合は空文字列にする
            pathWithoutLeadingSlash = "";
        }
        
        // マシン名を追加したフルパスを生成
        return $"{machineName}\\{pathWithoutLeadingSlash}";
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
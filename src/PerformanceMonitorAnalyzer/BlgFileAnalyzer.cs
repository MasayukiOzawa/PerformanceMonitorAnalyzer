using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// BLGファイル解析を行うクラス（PDH APIを使用）
/// </summary>
public class BlgFileAnalyzer : IDisposable
{
    private IntPtr _dataSource = IntPtr.Zero;
    private IntPtr _query = IntPtr.Zero;
    private bool _disposed = false;
    private string _filePath = string.Empty;
    private readonly Dictionary<string, IntPtr> _counterQueries = new();

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

        progress?.Report("BLGファイルを開いています...");
        
        // ファイルパスを保存（カウンター読み込み時に使用）
        _filePath = filePath;

        return await Task.Run(() =>
        {
            try
            {
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

                progress?.Report($"BLGファイルが正常に開かれました（ログタイプ: {logType}）");
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
                foreach (var obj in objects.Take(10))
                {
                    progress?.Report($"見つかったオブジェクト: {obj}");
                }
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
    /// BLGファイル内のマシン名を取得
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
                            var rawData = Marshal.PtrToStringUni(buffer);
                            if (!string.IsNullOrEmpty(rawData))
                            {
                                var machineNames = rawData.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                                machines.AddRange(machineNames);
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
        catch
        {
            // マシン名の取得に失敗した場合はnullを使用
        }
        
        return machines;
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
                    // バッファサイズを保存（呼び出し後に変更される可能性があるため）
                    uint originalCounterBufferSize = counterBufferSize;
                    uint originalInstanceBufferSize = instanceBufferSize;

                    // char単位でバッファを確保（Unicode文字列のため）
                    IntPtr counterBuffer = IntPtr.Zero;
                    IntPtr instanceBuffer = IntPtr.Zero;

                    try
                    {
                        if (counterBufferSize > 0)
                        {
                            counterBuffer = Marshal.AllocHGlobal((int)counterBufferSize * 2); // char = 2 bytes
                            Marshal.Copy(new byte[counterBufferSize * 2], 0, counterBuffer, (int)counterBufferSize * 2);
                        }

                        if (instanceBufferSize > 0)
                        {
                            instanceBuffer = Marshal.AllocHGlobal((int)instanceBufferSize * 2); // char = 2 bytes
                            Marshal.Copy(new byte[instanceBufferSize * 2], 0, instanceBuffer, (int)instanceBufferSize * 2);
                        }

                        result = PdhApi.PdhEnumObjectItemsH(
                            _dataSource,
                            machineName,
                            objectName,
                            counterBuffer,
                            ref counterBufferSize,
                            instanceBuffer,
                            ref instanceBufferSize,
                            100,
                            0);
                            
                        progress?.Report($"API呼び出し後のバッファサイズ - カウンター: {counterBufferSize}/{originalCounterBufferSize}, インスタンス: {instanceBufferSize}/{originalInstanceBufferSize}");

                        progress?.Report($"カウンター・インスタンス列挙結果: {PdhApi.GetErrorMessage(result)}");

                        if (result == PdhApi.ERROR_SUCCESS)
                        {
                            // カウンター名を解析（Unicode文字列から直接変換）
                            if (counterBuffer != IntPtr.Zero && counterBufferSize > 0)
                            {
                                var counterList = Marshal.PtrToStringUni(counterBuffer, (int)counterBufferSize - 1); // 最後のnull文字は除外
                                if (counterList != null)
                                {
                                    progress?.Report($"生のカウンターバッファ長: {counterList.Length}, 要求サイズ: {counterBufferSize}");
                                    
                                    // null文字位置の詳細調査
                                    int nullCount = 0;
                                    var counterNames = new List<string>();
                                    var currentCounter = new StringBuilder();
                                    
                                    for (int i = 0; i < counterList.Length; i++)
                                    {
                                        if (counterList[i] == '\0')
                                        {
                                            nullCount++;
                                            if (currentCounter.Length > 0)
                                            {
                                                counterNames.Add(currentCounter.ToString());
                                                currentCounter.Clear();
                                            }
                                        }
                                        else
                                        {
                                            currentCounter.Append(counterList[i]);
                                        }
                                    }
                                    
                                    // 最後のカウンター名を追加（null終端がない場合）
                                    if (currentCounter.Length > 0)
                                    {
                                        counterNames.Add(currentCounter.ToString());
                                    }
                                    
                                    progress?.Report($"合計null文字数: {nullCount}, 抽出されたカウンター数: {counterNames.Count}");
                                    
                                    // 最初の10個のカウンター名を表示
                                    for (int i = 0; i < Math.Min(10, counterNames.Count); i++)
                                    {
                                        progress?.Report($"カウンター[{i}]: '{counterNames[i]}'");
                                    }
                                    
                                    counters.AddRange(counterNames);
                                }
                            }

                            // インスタンス名を解析
                            if (instanceBuffer != IntPtr.Zero && instanceBufferSize > 0)
                            {
                                var instanceList = Marshal.PtrToStringUni(instanceBuffer, (int)instanceBufferSize - 1); // 最後のnull文字は除外
                                if (instanceList != null)
                                {
                                    progress?.Report($"生のインスタンスバッファ長: {instanceList.Length}, 要求サイズ: {instanceBufferSize}");
                                    
                                    var instanceNames = new List<string>();
                                    var currentInstance = new StringBuilder();
                                    
                                    for (int i = 0; i < instanceList.Length; i++)
                                    {
                                        if (instanceList[i] == '\0')
                                        {
                                            if (currentInstance.Length > 0)
                                            {
                                                instanceNames.Add(currentInstance.ToString());
                                                currentInstance.Clear();
                                            }
                                        }
                                        else
                                        {
                                            currentInstance.Append(instanceList[i]);
                                        }
                                    }
                                    
                                    // 最後のインスタンス名を追加（null終端がない場合）
                                    if (currentInstance.Length > 0)
                                    {
                                        instanceNames.Add(currentInstance.ToString());
                                    }
                                    
                                    progress?.Report($"抽出されたインスタンス数: {instanceNames.Count}");
                                    
                                    // 最初の10個のインスタンス名を表示
                                    for (int i = 0; i < Math.Min(10, instanceNames.Count); i++)
                                    {
                                        progress?.Report($"インスタンス[{i}]: '{instanceNames[i]}'");
                                    }
                                    
                                    instances.AddRange(instanceNames);
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
    /// BLGファイルから実際に利用可能なカウンターパスを取得
    /// </summary>
    public async Task<List<string>> GetAvailableCounterPathsAsync(IProgress<string>? progress = null)
    {
        if (_dataSource == IntPtr.Zero)
        {
            throw new InvalidOperationException("BLGファイルが開かれていません。");
        }

        progress?.Report("BLGファイルから利用可能なカウンターパスを取得中...");

        return await Task.Run(() =>
        {
            try
            {
                var counterPaths = new List<string>();
                uint bufferSize = 0;

                // まずバッファサイズを取得
                uint result = PdhApi.PdhEnumLogFileCounters(_dataSource, IntPtr.Zero, ref bufferSize);

                if (result == PdhApi.PDH_MORE_DATA && bufferSize > 0)
                {
                    progress?.Report($"必要なバッファサイズ: {bufferSize} バイト");

                    // バッファを確保してカウンターパスを取得
                    IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
                    try
                    {
                        result = PdhApi.PdhEnumLogFileCounters(_dataSource, buffer, ref bufferSize);

                        if (result == PdhApi.ERROR_SUCCESS)
                        {
                            // null区切りの文字列リストを解析
                            string counterList = Marshal.PtrToStringUni(buffer) ?? "";
                            
                            // null区切りの文字列を分割
                            var paths = counterList.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                            
                            foreach (var path in paths)
                            {
                                if (!string.IsNullOrWhiteSpace(path))
                                {
                                    counterPaths.Add(path);
                                }
                            }

                            progress?.Report($"BLGファイルから {counterPaths.Count} 個のカウンターパスを取得しました");
                        }
                        else
                        {
                            throw new Exception($"カウンターパスの取得に失敗: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
                else if (result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"カウンターパスのバッファサイズ取得に失敗: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                }

                return counterPaths;
            }
            catch (Exception ex)
            {
                progress?.Report($"カウンターパス取得エラー: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// 全てのカウンターパスを生成（従来の方法 - 参考用）
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

    /// <summary>
    /// 指定されたカウンターのデータを読み込み
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
            IntPtr counterQuery = IntPtr.Zero;
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
                // このカウンター専用のクエリを作成
                progress?.Report($"カウンター専用クエリを作成中: {counterPath}");
                uint result = PdhApi.PdhOpenQuery(_filePath, IntPtr.Zero, out counterQuery);
                if (result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"カウンター専用クエリの作成に失敗: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                }

                // カウンターをクエリに追加
                progress?.Report($"カウンターを専用クエリに追加中: {counterPath}");
                result = PdhApi.PdhAddCounter(counterQuery, counterPath, IntPtr.Zero, out counter);
                if (result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"カウンター追加に失敗: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                }

                progress?.Report($"カウンター追加成功: {counterPath}");

                // BLGファイルの場合は履歴データを取得する必要がある
                // PdhSetQueryTimeRangeを使用してBLGファイル全体のデータを読み込む
                result = PdhApi.PdhSetQueryTimeRange(counterQuery, IntPtr.Zero);
                if (result != PdhApi.ERROR_SUCCESS)
                {
                    progress?.Report($"タイムレンジ設定警告: {PdhApi.GetErrorMessage(result)}");
                }
                // BLGファイルからすべてのデータを読み込むためのループ
                var allDataPoints = new List<CounterDataPoint>();
                bool hasMoreData = true;
                int maxIterations = 10000; // 無限ループ防止
                int currentIteration = 0;

                while (hasMoreData && currentIteration < maxIterations)
                {
                    currentIteration++;
                    
                    // データを収集（専用クエリを使用）
                    result = PdhApi.PdhCollectQueryData(counterQuery);
                    
                    if (result == PdhApi.ERROR_SUCCESS)
                    {
                        // フォーマット済み値を取得
                        result = PdhApi.PdhGetFormattedCounterValue(
                            counter,
                            PdhApi.PDH_FMT_DOUBLE | PdhApi.PDH_FMT_NOCAP100,
                            out uint valueType,
                            out PdhApi.PDH_FMT_COUNTERVALUE value);

                        if (result == PdhApi.ERROR_SUCCESS || result == PdhApi.PDH_CSTATUS_VALID_DATA)
                        {
                            // タイムスタンプを取得するために生の値も取得
                            uint result2 = PdhApi.PdhGetRawCounterValue(counter, out uint valueType2, out PdhApi.PDH_RAW_COUNTER rawValue);
                            
                            DateTime timestamp = DateTime.Now;
                            if (result2 == PdhApi.ERROR_SUCCESS)
                            {
                                try
                                {
                                    // FileTimeからDateTimeに変換
                                    long fileTime = (long)rawValue.TimeStamp;
                                    if (fileTime > 0)
                                    {
                                        timestamp = DateTime.FromFileTime(fileTime);
                                    }
                                }
                                catch
                                {
                                    // FileTime変換に失敗した場合は現在時刻を使用
                                    timestamp = DateTime.Now.AddMinutes(-currentIteration);
                                }
                            }

                            var dataPoint = new CounterDataPoint
                            {
                                Timestamp = timestamp,
                                Value = value.doubleValue,
                                Status = value.CStatus
                            };
                            
                            allDataPoints.Add(dataPoint);
                            
                            if (currentIteration % 100 == 0)
                            {
                                progress?.Report($"データポイント読み込み中: {allDataPoints.Count}個");
                            }
                        }
                        else if (result == PdhApi.PDH_NO_MORE_DATA)
                        {
                            hasMoreData = false;
                            break;
                        }
                        else
                        {
                            progress?.Report($"データ取得警告: {PdhApi.GetErrorMessage(result)}");
                        }
                    }
                    else if (result == PdhApi.PDH_NO_MORE_DATA)
                    {
                        hasMoreData = false;
                        break;
                    }
                    else
                    {
                        progress?.Report($"データ収集エラー: {PdhApi.GetErrorMessage(result)}");
                        hasMoreData = false;
                    }
                }

                // 代替手法: PdhGetRawCounterArrayを試行
                if (allDataPoints.Count == 0)
                {
                    progress?.Report("代替手法でPdhGetRawCounterArrayを試行中...");
                    
                    uint bufferSize = 0;
                    uint itemCount = 0;

                    // まずバッファサイズを取得
                    result = PdhApi.PdhGetRawCounterArray(counter, ref bufferSize, out itemCount, IntPtr.Zero);
                    
                    if (result == PdhApi.PDH_MORE_DATA && bufferSize > 0)
                    {
                        // バッファを確保してデータを取得
                        IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
                        try
                        {
                            result = PdhApi.PdhGetRawCounterArray(counter, ref bufferSize, out itemCount, buffer);
                            
                            if (result == PdhApi.ERROR_SUCCESS && itemCount > 0)
                            {
                                progress?.Report($"履歴データ項目数: {itemCount}");
                                
                                // PDH_RAW_COUNTER_ITEM構造体のサイズ
                                int itemSize = Marshal.SizeOf<PdhApi.PDH_RAW_COUNTER_ITEM>();
                                
                                for (uint i = 0; i < itemCount && i < 1000; i++) // 最大1000項目に制限
                                {
                                    IntPtr itemPtr = IntPtr.Add(buffer, (int)(i * itemSize));
                                    var rawItem = Marshal.PtrToStructure<PdhApi.PDH_RAW_COUNTER_ITEM>(itemPtr);
                                    
                                    // FileTimeをDateTimeに変換
                                    DateTime timestamp;
                                    try
                                    {
                                        timestamp = DateTime.FromFileTime(rawItem.TimeStamp.dwLowDateTime | 
                                                                        ((long)rawItem.TimeStamp.dwHighDateTime << 32));
                                    }
                                    catch
                                    {
                                        // FileTime変換に失敗した場合のフォールバック
                                        timestamp = DateTime.Now.AddMinutes(-(int)i);
                                    }

                                    // 生の値をフォーマット済み値に変換
                                    var dataPoint = new CounterDataPoint
                                    {
                                        Timestamp = timestamp,
                                        Value = ConvertRawValueToDouble(rawItem.RawValue),
                                        Status = rawItem.RawValue.CStatus
                                    };
                                    
                                    allDataPoints.Add(dataPoint);
                                }
                            }
                            else
                            {
                                progress?.Report($"履歴データ取得エラー: {PdhApi.GetErrorMessage(result)}");
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(buffer);
                        }
                    }
                }

                // 最終手法: 単一データポイントの取得
                if (allDataPoints.Count == 0)
                {
                    progress?.Report("単一データポイント取得を試行中...");
                    
                    result = PdhApi.PdhCollectQueryData(_query);
                    if (result == PdhApi.ERROR_SUCCESS)
                    {
                        result = PdhApi.PdhGetFormattedCounterValue(
                            counter,
                            PdhApi.PDH_FMT_DOUBLE | PdhApi.PDH_FMT_NOCAP100,
                            out uint valueType,
                            out PdhApi.PDH_FMT_COUNTERVALUE value);

                        if (result == PdhApi.ERROR_SUCCESS || result == PdhApi.PDH_CSTATUS_VALID_DATA)
                        {
                            var dataPoint = new CounterDataPoint
                            {
                                Timestamp = DateTime.Now,
                                Value = value.doubleValue,
                                Status = value.CStatus
                            };
                            allDataPoints.Add(dataPoint);
                        }
                    }
                }

                counterInfo.DataPoints = allDataPoints;
                progress?.Report($"取得したデータポイント数: {counterInfo.DataPoints.Count}");
                
                // データポイントをタイムスタンプ順にソート
                counterInfo.DataPoints = counterInfo.DataPoints
                    .OrderBy(dp => dp.Timestamp)
                    .ToList();
                
                return counterInfo;
            }
            catch (Exception ex)
            {
                progress?.Report($"カウンターデータ読み込みエラー: {ex.Message}");
                throw;
            }
            finally
            {
                // カウンターを削除
                if (counter != IntPtr.Zero)
                {
                    PdhApi.PdhRemoveCounter(counter);
                }
                
                // 専用クエリをクローズ
                if (counterQuery != IntPtr.Zero)
                {
                    PdhApi.PdhCloseQuery(counterQuery);
                }
            }
        });
    }

    /// <summary>
    /// 生の値をdoubleに変換
    /// </summary>
    private static double ConvertRawValueToDouble(PdhApi.PDH_RAW_COUNTER rawValue)
    {
        // 生の値のタイプに応じて適切な変換を行う
        // 多くの場合、FirstValueまたはSecondValueが実際の値
        if (rawValue.CStatus == PdhApi.PDH_CSTATUS_VALID_DATA)
        {
            // 64bitの値として解釈
            long value64 = rawValue.FirstValue | ((long)rawValue.SecondValue << 32);
            return (double)value64;
        }
        
        // ステータスが無効な場合は0を返す
        return 0.0;
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
    /// PdhEnumObjectsH（BLGファイル専用API）を使用してオブジェクトを列挙
    /// </summary>
    private List<string> TryEnumerateObjectsH(string? machineName, IProgress<string>? progress)
    {
        var objects = new List<string>();

        try
        {
            uint bufferSize = 0;

            // PdhEnumObjectsH（BLGファイル専用API）を使用してバッファサイズを取得
            uint result = PdhApi.PdhEnumObjectsH(
                _dataSource,
                machineName,
                IntPtr.Zero,
                ref bufferSize,
                PdhApi.PERF_DETAIL_WIZARD,
                false);

            progress?.Report($"PdhEnumObjectsH バッファサイズ取得結果: {PdhApi.GetErrorMessage(result)}, サイズ: {bufferSize}");

            if (result == PdhApi.PDH_MORE_DATA && bufferSize > 0)
            {
                // Unicodeバッファを確保（文字数単位でサイズ指定、1文字=2バイト）
                IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize * 2);

                try
                {
                    // バッファをゼロで初期化
                    for (int i = 0; i < bufferSize * 2; i++)
                    {
                        Marshal.WriteByte(buffer, i, 0);
                    }

                    result = PdhApi.PdhEnumObjectsH(
                        _dataSource,
                        machineName,
                        buffer,
                        ref bufferSize,
                        PdhApi.PERF_DETAIL_WIZARD,
                        false);

                    progress?.Report($"PdhEnumObjectsH オブジェクト列挙結果: {PdhApi.GetErrorMessage(result)}");

                    if (result == PdhApi.ERROR_SUCCESS)
                    {
                        // 手動でUnicode文字列を解析（複数のnull終端文字列を処理）
                        var objectNames = new List<string>();
                        var currentString = new StringBuilder();
                        
                        // 文字単位で処理（bufferSizeは文字数）
                        for (int i = 0; i < bufferSize; i++)
                        {
                            // 2バイトずつ読み取ってUnicode文字を取得
                            char ch = (char)Marshal.ReadInt16(buffer, i * 2);
                            
                            if (ch == '\0')
                            {
                                // null文字の場合、現在の文字列を完了
                                if (currentString.Length > 0)
                                {
                                    objectNames.Add(currentString.ToString());
                                    currentString.Clear();
                                }
                                // 連続するnull文字は文字列リストの終端を示す
                                else if (objectNames.Count > 0)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                // 通常の文字を追加
                                currentString.Append(ch);
                            }
                        }
                        
                        // 最後の文字列が残っている場合は追加
                        if (currentString.Length > 0)
                        {
                            objectNames.Add(currentString.ToString());
                        }

                        progress?.Report($"PdhEnumObjectsH 分割後のオブジェクト数: {objectNames.Count}");

                        for (int i = 0; i < Math.Min(objectNames.Count, 10); i++)
                        {
                            progress?.Report($"PdhEnumObjectsH オブジェクト[{i}]: '{objectNames[i]}'");
                        }

                        objects.AddRange(objectNames);
                    }
                    else
                    {
                        progress?.Report($"PdhEnumObjectsH オブジェクト列挙に失敗: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            else if (result != PdhApi.ERROR_SUCCESS)
            {
                progress?.Report($"PdhEnumObjectsH バッファサイズ取得に失敗: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"PdhEnumObjectsH 例外が発生: {ex.Message}");
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

            // PdhEnumObjectsA（ANSI版）を使用してバッファサイズを取得
            uint result = PdhApi.PdhEnumObjectsA(
                machineName,
                IntPtr.Zero,
                ref bufferSize,
                PdhApi.PERF_DETAIL_WIZARD,
                false);

            progress?.Report($"PdhEnumObjectsA バッファサイズ取得結果: {PdhApi.GetErrorMessage(result)}, サイズ: {bufferSize}");

            if (result == PdhApi.PDH_MORE_DATA && bufferSize > 0)
            {
                // ANSIバッファを確保（1バイト文字）
                IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);

                try
                {
                    result = PdhApi.PdhEnumObjectsA(
                        machineName,
                        buffer,
                        ref bufferSize,
                        PdhApi.PERF_DETAIL_WIZARD,
                        false);

                    progress?.Report($"PdhEnumObjectsA オブジェクト列挙結果: {PdhApi.GetErrorMessage(result)}");

                    if (result == PdhApi.ERROR_SUCCESS)
                    {
                        // ANSI文字列として解析
                        var rawData = Marshal.PtrToStringAnsi(buffer);
                        progress?.Report($"PdhEnumObjectsA 生のオブジェクトバッファ長: {rawData?.Length ?? 0}, 最初の200文字: {rawData?[..Math.Min(200, rawData?.Length ?? 0)] ?? "null"}");

                        if (!string.IsNullOrEmpty(rawData))
                        {
                            // null文字で分割して複数のオブジェクト名を取得
                            var objectNames = rawData.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                            progress?.Report($"PdhEnumObjectsA 分割後のオブジェクト数: {objectNames.Length}");

                            for (int i = 0; i < Math.Min(objectNames.Length, 10); i++)
                            {
                                progress?.Report($"PdhEnumObjectsA オブジェクト[{i}]: '{objectNames[i]}'");
                            }

                            objects.AddRange(objectNames);
                        }
                    }
                    else
                    {
                        progress?.Report($"PdhEnumObjectsA オブジェクト列挙に失敗: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            else if (result != PdhApi.ERROR_SUCCESS)
            {
                progress?.Report($"PdhEnumObjectsA バッファサイズ取得に失敗: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"PdhEnumObjectsA 例外が発生: {ex.Message}");
        }

        return objects;
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
            // まず個別のカウンタークエリをクリーンアップ
            foreach (var query in _counterQueries.Values)
            {
                if (query != IntPtr.Zero)
                {
                    PdhApi.PdhCloseQuery(query);
                }
            }
            _counterQueries.Clear();

            // クエリを先に閉じる
            if (_query != IntPtr.Zero)
            {
                PdhApi.PdhCloseQuery(_query);
                _query = IntPtr.Zero;
            }

            // その後データソースを閉じる
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
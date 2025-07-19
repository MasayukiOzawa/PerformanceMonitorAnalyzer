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

        return await Task.Run(() =>
        {
            try
            {
                // BLGファイル専用のデータソースとして開く
                progress?.Report("BLGファイルをデータソースとして開いています...");
                uint result = PdhApi.PdhBindInputDataSource(out _dataSource, filePath);
                
                if (result == PdhApi.ERROR_SUCCESS)
                {
                    progress?.Report("BLGファイルが正常にデータソースとして開かれました");
                    return true;
                }
                
                // PdhBindInputDataSourceが失敗した場合のフォールバック
                progress?.Report($"PdhBindInputDataSource失敗 ({PdhApi.GetErrorMessage(result)})、代替方法を試行中...");
                
                // まずクエリを開く
                result = PdhApi.PdhOpenQuery(null, IntPtr.Zero, out _query);
                if (result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"クエリを開けませんでした: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                }
                
                // BLGファイルをログファイルとして開く
                result = PdhApi.PdhOpenLog(
                    filePath,
                    PdhApi.GENERIC_READ,
                    out uint logType,
                    _query,
                    0,
                    null,
                    out _dataSource);

                if (result != PdhApi.ERROR_SUCCESS)
                {
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
    /// BLGファイル内のマシン名を取得（公開メソッド）
    /// </summary>
    public List<string> GetMachineNamesFromBlg()
    {
        return GetMachineNames();
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

    /// <summary>
    /// 指定されたカウンターのデータを読み込み
    /// </summary>
    public async Task<CounterInfo> LoadCounterDataAsync(string counterPath, IProgress<string>? progress = null)
    {
        if (_dataSource == IntPtr.Zero)
        {
            throw new InvalidOperationException("BLGファイルが開かれていません。");
        }

        progress?.Report($"カウンターデータを読み込み中: {counterPath}");

        return await Task.Run(() =>
        {
            IntPtr tempQuery = IntPtr.Zero;
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
                // BLGファイル専用の一時クエリを作成
                uint result = PdhApi.PdhOpenQuery(_dataSource, IntPtr.Zero, out tempQuery);
                if (result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"一時クエリの作成に失敗: {PdhApi.GetErrorMessage(result)}");
                }

                // カウンターをクエリに追加
                result = PdhApi.PdhAddCounter(tempQuery, counterPath, IntPtr.Zero, out counter);
                if (result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"カウンター追加に失敗: {PdhApi.GetErrorMessage(result)}");
                }

                progress?.Report($"カウンター '{counterPath}' を追加しました");

                // BLGファイル内の全サンプルを読み込み
                var dataPoints = new List<CounterDataPoint>();
                bool hasMoreData = true;
                int sampleCount = 0;

                while (hasMoreData && sampleCount < 10000) // 安全のため最大10000サンプル
                {
                    result = PdhApi.PdhCollectQueryData(tempQuery);
                    
                    if (result == PdhApi.ERROR_SUCCESS)
                    {
                        // フォーマットされた値を取得
                        result = PdhApi.PdhGetFormattedCounterValue(
                            counter,
                            PdhApi.PDH_FMT_DOUBLE,
                            out uint valueType,
                            out PdhApi.PDH_FMT_COUNTERVALUE value);

                        if (result == PdhApi.ERROR_SUCCESS || result == PdhApi.PDH_CSTATUS_VALID_DATA)
                        {
                            // タイムスタンプを取得（現在は仮の値）
                            var timestamp = DateTime.Now.AddSeconds(-sampleCount);
                            
                            var dataPoint = new CounterDataPoint
                            {
                                Timestamp = timestamp,
                                Value = value.doubleValue,
                                Status = value.CStatus
                            };
                            dataPoints.Add(dataPoint);
                            sampleCount++;
                        }
                        else if (result == PdhApi.PDH_NO_MORE_DATA)
                        {
                            hasMoreData = false;
                        }
                        else
                        {
                            progress?.Report($"値取得警告: {PdhApi.GetErrorMessage(result)}");
                            break;
                        }
                    }
                    else if (result == PdhApi.PDH_NO_MORE_DATA)
                    {
                        hasMoreData = false;
                    }
                    else
                    {
                        progress?.Report($"データ収集エラー: {PdhApi.GetErrorMessage(result)}");
                        break;
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
                if (tempQuery != IntPtr.Zero)
                {
                    PdhApi.PdhCloseQuery(tempQuery);
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
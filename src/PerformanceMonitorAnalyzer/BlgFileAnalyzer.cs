using System.Text;
using System.Runtime.InteropServices;
using System.IO;

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
                progress?.Report("BLGファイルをデータソースとして開いています...");
                
                // BLGファイルをデータソースとして開く（hQueryはNULLを指定）
                uint result = PdhApi.PdhOpenLog(
                    filePath,
                    PdhApi.GENERIC_READ,
                    out uint logType,
                    IntPtr.Zero,  // hQueryはNULLを指定
                    0,
                    string.Empty,
                    out _dataSource);

                if (result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"BLGファイルを開けませんでした: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                }

                progress?.Report($"BLGファイルが正常に開かれました（ログタイプ: {logType}）");

                // データソースが開かれた後、クエリを作成
                result = PdhApi.PdhOpenQuery(null, IntPtr.Zero, out _query);
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

                uint bufferSize = 0;

                // まずバッファサイズを取得
                uint result = PdhApi.PdhEnumObjectsH(
                    _dataSource,
                    machineName,
                    null,
                    ref bufferSize,
                    100, // PERF_DETAIL_NOVICE
                    false);

                progress?.Report($"バッファサイズ取得結果: {PdhApi.GetErrorMessage(result)}, サイズ: {bufferSize}");

                if (result == PdhApi.PDH_MORE_DATA && bufferSize > 0)
                {
                    var buffer = new StringBuilder((int)bufferSize);
                    
                    result = PdhApi.PdhEnumObjectsH(
                        _dataSource,
                        machineName,
                        buffer,
                        ref bufferSize,
                        100,
                        false);

                    progress?.Report($"オブジェクト列挙結果: {PdhApi.GetErrorMessage(result)}");

                    if (result == PdhApi.ERROR_SUCCESS)
                    {
                        // NULL区切りの文字列を解析
                        var objectList = buffer.ToString();
                        progress?.Report($"取得したオブジェクトリスト（最初の100文字）: {objectList[..Math.Min(100, objectList.Length)]}");
                        
                        var objectNames = objectList.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                        objects.AddRange(objectNames);
                    }
                    else
                    {
                        throw new Exception($"オブジェクト列挙に失敗しました: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
                    }
                }
                else if (result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"オブジェクト列挙のバッファサイズ取得に失敗しました: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
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
                uint result = PdhApi.PdhEnumMachinesH(_dataSource, null, ref bufferSize);
                
                if (result == PdhApi.PDH_MORE_DATA && bufferSize > 0)
                {
                    var buffer = new StringBuilder((int)bufferSize);
                    result = PdhApi.PdhEnumMachinesH(_dataSource, buffer, ref bufferSize);
                    
                    if (result == PdhApi.ERROR_SUCCESS)
                    {
                        var machineList = buffer.ToString();
                        var machineNames = machineList.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                        machines.AddRange(machineNames);
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
                uint result = PdhApi.PdhEnumObjectItemsH(
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

                if (result == PdhApi.PDH_MORE_DATA)
                {
                    StringBuilder? counterBuffer = null;
                    StringBuilder? instanceBuffer = null;

                    if (counterBufferSize > 0)
                    {
                        counterBuffer = new StringBuilder((int)counterBufferSize);
                    }

                    if (instanceBufferSize > 0)
                    {
                        instanceBuffer = new StringBuilder((int)instanceBufferSize);
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

                    progress?.Report($"カウンター・インスタンス列挙結果: {PdhApi.GetErrorMessage(result)}");

                    if (result == PdhApi.ERROR_SUCCESS)
                    {
                        // カウンター名を解析
                        if (counterBuffer != null)
                        {
                            var counterList = counterBuffer.ToString();
                            var counterNames = counterList.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                            counters.AddRange(counterNames);
                        }

                        // インスタンス名を解析
                        if (instanceBuffer != null)
                        {
                            var instanceList = instanceBuffer.ToString();
                            var instanceNames = instanceList.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                            instances.AddRange(instanceNames);
                        }
                    }
                    else
                    {
                        throw new Exception($"カウンター列挙に失敗しました: {PdhApi.GetErrorMessage(result)} (0x{result:X8})");
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

        foreach (var objectName in objects)
        {
            try
            {
                var (counters, instances) = await EnumerateCountersAndInstancesAsync(objectName, progress);

                foreach (var counterName in counters)
                {
                    if (instances.Count > 0)
                    {
                        // インスタンスありの場合
                        foreach (var instanceName in instances)
                        {
                            var path = $"\\{objectName}({instanceName})\\{counterName}";
                            allPaths.Add(path);
                        }
                    }
                    else
                    {
                        // インスタンスなしの場合
                        var path = $"\\{objectName}\\{counterName}";
                        allPaths.Add(path);
                    }
                }
            }
            catch (Exception ex)
            {
                // 個別のオブジェクトでエラーが発生しても続行
                progress?.Report($"警告: {objectName} の処理でエラー: {ex.Message}");
                continue;
            }
        }

        progress?.Report($"合計 {allPaths.Count} 個のカウンターパスを生成しました");
        return allPaths;
    }

    /// <summary>
    /// 指定されたカウンターのデータを読み込み
    /// </summary>
    public async Task<CounterInfo> LoadCounterDataAsync(string counterPath, IProgress<string>? progress = null)
    {
        if (_query == IntPtr.Zero)
        {
            throw new InvalidOperationException("クエリが開かれていません。");
        }

        progress?.Report($"カウンターデータを読み込み中: {counterPath}");

        return await Task.Run(() =>
        {
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
                // カウンターをクエリに追加
                uint result = PdhApi.PdhAddCounter(_query, counterPath, IntPtr.Zero, out counter);
                if (result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"カウンター追加に失敗: {PdhApi.GetErrorMessage(result)}");
                }

                // データを収集
                result = PdhApi.PdhCollectQueryData(_query);
                if (result != PdhApi.ERROR_SUCCESS)
                {
                    throw new Exception($"データ収集に失敗: {PdhApi.GetErrorMessage(result)}");
                }

                // フォーマットされた値を取得
                result = PdhApi.PdhGetFormattedCounterValue(
                    counter,
                    PdhApi.PDH_FMT_DOUBLE,
                    out uint valueType,
                    out PdhApi.PDH_FMT_COUNTERVALUE value);

                if (result == PdhApi.ERROR_SUCCESS || result == PdhApi.PDH_CSTATUS_VALID_DATA)
                {
                    var dataPoint = new CounterDataPoint
                    {
                        Timestamp = DateTime.Now, // 実際の実装では適切なタイムスタンプを設定
                        Value = value.doubleValue,
                        Status = value.CStatus
                    };
                    counterInfo.DataPoints.Add(dataPoint);
                }
                else
                {
                    progress?.Report($"値取得警告: {PdhApi.GetErrorMessage(result)}");
                }

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
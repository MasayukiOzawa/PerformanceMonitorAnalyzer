using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// relog.exe を使用してBLGファイルをCSVに変換し、データを読み取るクラス
/// </summary>
public class RelogCsvAnalyzer : IDisposable
{
    private string? _csvFilePath;
    private string? _originalBlgPath;
    private bool _disposed = false;

    public class BlgTimeRange
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public string FormattedDuration => $"{Duration.TotalHours:F1}時間 ({Duration.TotalMinutes:F0}分)";
    }

    public class CounterData
    {
        public string CounterPath { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
        public string CounterName { get; set; } = string.Empty;
        public string InstanceName { get; set; } = string.Empty;
        public List<CounterDataPoint> DataPoints { get; set; } = new();
    }

    public class CounterDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string RawValue { get; set; } = string.Empty;
        public bool IsValid { get; set; } = true;
    }

    /// <summary>
    /// BLGファイルの時間範囲情報を取得
    /// </summary>
    public async Task<BlgTimeRange?> GetBlgTimeRangeAsync(string blgFilePath, IProgress<string>? progress = null)
    {
        if (!File.Exists(blgFilePath))
        {
            throw new FileNotFoundException($"BLGファイルが見つかりません: {blgFilePath}");
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("relog.exe はWindows環境でのみ利用可能です。");
        }

        progress?.Report("BLGファイルの時間範囲を取得中...");

        return await Task.Run(() =>
        {
            try
            {
                // relog.exe -q でファイル情報を取得
                var arguments = $"\"{blgFilePath}\" -q";
                
                progress?.Report($"実行コマンド: relog.exe {arguments}");

                var processInfo = new ProcessStartInfo
                {
                    FileName = "relog.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = processInfo };
                
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                };
                
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                var completed = process.WaitForExit(TimeSpan.FromMinutes(2));
                
                if (!completed)
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException("relog.exe の実行がタイムアウトしました（2分）");
                }

                var exitCode = process.ExitCode;
                var output = outputBuilder.ToString();
                var error = errorBuilder.ToString();

                if (exitCode != 0)
                {
                    throw new Exception($"relog.exe が失敗しました。終了コード: {exitCode}\nエラー: {error}");
                }

                // 出力から時間範囲を解析
                return ParseBlgTimeRangeFromOutput(output, progress);
            }
            catch (Exception ex)
            {
                progress?.Report($"時間範囲取得エラー: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// relog.exe -q の出力から時間範囲を解析
    /// </summary>
    private BlgTimeRange? ParseBlgTimeRangeFromOutput(string output, IProgress<string>? progress = null)
    {
        try
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            DateTime? startTime = null;
            DateTime? endTime = null;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // 開始時刻を検索 (Begin:, Start:, または類似パターン)
                if ((trimmedLine.StartsWith("Begin:", StringComparison.OrdinalIgnoreCase) ||
                     trimmedLine.StartsWith("Start:", StringComparison.OrdinalIgnoreCase)) && 
                    !startTime.HasValue)
                {
                    var timeString = trimmedLine.Substring(trimmedLine.IndexOf(':') + 1).Trim();
                    if (DateTime.TryParse(timeString, out var parsed))
                    {
                        startTime = parsed;
                        progress?.Report($"開始時刻を検出: {startTime}");
                    }
                }
                
                // 終了時刻を検索 (End:, または類似パターン)
                if (trimmedLine.StartsWith("End:", StringComparison.OrdinalIgnoreCase) && 
                    !endTime.HasValue)
                {
                    var timeString = trimmedLine.Substring(trimmedLine.IndexOf(':') + 1).Trim();
                    if (DateTime.TryParse(timeString, out var parsed))
                    {
                        endTime = parsed;
                        progress?.Report($"終了時刻を検出: {endTime}");
                    }
                }
            }

            if (startTime.HasValue && endTime.HasValue)
            {
                var timeRange = new BlgTimeRange
                {
                    StartTime = startTime.Value,
                    EndTime = endTime.Value
                };
                
                progress?.Report($"時間範囲: {timeRange.StartTime:yyyy/MM/dd HH:mm:ss} ～ {timeRange.EndTime:yyyy/MM/dd HH:mm:ss} ({timeRange.FormattedDuration})");
                return timeRange;
            }
            else
            {
                progress?.Report("relog.exe の出力から時間範囲情報を取得できませんでした。");
                progress?.Report($"出力内容:\n{output}");
                return null;
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"時間範囲解析エラー: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// relog.exe を使用してBLGファイルをCSVに変換（時間範囲指定対応）
    /// </summary>
    public async Task<bool> ConvertBlgToCsvAsync(string blgFilePath, IProgress<string>? progress = null, DateTime? startTime = null, DateTime? endTime = null)
    {
        if (!File.Exists(blgFilePath))
        {
            throw new FileNotFoundException($"BLGファイルが見つかりません: {blgFilePath}");
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("relog.exe はWindows環境でのみ利用可能です。");
        }

        _originalBlgPath = blgFilePath;
        
        // 一時CSVファイルのパスを生成
        var tempDir = Path.GetTempPath();
        var csvFileName = $"perfmon_{Guid.NewGuid():N}.csv";
        _csvFilePath = Path.Combine(tempDir, csvFileName);

        progress?.Report($"relog.exe を使用してCSVファイルに変換中...");
        progress?.Report($"変換先: {_csvFilePath}");

        return await Task.Run(() =>
        {
            try
            {
                // relog.exe のコマンドライン引数を構築
                var arguments = $"\"{blgFilePath}\" -f CSV -o \"{_csvFilePath}\"";
                
                // 時間範囲が指定されている場合は -b と -e オプションを追加
                if (startTime.HasValue)
                {
                    var beginTime = startTime.Value.ToString("MM/dd/yyyy-HH:mm:ss");
                    arguments += $" -b \"{beginTime}\"";
                    progress?.Report($"開始時刻指定: {beginTime}");
                }
                
                if (endTime.HasValue)
                {
                    var endTimeStr = endTime.Value.ToString("MM/dd/yyyy-HH:mm:ss");
                    arguments += $" -e \"{endTimeStr}\"";
                    progress?.Report($"終了時刻指定: {endTimeStr}");
                }
                
                progress?.Report($"実行コマンド: relog.exe {arguments}");

                var processInfo = new ProcessStartInfo
                {
                    FileName = "relog.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = processInfo };
                
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        progress?.Report($"relog 出力: {e.Data}");
                    }
                };
                
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        progress?.Report($"relog エラー: {e.Data}");
                    }
                };

                progress?.Report("relog.exe を実行中...");
                process.Start();
                
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                // プロセスの完了を待機（最大5分）
                var completed = process.WaitForExit(TimeSpan.FromMinutes(5));
                
                if (!completed)
                {
                    progress?.Report("relog.exe がタイムアウトしました。プロセスを強制終了します。");
                    try
                    {
                        process.Kill();
                    }
                    catch { }
                    throw new TimeoutException("relog.exe の実行がタイムアウトしました（5分）");
                }

                var exitCode = process.ExitCode;
                var output = outputBuilder.ToString();
                var error = errorBuilder.ToString();

                progress?.Report($"relog.exe 終了コード: {exitCode}");
                
                if (exitCode == 0)
                {
                    if (File.Exists(_csvFilePath))
                    {
                        var fileInfo = new FileInfo(_csvFilePath);
                        progress?.Report($"CSV変換が完了しました。ファイルサイズ: {fileInfo.Length:N0} bytes");
                        return true;
                    }
                    else
                    {
                        throw new FileNotFoundException("CSVファイルが生成されませんでした。");
                    }
                }
                else
                {
                    throw new Exception($"relog.exe が失敗しました。終了コード: {exitCode}\n出力: {output}\nエラー: {error}");
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"relog.exe 実行エラー: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// 変換されたCSVファイルからカウンターのリストを取得
    /// </summary>
    public async Task<List<string>> GetAvailableCountersAsync(IProgress<string>? progress = null)
    {
        if (string.IsNullOrEmpty(_csvFilePath) || !File.Exists(_csvFilePath))
        {
            throw new InvalidOperationException("CSVファイルが見つかりません。まずConvertBlgToCsvAsyncを実行してください。");
        }

        progress?.Report("CSVファイルからカウンター一覧を取得中...");

        return await Task.Run(() =>
        {
            try
            {
                using var reader = new StreamReader(_csvFilePath, Encoding.UTF8);
                
                // ヘッダー行を読み取り
                var headerLine = reader.ReadLine();
                if (string.IsNullOrEmpty(headerLine))
                {
                    throw new InvalidDataException("CSVファイルのヘッダーが見つかりません。");
                }

                progress?.Report("CSVヘッダーを解析中...");
                
                // ヘッダーをパース（最初の列は通常タイムスタンプなので除外）
                var headers = ParseCsvLine(headerLine);
                
                // 最初の列（タイムスタンプ）を除いたカウンター名を取得
                var counters = headers.Skip(1).Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
                
                progress?.Report($"見つかったカウンター数: {counters.Count}");
                
                // 最初の数個のカウンター名をログ出力（デバッグ用）
                for (int i = 0; i < Math.Min(5, counters.Count); i++)
                {
                    progress?.Report($"カウンター[{i}]: {counters[i]}");
                }

                return counters;
            }
            catch (Exception ex)
            {
                progress?.Report($"カウンター一覧取得エラー: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// 指定されたカウンターのデータを読み込み
    /// </summary>
    public async Task<CounterData> LoadCounterDataAsync(string counterPath, IProgress<string>? progress = null)
    {
        if (string.IsNullOrEmpty(_csvFilePath) || !File.Exists(_csvFilePath))
        {
            throw new InvalidOperationException("CSVファイルが見つかりません。まずConvertBlgToCsvAsyncを実行してください。");
        }

        progress?.Report($"カウンターデータを読み込み中: {counterPath}");

        return await Task.Run(() =>
        {
            try
            {
                var counterData = new CounterData
                {
                    CounterPath = counterPath,
                    ObjectName = ExtractObjectName(counterPath),
                    CounterName = ExtractCounterName(counterPath),
                    InstanceName = ExtractInstanceName(counterPath)
                };

                using var reader = new StreamReader(_csvFilePath, Encoding.UTF8);
                
                // ヘッダー行を読み取り
                var headerLine = reader.ReadLine();
                if (string.IsNullOrEmpty(headerLine))
                {
                    throw new InvalidDataException("CSVファイルのヘッダーが見つかりません。");
                }

                var headers = ParseCsvLine(headerLine);
                
                // 指定されたカウンターの列インデックスを検索
                var columnIndex = -1;
                for (int i = 1; i < headers.Count; i++) // 最初の列はタイムスタンプなので1から開始
                {
                    if (string.Equals(headers[i], counterPath, StringComparison.OrdinalIgnoreCase))
                    {
                        columnIndex = i;
                        break;
                    }
                }

                if (columnIndex == -1)
                {
                    throw new ArgumentException($"指定されたカウンターが見つかりません: {counterPath}");
                }

                progress?.Report($"カウンターの列インデックス: {columnIndex}");

                // データ行を読み取り
                string? line;
                int lineNumber = 1;
                var dataPoints = new List<CounterDataPoint>();
                
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var values = ParseCsvLine(line);
                        
                        if (values.Count <= columnIndex)
                        {
                            progress?.Report($"行 {lineNumber}: 列数が不足しています");
                            continue;
                        }

                        // タイムスタンプ（最初の列）をパース
                        if (!DateTime.TryParse(values[0], out var timestamp))
                        {
                            progress?.Report($"行 {lineNumber}: タイムスタンプの解析に失敗: {values[0]}");
                            continue;
                        }

                        // カウンター値をパース
                        var valueString = values[columnIndex];
                        var isValid = true;
                        var numericValue = 0.0;
                        
                        if (string.IsNullOrWhiteSpace(valueString) || 
                            valueString.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                            valueString.Equals("無効", StringComparison.OrdinalIgnoreCase))
                        {
                            isValid = false;
                        }
                        else
                        {
                            // 数値形式を柔軟に解析（カンマ区切りや科学記法に対応）
                            valueString = valueString.Replace(",", ""); // カンマを除去
                            
                            if (!double.TryParse(valueString, NumberStyles.Float | NumberStyles.AllowThousands, 
                                               CultureInfo.InvariantCulture, out numericValue))
                            {
                                // 日本語ロケールでも試行
                                if (!double.TryParse(valueString, NumberStyles.Float | NumberStyles.AllowThousands, 
                                                   CultureInfo.CurrentCulture, out numericValue))
                                {
                                    isValid = false;
                                    progress?.Report($"行 {lineNumber}: 数値の解析に失敗: '{valueString}'");
                                }
                            }
                        }

                        var dataPoint = new CounterDataPoint
                        {
                            Timestamp = timestamp,
                            Value = numericValue,
                            RawValue = valueString,
                            IsValid = isValid
                        };

                        dataPoints.Add(dataPoint);
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"行 {lineNumber} の処理でエラー: {ex.Message}");
                        continue;
                    }
                }

                counterData.DataPoints = dataPoints;
                
                var validPoints = dataPoints.Count(dp => dp.IsValid);
                progress?.Report($"データポイント読み込み完了: 総数 {dataPoints.Count}, 有効 {validPoints}");

                return counterData;
            }
            catch (Exception ex)
            {
                progress?.Report($"カウンターデータ読み込みエラー: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// CSVの行をパースしてフィールドに分割
    /// </summary>
    private List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var currentField = new StringBuilder();
        var inQuotes = false;
        
        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // エスケープされた引用符
                    currentField.Append('"');
                    i++; // 次の引用符をスキップ
                }
                else
                {
                    // 引用符の開始または終了
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                // フィールドの区切り
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(ch);
            }
        }
        
        // 最後のフィールドを追加
        fields.Add(currentField.ToString());
        
        return fields;
    }

    /// <summary>
    /// カウンターパスからオブジェクト名を抽出
    /// </summary>
    private static string ExtractObjectName(string counterPath)
    {
        // \\ObjectName(Instance)\\CounterName 形式から ObjectName を抽出
        if (counterPath.StartsWith("\\"))
        {
            counterPath = counterPath[1..];
        }
        
        var parts = counterPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 1)
        {
            var objectPart = parts[0];
            var parenIndex = objectPart.IndexOf('(');
            return parenIndex > 0 ? objectPart[..parenIndex] : objectPart;
        }
        return string.Empty;
    }

    /// <summary>
    /// カウンターパスからカウンター名を抽出
    /// </summary>
    private static string ExtractCounterName(string counterPath)
    {
        // \\ObjectName(Instance)\\CounterName 形式から CounterName を抽出
        var parts = counterPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[^1] : string.Empty;
    }

    /// <summary>
    /// カウンターパスからインスタンス名を抽出
    /// </summary>
    private static string ExtractInstanceName(string counterPath)
    {
        // \\ObjectName(Instance)\\CounterName 形式から Instance を抽出
        if (counterPath.StartsWith("\\"))
        {
            counterPath = counterPath[1..];
        }
        
        var parts = counterPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 1)
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
    /// 一時CSVファイルを削除
    /// </summary>
    public void CleanupCsvFile()
    {
        if (!string.IsNullOrEmpty(_csvFilePath) && File.Exists(_csvFilePath))
        {
            try
            {
                File.Delete(_csvFilePath);
            }
            catch
            {
                // クリーンアップエラーは無視
            }
        }
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
            if (disposing)
            {
                CleanupCsvFile();
            }
            _disposed = true;
        }
    }

    ~RelogCsvAnalyzer()
    {
        Dispose(false);
    }
}
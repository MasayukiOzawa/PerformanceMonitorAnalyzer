using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// TreeViewで使用する階層構造のノードクラス
/// </summary>
public class CounterTreeNode : INotifyPropertyChanged
{
    private bool _isChecked = false;
    
    public string DisplayName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public ObservableCollection<CounterTreeNode> Children { get; set; } = new();
    public bool IsLeaf => Children.Count == 0;
    public Visibility CheckBoxVisibility => IsLeaf ? Visibility.Visible : Visibility.Collapsed;
    
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked != value)
            {
                _isChecked = value;
                OnPropertyChanged(nameof(IsChecked));
            }
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Dictionary<string, List<PerformanceDataPoint>> _counterData = new();
    private string? _currentBlgFile;
    private ObservableCollection<CounterTreeNode> _counterTreeNodes = new();

    public MainWindow()
    {
        InitializeComponent();
        InitializeChart();
        CounterTreeView.ItemsSource = _counterTreeNodes;
    }

    private void InitializeChart()
    {
        // ScottPlotは現在無効化されています
        // 後でチャート機能を実装する予定です
    }

    private async void OpenBlgFile_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Performance Monitor Files (*.blg)|*.blg|All Files (*.*)|*.*",
            Title = "BLGファイルを選択してください"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                await LoadBlgFileAsync(openFileDialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルの読み込みに失敗しました: {ex.Message}", 
                              "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                LogError($"Failed to load BLG file: {ex}");
            }
        }
    }

    private async Task LoadBlgFileAsync(string fileName)
    {
        _currentBlgFile = fileName;
        
        // ファイル名を表示
        FileNameDisplay.Text = $"読み込みファイル: {Path.GetFileName(fileName)}";
        
        // プログレスバーを表示
        ProgressGrid.Visibility = Visibility.Visible;
        ProgressStatusText.Text = "BLGファイルを解析中...";
        
        // UI状態をリセット
        _counterTreeNodes.Clear();
        DataTabControl.Items.Clear();
        _counterData.Clear();

        try
        {
            // BLGファイルを非同期で解析
            var progress = new Progress<string>(status => 
            {
                ProgressStatusText.Text = status;
                LogError($"Progress: {status}"); // デバッグ用ログ追加
            });
            
            var counters = await ParseBlgFileAsync(fileName, progress);
            
            LogError($"ParseBlgFileAsync completed with {counters.Count} counters"); // デバッグ用ログ
            
            if (counters.Count == 0)
            {
                throw new Exception("BLGファイルからカウンターを取得できませんでした。ファイルが破損しているか、対応していない形式の可能性があります。");
            }
            
            // 階層構造を作成（UIスレッドで実行）
            ProgressStatusText.Text = "カウンター階層を構築中...";
            BuildCounterTree(counters);

            NoDataMessage.Visibility = Visibility.Collapsed;
            
            LogError($"Counter tree built successfully with {_counterTreeNodes.Count} root nodes"); // デバッグ用ログ
            
            MessageBox.Show($"BLGファイルが読み込まれました。\n{counters.Count}個のカウンターが見つかりました。\n階層構造: {_counterTreeNodes.Count}個のオブジェクト", 
                           "読み込み完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LogError($"LoadBlgFileAsync failed: {ex}");
            MessageBox.Show($"ファイルの読み込みに失敗しました: {ex.Message}\n\n詳細はerror.logファイルを確認してください。", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            // プログレスバーを非表示
            ProgressGrid.Visibility = Visibility.Collapsed;
        }
    }

    private async Task<List<string>> ParseBlgFileAsync(string fileName, IProgress<string>? progress)
    {
        progress?.Report("BLGファイルの解析を開始中...");
        
        // Windows環境での実際のBLGファイル解析を実行
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("このアプリケーションはWindows環境でのみ動作します。");
        }
        
        return await ParseBlgFileWindowsAsync(fileName, progress);
    }

    private async Task<List<string>> ParseBlgFileWindowsAsync(string fileName, IProgress<string>? progress)
    {
        var counters = new List<string>();
        
        try
        {
            progress?.Report("PowerShellを使用してBLGファイルを解析中...");
            LogError($"Starting PowerShell parsing for file: {fileName}");
            
            // PowerShellを使用してBLGファイルからパフォーマンスカウンターを取得
            var processInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"(Import-Counter -Path '{fileName}' -ListSet *).Paths | Sort-Object\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            LogError($"PowerShell command: {processInfo.Arguments}");

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                var errors = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                LogError($"PowerShell exit code: {process.ExitCode}");
                LogError($"PowerShell output length: {output?.Length ?? 0}");
                LogError($"PowerShell errors: {errors}");

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    progress?.Report("カウンター一覧を処理中...");
                    
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    LogError($"PowerShell output lines count: {lines.Length}");
                    
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmedLine) && trimmedLine.StartsWith("\\"))
                        {
                            counters.Add(trimmedLine);
                        }
                    }
                    
                    LogError($"Extracted {counters.Count} counters from PowerShell output");
                    
                    if (counters.Count > 0)
                    {
                        progress?.Report("カウンターデータを読み込み中...");
                        // 実際のBLGファイルからデータポイントを取得
                        await LoadActualCounterDataAsync(fileName, counters, progress);
                        return counters;
                    }
                    else
                    {
                        LogError("No valid counters found in PowerShell output");
                    }
                }
                else
                {
                    LogError($"PowerShell command failed. Exit code: {process.ExitCode}, Output empty: {string.IsNullOrWhiteSpace(output)}");
                }
                
                if (!string.IsNullOrWhiteSpace(errors))
                {
                    LogError($"PowerShell error: {errors}");
                    throw new Exception($"PowerShellエラー: {errors}");
                }
            }
            else
            {
                LogError("Failed to start PowerShell process");
                throw new Exception("PowerShellプロセスの開始に失敗しました");
            }
        }
        catch (Exception ex)
        {
            LogError($"PowerShell execution failed: {ex.Message}");
            throw new Exception($"PowerShell実行エラー: {ex.Message}", ex);
        }

        // PowerShellが失敗した場合の処理
        LogError("PowerShell parsing failed, trying alternative method");
        progress?.Report("代替手法でBLGファイルを解析中...");
        
        try
        {
            return await ParseBlgFileWithTypeLibAsync(fileName, progress);
        }
        catch (Exception ex)
        {
            LogError($"TypeLib parsing failed: {ex.Message}");
            throw new Exception($"すべてのBLG解析方法が失敗しました。PowerShellとTypeLibの両方でエラーが発生しました。", ex);
        }
    }

    private async Task<List<string>> ParseBlgFileWithTypeLibAsync(string fileName, IProgress<string>? progress)
    {
        // COM経由でPDH APIを使用（実装は複雑なため、現在は未実装）
        progress?.Report("代替手法は現在未実装です...");
        LogError("TypeLib parsing not yet implemented");
        throw new NotImplementedException("TypeLibを使用したBLGファイル解析は現在未実装です。");
    }

    private async Task LoadActualCounterDataAsync(string fileName, List<string> counters, IProgress<string>? progress)
    {
        try
        {
            progress?.Report("パフォーマンスデータを取得中...");
            LogError($"Loading counter data for {counters.Count} counters");
            
            // PowerShellを使用してカウンターデータを取得
            var processInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"Import-Counter -Path '{fileName}' | ForEach-Object {{ $_.CounterSamples | ForEach-Object {{ [PSCustomObject]@{{ Counter = $_.Path; Value = $_.CookedValue; Timestamp = $_.Timestamp }} }} }} | ConvertTo-Json\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            LogError($"Counter data PowerShell command: {processInfo.Arguments}");

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                var errors = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                LogError($"Counter data PowerShell exit code: {process.ExitCode}");
                LogError($"Counter data output length: {output?.Length ?? 0}");
                
                if (!string.IsNullOrWhiteSpace(errors))
                {
                    LogError($"Counter data PowerShell errors: {errors}");
                }

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    progress?.Report("パフォーマンスデータを解析中...");
                    // JSONデータを解析してカウンターデータを構築
                    await Task.Run(() => ParseCounterDataFromJson(output));
                    LogError($"Counter data loaded successfully. Total counters with data: {_counterData.Count}");
                    return;
                }
                else
                {
                    LogError("Failed to get counter data from PowerShell");
                    throw new Exception($"カウンターデータの取得に失敗しました。PowerShell終了コード: {process.ExitCode}");
                }
            }
            else
            {
                LogError("Failed to start PowerShell process for counter data");
                throw new Exception("カウンターデータ取得用PowerShellプロセスの開始に失敗しました");
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to load actual counter data: {ex.Message}");
            throw;
        }
    }

    private void ParseCounterDataFromJson(string jsonOutput)
    {
        try
        {
            LogError($"Parsing JSON output, length: {jsonOutput.Length}");
            LogError($"JSON sample (first 500 chars): {jsonOutput.Substring(0, Math.Min(500, jsonOutput.Length))}");
            
            // Newtonsoft.Jsonを使用してJSON解析
            var jsonData = JToken.Parse(jsonOutput);
            var dataArray = jsonData is JArray array ? array : new JArray { jsonData };
            
            LogError($"JSON parsed, found {dataArray.Count} items");
            
            foreach (var item in dataArray)
            {
                var counter = item["Counter"]?.ToString();
                var valueStr = item["Value"]?.ToString();
                var timestampStr = item["Timestamp"]?.ToString();
                
                if (string.IsNullOrEmpty(counter) || string.IsNullOrEmpty(valueStr) || string.IsNullOrEmpty(timestampStr))
                    continue;
                
                if (double.TryParse(valueStr, out var value) && DateTime.TryParse(timestampStr, out var timestamp))
                {
                    if (!_counterData.ContainsKey(counter))
                    {
                        _counterData[counter] = new List<PerformanceDataPoint>();
                    }
                    
                    _counterData[counter].Add(new PerformanceDataPoint
                    {
                        Counter = counter,
                        Value = value,
                        Timestamp = timestamp
                    });
                }
            }
            
            LogError($"Successfully loaded {_counterData.Count} counters from JSON data");
            
            // カウンターごとのデータ点数をログ出力
            foreach (var counter in _counterData.Keys.Take(5)) // 最初の5個だけ表示
            {
                LogError($"Counter '{counter}' has {_counterData[counter].Count} data points");
            }
        }
        catch (Exception ex)
        {
            LogError($"JSON parsing failed: {ex.Message}");
            LogError($"JSON content that failed: {jsonOutput}");
            throw;
        }
    }


    private void BuildCounterTree(List<string> counters)
    {
        LogError($"Building counter tree with {counters.Count} counters");
        
        // カウンターを解析してオブジェクト別にグループ化
        var objectGroups = new Dictionary<string, Dictionary<string, List<string>>>();
        
        foreach (var counter in counters)
        {
            // パフォーマンスカウンターのパス解析: \ObjectName(InstanceName)\CounterName
            var parts = counter.Split('\\');
            if (parts.Length < 3) continue;
            
            var objectName = parts[1];
            var counterName = parts[2];
            
            // インスタンス名を抽出
            var instanceName = "(なし)";
            if (objectName.Contains('(') && objectName.Contains(')'))
            {
                var startIndex = objectName.IndexOf('(');
                var endIndex = objectName.IndexOf(')');
                instanceName = objectName.Substring(startIndex + 1, endIndex - startIndex - 1);
                objectName = objectName.Substring(0, startIndex);
            }
            
            // 階層構造を構築
            if (!objectGroups.ContainsKey(objectName))
            {
                objectGroups[objectName] = new Dictionary<string, List<string>>();
            }
            
            if (!objectGroups[objectName].ContainsKey(instanceName))
            {
                objectGroups[objectName][instanceName] = new List<string>();
            }
            
            objectGroups[objectName][instanceName].Add(counter);
        }
        
        LogError($"Created {objectGroups.Count} object groups");
        
        // TreeViewノードを作成（UIスレッドで実行されているため直接更新可能）
        _counterTreeNodes.Clear();
        
        foreach (var objectGroup in objectGroups.OrderBy(x => x.Key))
        {
            var objectNode = new CounterTreeNode
            {
                DisplayName = objectGroup.Key,
                FullPath = ""
            };
            
            foreach (var instanceGroup in objectGroup.Value.OrderBy(x => x.Key))
            {
                var instanceNode = new CounterTreeNode
                {
                    DisplayName = instanceGroup.Key,
                    FullPath = ""
                };
                
                foreach (var counter in instanceGroup.Value.OrderBy(x => x))
                {
                    var parts = counter.Split('\\');
                    var counterName = parts.Length >= 3 ? parts[2] : counter;
                    
                    var counterNode = new CounterTreeNode
                    {
                        DisplayName = counterName,
                        FullPath = counter
                    };
                    
                    instanceNode.Children.Add(counterNode);
                }
                
                objectNode.Children.Add(instanceNode);
            }
            
            _counterTreeNodes.Add(objectNode);
        }
        
        LogError($"Counter tree built with {_counterTreeNodes.Count} root nodes");
        
        // 各オブジェクトグループの詳細をログ出力
        foreach (var objNode in _counterTreeNodes.Take(3)) // 最初の3個のオブジェクトのみ
        {
            LogError($"Object '{objNode.DisplayName}' has {objNode.Children.Count} instances");
        }
    }

    private void CounterCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.Tag is string counter && !string.IsNullOrEmpty(counter))
        {
            AddCounterToChart(counter);
            AddCounterTab(counter);
        }
    }

    private void CounterCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.Tag is string counter && !string.IsNullOrEmpty(counter))
        {
            RemoveCounterFromChart(counter);
            RemoveCounterTab(counter);
        }
    }

    private void AddCounterToChart(string counter)
    {
        if (!_counterData.ContainsKey(counter)) return;

        // ScottPlot機能は現在無効化されています
        // チャート表示機能は後で実装される予定です
        
        // データテーブルの更新のみ実行
        Console.WriteLine($"カウンター追加: {GetCounterDisplayName(counter)}");
    }

    private void RemoveCounterFromChart(string counter)
    {
        // ScottPlot機能は現在無効化されています
        // チャート表示機能は後で実装される予定です
        
        Console.WriteLine($"カウンター削除: {GetCounterDisplayName(counter)}");
    }

    private void AddCounterTab(string counter)
    {
        if (!_counterData.ContainsKey(counter)) return;

        var tabItem = new TabItem
        {
            Header = GetCounterDisplayName(counter),
            Tag = counter
        };

        var dataGrid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            ItemsSource = _counterData[counter]
        };

        // 列を定義
        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "時間",
            Binding = new System.Windows.Data.Binding("Timestamp") 
            { 
                StringFormat = "yyyy/MM/dd HH:mm:ss" 
            },
            Width = 150
        });

        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "値",
            Binding = new System.Windows.Data.Binding("Value") 
            { 
                StringFormat = "N2" 
            },
            Width = 100
        });

        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "カウンター",
            Binding = new System.Windows.Data.Binding("Counter"),
            Width = DataGridLength.SizeToCells
        });

        tabItem.Content = dataGrid;
        DataTabControl.Items.Add(tabItem);
    }

    private void RemoveCounterTab(string counter)
    {
        var tabToRemove = DataTabControl.Items.Cast<TabItem>()
            .FirstOrDefault(tab => (string)tab.Tag == counter);
        if (tabToRemove != null)
        {
            DataTabControl.Items.Remove(tabToRemove);
        }
    }

    private string GetCounterDisplayName(string counter)
    {
        // カウンター名を短縮して表示用の名前を生成
        var parts = counter.Split('\\');
        if (parts.Length >= 3)
        {
            return $"{parts[1]} - {parts[2]}";
        }
        return counter;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        SetAllCheckBoxes(true);
    }

    private void UnselectAll_Click(object sender, RoutedEventArgs e)
    {
        SetAllCheckBoxes(false);
    }
    
    private void SetAllCheckBoxes(bool isChecked)
    {
        foreach (var objectNode in _counterTreeNodes)
        {
            foreach (var instanceNode in objectNode.Children)
            {
                foreach (var counterNode in instanceNode.Children)
                {
                    counterNode.IsChecked = isChecked;
                }
            }
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void LogError(string message)
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

public class PerformanceDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string Counter { get; set; } = string.Empty;
}
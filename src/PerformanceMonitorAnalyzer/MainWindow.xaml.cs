using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

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
    public NodeType Type { get; set; } = NodeType.Counter;
    
    public bool IsLeaf => Children.Count == 0;
    public Visibility CheckBoxVisibility => IsLeaf ? Visibility.Visible : Visibility.Collapsed;
    
    // 階層レベルに応じた表示プロパティ
    public string FontWeight => Type == NodeType.Object ? "Bold" : "Normal";
    public string TextColor => Type switch
    {
        NodeType.Object => "DarkBlue",
        NodeType.Instance => "DarkGreen", 
        _ => "Black"
    };
    
    public string CountDisplay => Type == NodeType.Object ? $"({Children.Count} インスタンス)" :
                                 Type == NodeType.Instance ? $"({Children.Count} カウンター)" : "";
    
    public Visibility CountVisibility => Type != NodeType.Counter ? Visibility.Visible : Visibility.Collapsed;
    
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

public enum NodeType
{
    Object,
    Instance,
    Counter
}

/// <summary>
/// パフォーマンスデータポイント
/// </summary>
public class PerformanceDataPoint
{
    public string Counter { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
    public string FormattedValue { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}

/// <summary>
/// カウンター統計情報
/// </summary>
public class CounterStatistics
{
    public string CounterName { get; set; } = string.Empty;
    public int DataPointCount { get; set; }
    public double Average { get; set; }
    public double Maximum { get; set; }
    public double Minimum { get; set; }
    public double StandardDeviation { get; set; }
    public DateTime FirstTimestamp { get; set; }
    public DateTime LastTimestamp { get; set; }
    public string Unit { get; set; } = string.Empty;
    
    public string FormattedAverage => $"{Average:N2} {Unit}".Trim();
    public string FormattedMaximum => $"{Maximum:N2} {Unit}".Trim();
    public string FormattedMinimum => $"{Minimum:N2} {Unit}".Trim();
    public string FormattedStandardDeviation => $"{StandardDeviation:N2} {Unit}".Trim();
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Dictionary<string, List<PerformanceDataPoint>> _counterData = new();
    private string? _currentBlgFile;
    private ObservableCollection<CounterTreeNode> _counterTreeNodes = new();
    private DateTime _fileStartTime;
    private DateTime _fileEndTime;
    private bool _timeRangeDetected = false;

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

    public async Task LoadBlgFileFromCommandLineAsync(string fileName)
    {
        try
        {
            await LoadBlgFileAsync(fileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"コマンドライン引数で指定されたBLGファイルの読み込みに失敗しました: {ex.Message}", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            LogError($"Failed to load BLG file from command line: {ex}");
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

            // 時間範囲を検出
            ProgressStatusText.Text = "時間範囲を検出中...";
            await DetectTimeRangeAsync(fileName, progress);

            NoDataMessage.Visibility = Visibility.Collapsed;
            
            LogError($"Counter tree built successfully with {_counterTreeNodes.Count} root nodes"); // デバッグ用ログ
            
            var totalCounters = _counterTreeNodes.Sum(obj => obj.Children.Sum(inst => inst.Children.Count));
            var totalInstances = _counterTreeNodes.Sum(obj => obj.Children.Count);
            
            var timeRangeInfo = _timeRangeDetected 
                ? $"\n⏰ 時間範囲: {_fileStartTime:yyyy/MM/dd HH:mm:ss} ～ {_fileEndTime:yyyy/MM/dd HH:mm:ss}" 
                : "\n⚠️ 時間範囲の検出に失敗しました";
            
            MessageBox.Show($"BLGファイルが読み込まれました。\n\n" +
                           $"📊 パフォーマンスオブジェクト: {_counterTreeNodes.Count}個\n" +
                           $"🏷️  インスタンス: {totalInstances}個\n" +
                           $"📈 カウンター: {totalCounters}個{timeRangeInfo}\n\n" +
                           $"カウンターを選択して「🚀 選択されたカウンターを読み込み」ボタンを押してください。", 
                           "読み込み完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LogError($"LoadBlgFileAsync failed: {ex}");
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            MessageBox.Show($"ファイルの読み込みに失敗しました: {ex.Message}\n\n詳細はerror.logファイルを確認してください。\n場所: {logPath}", 
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
        
        return await ParseBlgFileWithPdhApiAsync(fileName, progress);
    }

    private async Task<List<string>> ParseBlgFileWithPdhApiAsync(string fileName, IProgress<string>? progress)
    {
        var counters = new List<string>();
        BlgFileAnalyzer? analyzer = null;
        
        try
        {
            progress?.Report("PDH APIを使用してBLGファイルを解析中...");
            LogError($"Starting PDH API parsing for file: {fileName}");
            
            analyzer = new BlgFileAnalyzer();
            
            // BLGファイルを開く
            var opened = await analyzer.OpenBlgFileAsync(fileName, progress);
            if (!opened)
            {
                throw new Exception("BLGファイルを開くことができませんでした。");
            }
            
            // 全てのカウンターパスを生成
            counters = await analyzer.GenerateAllCounterPathsAsync(progress);
            
            LogError($"PDH API parsing completed with {counters.Count} counters");
            
            if (counters.Count > 0)
            {
                progress?.Report("カウンターデータを読み込み中...");
                // 実際のカウンターデータを読み込み（最初の10個のみテスト用）
                await LoadCounterDataWithPdhApiAsync(analyzer, counters, progress);
                return counters;
            }
            else
            {
                LogError("No valid counters found in BLG file");
                throw new Exception("BLGファイルからカウンターを取得できませんでした。");
            }
        }
        catch (Exception ex)
        {
            LogError($"PDH API execution failed: {ex.Message}");
            throw new Exception($"PDH API解析エラー: {ex.Message}", ex);
        }
        finally
        {
            analyzer?.Dispose();
        }
    }



    private async Task LoadCounterDataWithPdhApiAsync(BlgFileAnalyzer analyzer, List<string> counters, IProgress<string>? progress)
    {
        try
        {
            progress?.Report("パフォーマンスデータを取得中...");
            LogError($"Loading counter data for {counters.Count} counters using PDH API");
            
            // 各カウンターのデータを読み込み（最初の10個のみ処理）
            var processedCount = 0;
            foreach (var counter in counters.Take(10))
            {
                try
                {
                    var counterInfo = await analyzer.LoadCounterDataAsync(counter, progress);
                    
                    // 実際のBLGファイルから読み込んだデータを使用
                    if (counterInfo != null && counterInfo.DataPoints.Count > 0)
                    {
                        var dataPoints = new List<PerformanceDataPoint>();
                        
                        foreach (var dataPoint in counterInfo.DataPoints)
                        {
                            var unit = EstimateUnit(counter);
                            var formattedValue = FormatValueWithUnit(dataPoint.Value, unit);
                            
                            dataPoints.Add(new PerformanceDataPoint
                            {
                                Counter = counter,
                                Value = dataPoint.Value,
                                Timestamp = dataPoint.Timestamp,
                                FormattedValue = formattedValue,
                                Unit = unit
                            });
                        }
                        
                        _counterData[counter] = dataPoints;
                        processedCount++;
                    }
                    else
                    {
                        LogError($"No data available for counter: {counter}");
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Failed to load data for counter {counter}: {ex.Message}");
                    // 個別のカウンターエラーは続行
                }
            }
            
            LogError($"Successfully loaded data for {processedCount} counters using PDH API");
            progress?.Report($"カウンターデータ読み込み完了: {processedCount}個");
        }
        catch (Exception ex)
        {
            LogError($"Counter data loading failed: {ex.Message}");
            throw;
        }
    }

    private void BuildCounterTree(List<string> counters)
    {
        LogError($"Building counter tree with {counters.Count} counters");
        
        // 最初のいくつかのカウンターをログ出力
        if (counters.Count > 0)
        {
            var sampleCounters = counters.Take(10).ToList();
            LogError($"Sample counters: {string.Join(", ", sampleCounters)}");
        }
        
        // カウンターを解析してオブジェクト別にグループ化
        var objectGroups = new Dictionary<string, Dictionary<string, List<string>>>();
        
        foreach (var counter in counters)
        {
            // パフォーマンスカウンターのパス解析: \ObjectName(InstanceName)\CounterName
            var parts = counter.Split('\\');
            if (parts.Length < 3) 
            {
                LogError($"Skipping invalid counter path: {counter}");
                continue;
            }
            
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
                LogError($"Created new object group: {objectName}");
            }
            
            if (!objectGroups[objectName].ContainsKey(instanceName))
            {
                objectGroups[objectName][instanceName] = new List<string>();
            }
            
            objectGroups[objectName][instanceName].Add(counter);
        }
        
        LogError($"Created {objectGroups.Count} object groups");
        
        // 各オブジェクトグループの詳細をログ出力
        foreach (var objGroup in objectGroups)
        {
            var totalCounters = objGroup.Value.Sum(x => x.Value.Count);
            LogError($"Object '{objGroup.Key}' has {objGroup.Value.Count} instances, total counters: {totalCounters}");
            
            // LogicalDiskの詳細を特に出力
            if (objGroup.Key == "LogicalDisk")
            {
                foreach (var instGroup in objGroup.Value)
                {
                    LogError($"  LogicalDisk instance '{instGroup.Key}' has {instGroup.Value.Count} counters");
                }
            }
        }
        
        // TreeViewノードを作成（UIスレッドで実行されているため直接更新可能）
        _counterTreeNodes.Clear();
        
        foreach (var objectGroup in objectGroups.OrderBy(x => x.Key))
        {
            var objectNode = new CounterTreeNode
            {
                DisplayName = objectGroup.Key,
                FullPath = "",
                Type = NodeType.Object
            };
            
            foreach (var instanceGroup in objectGroup.Value.OrderBy(x => x.Key))
            {
                var instanceNode = new CounterTreeNode
                {
                    DisplayName = instanceGroup.Key == "(なし)" ? "(総合)" : instanceGroup.Key,
                    FullPath = "",
                    Type = NodeType.Instance
                };
                
                foreach (var counter in instanceGroup.Value.OrderBy(x => x))
                {
                    var parts = counter.Split('\\');
                    var counterName = parts.Length >= 3 ? parts[2] : counter;
                    
                    var counterNode = new CounterTreeNode
                    {
                        DisplayName = counterName,
                        FullPath = counter,
                        Type = NodeType.Counter
                    };
                    
                    instanceNode.Children.Add(counterNode);
                }
                
                objectNode.Children.Add(instanceNode);
            }
            
            _counterTreeNodes.Add(objectNode);
        }
        
        LogError($"Counter tree built with {_counterTreeNodes.Count} root nodes");
        
        // 各ルートノードの詳細をログ出力
        foreach (var objNode in _counterTreeNodes)
        {
            var totalCounters = objNode.Children.Sum(inst => inst.Children.Count);
            LogError($"Tree node '{objNode.DisplayName}' has {objNode.Children.Count} child instances, {totalCounters} total counters");
            
            // LogicalDiskノードの詳細
            if (objNode.DisplayName == "LogicalDisk")
            {
                foreach (var instNode in objNode.Children)
                {
                    LogError($"  LogicalDisk instance '{instNode.DisplayName}' has {instNode.Children.Count} counters");
                }
            }
        }
        
        LogError("TreeView structure updated via ObservableCollection");
    }

    private void CounterCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"CounterCheckBox_Checked called");
            
            if (sender is CheckBox checkBox)
            {
                System.Diagnostics.Debug.WriteLine($"CheckBox found, Tag: {checkBox.Tag}");
                
                if (checkBox.Tag is string counter && !string.IsNullOrEmpty(counter))
                {
                    System.Diagnostics.Debug.WriteLine($"CounterCheckBox_Checked for: {counter}");
                    
                    // 自動データ読み込みは行わず、チェック状態のみ保持
                    // 実際のデータ読み込みは「実行」ボタンで行う
                    System.Diagnostics.Debug.WriteLine($"Counter {counter} marked for execution");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"CheckBox Tag is not a valid string. Tag: {checkBox.Tag}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Sender is not a CheckBox");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in CounterCheckBox_Checked: {ex.Message}");
            LogError($"Error in CounterCheckBox_Checked: {ex}");
        }
    }

    private void CounterCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"CounterCheckBox_Unchecked called");
            
            if (sender is CheckBox checkBox && checkBox.Tag is string counter && !string.IsNullOrEmpty(counter))
            {
                System.Diagnostics.Debug.WriteLine($"CounterCheckBox_Unchecked for: {counter}");
                
                // チェック解除時はデータテーブルからも削除
                RemoveCounterFromChart(counter);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in CounterCheckBox_Unchecked: {ex.Message}");
            LogError($"Error in CounterCheckBox_Unchecked: {ex}");
        }
    }



    private void AddCounterToChart(string counter)
    {
        System.Diagnostics.Debug.WriteLine($"AddCounterToChart called for: {counter}");
        
        if (!_counterData.ContainsKey(counter))
        {
            System.Diagnostics.Debug.WriteLine($"Counter not found in _counterData: {counter}");
            System.Diagnostics.Debug.WriteLine($"Available counters: {string.Join(", ", _counterData.Keys.Take(5))}...");
            
            // カウンターデータが存在しない場合は何もしない（ファイルに含まれるデータのみ処理）
            System.Diagnostics.Debug.WriteLine($"Counter data not available for: {counter}");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"Counter found in _counterData with {_counterData[counter].Count} data points");
        
        // ScottPlot機能は現在無効化されています
        // チャート表示機能は後で実装される予定です
        
        // データテーブルタブを作成（チェックボックス経由）
        AddCounterTab(counter);
    }

    private void RemoveCounterFromChart(string counter)
    {
        System.Diagnostics.Debug.WriteLine($"RemoveCounterFromChart called for: {counter}");
        
        // ScottPlot機能は現在無効化されています
        // チャート表示機能は後で実装される予定です
        
        // データテーブルタブを削除
        RemoveCounterTab(counter);
    }



    private void AddCounterTab(string counter)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"AddCounterTab called for: {counter}");
            
            if (!_counterData.ContainsKey(counter))
            {
                System.Diagnostics.Debug.WriteLine($"Counter data not found for: {counter}");
                return;
            }

            // 既存のタブがあるかチェック
            var existingTab = DataTabControl.Items.Cast<TabItem>()
                .FirstOrDefault(tab => (string)tab.Tag == counter);
            if (existingTab != null)
            {
                System.Diagnostics.Debug.WriteLine($"Tab already exists for: {counter}");
                DataTabControl.SelectedItem = existingTab;
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Creating new tab for: {counter}");

            var tabItem = new TabItem
            {
                Header = GetCounterDisplayName(counter),
                Tag = counter
            };

            // メインコンテナを作成
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // データグリッドを作成
            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                ItemsSource = _counterData[counter],
                AlternatingRowBackground = System.Windows.Media.Brushes.AliceBlue,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HeadersVisibility = DataGridHeadersVisibility.Column
            };

            System.Diagnostics.Debug.WriteLine($"Data points count: {_counterData[counter].Count}");

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
                Header = "フォーマット済み値",
                Binding = new System.Windows.Data.Binding("FormattedValue"),
                Width = 120
            });

            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "単位",
                Binding = new System.Windows.Data.Binding("Unit"),
                Width = 80
            });

            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "カウンター",
                Binding = new System.Windows.Data.Binding("Counter"),
                Width = DataGridLength.SizeToCells
            });

            Grid.SetRow(dataGrid, 0);
            mainGrid.Children.Add(dataGrid);

            // 統計情報パネルを作成
            var statisticsPanel = CreateStatisticsPanel(counter, _counterData[counter]);
            Grid.SetRow(statisticsPanel, 1);
            mainGrid.Children.Add(statisticsPanel);

            tabItem.Content = mainGrid;
            DataTabControl.Items.Add(tabItem);
            DataTabControl.SelectedItem = tabItem;

            System.Diagnostics.Debug.WriteLine($"Tab created and added successfully for: {counter}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in AddCounterTab: {ex.Message}");
            MessageBox.Show($"タブ作成でエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

    /// <summary>
    /// 統計情報パネルを作成
    /// </summary>
    private UIElement CreateStatisticsPanel(string counter, List<PerformanceDataPoint> dataPoints)
    {
        var statistics = CalculateStatistics(counter, dataPoints);
        
        var border = new Border
        {
            Background = System.Windows.Media.Brushes.LightGray,
            Padding = new Thickness(10),
            Margin = new Thickness(0, 5, 0, 0)
        };

        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };

        // 統計情報を表示するテキストブロックを作成
        var statisticsItems = new[]
        {
            $"データ数: {statistics.DataPointCount}",
            $"平均: {statistics.FormattedAverage}",
            $"最大: {statistics.FormattedMaximum}",
            $"最小: {statistics.FormattedMinimum}",
            $"標準偏差: {statistics.FormattedStandardDeviation}",
            $"期間: {statistics.FirstTimestamp:MM/dd HH:mm} - {statistics.LastTimestamp:MM/dd HH:mm}"
        };

        foreach (var item in statisticsItems)
        {
            var textBlock = new TextBlock
            {
                Text = item,
                Margin = new Thickness(0, 0, 20, 0),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            stackPanel.Children.Add(textBlock);
        }

        // エクスポートボタンを追加
        var exportButton = new Button
        {
            Content = "CSV出力",
            Padding = new Thickness(10, 2, 10, 2),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        exportButton.Click += (sender, e) => ExportCounterDataToCsv(counter, dataPoints);
        stackPanel.Children.Add(exportButton);

        border.Child = stackPanel;
        return border;
    }

    /// <summary>
    /// 統計情報を計算
    /// </summary>
    private CounterStatistics CalculateStatistics(string counter, List<PerformanceDataPoint> dataPoints)
    {
        if (!dataPoints.Any())
        {
            return new CounterStatistics
            {
                CounterName = counter,
                DataPointCount = 0
            };
        }

        var values = dataPoints.Select(dp => dp.Value).ToList();
        var average = values.Average();
        var variance = values.Select(v => Math.Pow(v - average, 2)).Average();
        var standardDeviation = Math.Sqrt(variance);

        // カウンターの種類に基づいて単位を推定
        var unit = EstimateUnit(counter);

        return new CounterStatistics
        {
            CounterName = counter,
            DataPointCount = dataPoints.Count,
            Average = average,
            Maximum = values.Max(),
            Minimum = values.Min(),
            StandardDeviation = standardDeviation,
            FirstTimestamp = dataPoints.Min(dp => dp.Timestamp),
            LastTimestamp = dataPoints.Max(dp => dp.Timestamp),
            Unit = unit
        };
    }

    /// <summary>
    /// カウンターの種類から単位を推定
    /// </summary>
    private string EstimateUnit(string counter)
    {
        var lowerCounter = counter.ToLower();
        
        if (lowerCounter.Contains("% processor time") || lowerCounter.Contains("% idle time"))
            return "%";
        if (lowerCounter.Contains("available mbytes") || lowerCounter.Contains("mbytes"))
            return "MB";
        if (lowerCounter.Contains("bytes") && !lowerCounter.Contains("mbytes"))
            return "Bytes";
        if (lowerCounter.Contains("/sec"))
            return "/sec";
        if (lowerCounter.Contains("count"))
            return "count";
        
        return "";
    }

    /// <summary>
    /// カウンターデータをCSVファイルに出力
    /// </summary>
    private void ExportCounterDataToCsv(string counter, List<PerformanceDataPoint> dataPoints)
    {
        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"{GetCounterDisplayName(counter).Replace(" - ", "_").Replace(" ", "_")}.csv",
                Title = "CSVファイルを保存"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var csv = new StringBuilder();
                
                // ヘッダー行
                csv.AppendLine("Timestamp,Value,FormattedValue,Unit,Counter");
                
                // データ行
                foreach (var dataPoint in dataPoints.OrderBy(dp => dp.Timestamp))
                {
                    csv.AppendLine($"{dataPoint.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                                 $"{dataPoint.Value}," +
                                 $"\"{dataPoint.FormattedValue}\"," +
                                 $"\"{dataPoint.Unit}\"," +
                                 $"\"{dataPoint.Counter}\"");
                }
                
                File.WriteAllText(saveFileDialog.FileName, csv.ToString(), Encoding.UTF8);
                
                MessageBox.Show($"CSVファイルが保存されました。\n{saveFileDialog.FileName}", 
                              "エクスポート完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"CSVファイルの保存に失敗しました: {ex.Message}", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            LogError($"CSV export failed: {ex}");
        }
    }

    /// <summary>
    /// カウンター名を短縮して表示用の名前を生成
    /// </summary>
    private string GetCounterDisplayName(string counter)
    {
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



    /// <summary>
    /// 値を単位付きでフォーマット
    /// </summary>
    private string FormatValueWithUnit(double value, string unit)
    {
        if (unit == "%")
            return $"{value:N1}%";
        if (unit == "MB")
            return $"{value:N0} MB";
        if (unit == "Bytes")
        {
            if (value >= 1073741824) // >= 1GB
                return $"{value / 1073741824:N2} GB";
            if (value >= 1048576) // >= 1MB
                return $"{value / 1048576:N2} MB";
            if (value >= 1024) // >= 1KB
                return $"{value / 1024:N2} KB";
            return $"{value:N0} Bytes";
        }
        if (unit == "/sec")
            return $"{value:N2}/sec";
        if (unit == "count")
            return $"{value:N0}";
        
        return $"{value:N2}";
    }

    private void CloseAllTabs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 全てのカウンターのチェックを外す
            SetAllCheckBoxes(false);
            
            // 全てのタブを削除
            DataTabControl.Items.Clear();
            
            MessageBox.Show("全てのデータテーブルタブが閉じられました。", 
                          "タブクリア完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"タブのクリアに失敗しました: {ex.Message}", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            LogError($"Failed to close all tabs: {ex}");
        }
    }

    private void ExportAllDataToCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_counterData.Any())
            {
                MessageBox.Show("エクスポートするデータがありません。\nBLGファイルを読み込んでカウンターを選択してください。", 
                              "データなし", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"PerformanceData_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Title = "全データのCSVファイルを保存"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var csv = new StringBuilder();
                
                // ヘッダー行
                csv.AppendLine("Timestamp,CounterName,Value,FormattedValue,Unit");
                
                // 各カウンターのデータを統合
                var allData = new List<(DateTime timestamp, string counter, PerformanceDataPoint data)>();
                
                foreach (var counterPair in _counterData)
                {
                    foreach (var dataPoint in counterPair.Value)
                    {
                        allData.Add((dataPoint.Timestamp, counterPair.Key, dataPoint));
                    }
                }
                
                // タイムスタンプ順にソート
                foreach (var item in allData.OrderBy(x => x.timestamp).ThenBy(x => x.counter))
                {
                    var dp = item.data;
                    csv.AppendLine($"{dp.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                                 $"\"{GetCounterDisplayName(item.counter)}\"," +
                                 $"{dp.Value}," +
                                 $"\"{dp.FormattedValue}\"," +
                                 $"\"{dp.Unit}\"");
                }
                
                File.WriteAllText(saveFileDialog.FileName, csv.ToString(), Encoding.UTF8);
                
                MessageBox.Show($"全データがCSVファイルに保存されました。\n" +
                              $"ファイル: {saveFileDialog.FileName}\n" +
                              $"カウンター数: {_counterData.Count}個\n" +
                              $"データポイント数: {allData.Count}個", 
                              "エクスポート完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"CSVファイルの保存に失敗しました: {ex.Message}", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            LogError($"All data CSV export failed: {ex}");
        }
    }

    /// <summary>
    /// BLGファイルの時間範囲を検出する
    /// </summary>
    private async Task<bool> DetectTimeRangeAsync(string blgFilePath, IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("BLGファイルの時間範囲を検出中...");
            
            // relog.exe を使用してBLGファイルの情報を取得
            var tempCsvPath = Path.GetTempFileName();
            
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "relog.exe",
                    Arguments = $"\"{blgFilePath}\" -f CSV -o \"{tempCsvPath}\" -y",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();
                
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0 && File.Exists(tempCsvPath))
                {
                    // CSVファイルから最初と最後のタイムスタンプを取得
                    var lines = await File.ReadAllLinesAsync(tempCsvPath, Encoding.UTF8);
                    if (lines.Length >= 2)
                    {
                        // ヘッダー行をスキップして最初のデータ行を取得
                        var firstDataLine = lines.Skip(1).FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
                        var lastDataLine = lines.Skip(1).LastOrDefault(line => !string.IsNullOrWhiteSpace(line));
                        
                        if (firstDataLine != null && lastDataLine != null)
                        {
                            var firstTimestamp = ParseTimestampFromCsvLine(firstDataLine);
                            var lastTimestamp = ParseTimestampFromCsvLine(lastDataLine);
                            
                            if (firstTimestamp.HasValue && lastTimestamp.HasValue)
                            {
                                _fileStartTime = firstTimestamp.Value;
                                _fileEndTime = lastTimestamp.Value;
                                _timeRangeDetected = true;
                                
                                // UIを更新
                                UpdateTimeRangeUI();
                                
                                LogError($"Time range detected: {_fileStartTime:yyyy-MM-dd HH:mm:ss} - {_fileEndTime:yyyy-MM-dd HH:mm:ss}");
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    LogError($"relog.exe failed. Exit code: {process.ExitCode}, Error: {error}");
                }
            }
            finally
            {
                // 一時ファイルを削除
                if (File.Exists(tempCsvPath))
                {
                    try { File.Delete(tempCsvPath); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Time range detection failed: {ex.Message}");
        }
        
        return false;
    }

    /// <summary>
    /// CSV行からタイムスタンプを解析
    /// </summary>
    private DateTime? ParseTimestampFromCsvLine(string csvLine)
    {
        try
        {
            var parts = csvLine.Split(',');
            if (parts.Length > 0)
            {
                var timestampStr = parts[0].Trim('"');
                if (DateTime.TryParse(timestampStr, out var timestamp))
                {
                    return timestamp;
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to parse timestamp from CSV line: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// 時間範囲UIを更新
    /// </summary>
    private void UpdateTimeRangeUI()
    {
        if (_timeRangeDetected)
        {
            TimeRangeExpander.Visibility = Visibility.Visible;
            TimeRangeDisplay.Text = $"{_fileStartTime:yyyy/MM/dd HH:mm:ss} ～ {_fileEndTime:yyyy/MM/dd HH:mm:ss}";
            
            // スライダーを初期化
            StartTimeSlider.Minimum = 0;
            StartTimeSlider.Maximum = 100;
            StartTimeSlider.Value = 0;
            
            EndTimeSlider.Minimum = 0;
            EndTimeSlider.Maximum = 100;
            EndTimeSlider.Value = 100;
            
            UpdateTimeSliderTexts();
            
            // 実行ボタンを有効化
            ExecuteButton.IsEnabled = true;
        }
        else
        {
            TimeRangeExpander.Visibility = Visibility.Collapsed;
            ExecuteButton.IsEnabled = false;
        }
    }

    /// <summary>
    /// スライダーのテキスト表示を更新
    /// </summary>
    private void UpdateTimeSliderTexts()
    {
        if (_timeRangeDetected)
        {
            var totalDuration = _fileEndTime - _fileStartTime;
            var startOffset = TimeSpan.FromTicks((long)(totalDuration.Ticks * StartTimeSlider.Value / 100));
            var endOffset = TimeSpan.FromTicks((long)(totalDuration.Ticks * EndTimeSlider.Value / 100));
            
            var selectedStartTime = _fileStartTime + startOffset;
            var selectedEndTime = _fileStartTime + endOffset;
            
            StartTimeText.Text = selectedStartTime.ToString("MM/dd HH:mm:ss");
            EndTimeText.Text = selectedEndTime.ToString("MM/dd HH:mm:ss");
        }
    }

    /// <summary>
    /// 時間範囲スライダーの値変更イベント
    /// </summary>
    private void TimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_timeRangeDetected)
        {
            // 開始時間が終了時間を超えないように調整
            if (StartTimeSlider.Value >= EndTimeSlider.Value)
            {
                if (sender == StartTimeSlider)
                {
                    EndTimeSlider.Value = Math.Min(100, StartTimeSlider.Value + 1);
                }
                else
                {
                    StartTimeSlider.Value = Math.Max(0, EndTimeSlider.Value - 1);
                }
            }
            
            UpdateTimeSliderTexts();
        }
    }

    /// <summary>
    /// 選択されたカウンターを実行するボタンのクリックイベント
    /// </summary>
    private async void ExecuteSelectedCounters_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentBlgFile) || !_timeRangeDetected)
        {
            MessageBox.Show("BLGファイルが読み込まれていないか、時間範囲が検出されていません。", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedCounters = GetSelectedCounters();
        if (selectedCounters.Count == 0)
        {
            MessageBox.Show("実行するカウンターが選択されていません。\nチェックボックスを選択してからボタンを押してください。", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // プログレスバーを表示
            ProgressGrid.Visibility = Visibility.Visible;
            ProgressStatusText.Text = $"選択されたカウンター ({selectedCounters.Count}個) を処理中...";
            
            ExecuteButton.IsEnabled = false;
            
            var progress = new Progress<string>(status => ProgressStatusText.Text = status);
            
            // 選択された時間範囲を計算
            var totalDuration = _fileEndTime - _fileStartTime;
            var startOffset = TimeSpan.FromTicks((long)(totalDuration.Ticks * StartTimeSlider.Value / 100));
            var endOffset = TimeSpan.FromTicks((long)(totalDuration.Ticks * EndTimeSlider.Value / 100));
            
            var selectedStartTime = _fileStartTime + startOffset;
            var selectedEndTime = _fileStartTime + endOffset;
            
            await ExecuteRelogForSelectedCounters(selectedCounters, selectedStartTime, selectedEndTime, progress);
            
            MessageBox.Show($"カウンターデータの読み込みが完了しました。\n\n" +
                           $"処理されたカウンター数: {selectedCounters.Count}個\n" +
                           $"時間範囲: {selectedStartTime:yyyy/MM/dd HH:mm:ss} ～ {selectedEndTime:yyyy/MM/dd HH:mm:ss}",
                           "処理完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"カウンターデータの処理に失敗しました: {ex.Message}", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            LogError($"Execute selected counters failed: {ex}");
        }
        finally
        {
            ProgressGrid.Visibility = Visibility.Collapsed;
            ExecuteButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// 選択されたカウンターの一覧を取得
    /// </summary>
    private List<string> GetSelectedCounters()
    {
        var selected = new List<string>();
        
        foreach (var objectNode in _counterTreeNodes)
        {
            foreach (var instanceNode in objectNode.Children)
            {
                foreach (var counterNode in instanceNode.Children)
                {
                    if (counterNode.IsChecked)
                    {
                        selected.Add(counterNode.FullPath);
                    }
                }
            }
        }
        
        return selected;
    }

    /// <summary>
    /// 選択されたカウンターに対してrelog.exeを実行
    /// </summary>
    private async Task ExecuteRelogForSelectedCounters(List<string> counters, DateTime startTime, DateTime endTime, IProgress<string>? progress)
    {
        var tempCsvPath = Path.GetTempFileName();
        
        try
        {
            progress?.Report("relog.exe でCSVファイルを生成中...");
            
            // relog.exe で指定された時間範囲とカウンターでCSV生成
            // 時間制約有効かどうかを判定（スライダーが初期値でない場合は時間制約を適用）
            bool useTimeConstraints = StartTimeSlider.Value > 0 || EndTimeSlider.Value < 100;
            
            string arguments;
            if (useTimeConstraints)
            {
                // 複数の時間形式を試す（日本語環境対応のため yyyy/MM/dd を最優先）
                var formats = new[]
                {
                    "yyyy/MM/dd HH:mm:ss",     // 日本語環境推奨形式
                    "MM/dd/yyyy HH:mm:ss",     // 米国形式
                    "yyyy-MM-dd HH:mm:ss",     // ISO形式
                    "dd/MM/yyyy HH:mm:ss",     // ヨーロッパ形式
                    "M/d/yyyy H:mm:ss"         // 短縮形式
                };
                
                arguments = $"\"{_currentBlgFile}\" -f CSV -o \"{tempCsvPath}\" " +
                           $"-b \"{startTime.ToString(formats[0], System.Globalization.CultureInfo.InvariantCulture)}\" " +
                           $"-e \"{endTime.ToString(formats[0], System.Globalization.CultureInfo.InvariantCulture)}\" -y";
            }
            else
            {
                // 時間制約なし
                arguments = $"\"{_currentBlgFile}\" -f CSV -o \"{tempCsvPath}\" -y";
            }
            
            // UIにrelog.exeコマンドを表示
            await Dispatcher.InvokeAsync(() =>
            {
                RelogStatusExpander.Visibility = Visibility.Visible;
                RelogCommandDisplay.Text = $"relog.exe {arguments}";
                RelogResultDisplay.Text = "実行中...";
                // デフォルトで折りたたまれた状態を維持
            });
            
            // デバッグ情報をログに出力
            LogInfo($"Executing relog.exe with arguments: {arguments}");
            LogInfo($"Use time constraints: {useTimeConstraints}");
            LogInfo($"Start time: {startTime:yyyy-MM-dd HH:mm:ss}, End time: {endTime:yyyy-MM-dd HH:mm:ss}");
            LogInfo($"Selected counters count: {counters.Count}");
            
            // 特定のカウンターのみを指定する場合（relog.exeの制限により全カウンターを一度取得）
            var processInfo = new ProcessStartInfo
            {
                FileName = "relog.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.Unicode, // UTF-16対応
                StandardErrorEncoding = Encoding.Unicode   // UTF-16対応
            };

            using var process = new Process { StartInfo = processInfo };
            process.Start();
            
            await process.WaitForExitAsync();
            
            // 実行結果をUIに表示
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await Dispatcher.InvokeAsync(() =>
            {
                var result = $"Exit Code: {process.ExitCode}\n";
                if (!string.IsNullOrEmpty(output))
                    result += $"Output: {output}\n";
                if (!string.IsNullOrEmpty(error))
                    result += $"Error: {error}\n";
                
                RelogResultDisplay.Text = result;
            });
            
            // Exit code 11で時間制約使用時は、異なる時間形式を試す
            if (process.ExitCode == 11 && useTimeConstraints)
            {
                LogInfo("Exit code 11 detected, trying different time formats...");
                
                var timeFormats = new[]
                {
                    "yyyy/MM/dd HH:mm:ss",     // 日本語環境推奨形式
                    "yyyy-MM-dd HH:mm:ss",     // ISO形式
                    "dd/MM/yyyy HH:mm:ss",     // ヨーロッパ形式
                    "M/d/yyyy H:mm:ss",        // 短縮形式
                    "MM/dd/yyyy HH:mm:ss"      // 米国形式
                };
                
                foreach (var format in timeFormats)
                {
                    LogInfo($"Trying time format: {format}");
                    var altArguments = $"\"{_currentBlgFile}\" -f CSV -o \"{tempCsvPath}\" " +
                                      $"-b \"{startTime.ToString(format, System.Globalization.CultureInfo.InvariantCulture)}\" " +
                                      $"-e \"{endTime.ToString(format, System.Globalization.CultureInfo.InvariantCulture)}\" -y";
                    
                    // UIに代替コマンドを表示
                    await Dispatcher.InvokeAsync(() =>
                    {
                        RelogCommandDisplay.Text += $"\n\n[再試行] relog.exe {altArguments}";
                        RelogResultDisplay.Text += $"\n\n[再試行 - {format}] 実行中...";
                    });
                    
                    var altProcessInfo = new ProcessStartInfo
                    {
                        FileName = "relog.exe",
                        Arguments = altArguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.Unicode, // UTF-16対応
                        StandardErrorEncoding = Encoding.Unicode   // UTF-16対応
                    };
                    
                    using var altProcess = new Process { StartInfo = altProcessInfo };
                    altProcess.Start();
                    await altProcess.WaitForExitAsync();
                    
                    // 代替実行結果をUIに追加
                    var altOutput = await altProcess.StandardOutput.ReadToEndAsync();
                    var altError = await altProcess.StandardError.ReadToEndAsync();
                    
                    await Dispatcher.InvokeAsync(() =>
                    {
                        var altResult = $"Exit Code: {altProcess.ExitCode}";
                        if (!string.IsNullOrEmpty(altOutput))
                            altResult += $"\nOutput: {altOutput}";
                        if (!string.IsNullOrEmpty(altError))
                            altResult += $"\nError: {altError}";
                        
                        RelogResultDisplay.Text = RelogResultDisplay.Text.Replace($"[再試行 - {format}] 実行中...", altResult);
                    });
                    
                    if (altProcess.ExitCode == 0 && File.Exists(tempCsvPath))
                    {
                        LogInfo($"Success with time format: {format}");
                        break;
                    }
                    else
                    {
                        LogInfo($"Failed with time format: {format}, exit code: {altProcess.ExitCode}");
                    }
                }
                
                // 全ての形式で失敗した場合は時間制約なしで実行
                if (!File.Exists(tempCsvPath))
                {
                    LogInfo("All time formats failed, falling back to no time constraints");
                    var fallbackArguments = $"\"{_currentBlgFile}\" -f CSV -o \"{tempCsvPath}\" -y";
                    
                    // UIにフォールバックコマンドを表示
                    await Dispatcher.InvokeAsync(() =>
                    {
                        RelogCommandDisplay.Text += $"\n\n[フォールバック] relog.exe {fallbackArguments}";
                        RelogResultDisplay.Text += $"\n\n[フォールバック] 時間制約なしで実行中...";
                    });
                    
                    var fallbackProcessInfo = new ProcessStartInfo
                    {
                        FileName = "relog.exe",
                        Arguments = fallbackArguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.Unicode, // UTF-16対応
                        StandardErrorEncoding = Encoding.Unicode   // UTF-16対応
                    };
                    
                    using var fallbackProcess = new Process { StartInfo = fallbackProcessInfo };
                    fallbackProcess.Start();
                    await fallbackProcess.WaitForExitAsync();
                    
                    // フォールバック実行結果をUIに追加
                    var fallbackOutput = await fallbackProcess.StandardOutput.ReadToEndAsync();
                    var fallbackError = await fallbackProcess.StandardError.ReadToEndAsync();
                    
                    await Dispatcher.InvokeAsync(() =>
                    {
                        var fallbackResult = $"Exit Code: {fallbackProcess.ExitCode}";
                        if (!string.IsNullOrEmpty(fallbackOutput))
                            fallbackResult += $"\nOutput: {fallbackOutput}";
                        if (!string.IsNullOrEmpty(fallbackError))
                            fallbackResult += $"\nError: {fallbackError}";
                        
                        RelogResultDisplay.Text = RelogResultDisplay.Text.Replace("[フォールバック] 時間制約なしで実行中...", fallbackResult);
                    });
                    
                    if (fallbackProcess.ExitCode != 0)
                    {
                        LogError($"Fallback execution also failed - Exit code: {fallbackProcess.ExitCode}, Error: {fallbackError}");
                    }
                }
            }
            
            if (File.Exists(tempCsvPath) && new FileInfo(tempCsvPath).Length > 0)
            {
                progress?.Report("CSVデータを解析中...");
                await LoadSelectedCountersFromCsv(tempCsvPath, counters, progress);
            }
            else
            {
                LogError($"relog.exe failed - Exit code: {process.ExitCode}");
                LogError($"relog.exe error output: {error}");
                LogError($"relog.exe standard output: {output}");
                LogError($"Command line: relog.exe {arguments}");
                throw new Exception($"relog.exe の実行に失敗しました。Exit code: {process.ExitCode}, Error: {error}");
            }
        }
        finally
        {
            // 一時ファイルを削除
            if (File.Exists(tempCsvPath))
            {
                try { File.Delete(tempCsvPath); } catch { }
            }
        }
    }

    /// <summary>
    /// CSVファイルから選択されたカウンターのデータを読み込み
    /// </summary>
    private async Task LoadSelectedCountersFromCsv(string csvPath, List<string> selectedCounters, IProgress<string>? progress)
    {
        await Task.Run(() =>
        {
            var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
            if (lines.Length < 2) return;
            
            var headers = ParseCsvLine(lines[0]);
            var counterColumns = new Dictionary<string, int>();
            
            // Preprocess selectedCounters into a dictionary for fast lookups
            var normalizedCounters = selectedCounters.ToDictionary(
                c => NormalizeCounterPath(c), 
                c => c
            );
            
            // ヘッダーから該当するカウンターのカラムインデックスを取得
            for (int i = 1; i < headers.Count; i++)
            {
                var headerName = headers[i];
                var normalizedHeader = NormalizeCounterPath(headerName);
                
                if (normalizedCounters.TryGetValue(normalizedHeader, out var matchingCounter) ||
                    selectedCounters.Any(c => headerName.Contains(c) || c.Contains(headerName)))
                {
                    counterColumns[matchingCounter ?? headerName] = i;
                }
            }
            
            progress?.Report($"対象カウンター {counterColumns.Count}個 のデータを読み込み中...");
            
            // 各カウンターのデータを読み込み
            foreach (var kvp in counterColumns)
            {
                var counterPath = kvp.Key;
                var columnIndex = kvp.Value;
                var dataPoints = new List<PerformanceDataPoint>();
                
                for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
                {
                    var values = ParseCsvLine(lines[lineIndex]);
                    if (values.Count > columnIndex)
                    {
                        if (DateTime.TryParse(values[0], out var timestamp) &&
                            double.TryParse(values[columnIndex], out var value))
                        {
                            var unit = EstimateUnit(counterPath);
                            var formattedValue = FormatValueWithUnit(value, unit);
                            
                            dataPoints.Add(new PerformanceDataPoint
                            {
                                Counter = counterPath,
                                Value = value,
                                Timestamp = timestamp,
                                FormattedValue = formattedValue,
                                Unit = unit
                            });
                        }
                    }
                }
                
                if (dataPoints.Count > 0)
                {
                    _counterData[counterPath] = dataPoints;
                    
                    // UIスレッドでデータテーブルを更新
                    Dispatcher.Invoke(() =>
                    {
                        AddCounterTab(counterPath);
                    });
                }
            }
        });
    }

    /// <summary>
    /// カウンターパスを正規化（比較用）
    /// </summary>
    private string NormalizeCounterPath(string path)
    {
        return path.Replace("\\", "/").Replace("\"", "").Trim().ToLowerInvariant();
    }

    /// <summary>
    /// CSV行を解析（カンマ区切り、ダブルクォート対応）
    /// </summary>
    private List<string> ParseCsvLine(string csvLine)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        
        for (int i = 0; i < csvLine.Length; i++)
        {
            char c = csvLine[i];
            
            if (c == '"')
            {
                if (inQuotes && i + 1 < csvLine.Length && csvLine[i + 1] == '"')
                {
                    // エスケープされたダブルクォート
                    current.Append('"');
                    i++; // 次の文字をスキップ
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        
        result.Add(current.ToString());
        return result;
    }

    private void LogError(string message)
    {
        try
        {
            // アプリケーション実行ディレクトリのerror.logに出力
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
            File.AppendAllText(logPath, logMessage);
            
            // 初回ログ出力時にパスを表示
            if (!File.Exists(logPath + ".logged"))
            {
                File.WriteAllText(logPath + ".logged", ""); // フラグファイル作成
                Debug.WriteLine($"Error log file location: {logPath}");
            }
        }
        catch
        {
            // ログ出力に失敗した場合は何もしない
        }
    }

    private void LogInfo(string message)
    {
        try
        {
            // アプリケーション実行ディレクトリのerror.logに出力（デバッグ情報も同じファイルに）
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}\n";
            File.AppendAllText(logPath, logMessage);
        }
        catch
        {
            // ログ出力に失敗した場合は何もしない
        }
    }
}
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Dictionary<string, List<PerformanceDataPoint>> _counterData = new();
    private string? _currentBlgFile;

    public MainWindow()
    {
        InitializeComponent();
        InitializeChart();
    }

    private void InitializeChart()
    {
        // ScottPlotは現在無効化されています
        // 後でチャート機能を実装する予定です
    }

    private void OpenBlgFile_Click(object sender, RoutedEventArgs e)
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
                LoadBlgFile(openFileDialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルの読み込みに失敗しました: {ex.Message}", 
                              "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                LogError($"Failed to load BLG file: {ex}");
            }
        }
    }

    private void LoadBlgFile(string fileName)
    {
        _currentBlgFile = fileName;
        
        // UI状態をリセット
        CounterPanel.Children.Clear();
        // PerformanceChart.Plot.Clear(); // ScottPlot機能は無効化中
        DataTabControl.Items.Clear();
        _counterData.Clear();

        // BLGファイルを解析（シミュレーション）
        var counters = ParseBlgFile(fileName);
        
        // カウンター一覧を表示
        foreach (var counter in counters)
        {
            var checkBox = new CheckBox
            {
                Content = counter,
                Margin = new Thickness(5, 2, 5, 2),
                Tag = counter
            };
            checkBox.Checked += CounterCheckBox_Checked;
            checkBox.Unchecked += CounterCheckBox_Unchecked;
            
            CounterPanel.Children.Add(checkBox);
        }

        NoDataMessage.Visibility = Visibility.Collapsed;
        
        MessageBox.Show($"BLGファイルが読み込まれました。\n{counters.Count}個のカウンターが見つかりました。", 
                       "読み込み完了", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private List<string> ParseBlgFile(string fileName)
    {
        // 実際の実装では、PDH APIやWMIを使用してBLGファイルを解析します
        // ここではサンプルデータを生成
        var counters = new List<string>
        {
            "\\Processor(_Total)\\% Processor Time",
            "\\Memory\\Available MBytes",
            "\\PhysicalDisk(_Total)\\Disk Reads/sec",
            "\\PhysicalDisk(_Total)\\Disk Writes/sec",
            "\\Network Interface(*)\\Bytes Total/sec",
            "\\System\\Context Switches/sec",
            "\\Process(_Total)\\Working Set"
        };

        // サンプルデータを生成
        var random = new Random();
        var startTime = DateTime.Now.AddMinutes(-30);
        
        foreach (var counter in counters)
        {
            var dataPoints = new List<PerformanceDataPoint>();
            
            for (int i = 0; i < 180; i++) // 30分間、10秒間隔
            {
                var timestamp = startTime.AddSeconds(i * 10);
                var value = GenerateSampleValue(counter, random, i);
                
                dataPoints.Add(new PerformanceDataPoint
                {
                    Timestamp = timestamp,
                    Value = value,
                    Counter = counter
                });
            }
            
            _counterData[counter] = dataPoints;
        }

        return counters;
    }

    private double GenerateSampleValue(string counter, Random random, int index)
    {
        // カウンターの種類に応じてリアルなサンプルデータを生成
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

    private void CounterCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.Tag is string counter)
        {
            AddCounterToChart(counter);
            AddCounterTab(counter);
        }
    }

    private void CounterCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.Tag is string counter)
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
        foreach (CheckBox checkBox in CounterPanel.Children.OfType<CheckBox>())
        {
            checkBox.IsChecked = true;
        }
    }

    private void UnselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (CheckBox checkBox in CounterPanel.Children.OfType<CheckBox>())
        {
            checkBox.IsChecked = false;
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
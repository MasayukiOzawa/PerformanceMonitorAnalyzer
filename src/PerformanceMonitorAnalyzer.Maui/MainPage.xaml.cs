using System.ComponentModel;
using System.Collections.ObjectModel;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using Newtonsoft.Json;
using System.Text;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// 統計情報表示用のデータクラス
/// </summary>
public class CounterStatisticsItem
{
    public string CounterName { get; set; } = string.Empty;
    public string Average { get; set; } = string.Empty;
    public string Maximum { get; set; } = string.Empty;
    public string Minimum { get; set; } = string.Empty;
}

/// <summary>
/// CollectionViewで使用するカウンターアイテムクラス
/// </summary>
public class CounterTreeNode : INotifyPropertyChanged
{
    private bool _isChecked = false;
    private CounterTreeNode? _parent;
    
    public string DisplayName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public ObservableCollection<CounterTreeNode> Children { get; set; } = new();
    public NodeType Type { get; set; } = NodeType.Counter;
    
    public bool IsLeaf => Children.Count == 0;
    
    /// <summary>
    /// チェック状態（MAUI用に簡素化）
    /// </summary>
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked != value)
            {
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                
                // 子要素も同じ状態に設定
                foreach (var child in Children)
                {
                    child.IsChecked = value;
                }
                
                // 親要素に通知
                _parent?.UpdateFromChild();
            }
        }
    }
    
    public CounterTreeNode? Parent
    {
        get => _parent;
        set => _parent = value;
    }
    
    private void UpdateFromChild()
    {
        var checkedChildren = Children.Count(c => c.IsChecked);
        if (checkedChildren == 0)
            _isChecked = false;
        else if (checkedChildren == Children.Count)
            _isChecked = true;
        else
            _isChecked = false; // 部分選択の場合（MAUIでは簡素化）
        
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        _parent?.UpdateFromChild();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public enum NodeType
{
    Object,
    Instance, 
    Counter
}

public partial class MainPage : ContentPage
{
    private BlgFileAnalyzer? _blgAnalyzer;
    private CounterPattern? _counterPattern;
    private ObservableCollection<CounterTreeNode> _counterItems = new();
    private string? _currentBlgFilePath;
    private CancellationTokenSource? _cancellationTokenSource;
    
    public MainPage()
    {
        InitializeComponent();
        CounterCollectionView.ItemsSource = _counterItems;
        
        InitializeAsync();
    }
    
    private async void InitializeAsync()
    {
        try
        {
            _counterPattern = new CounterPattern();
            await LoadCounterPatternsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("初期化エラー", $"初期化中にエラーが発生しました: {ex.Message}", "OK");
        }
    }
    
    private async Task LoadCounterPatternsAsync()
    {
        try
        {
            if (_counterPattern != null)
            {
                await _counterPattern.LoadPatternsAsync();
                var patterns = _counterPattern.GetPatterns();
                
                // Pickerにパターンを追加
                PatternPicker.ItemsSource = patterns.Select(p => p.Name).ToList();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("パターン読み込みエラー", $"カウンターパターンの読み込みに失敗しました: {ex.Message}", "OK");
        }
    }
    
    private async void OpenBlgFile_Click(object sender, EventArgs e)
    {
        try
        {
            var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".blg" } },
                { DevicePlatform.macOS, new[] { "blg" } },
                { DevicePlatform.iOS, new[] { "public.data" } },
                { DevicePlatform.Android, new[] { "*/*" } }
            });

            var result = await FilePicker.PickAsync(new PickOptions
            {
                FileTypes = fileTypes,
                PickerTitle = "BLGファイルを選択してください"
            });

            if (result != null)
            {
                await LoadBlgFileAsync(result.FullPath);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("ファイル選択エラー", $"ファイルの選択中にエラーが発生しました: {ex.Message}", "OK");
        }
    }
    
    private async Task LoadBlgFileAsync(string filePath)
    {
        try
        {
            ProgressFrame.IsVisible = true;
            ProgressStatusText.Text = "BLGファイルを解析しています...";
            
            _currentBlgFilePath = filePath;
            FileNameDisplay.Text = Path.GetFileName(filePath);
            
            _blgAnalyzer = new BlgFileAnalyzer();
            
            var progress = new Progress<string>(status =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ProgressStatusText.Text = status;
                });
            });
            
            var success = await _blgAnalyzer.OpenBlgFileAsync(filePath, progress);
            
            if (success)
            {
                var counters = await _blgAnalyzer.GetAvailableCountersAsync();
                BuildCounterTree(counters);
                
                // UI状態を更新
                ExecuteButton.IsEnabled = true;
                TimeRangeExpander.IsVisible = true;
                
                await DisplayAlert("成功", "BLGファイルの読み込みが完了しました。", "OK");
            }
            else
            {
                await DisplayAlert("エラー", "BLGファイルの読み込みに失敗しました。", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("読み込みエラー", $"ファイルの読み込み中にエラーが発生しました: {ex.Message}", "OK");
        }
        finally
        {
            ProgressFrame.IsVisible = false;
        }
    }
    
    private void BuildCounterTree(List<string> counterPaths)
    {
        _counterItems.Clear();
        
        var rootNodes = new Dictionary<string, CounterTreeNode>();
        
        foreach (var path in counterPaths)
        {
            var parts = path.Split('\\');
            if (parts.Length < 2) continue;
            
            var objectName = parts[1];
            var instanceName = parts.Length > 2 && !string.IsNullOrEmpty(parts[2]) ? parts[2] : "(総計)";
            var counterName = parts.Length > 3 ? parts[3] : parts.LastOrDefault() ?? "";
            
            // オブジェクトノードを取得または作成
            if (!rootNodes.ContainsKey(objectName))
            {
                rootNodes[objectName] = new CounterTreeNode
                {
                    DisplayName = objectName,
                    Type = NodeType.Object,
                    FullPath = $"\\{objectName}"
                };
            }
            
            var objectNode = rootNodes[objectName];
            
            // インスタンスノードを取得または作成
            var instanceNode = objectNode.Children.FirstOrDefault(c => c.DisplayName == instanceName);
            if (instanceNode == null)
            {
                instanceNode = new CounterTreeNode
                {
                    DisplayName = instanceName,
                    Type = NodeType.Instance,
                    FullPath = $"\\{objectName}\\{instanceName}",
                    Parent = objectNode
                };
                objectNode.Children.Add(instanceNode);
            }
            
            // カウンターノードを作成
            var counterNode = new CounterTreeNode
            {
                DisplayName = counterName,
                Type = NodeType.Counter,
                FullPath = path,
                Parent = instanceNode
            };
            instanceNode.Children.Add(counterNode);
        }
        
        foreach (var node in rootNodes.Values.OrderBy(n => n.DisplayName))
        {
            _counterItems.Add(node);
        }
    }
    
    private void PatternPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        ApplyPatternButton.IsEnabled = PatternPicker.SelectedIndex >= 0;
    }
    
    private async void ApplyPattern_Click(object sender, EventArgs e)
    {
        try
        {
            if (PatternPicker.SelectedIndex < 0 || _counterPattern == null)
                return;
            
            var selectedPatternName = PatternPicker.Items[PatternPicker.SelectedIndex];
            var pattern = _counterPattern.GetPatterns().FirstOrDefault(p => p.Name == selectedPatternName);
            
            if (pattern != null)
            {
                ApplyCounterPattern(pattern);
                await DisplayAlert("パターン適用", $"パターン '{pattern.Name}' を適用しました。", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("パターン適用エラー", $"パターンの適用中にエラーが発生しました: {ex.Message}", "OK");
        }
    }
    
    private void ApplyCounterPattern(CounterPatternDefinition pattern)
    {
        // すべてのカウンターを一旦無効化
        SetAllCountersChecked(false);
        
        // パターンに一致するカウンターを有効化
        foreach (var patternPath in pattern.CounterPaths)
        {
            SelectCountersByPattern(patternPath);
        }
    }
    
    private void SetAllCountersChecked(bool isChecked)
    {
        foreach (var item in _counterItems)
        {
            item.IsChecked = isChecked;
        }
    }
    
    private void SelectCountersByPattern(string pattern)
    {
        foreach (var objectNode in _counterItems)
        {
            foreach (var instanceNode in objectNode.Children)
            {
                foreach (var counterNode in instanceNode.Children)
                {
                    if (IsPatternMatch(counterNode.FullPath, pattern))
                    {
                        counterNode.IsChecked = true;
                    }
                }
            }
        }
    }
    
    private bool IsPatternMatch(string counterPath, string pattern)
    {
        // 簡単なワイルドカードマッチング
        if (pattern.Contains("*"))
        {
            var regexPattern = "^" + pattern.Replace("*", ".*").Replace("?", ".") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(counterPath, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        
        return counterPath.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
    
    private void CounterCheckBox_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        // チェック状態の変更はCounterTreeNodeで処理される
    }
    
    private async void ExecuteSelectedCounters_Click(object sender, EventArgs e)
    {
        try
        {
            var selectedCounters = GetSelectedCounters();
            if (selectedCounters.Count == 0)
            {
                await DisplayAlert("警告", "カウンターが選択されていません。", "OK");
                return;
            }
            
            ProgressFrame.IsVisible = true;
            ProgressStatusText.Text = "選択されたカウンターのデータを読み込み中...";
            
            await LoadSelectedCountersDataAsync(selectedCounters);
            
            await DisplayAlert("完了", "カウンターデータの読み込みが完了しました。", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("実行エラー", $"カウンターデータの読み込み中にエラーが発生しました: {ex.Message}", "OK");
        }
        finally
        {
            ProgressFrame.IsVisible = false;
        }
    }
    
    private List<string> GetSelectedCounters()
    {
        var selected = new List<string>();
        
        foreach (var objectNode in _counterItems)
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
    
    private async Task LoadSelectedCountersDataAsync(List<string> selectedCounters)
    {
        if (_blgAnalyzer == null) return;
        
        var counterDataMap = new Dictionary<string, List<BlgFileAnalyzer.CounterDataPoint>>();
        
        foreach (var counterPath in selectedCounters)
        {
            try
            {
                var data = await _blgAnalyzer.GetCounterDataAsync(counterPath);
                counterDataMap[counterPath] = data;
            }
            catch (Exception ex)
            {
                // 個別のカウンターエラーはログに記録して続行
                System.Diagnostics.Debug.WriteLine($"カウンター '{counterPath}' の読み込みに失敗: {ex.Message}");
            }
        }
        
        // 統計情報を更新
        UpdateStatistics(counterDataMap);
        
        // データテーブルを更新
        UpdateDataTables(counterDataMap);
    }
    
    private void UpdateStatistics(Dictionary<string, List<BlgFileAnalyzer.CounterDataPoint>> counterDataMap)
    {
        StatisticsStackLayout.Children.Clear();
        
        foreach (var kvp in counterDataMap)
        {
            var counterPath = kvp.Key;
            var dataPoints = kvp.Value;
            
            if (dataPoints.Count == 0) continue;
            
            var values = dataPoints.Select(dp => dp.Value).ToList();
            var average = values.Average();
            var maximum = values.Max();
            var minimum = values.Min();
            
            var itemFrame = new Frame
            {
                BorderColor = Color.FromArgb("#E0E0E0"),
                BackgroundColor = Color.FromArgb("#F9F9F9"),
                Margin = new Thickness(0, 0, 0, 5),
                Padding = new Thickness(10)
            };
            
            var stackLayout = new StackLayout();
            
            stackLayout.Children.Add(new Label
            {
                Text = counterPath,
                FontAttributes = FontAttributes.Bold,
                FontSize = 12
            });
            
            var statsGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                }
            };
            
            statsGrid.Children.Add(new Label
            {
                Text = $"平均: {average:F2}",
                TextColor = Color.FromArgb("#008000"),
                FontAttributes = FontAttributes.Bold
            });
            Grid.SetColumn(statsGrid.Children.Last(), 0);
            
            statsGrid.Children.Add(new Label
            {
                Text = $"最大: {maximum:F2}",
                TextColor = Color.FromArgb("#B40000"),
                FontAttributes = FontAttributes.Bold
            });
            Grid.SetColumn(statsGrid.Children.Last(), 1);
            
            statsGrid.Children.Add(new Label
            {
                Text = $"最小: {minimum:F2}",
                TextColor = Color.FromArgb("#0000B4"),
                FontAttributes = FontAttributes.Bold
            });
            Grid.SetColumn(statsGrid.Children.Last(), 2);
            
            stackLayout.Children.Add(statsGrid);
            itemFrame.Content = stackLayout;
            
            StatisticsStackLayout.Children.Add(itemFrame);
        }
        
        StatisticsFrame.IsVisible = StatisticsStackLayout.Children.Count > 0;
    }
    
    private void UpdateDataTables(Dictionary<string, List<BlgFileAnalyzer.CounterDataPoint>> counterDataMap)
    {
        DataTabStackLayout.Children.Clear();
        
        foreach (var kvp in counterDataMap)
        {
            var counterPath = kvp.Key;
            var dataPoints = kvp.Value;
            
            var tabFrame = new Frame
            {
                BorderColor = Color.FromArgb("#E0E0E0"),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10)
            };
            
            var tabContent = new StackLayout();
            
            tabContent.Children.Add(new Label
            {
                Text = counterPath,
                FontAttributes = FontAttributes.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10)
            });
            
            var scrollView = new ScrollView
            {
                HeightRequest = 200
            };
            
            var dataStackLayout = new StackLayout();
            
            // ヘッダー
            var headerGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(150) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                BackgroundColor = Color.FromArgb("#F0F0F0"),
                Padding = new Thickness(5)
            };
            
            headerGrid.Children.Add(new Label { Text = "タイムスタンプ", FontAttributes = FontAttributes.Bold });
            Grid.SetColumn(headerGrid.Children.Last(), 0);
            
            headerGrid.Children.Add(new Label { Text = "値", FontAttributes = FontAttributes.Bold });
            Grid.SetColumn(headerGrid.Children.Last(), 1);
            
            dataStackLayout.Children.Add(headerGrid);
            
            // データ行
            foreach (var dataPoint in dataPoints.Take(100)) // 最初の100件のみ表示
            {
                var rowGrid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(150) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    },
                    Padding = new Thickness(5)
                };
                
                rowGrid.Children.Add(new Label { Text = dataPoint.Timestamp.ToString("yyyy/MM/dd HH:mm:ss") });
                Grid.SetColumn(rowGrid.Children.Last(), 0);
                
                rowGrid.Children.Add(new Label { Text = dataPoint.Value.ToString("F2") });
                Grid.SetColumn(rowGrid.Children.Last(), 1);
                
                dataStackLayout.Children.Add(rowGrid);
            }
            
            scrollView.Content = dataStackLayout;
            tabContent.Children.Add(scrollView);
            
            tabFrame.Content = tabContent;
            DataTabStackLayout.Children.Add(tabFrame);
        }
    }
    
    private void SelectAll_Click(object sender, EventArgs e)
    {
        SetAllCountersChecked(true);
    }
    
    private void UnselectAll_Click(object sender, EventArgs e)
    {
        SetAllCountersChecked(false);
    }
    
    private void TimeSlider_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        // 時間範囲スライダーの値変更処理
        UpdateTimeRangeDisplay();
    }
    
    private void UpdateTimeRangeDisplay()
    {
        // 簡単な実装：スライダーの値をそのまま表示
        var startPercent = StartTimeSlider.Value;
        var endPercent = EndTimeSlider.Value;
        
        StartTimeText.Text = $"{startPercent:F0}%";
        EndTimeText.Text = $"{endPercent:F0}%";
    }
    
    private async void OpenPatternConfig_Click(object sender, EventArgs e)
    {
        await LoadCounterPatternsAsync();
        await DisplayAlert("パターン設定", "パターン設定を再読み込みしました。", "OK");
    }
    
    private async void Exit_Click(object sender, EventArgs e)
    {
        await Application.Current.MainPage.DisplayAlert("終了", "アプリケーションを終了しますか？", "はい", "いいえ");
        // MAUIでは直接終了できないため、メッセージのみ表示
    }
    
    private void CloseAllTabs_Click(object sender, EventArgs e)
    {
        DataTabStackLayout.Children.Clear();
    }
    
    private async void ExportAllDataToCsv_Click(object sender, EventArgs e)
    {
        try
        {
            await DisplayAlert("CSV出力", "CSV出力機能は実装予定です。", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("CSV出力エラー", $"CSV出力中にエラーが発生しました: {ex.Message}", "OK");
        }
    }
}
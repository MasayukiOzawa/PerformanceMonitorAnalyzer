using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Documents;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using ScottPlot;
using ScottPlot.WPF;

namespace PerformanceMonitorAnalyzer;

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
    private string? _actualComputerName;
    private TimeSpan _samplingInterval = TimeSpan.Zero;
    
    // ScottPlot用のプロパティ
    private readonly Dictionary<string, ScottPlot.Plottables.Scatter> _chartSeries = new();
    

    private readonly Dictionary<string, ScottPlot.Plottables.FillY> _areaChartSeries = new();
    private ChartType _currentChartType = ChartType.LineChart;
    private CounterValueMode _currentValueMode = CounterValueMode.RawValue;
    
    // カウンターごとのスケール設定を管理
    private readonly Dictionary<string, double> _counterScales = new();
    
    // スケールコントロール更新中フラグ
    private bool _isUpdatingScaleControls = false;
    private bool _isInitializingBulkScaleComboBox = false;
    private bool _isSynchronizingModeControls = false;
    
    // パターン管理機能
    private CounterPatternManager? _patternManager;
    
    // 凡例管理用のプロパティ
    private readonly ObservableCollection<LegendItem> _legendItems = new();
    private readonly Dictionary<string, bool> _seriesVisibility = new();
    private readonly Dictionary<string, ScottPlot.Color> _counterLineColors = new(StringComparer.Ordinal);
    private bool _isBulkLegendVisibilityUpdate = false;
    private readonly HashSet<string> _highlightedLegendCounterPaths = new(StringComparer.Ordinal);
    private const float DefaultLineWidth = 2f;
    private const float HighlightedLineWidth = 4f;
    private const float StackedAreaOutlineWidth = 1f;
    private const double StatisticsPanelMinHeight = 80;
    private const double StatisticsPanelMaxHeight = 450;
    private const double BottomPanelMinHeight = 100;
    private const double BottomPanelMaxHeight = 750;
    
    // グラフリサイズハンドル関連
    private bool _isResizing = false;
    private Point _resizeStartPoint;
    private Size _resizeStartSize;

    // カウンター選択エリアの表示制御
    private bool _isCounterPanelVisible = true;
    private GridLength _lastCounterPanelWidth = new(350);
    private bool _isLegendPanelCollapsed = false;
    private GridLength _lastLegendPanelWidth = new(250);
    private bool _isStatisticsPanelCollapsed = false;
    private GridLength _lastStatisticsPanelHeight = new(170);
    private bool _isBottomPanelCollapsed = false;
    private GridLength _lastBottomPanelHeight = new(400);
    private GridLength _lastScalePanelWidth = new(230);
    private bool _isScalePanelCollapsed = false;
    private bool _isManualYAxisRangeEnabled = false;
    private double _manualYAxisMin = 0;
    private double _manualYAxisMax = 100;
    
    
    // ログ機能
    private readonly ObservableCollection<LogEntry> _operationLogs = new();
    private readonly ObservableCollection<LogEntry> _errorLogs = new();

    public MainWindow()
    {
        InitializeComponent();
        InitializeChart();
        InitializeBulkScaleComboBox();
        InitializeCounterPanelControls();
        CounterTreeView.ItemsSource = _counterTreeNodes;
        
        // 凡例の初期化
        InitializeLegend();
        InitializeLegendPanelControls();
        InitializeStatisticsPanelControls();
        InitializeBottomPanelControls();
        
        // キーボードショートカットの設定
        this.KeyDown += MainWindow_KeyDown;
        
        // ウィンドウサイズ監視の初期化
        InitializeWindowSizeTracking();
        
        // グラフサイズ監視の初期化
        InitializeGraphSizeTracking();
        
        // パターン管理機能の初期化
        _ = InitializePatternManagerAsync();
        
        // ログタブの初期化
        InitializeLogTabs();
    }

    private void InitializeChart()
    {
        // ScottPlot グラフの初期設定
        PerformanceChart.Plot.Clear();
        
        PerformanceChart.Plot.XLabel("時間");
        PerformanceChart.Plot.YLabel(GetCurrentYAxisLabel());
        
        // 時間軸の設定
        PerformanceChart.Plot.Axes.DateTimeTicksBottom();
        
        // Y軸の初期範囲を設定
        PerformanceChart.Plot.Axes.Left.Min = 0;
        PerformanceChart.Plot.Axes.Left.Max = 100;
        PerformanceChart.Plot.Axes.Left.IsVisible = true;
        
        // ユーザー操作後も現在データに応じたY軸範囲を維持
        PerformanceChart.Plot.RenderManager.RenderFinished += (sender, args) =>
        {
            EnsureYAxisFixedRange();
        };
        
        // グラフ領域のフォントサイズを16に設定
        // 軸ラベルのフォントサイズ設定
        PerformanceChart.Plot.Axes.Bottom.Label.FontSize = 16;
        PerformanceChart.Plot.Axes.Left.Label.FontSize = 16;
        
        // 軸目盛りのフォントサイズ設定
        PerformanceChart.Plot.Axes.Bottom.TickLabelStyle.FontSize = 16;
        PerformanceChart.Plot.Axes.Left.TickLabelStyle.FontSize = 16;
        
        // 凡例を無効化（独立した凡例コンポーネントを使用）
        PerformanceChart.Plot.Legend.IsVisible = false;

        // ScottPlot既定メニューは Open in New Window でNREが発生するため無効化
        // （右クリック時はXAMLで定義したアプリ独自メニューを使用）
        var plotMenu = new WpfPlotMenu(PerformanceChart);
        plotMenu.Clear();
        PerformanceChart.Menu = plotMenu;
        // 右クリックはコンテキストメニュー用途のみとし、ドラッグズームを無効化
        PerformanceChart.UserInputProcessor.RightClickDragZoom(false);
        PerformanceChart.PreviewMouseRightButtonUp -= PerformanceChart_PreviewMouseRightButtonUp;
        PerformanceChart.PreviewMouseRightButtonUp += PerformanceChart_PreviewMouseRightButtonUp;
        
        // グラフの更新
        PerformanceChart.Refresh();
    }

    /// <summary>
    /// Y軸範囲を設定する（通常時は自動、手動設定時は指定範囲）
    /// </summary>
    private void EnsureYAxisFixedRange()
    {
        try
        {
            const double defaultYAxisMin = 0;
            const double defaultYAxisMax = 100;

            var yAxisMin = defaultYAxisMin;
            var yAxisMax = defaultYAxisMax;

            if (_isManualYAxisRangeEnabled)
            {
                yAxisMin = _manualYAxisMin;
                yAxisMax = _manualYAxisMax;
            }
            else if (TryGetCurrentDisplayMax(out var displayMax) && displayMax > defaultYAxisMax)
            {
                yAxisMax = Math.Ceiling(displayMax * 1.1);
            }

            if (Math.Abs(PerformanceChart.Plot.Axes.Left.Min - yAxisMin) > 1e-9 ||
                Math.Abs(PerformanceChart.Plot.Axes.Left.Max - yAxisMax) > 1e-9)
            {
                PerformanceChart.Plot.Axes.Left.Min = yAxisMin;
                PerformanceChart.Plot.Axes.Left.Max = yAxisMax;
                System.Diagnostics.Debug.WriteLine($"Y軸範囲を更新しました: {yAxisMin} - {yAxisMax}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Y軸範囲設定エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// 現在表示中データの最大表示値を取得
    /// </summary>
    private bool TryGetCurrentDisplayMax(out double maxValue)
    {
        maxValue = 0;

        var currentCounters = GetCurrentChartCounters()
            .Where(counter => _seriesVisibility.GetValueOrDefault(counter, true))
            .ToList();

        if (!currentCounters.Any())
        {
            return false;
        }

        bool hasValue = false;
        double currentMax = double.MinValue;
        double stackedUpperBound = 0;

        foreach (var counter in currentCounters)
        {
            var dataPoints = GetDisplayDataPoints(counter);
            if (!dataPoints.Any())
            {
                continue;
            }

            var manualScale = _counterScales.GetValueOrDefault(counter, 1.0);
            var finalScale = manualScale;
            var counterMax = dataPoints.Max(dp => dp.Value * finalScale);

            if (double.IsNaN(counterMax) || double.IsInfinity(counterMax))
            {
                continue;
            }

            hasValue = true;
            currentMax = Math.Max(currentMax, counterMax);

            if (_currentChartType == ChartType.StackedAreaChart && counterMax > 0)
            {
                stackedUpperBound += counterMax;
            }
        }

        if (_currentChartType == ChartType.StackedAreaChart && stackedUpperBound > 0)
        {
            currentMax = Math.Max(currentMax, stackedUpperBound);
            hasValue = true;
        }

        if (!hasValue)
        {
            return false;
        }

        maxValue = currentMax;
        return true;
    }

    /// <summary>
    /// 現在の設定に応じたY軸ラベルを取得
    /// </summary>
    private string GetCurrentYAxisLabel()
    {
        return string.Empty;
    }

    private bool HasVisibleChartData()
    {
        return _chartSeries.Any() || _areaChartSeries.Any();
    }

    private bool HasChartPanelContext()
    {
        return HasVisibleChartData() || _legendItems.Any();
    }

    /// <summary>
    /// 現在の値モードに応じた表示用データポイントを取得
    /// </summary>
    private List<PerformanceDataPoint> GetDisplayDataPoints(string counter)
    {
        if (!_counterData.TryGetValue(counter, out var rawData) || rawData.Count == 0)
        {
            return new List<PerformanceDataPoint>();
        }

        if (_currentValueMode == CounterValueMode.RawValue)
        {
            return rawData;
        }

        if (rawData.Count < 2)
        {
            return new List<PerformanceDataPoint>();
        }

        var unit = EstimateUnit(counter);
        var deltaDataPoints = new List<PerformanceDataPoint>(rawData.Count - 1);

        for (int i = 1; i < rawData.Count; i++)
        {
            var current = rawData[i];
            var previous = rawData[i - 1];
            var deltaValue = current.Value - previous.Value;

            deltaDataPoints.Add(new PerformanceDataPoint
            {
                Counter = counter,
                Value = deltaValue,
                Timestamp = current.Timestamp,
                FormattedValue = FormatValueWithUnit(deltaValue, unit),
                Unit = unit
            });
        }

        return deltaDataPoints;
    }

    /// <summary>
    /// 現在の値モードに応じた最新値を取得
    /// </summary>
    private bool TryGetLatestDisplayValue(string counter, out double latestValue)
    {
        latestValue = 0;

        if (!_counterData.TryGetValue(counter, out var rawData) || rawData.Count == 0)
        {
            return false;
        }

        if (_currentValueMode == CounterValueMode.RawValue)
        {
            latestValue = rawData[^1].Value;
            return true;
        }

        if (rawData.Count < 2)
        {
            return false;
        }

        latestValue = rawData[^1].Value - rawData[^2].Value;
        return true;
    }

    /// <summary>
    /// 値モード変更時に全データタブを再生成
    /// </summary>
    private void RefreshAllDataTabsForCurrentMode()
    {
        var currentTabCounters = DataTabControl.Items
            .Cast<TabItem>()
            .Select(tab => tab.Tag as string)
            .Where(counter => !string.IsNullOrEmpty(counter))
            .Cast<string>()
            .ToList();

        if (!currentTabCounters.Any())
        {
            return;
        }

        var selectedCounter = (DataTabControl.SelectedItem as TabItem)?.Tag as string;

        DataTabControl.Items.Clear();

        foreach (var counter in currentTabCounters)
        {
            AddCounterTab(counter);
        }

        if (!string.IsNullOrEmpty(selectedCounter))
        {
            var selectedTab = DataTabControl.Items.Cast<TabItem>()
                .FirstOrDefault(tab => string.Equals(tab.Tag as string, selectedCounter, StringComparison.Ordinal));
            if (selectedTab != null)
            {
                DataTabControl.SelectedItem = selectedTab;
            }
        }
    }

    /// <summary>
    /// 値モード変更時のイベントハンドラー
    /// </summary>
    private void ValueMode_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isSynchronizingModeControls || sender is not RadioButton radioButton)
            {
                return;
            }

            var newValueMode = radioButton.Name switch
            {
                "DeltaValueModeRadio" => CounterValueMode.DeltaFromPrevious,
                _ => CounterValueMode.RawValue
            };

            ApplyValueMode(newValueMode, refreshDisplays: true, logChange: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in ValueMode_Changed: {ex.Message}");
            LogError($"値モード変更エラー: {ex}");
        }
    }

    /// <summary>
    /// カウンター選択エリア制御の初期化
    /// </summary>
    private void InitializeCounterPanelControls()
    {
        _isCounterPanelVisible = CounterPanelGroupBox.Visibility == Visibility.Visible;

        if (CounterPanelColumn.Width.Value > 0)
        {
            _lastCounterPanelWidth = CounterPanelColumn.Width;
        }

        CounterPanelSplitterColumn.Width = new GridLength(22);

        UpdateCounterPanelControls();
    }

    /// <summary>
    /// カウンター選択エリアの表示/非表示切替
    /// </summary>
    private void CounterPanelVisibilityButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isCounterPanelVisible)
            {
                HideCounterPanel();
                AddOperationLog(LogLevel.Info, "カウンター選択エリアを非表示にしました。");
            }
            else
            {
                ShowCounterPanel();
                AddOperationLog(LogLevel.Info, "カウンター選択エリアを表示しました。");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in CounterPanelVisibilityButton_Click: {ex.Message}");
            LogError($"カウンター選択エリア表示切替エラー: {ex}");
        }
    }

    /// <summary>
    /// カウンター選択エリアを非表示にする
    /// </summary>
    private void HideCounterPanel()
    {
        if (!_isCounterPanelVisible)
        {
            return;
        }

        if (CounterPanelColumn.Width.Value > 0)
        {
            _lastCounterPanelWidth = CounterPanelColumn.Width;
        }

        CounterPanelGroupBox.Visibility = Visibility.Collapsed;
        CounterPanelColumn.MinWidth = 0;
        CounterPanelColumn.Width = new GridLength(0);
        CounterPanelSplitterColumn.Width = new GridLength(22);
        _isCounterPanelVisible = false;

        UpdateCounterPanelControls();
    }

    /// <summary>
    /// カウンター選択エリアを表示する
    /// </summary>
    private void ShowCounterPanel()
    {
        if (_isCounterPanelVisible)
        {
            return;
        }

        CounterPanelGroupBox.Visibility = Visibility.Visible;
        CounterPanelColumn.MinWidth = 220;
        CounterPanelColumn.Width = _lastCounterPanelWidth.Value > 0 ? _lastCounterPanelWidth : new GridLength(350);
        CounterPanelSplitterColumn.Width = new GridLength(22);
        _isCounterPanelVisible = true;

        UpdateCounterPanelControls();
    }

    /// <summary>
    /// カウンター選択エリア制御UIの表示状態を更新する
    /// </summary>
    private void UpdateCounterPanelControls()
    {
        CounterPanelToggleButton.Content = _isCounterPanelVisible ? "◀" : "▶";
        CounterPanelToggleButton.ToolTip = _isCounterPanelVisible
            ? "クリックでカウンター選択エリアを非表示にします"
            : "クリックでカウンター選択エリアを表示します";
    }

    /// <summary>
    /// 凡例表示エリア制御の初期化
    /// </summary>
    private void InitializeLegendPanelControls()
    {
        if (LegendColumn.Width.Value > 0)
        {
            _lastLegendPanelWidth = LegendColumn.Width;
        }

        UpdateLegendPanelControls(hasChartData: HasChartPanelContext());
    }

    /// <summary>
    /// 凡例表示エリアの表示/非表示切替
    /// </summary>
    private void LegendPanelToggleButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _isLegendPanelCollapsed = !_isLegendPanelCollapsed;
            UpdateLegendPanelControls(hasChartData: HasChartPanelContext());
            AddOperationLog(LogLevel.Info, _isLegendPanelCollapsed
                ? "凡例を非表示にしました。"
                : "凡例を表示しました。");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in LegendPanelToggleButton_Click: {ex.Message}");
            LogError($"凡例表示切替エラー: {ex}");
        }
    }

    /// <summary>
    /// 凡例表示エリア制御UIの表示状態を更新する
    /// </summary>
    private void UpdateLegendPanelControls(bool hasChartData)
    {
        if (!hasChartData)
        {
            LegendDividerHost.Visibility = Visibility.Collapsed;

            if (LegendColumn.Width.Value > 0)
            {
                _lastLegendPanelWidth = LegendColumn.Width;
            }

            LegendGroupBox.Visibility = Visibility.Collapsed;
            LegendColumn.MinWidth = 0;
            LegendColumn.Width = new GridLength(0);
            LegendSplitterColumn.Width = new GridLength(0);
            return;
        }

        LegendDividerHost.Visibility = Visibility.Visible;
        LegendPanelToggleButton.Content = _isLegendPanelCollapsed ? "◀" : "▶";
        LegendPanelToggleButton.ToolTip = _isLegendPanelCollapsed
            ? "クリックで凡例を表示します"
            : "クリックで凡例を非表示にします";

        if (_isLegendPanelCollapsed)
        {
            if (LegendColumn.Width.Value > 0)
            {
                _lastLegendPanelWidth = LegendColumn.Width;
            }

            LegendGroupBox.Visibility = Visibility.Collapsed;
            LegendColumn.MinWidth = 0;
            LegendColumn.Width = new GridLength(0);
            LegendSplitterColumn.Width = new GridLength(22);
            return;
        }

        LegendGroupBox.Visibility = Visibility.Visible;
        LegendColumn.MinWidth = 150;
        LegendSplitterColumn.Width = new GridLength(22);

        if (LegendColumn.Width.Value <= 0)
        {
            LegendColumn.Width = _lastLegendPanelWidth.Value > 0 ? _lastLegendPanelWidth : new GridLength(250);
        }
    }

    /// <summary>
    /// 統計情報エリア制御の初期化
    /// </summary>
    private void InitializeStatisticsPanelControls()
    {
        if (StatisticsAreaRowDefinition.Height.Value > 0)
        {
            _lastStatisticsPanelHeight = StatisticsAreaRowDefinition.Height;
        }

        UpdateStatisticsPanelControls(hasChartData: HasChartPanelContext());
    }

    /// <summary>
    /// 統計情報エリアの表示/非表示切替
    /// </summary>
    private void StatisticsPanelToggleButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _isStatisticsPanelCollapsed = !_isStatisticsPanelCollapsed;
            UpdateStatisticsPanelControls(hasChartData: HasChartPanelContext());
            AddOperationLog(LogLevel.Info, _isStatisticsPanelCollapsed
                ? "統計情報エリアを最小化しました。"
                : "統計情報エリアを表示しました。");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in StatisticsPanelToggleButton_Click: {ex.Message}");
            LogError($"統計情報エリア表示切替エラー: {ex}");
        }
    }

    /// <summary>
    /// 統計情報エリア制御UIの表示状態を更新する
    /// </summary>
    private void UpdateStatisticsPanelControls(bool hasChartData)
    {
        if (!hasChartData)
        {
            if (StatisticsAreaRowDefinition.Height.Value > 0)
            {
                _lastStatisticsPanelHeight = StatisticsAreaRowDefinition.Height;
            }

            StatisticsDividerBorder.Visibility = Visibility.Collapsed;
            StatisticsGridSplitter.Visibility = Visibility.Collapsed;
            StatisticsPanelToggleButton.Visibility = Visibility.Collapsed;
            StatisticsBorder.Visibility = Visibility.Collapsed;
            StatisticsDividerRowDefinition.Height = new GridLength(0);
            StatisticsAreaRowDefinition.MinHeight = 0;
            StatisticsAreaRowDefinition.Height = new GridLength(0);
            return;
        }

        StatisticsDividerBorder.Visibility = Visibility.Visible;
        StatisticsPanelToggleButton.Visibility = Visibility.Visible;
        StatisticsDividerRowDefinition.Height = new GridLength(22);
        StatisticsPanelToggleButton.Content = _isStatisticsPanelCollapsed ? "▲" : "▼";
        StatisticsPanelToggleButton.ToolTip = _isStatisticsPanelCollapsed
            ? "クリックで統計情報エリアを表示します"
            : "クリックで統計情報エリアを最小化します";

        if (_isStatisticsPanelCollapsed)
        {
            if (StatisticsAreaRowDefinition.Height.Value > 0)
            {
                _lastStatisticsPanelHeight = StatisticsAreaRowDefinition.Height;
            }

            StatisticsGridSplitter.Visibility = Visibility.Collapsed;
            StatisticsBorder.Visibility = Visibility.Collapsed;
            StatisticsAreaRowDefinition.MinHeight = 0;
            StatisticsAreaRowDefinition.Height = new GridLength(0);
            return;
        }

        StatisticsGridSplitter.Visibility = Visibility.Visible;
        StatisticsBorder.Visibility = Visibility.Visible;
        StatisticsAreaRowDefinition.MinHeight = StatisticsPanelMinHeight;
        StatisticsAreaRowDefinition.MaxHeight = StatisticsPanelMaxHeight;

        if (StatisticsAreaRowDefinition.Height.Value <= 0)
        {
            StatisticsAreaRowDefinition.Height = _lastStatisticsPanelHeight.Value > 0 ? _lastStatisticsPanelHeight : new GridLength(170);
        }
    }

    /// <summary>
    /// 下部データテーブル / ログ表示エリア制御の初期化
    /// </summary>
    private void InitializeBottomPanelControls()
    {
        if (BottomPanelAreaRowDefinition.Height.Value > 0)
        {
            _lastBottomPanelHeight = BottomPanelAreaRowDefinition.Height;
        }

        UpdateBottomPanelControls();
    }

    /// <summary>
    /// 下部データテーブル / ログ表示エリアの表示/非表示切替
    /// </summary>
    private void BottomPanelToggleButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _isBottomPanelCollapsed = !_isBottomPanelCollapsed;
            UpdateBottomPanelControls();
            AddOperationLog(LogLevel.Info, _isBottomPanelCollapsed
                ? "データテーブル / ログ表示エリアを最小化しました。"
                : "データテーブル / ログ表示エリアを表示しました。");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in BottomPanelToggleButton_Click: {ex.Message}");
            LogError($"データテーブル / ログ表示エリア表示切替エラー: {ex}");
        }
    }

    /// <summary>
    /// 下部データテーブル / ログ表示エリア制御UIの表示状態を更新する
    /// </summary>
    private void UpdateBottomPanelControls()
    {
        BottomPanelToggleButton.Content = _isBottomPanelCollapsed ? "▲" : "▼";
        BottomPanelToggleButton.ToolTip = _isBottomPanelCollapsed
            ? "クリックでデータテーブル / ログ表示エリアを表示します"
            : "クリックでデータテーブル / ログ表示エリアを最小化します";
        BottomPanelGridSplitter.Visibility = _isBottomPanelCollapsed ? Visibility.Collapsed : Visibility.Visible;

        if (_isBottomPanelCollapsed)
        {
            if (BottomPanelAreaRowDefinition.Height.Value > 0)
            {
                _lastBottomPanelHeight = BottomPanelAreaRowDefinition.Height;
            }

            BottomPanelGrid.Visibility = Visibility.Collapsed;
            BottomPanelAreaRowDefinition.MinHeight = 0;
            BottomPanelAreaRowDefinition.Height = new GridLength(0);
            return;
        }

        BottomPanelGrid.Visibility = Visibility.Visible;
        BottomPanelAreaRowDefinition.MinHeight = BottomPanelMinHeight;
        BottomPanelAreaRowDefinition.MaxHeight = BottomPanelMaxHeight;

        if (BottomPanelAreaRowDefinition.Height.Value <= 0)
        {
            BottomPanelAreaRowDefinition.Height = _lastBottomPanelHeight.Value > 0 ? _lastBottomPanelHeight : new GridLength(400);
        }
    }

    /// <summary>
    /// 下部のデータテーブル / ログ表示エリアを必要に応じて再表示する
    /// </summary>
    private void EnsureBottomPanelVisible()
    {
        if (!_isBottomPanelCollapsed)
        {
            return;
        }

        _isBottomPanelCollapsed = false;
        UpdateBottomPanelControls();
    }

    /// <summary>
    /// すべてのトグル対応エリアの状態を一括設定する
    /// </summary>
    private void SetAllTogglePanelsCollapsed(bool collapsed)
    {
        if (collapsed)
        {
            HideCounterPanel();
        }
        else
        {
            ShowCounterPanel();
        }

        var hasChartData = HasChartPanelContext();

        _isLegendPanelCollapsed = collapsed;
        UpdateLegendPanelControls(hasChartData);

        _isStatisticsPanelCollapsed = collapsed;
        UpdateStatisticsPanelControls(hasChartData);

        _isScalePanelCollapsed = collapsed;
        UpdateScaleControlVisibility();

        _isBottomPanelCollapsed = collapsed;
        UpdateBottomPanelControls();
    }

    /// <summary>
    /// トグル対応エリアをすべて折りたたむ
    /// </summary>
    private void CollapseAllPanelsMenu_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetAllTogglePanelsCollapsed(collapsed: true);
            AddOperationLog(LogLevel.Info, "トグル対応エリアを一括で折りたたみました。");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in CollapseAllPanelsMenu_Click: {ex.Message}");
            LogError($"一括折りたたみエラー: {ex}");
        }
    }

    /// <summary>
    /// トグル対応エリアをすべて展開する
    /// </summary>
    private void ExpandAllPanelsMenu_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetAllTogglePanelsCollapsed(collapsed: false);
            AddOperationLog(LogLevel.Info, "トグル対応エリアを一括で展開しました。");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in ExpandAllPanelsMenu_Click: {ex.Message}");
            LogError($"一括展開エラー: {ex}");
        }
    }

    /// <summary>
    /// スケール設定エリアの表示/非表示切替
    /// </summary>
    private void ScalePanelToggleButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _isScalePanelCollapsed = !_isScalePanelCollapsed;
            UpdateScaleControlVisibility();
            AddOperationLog(LogLevel.Info, _isScalePanelCollapsed
                ? "スケール設定エリアを非表示にしました。"
                : "スケール設定エリアを表示しました。");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in ScalePanelToggleButton_Click: {ex.Message}");
            LogError($"スケール設定エリア表示切替エラー: {ex}");
        }
    }

    /// <summary>
    /// グラフタイプ変更時のイベントハンドラー
    /// </summary>
    private void ChartType_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isSynchronizingModeControls || sender is not RadioButton radioButton)
            {
                return;
            }

            var newChartType = radioButton.Name switch
            {
                "LineChartRadio" => ChartType.LineChart,
                "StackedAreaChartRadio" => ChartType.StackedAreaChart,
                _ => ChartType.LineChart
            };

            ApplyChartType(newChartType, refreshChart: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in ChartType_Changed: {ex.Message}");
            LogError($"グラフタイプ変更エラー: {ex}");
        }
    }

    private bool ApplyChartType(ChartType newChartType, bool refreshChart)
    {
        if (_currentChartType == newChartType)
        {
            SyncChartTypeRadioButtons();
            return false;
        }

        _currentChartType = newChartType;
        SyncChartTypeRadioButtons();
        System.Diagnostics.Debug.WriteLine($"Chart type changed to: {_currentChartType}");

        if (refreshChart)
        {
            RefreshChartWithCurrentType();
        }

        return true;
    }

    private bool ApplyValueMode(CounterValueMode newValueMode, bool refreshDisplays, bool logChange)
    {
        if (_currentValueMode == newValueMode)
        {
            SyncValueModeRadioButtons();
            return false;
        }

        _currentValueMode = newValueMode;
        SyncValueModeRadioButtons();

        if (refreshDisplays)
        {
            RefreshChartWithCurrentType();
            RefreshAllDataTabsForCurrentMode();
        }

        if (logChange)
        {
            AddOperationLog(LogLevel.Info, $"値モードを「{GetValueModeDisplayName(_currentValueMode)}」に変更しました。");
        }

        return true;
    }

    private void SyncChartTypeRadioButtons()
    {
        if (LineChartRadio == null || StackedAreaChartRadio == null)
        {
            return;
        }

        _isSynchronizingModeControls = true;
        try
        {
            LineChartRadio.IsChecked = _currentChartType == ChartType.LineChart;
            StackedAreaChartRadio.IsChecked = _currentChartType == ChartType.StackedAreaChart;
        }
        finally
        {
            _isSynchronizingModeControls = false;
        }
    }

    private void SyncValueModeRadioButtons()
    {
        if (RawValueModeRadio == null || DeltaValueModeRadio == null)
        {
            return;
        }

        _isSynchronizingModeControls = true;
        try
        {
            RawValueModeRadio.IsChecked = _currentValueMode == CounterValueMode.RawValue;
            DeltaValueModeRadio.IsChecked = _currentValueMode == CounterValueMode.DeltaFromPrevious;
        }
        finally
        {
            _isSynchronizingModeControls = false;
        }
    }

    private static string GetChartTypeDisplayName(ChartType chartType)
    {
        return chartType == ChartType.StackedAreaChart ? "積み重ね面グラフ" : "折れ線グラフ";
    }

    private static string GetValueModeDisplayName(CounterValueMode valueMode)
    {
        return valueMode == CounterValueMode.DeltaFromPrevious ? "差分" : "Raw";
    }

    /// <summary>
    /// ログタブの初期化
    /// </summary>
    private void InitializeLogTabs()
    {
        try
        {
            // 既存のログタブをクリア（重複防止）
            LogTabControl.Items.Clear();
            
            // 操作ログタブを作成
            var operationLogTab = CreateOperationLogTab();
            LogTabControl.Items.Add(operationLogTab);
            
            // エラーログタブを作成
            var errorLogTab = CreateErrorLogTab();
            LogTabControl.Items.Add(errorLogTab);
            
            // 初期ログメッセージを追加
            AddOperationLog(LogLevel.Info, "アプリケーションが開始されました。");
            
            // error.logファイルからエラーログを読み込み
            LoadErrorLogFromFile();
        }
        catch (Exception ex)
        {
            LogError($"ログタブの初期化に失敗しました: {ex.Message}");
        }
    }

    /// <summary>
    /// 操作ログタブを作成
    /// </summary>
    private TabItem CreateOperationLogTab()
    {
        var tabItem = new TabItem
        {
            Header = "📋 操作ログ",
            Tag = "OperationLog"
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ヘッダー部分
        var headerPanel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(5),
            Background = System.Windows.Media.Brushes.LightBlue
        };
        headerPanel.Children.Add(new TextBlock
        {
            Text = "📋 操作ログ - アプリケーションの操作履歴",
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(10, 5, 10, 5),
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        });
        
        var clearButton = new Button
        {
            Content = "ログクリア",
            Margin = new Thickness(10, 2, 10, 2),
            Padding = new Thickness(8, 2, 8, 2)
        };
        clearButton.Click += (s, e) => ClearOperationLogs();
        headerPanel.Children.Add(clearButton);

        Grid.SetRow(headerPanel, 0);
        grid.Children.Add(headerPanel);

        // ログ表示用DataGrid
        var dataGrid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            ItemsSource = _operationLogs,
            AlternatingRowBackground = System.Windows.Media.Brushes.AliceBlue,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            CanUserReorderColumns = false,
            CanUserResizeRows = false
        };

        // 列を定義
        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "時間",
            Binding = new System.Windows.Data.Binding("FormattedTimestamp"),
            Width = 130
        });

        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "レベル",
            Binding = new System.Windows.Data.Binding("LevelDisplay"),
            Width = 60
        });

        var messageColumn = new DataGridTextColumn
        {
            Header = "メッセージ",
            Binding = new System.Windows.Data.Binding("Message"),
            Width = DataGridLength.SizeToCells
        };
        
        // メッセージ列のスタイルを設定（色分け）
        var messageStyle = new Style(typeof(TextBlock));
        messageStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, 
            new System.Windows.Data.Binding("TextColor")));
        messageColumn.ElementStyle = messageStyle;
        
        dataGrid.Columns.Add(messageColumn);

        Grid.SetRow(dataGrid, 1);
        grid.Children.Add(dataGrid);

        tabItem.Content = grid;
        return tabItem;
    }

    /// <summary>
    /// エラーログタブを作成
    /// </summary>
    private TabItem CreateErrorLogTab()
    {
        var tabItem = new TabItem
        {
            Header = "❌ エラーログ",
            Tag = "ErrorLog"
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ヘッダー部分
        var headerPanel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(5),
            Background = System.Windows.Media.Brushes.LightCoral
        };
        headerPanel.Children.Add(new TextBlock
        {
            Text = "❌ エラーログ - error.logファイルの内容",
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(10, 5, 10, 5),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Foreground = System.Windows.Media.Brushes.White
        });
        
        var refreshButton = new Button
        {
            Content = "ログ再読み込み",
            Margin = new Thickness(10, 2, 10, 2),
            Padding = new Thickness(8, 2, 8, 2)
        };
        refreshButton.Click += (s, e) => LoadErrorLogFromFile();
        headerPanel.Children.Add(refreshButton);

        var clearButton = new Button
        {
            Content = "ログクリア",
            Margin = new Thickness(5, 2, 5, 2),
            Padding = new Thickness(8, 2, 8, 2)
        };
        clearButton.Click += (s, e) => ClearErrorLogs();
        headerPanel.Children.Add(clearButton);

        Grid.SetRow(headerPanel, 0);
        grid.Children.Add(headerPanel);

        // ログ表示用DataGrid
        var dataGrid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            ItemsSource = _errorLogs,
            AlternatingRowBackground = System.Windows.Media.Brushes.MistyRose,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            CanUserReorderColumns = false,
            CanUserResizeRows = false
        };

        // 列を定義
        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "時間",
            Binding = new System.Windows.Data.Binding("FormattedTimestamp"),
            Width = 130
        });

        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "レベル",
            Binding = new System.Windows.Data.Binding("LevelDisplay"),
            Width = 60
        });

        var messageColumn = new DataGridTextColumn
        {
            Header = "メッセージ",
            Binding = new System.Windows.Data.Binding("Message"),
            Width = DataGridLength.SizeToCells
        };
        
        // メッセージ列のスタイルを設定（色分け）
        var messageStyle = new Style(typeof(TextBlock));
        messageStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, 
            new System.Windows.Data.Binding("TextColor")));
        messageColumn.ElementStyle = messageStyle;
        
        dataGrid.Columns.Add(messageColumn);

        Grid.SetRow(dataGrid, 1);
        grid.Children.Add(dataGrid);

        tabItem.Content = grid;
        return tabItem;
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
                AddOperationLog(LogLevel.Error, $"ファイルの読み込みに失敗しました: {ex.Message}");
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
            AddOperationLog(LogLevel.Error, $"コマンドライン引数で指定されたBLGファイルの読み込みに失敗しました: {ex.Message}");
            MessageBox.Show($"コマンドライン引数で指定されたBLGファイルの読み込みに失敗しました: {ex.Message}", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            LogError($"Failed to load BLG file from command line: {ex}");
        }
    }



    private async Task LoadBlgFileAsync(string fileName)
    {
        _currentBlgFile = fileName;
        
        // ファイル名をフルパスで表示
        FileNameDisplay.Text = $"読み込みファイル: {fileName}";
        
        // ファイルサイズを表示
        try
        {
            var fileInfo = new FileInfo(fileName);
            var fileSizeText = FormatFileSize(fileInfo.Length);
            FileSizeDisplay.Text = $"ファイルサイズ: {fileSizeText}";
            FileSizeDisplay.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            FileSizeDisplay.Text = "ファイルサイズ: 取得できません";
            FileSizeDisplay.Visibility = Visibility.Visible;
            LogError($"ファイルサイズの取得に失敗: {ex.Message}");
        }
        
        // プログレスバーを表示
        ProgressGrid.Visibility = Visibility.Visible;
        ProgressStatusText.Text = "BLGファイルを解析中...";
        
        // UI状態をリセット
        _counterTreeNodes.Clear();
        DataTabControl.Items.Clear();
        _counterData.Clear();
        _counterLineColors.Clear();
        
        // 凡例をクリア
        ClearLegendItems();
        
        _actualComputerName = null;
        _samplingInterval = TimeSpan.Zero;
        ComputerNameDisplay.Visibility = Visibility.Collapsed;
        SamplingIntervalDisplay.Visibility = Visibility.Collapsed;

        // ログタブを再初期化
        InitializeLogTabs();

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

            // コンピューター名を表示（最初のカウンターから抽出）
            if (counters.Count > 0)
            {
                var computerName = GetComputerNameFromCounterPath(counters[0]);
                ComputerNameDisplay.Text = $"コンピューター: {computerName}";
                ComputerNameDisplay.Visibility = Visibility.Visible;
            }

            // データ取得間隔を表示
            if (_samplingInterval != TimeSpan.Zero)
            {
                SamplingIntervalDisplay.Text = $"取得間隔: {FormatSamplingInterval(_samplingInterval)}";
                SamplingIntervalDisplay.Visibility = Visibility.Visible;
            }
            else
            {
                // 取得間隔が0の場合でも情報を表示
                SamplingIntervalDisplay.Text = "取得間隔: 取得失敗";
                SamplingIntervalDisplay.Visibility = Visibility.Visible;
            }

            // 時間範囲を検出
            ProgressStatusText.Text = "時間範囲を検出中...";
            var timeRangeDetected = await DetectTimeRangeAsync(fileName, progress);
            if (!timeRangeDetected)
            {
                ExecuteButton.IsEnabled = true;
            }

            NoDataMessage.Visibility = Visibility.Collapsed;
            
            LogError($"Counter tree built successfully with {_counterTreeNodes.Count} root nodes"); // デバッグ用ログ
            
            var totalCounters = _counterTreeNodes.Sum(obj => obj.Children.Sum(inst => inst.Children.Count));
            var totalInstances = _counterTreeNodes.Sum(obj => obj.Children.Count);
            
            var timeRangeInfo = _timeRangeDetected 
                ? $"⏰ 時間範囲: {_fileStartTime:yyyy/MM/dd HH:mm:ss} ～ {_fileEndTime:yyyy/MM/dd HH:mm:ss}" 
                : "⚠️ 時間範囲の検出に失敗しました";
            
            var samplingIntervalInfo = _samplingInterval != TimeSpan.Zero
                ? $"⏱️ 取得間隔: {FormatSamplingInterval(_samplingInterval)}"
                : "⚠️ 取得間隔の検出に失敗しました";
            
            AddOperationLog(LogLevel.Success, $"BLGファイルが読み込まれました。\n" +
                           $"📊 パフォーマンスオブジェクト: {_counterTreeNodes.Count}個\n" +
                           $"🏷️  インスタンス: {totalInstances}個\n" +
                           $"📈 カウンター: {totalCounters}個\n" +
                           $"{timeRangeInfo}\n" +
                           $"{samplingIntervalInfo}\n" +
                           $"カウンターを選択して「🚀 選択されたカウンターを読み込み」ボタンを押してください。");
        }
        catch (Exception ex)
        {
            LogError($"LoadBlgFileAsync failed: {ex}");
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            AddOperationLog(LogLevel.Error, $"ファイルの読み込みに失敗しました: {ex.Message}");
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

            // BLGファイルから実際のコンピューター名を取得
            try
            {
                var machineNames = analyzer.GetMachineNamesFromBlg();
                _actualComputerName = machineNames.FirstOrDefault();
                if (!string.IsNullOrEmpty(_actualComputerName))
                {
                    LogError($"Computer name extracted from BLG file: {_actualComputerName}");
                }
                else
                {
                    LogError("No computer name found in BLG file");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to extract computer name from BLG file: {ex.Message}");
                _actualComputerName = null;
            }

            // BLGファイルからサンプリング間隔を取得
            try
            {
                _samplingInterval = await analyzer.GetSamplingIntervalAsync(progress);
                LogError($"Sampling interval extracted from BLG file: {_samplingInterval}");
                if (_samplingInterval == TimeSpan.Zero)
                {
                    LogError("警告: 取得間隔が0です。単一サンプルまたはデータが不足している可能性があります。");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to extract sampling interval from BLG file: {ex.Message}");
                _samplingInterval = TimeSpan.Zero;
            }
            
            // 全てのカウンターパスを生成
            counters = await analyzer.GenerateAllCounterPathsAsync(progress);
            
            LogError($"PDH API parsing completed with {counters.Count} counters");
            
            if (counters.Count > 0)
            {
                // カウンターパスの生成が完了 - データポイントの読み込みは選択時に実行
                progress?.Report($"カウンター構造を構築しました（{counters.Count}個のカウンター）");
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



    /* 
    // 注意: このメソッドは初期読み込み時の処理高速化のため無効化されました
    // 実際のカウンターデータ読み込みは「選択されたカウンターを読み込み」ボタン押下時に実行されます
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
    */

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
            // パフォーマンスカウンターのパス解析: \\ComputerName\ObjectName(InstanceName)\CounterName または \ObjectName(InstanceName)\CounterName
            var parts = counter.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            
            string objectName, counterName;
            
            if (parts.Length >= 3) // \\コンピューター名\オブジェクト\カウンター の場合
            {
                objectName = parts[1];
                counterName = parts[2];
            }
            else if (parts.Length >= 2) // \オブジェクト\カウンター の場合（ローカルコンピューター）
            {
                objectName = parts[0];
                counterName = parts[1];
            }
            else
            {
                LogError($"Skipping invalid counter path: {counter}");
                continue;
            }
            
            // インスタンス名を抽出
            var instanceName = "(なし)";
            if (objectName.Contains('(') && objectName.Contains(')'))
            {
                var startIndex = objectName.IndexOf('(');
                var endIndex = objectName.LastIndexOf(')');
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    instanceName = objectName.Substring(startIndex + 1, endIndex - startIndex - 1);
                    objectName = objectName.Substring(0, startIndex);
                }
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
        
        // 凡例をクリア（新しいファイル読み込み時）
        ClearLegendItems();
        
        foreach (var objectGroup in objectGroups.OrderBy(x => x.Key))
        {
            var objectNode = new CounterTreeNode
            {
                DisplayName = objectGroup.Key,
                FullPath = "",
                Type = NodeType.Object
            };
            
            // 複数のインスタンスがある場合、最初に "*" ノードを追加
            bool hasMultipleInstances = objectGroup.Value.Count > 1 || 
                                      (objectGroup.Value.Count == 1 && !objectGroup.Value.Keys.First().Equals("(なし)"));
            
            if (hasMultipleInstances)
            {
                // すべてのカウンター名を収集（重複を除去）
                var allCounterNames = new HashSet<string>();
                foreach (var instanceGroup in objectGroup.Value)
                {
                    foreach (var counter in instanceGroup.Value)
                    {
                        var parts = counter.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                        string counterName;
                        
                        if (parts.Length >= 3) // \\コンピューター名\オブジェクト\カウンター の場合
                        {
                            counterName = parts[2];
                        }
                        else if (parts.Length >= 2) // \オブジェクト\カウンター の場合
                        {
                            counterName = parts[1];
                        }
                        else
                        {
                            counterName = counter;
                        }
                        
                        allCounterNames.Add(counterName);
                    }
                }
                
                // "*" インスタンスノードを作成
                var wildcardInstanceNode = new CounterTreeNode
                {
                    DisplayName = "*",
                    FullPath = "",
                    Type = NodeType.Instance,
                    IsWildCard = true
                };
                
                // "*" ノード下に各カウンターを追加
                foreach (var counterName in allCounterNames.OrderBy(x => x))
                {
                    var wildcardCounterNode = new CounterTreeNode
                    {
                        DisplayName = counterName,
                        FullPath = $"WILDCARD:{objectGroup.Key}:*:{counterName}",
                        Type = NodeType.Counter,
                        Parent = wildcardInstanceNode,
                        IsWildCard = true
                    };
                    
                    wildcardInstanceNode.Children.Add(wildcardCounterNode);
                }
                
                wildcardInstanceNode.Parent = objectNode;
                objectNode.Children.Add(wildcardInstanceNode);
            }
            
            // 通常のインスタンスノードを追加
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
                    var parts = counter.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                    string counterName;
                    
                    if (parts.Length >= 3) // \\コンピューター名\オブジェクト\カウンター の場合
                    {
                        counterName = parts[2];
                    }
                    else if (parts.Length >= 2) // \オブジェクト\カウンター の場合
                    {
                        counterName = parts[1];
                    }
                    else
                    {
                        counterName = counter;
                    }
                    
                    var counterNode = new CounterTreeNode
                    {
                        DisplayName = counterName,
                        FullPath = counter,
                        Type = NodeType.Counter,
                        Parent = instanceNode  // 親の設定
                    };
                    
                    instanceNode.Children.Add(counterNode);
                }
                
                instanceNode.Parent = objectNode;  // 親の設定
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
            
            if (sender is CheckBox checkBox && checkBox.Tag is CounterTreeNode node)
            {
                System.Diagnostics.Debug.WriteLine($"CheckBox found for node: {node.DisplayName} (Type: {node.Type})");
                
                // チェックボックスの状態変更を一時的に無効化して無限ループを防ぐ
                checkBox.Checked -= CounterCheckBox_Checked;
                checkBox.Unchecked -= CounterCheckBox_Unchecked;
                checkBox.Indeterminate -= CounterCheckBox_Indeterminate;
                
                // ノードのIsCheckedを更新（階層管理の自動処理が実行される）
                if (node.IsChecked != true)
                {
                    node.IsChecked = true;
                }
                
                // イベントハンドラーを再度登録
                checkBox.Checked += CounterCheckBox_Checked;
                checkBox.Unchecked += CounterCheckBox_Unchecked;
                checkBox.Indeterminate += CounterCheckBox_Indeterminate;
                
                // リーフノード（カウンター）の場合は、データ読み込みの準備
                if (node.IsLeaf)
                {
                    System.Diagnostics.Debug.WriteLine($"Counter {node.FullPath} marked for execution");
                    
                    // ワイルドカードカウンターの場合、対応するすべてのインスタンスのカウンターを選択
                    if (node.IsWildCard && node.FullPath.StartsWith("WILDCARD:"))
                    {
                        HandleWildcardCounterSelection(node, true);
                    }
                }

                // ノード種別に関係なく、現在の選択状態でrelog表示を更新
                UpdateRelogCommandDisplay();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("CheckBox Tag is not a CounterTreeNode");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in CounterCheckBox_Checked: {ex.Message}");
            LogError($"Error in CounterCheckBox_Checked: {ex}");
        }
    }

    private bool TryHandleParentCounterNodeToggle(CheckBox checkBox)
    {
        if (checkBox.Tag is not CounterTreeNode node || node.IsLeaf)
        {
            return false;
        }

        node.ToggleFromUserInteraction();
        UpdateRelogCommandDisplay();
        return true;
    }

    private void CounterCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is CheckBox checkBox && TryHandleParentCounterNodeToggle(checkBox))
        {
            e.Handled = true;
        }
    }

    private void CounterCheckBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((e.Key == Key.Space || e.Key == Key.Enter) &&
            sender is CheckBox checkBox &&
            TryHandleParentCounterNodeToggle(checkBox))
        {
            e.Handled = true;
        }
    }

    private void CounterCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"CounterCheckBox_Unchecked called");
            
            if (sender is CheckBox checkBox && checkBox.Tag is CounterTreeNode node)
            {
                System.Diagnostics.Debug.WriteLine($"CounterCheckBox_Unchecked for node: {node.DisplayName} (Type: {node.Type})");
                
                // チェックボックスの状態変更を一時的に無効化して無限ループを防ぐ
                checkBox.Checked -= CounterCheckBox_Checked;
                checkBox.Unchecked -= CounterCheckBox_Unchecked;
                checkBox.Indeterminate -= CounterCheckBox_Indeterminate;
                
                // ノードのIsCheckedを更新（階層管理の自動処理が実行される）
                if (node.IsChecked != false)
                {
                    node.IsChecked = false;
                }
                
                // イベントハンドラーを再度登録
                checkBox.Checked += CounterCheckBox_Checked;
                checkBox.Unchecked += CounterCheckBox_Unchecked;
                checkBox.Indeterminate += CounterCheckBox_Indeterminate;
                
                // リーフノード（カウンター）の場合は、チャートからも削除
                if (node.IsLeaf)
                {
                    RemoveCounterFromChart(node.FullPath);
                    
                    // ワイルドカードカウンターの場合、対応するすべてのインスタンスのカウンターを選択解除
                    if (node.IsWildCard && node.FullPath.StartsWith("WILDCARD:"))
                    {
                        HandleWildcardCounterSelection(node, false);
                    }
                }

                // ノード種別に関係なく、現在の選択状態でrelog表示を更新
                UpdateRelogCommandDisplay();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in CounterCheckBox_Unchecked: {ex.Message}");
            LogError($"Error in CounterCheckBox_Unchecked: {ex}");
        }
    }
    
    private void CounterCheckBox_Indeterminate(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"CounterCheckBox_Indeterminate called");
            
            if (sender is CheckBox checkBox && checkBox.Tag is CounterTreeNode node)
            {
                System.Diagnostics.Debug.WriteLine($"CounterCheckBox_Indeterminate for node: {node.DisplayName} (Type: {node.Type})");
                
                // 中間選択状態の処理
                // 親ノードが中間選択状態の場合は、特別な処理は必要なし
                // 子ノードの状態に基づいて自動的に中間選択状態が設定される
                
                System.Diagnostics.Debug.WriteLine($"Node {node.DisplayName} is in indeterminate state (partially selected)");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in CounterCheckBox_Indeterminate: {ex.Message}");
            LogError($"Error in CounterCheckBox_Indeterminate: {ex}");
        }
    }

    /// <summary>
    /// ワイルドカードカウンターの選択/選択解除時に対応するすべてのインスタンスのカウンターを処理
    /// </summary>
    private void HandleWildcardCounterSelection(CounterTreeNode wildcardNode, bool isChecked)
    {
        try
        {
            // WILDCARD:ObjectName:*:CounterName 形式からパースする
            var parts = wildcardNode.FullPath.Split(':');
            if (parts.Length != 4 || parts[0] != "WILDCARD")
            {
                LogError($"Invalid wildcard counter path: {wildcardNode.FullPath}");
                return;
            }

            var objectName = parts[1];
            var counterName = parts[3];
            
            System.Diagnostics.Debug.WriteLine($"HandleWildcardCounterSelection: {objectName}.*.{counterName} = {isChecked}");

            // 対応するオブジェクトノードを検索
            var objectNode = _counterTreeNodes.FirstOrDefault(o => o.DisplayName == objectName);
            if (objectNode == null)
            {
                LogError($"Object node not found: {objectName}");
                return;
            }

            // すべてのインスタンス（"*"以外）のカウンターを選択/選択解除
            int matchedCount = 0;
            foreach (var instanceNode in objectNode.Children)
            {
                // "*" ノード自体は除外
                if (instanceNode.IsWildCard) continue;

                // 対応するカウンター名のノードを検索
                var targetCounterNode = instanceNode.Children.FirstOrDefault(c => c.DisplayName == counterName);
                if (targetCounterNode != null)
                {
                    // 無限ループを防ぐため、イベントハンドラーを一時的に無効化
                    targetCounterNode.IsChecked = isChecked;
                    matchedCount++;
                    
                    System.Diagnostics.Debug.WriteLine($"  {(isChecked ? "Selected" : "Unselected")}: {targetCounterNode.FullPath}");
                }
            }

            LogError($"Wildcard selection: {objectName}.*.{counterName} -> {matchedCount} counters {(isChecked ? "selected" : "unselected")}");
        }
        catch (Exception ex)
        {
            LogError($"Error in HandleWildcardCounterSelection: {ex}");
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
        
        // 既存のシリーズをチェック（現在のチャートタイプに応じて）
        bool seriesExists = _currentChartType == ChartType.LineChart 
            ? _chartSeries.ContainsKey(counter) 
            : _areaChartSeries.ContainsKey(counter);
            
        if (seriesExists)
        {
            System.Diagnostics.Debug.WriteLine($"Series already exists for: {counter}");
            return;
        }
        
        // データテーブルタブを作成（チェックボックス経由）
        AddCounterTab(counter);
        
        // 積み重ね面グラフの場合は全体を再描画
        if (_currentChartType == ChartType.StackedAreaChart)
        {
            System.Diagnostics.Debug.WriteLine("Stacked area chart requires full redraw");
            RefreshChartWithCurrentType();
        }
        else
        {
            // 折れ線グラフの場合は個別に追加
            AddLineChartSeries(counter);
        }
        
        // グラフが表示されたらメッセージを非表示
        UpdateChartVisibility();
        
        // スケールコントロールの表示を更新
        UpdateScaleControlVisibility();
    }

    private sealed class LineSeriesBuildResult
    {
        public LineSeriesBuildResult(
            string counter,
            string displayName,
            List<PerformanceDataPoint> dataPoints,
            double scale,
            double[] xValues,
            double[] yValues,
            ScottPlot.Color lineColor,
            System.Windows.Media.Color legendColor)
        {
            Counter = counter;
            DisplayName = displayName;
            DataPoints = dataPoints;
            Scale = scale;
            XValues = xValues;
            YValues = yValues;
            LineColor = lineColor;
            LegendColor = legendColor;
        }

        public string Counter { get; }
        public string DisplayName { get; }
        public List<PerformanceDataPoint> DataPoints { get; }
        public double Scale { get; }
        public double[] XValues { get; }
        public double[] YValues { get; }
        public ScottPlot.Color LineColor { get; }
        public System.Windows.Media.Color LegendColor { get; }
    }

    private LineSeriesBuildResult? BuildLineSeries(string counter)
    {
        var dataPoints = GetDisplayDataPoints(counter);
        if (!dataPoints.Any())
        {
            return null;
        }

        var scale = _counterScales.GetValueOrDefault(counter, 1.0);
        var xValues = dataPoints.Select(dp => dp.Timestamp.ToOADate()).ToArray();
        var yValues = dataPoints.Select(dp => dp.Value * scale).ToArray();
        var lineColor = GetOrCreateCounterColor(counter);

        return new LineSeriesBuildResult(
            counter,
            GetCounterDisplayName(counter),
            dataPoints,
            scale,
            xValues,
            yValues,
            lineColor,
            ConvertToMediaColor(lineColor));
    }

    private void AddBuiltLineSeriesToChart(LineSeriesBuildResult lineSeries)
    {
        var scatter = PerformanceChart.Plot.Add.Scatter(lineSeries.XValues, lineSeries.YValues);
        scatter.LegendText = lineSeries.DisplayName;
        scatter.LineWidth = DefaultLineWidth;
        scatter.MarkerSize = 0; // マーカーを非表示にしてパフォーマンス向上
        scatter.LineStyle.Width = DefaultLineWidth; // 線の太さを明示的に設定
        scatter.LineColor = lineSeries.LineColor; // 色を設定
        scatter.IsVisible = _seriesVisibility.GetValueOrDefault(lineSeries.Counter, true);

        _chartSeries[lineSeries.Counter] = scatter;
        AddLegendItem(lineSeries.Counter, lineSeries.DisplayName, lineSeries.LegendColor);
    }

    /// <summary>
    /// 折れ線グラフのシリーズを個別に追加
    /// </summary>
    private void AddLineChartSeries(string counter)
    {
        var lineSeries = BuildLineSeries(counter);
        if (lineSeries is null)
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine($"Original value range: {lineSeries.DataPoints.Min(dp => dp.Value)} to {lineSeries.DataPoints.Max(dp => dp.Value)}");
        System.Diagnostics.Debug.WriteLine($"Manual scale: {lineSeries.Scale:F6}, Final scale: {lineSeries.Scale:F6}");
        System.Diagnostics.Debug.WriteLine($"Display position range (scaled): {lineSeries.YValues.Min()} to {lineSeries.YValues.Max()}");

        AddBuiltLineSeriesToChart(lineSeries);

        // 凡例ハイライト状態を反映
        ApplyLineSeriesHighlight(refresh: false);
        
        System.Diagnostics.Debug.WriteLine($"Added series to chart for: {counter}");
        
        // グラフを更新（Y軸固定範囲なのでAutoScaleは使わない）
        EnsureYAxisFixedRange();
        
        // X軸の範囲を選択された時間範囲に設定
        UpdateChartXAxisRange();
        
        PerformanceChart.Refresh();
        
        // 凡例の現在値を更新
        UpdateLegendCurrentValues();
    }

    /// <summary>
    /// カウンターをチャートに追加（スケールコントロール更新なし）
    /// </summary>
    private void AddCounterToChartInternal(string counter)
    {
        System.Diagnostics.Debug.WriteLine($"AddCounterToChartInternal called for: {counter}");
        
        if (!_counterData.ContainsKey(counter))
        {
            System.Diagnostics.Debug.WriteLine($"Counter not found in _counterData: {counter}");
            return;
        }

        // 既存のシリーズをチェック
        if (_chartSeries.ContainsKey(counter))
        {
            System.Diagnostics.Debug.WriteLine($"Series already exists for: {counter}");
            return;
        }
        
        var lineSeries = BuildLineSeries(counter);
        if (lineSeries is null)
        {
            System.Diagnostics.Debug.WriteLine($"No data points for counter: {counter}");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"Manual scale: {lineSeries.Scale:F6}, Final scale: {lineSeries.Scale:F6} for counter: {counter}");
        System.Diagnostics.Debug.WriteLine($"Original value range: {lineSeries.DataPoints.Min(dp => dp.Value)} to {lineSeries.DataPoints.Max(dp => dp.Value)}");
        System.Diagnostics.Debug.WriteLine($"Display position range (scaled): {lineSeries.YValues.Min()} to {lineSeries.YValues.Max()}");

        AddBuiltLineSeriesToChart(lineSeries);

        // 凡例ハイライト状態を反映
        ApplyLineSeriesHighlight(refresh: false);
        
        System.Diagnostics.Debug.WriteLine($"Added series to chart for: {counter}");
        
        // グラフを更新（Y軸固定範囲なのでAutoScaleは使わない）
        EnsureYAxisFixedRange();
        
        // X軸の範囲を選択された時間範囲に設定
        UpdateChartXAxisRange();
        
        PerformanceChart.Refresh();
        
        // データテーブルタブを作成（チェックボックス経由）
        AddCounterTab(counter);
        
        // グラフが表示されたらメッセージを非表示
        UpdateChartVisibility();
        
        // 凡例の現在値を更新
        UpdateLegendCurrentValues();
    }

    private void RemoveCounterFromChart(string counter)
    {
        System.Diagnostics.Debug.WriteLine($"RemoveCounterFromChart called for: {counter}");
        
        // 折れ線グラフのシリーズを削除
        bool removedLine = false;
        if (_chartSeries.TryGetValue(counter, out var scatter))
        {
            PerformanceChart.Plot.Remove(scatter);
            _chartSeries.Remove(counter);
            _highlightedLegendCounterPaths.Remove(counter);
            removedLine = true;
            System.Diagnostics.Debug.WriteLine($"Removed line series from chart for: {counter}");
        }
        
        // 積み重ね面グラフのシリーズを削除
        bool removedArea = false;
        if (_areaChartSeries.TryGetValue(counter, out var area))
        {
            PerformanceChart.Plot.Remove(area);
            _areaChartSeries.Remove(counter);
            removedArea = true;
            System.Diagnostics.Debug.WriteLine($"Removed area series from chart for: {counter}");
        }
        
        // 積み重ね面グラフの場合は全体を再描画
        if (_currentChartType == ChartType.StackedAreaChart && removedArea)
        {
            System.Diagnostics.Debug.WriteLine("Stacked area chart requires full redraw after removal");
            RefreshChartWithCurrentType();
        }
        else if (removedLine)
        {
            
            
            PerformanceChart.Refresh();
        }
        
        // スケール設定も削除
        _counterScales.Remove(counter);
        
        // 凡例アイテムを削除
        RemoveLegendItem(counter);
        
        // データテーブルタブを削除
        RemoveCounterTab(counter);
        
        // グラフの表示状態を更新
        UpdateChartVisibility();
        
        // スケールコントロールの表示を更新
        UpdateScaleControlVisibility();
    }

    /// <summary>
    /// グラフ表示エリアを現在選択されているチェックの内容で初期化
    /// </summary>
    private void InitializeChartWithSelectedCounters()
    {
        System.Diagnostics.Debug.WriteLine("InitializeChartWithSelectedCounters called");
        
        // 現在選択されているカウンターを取得
        var selectedCounters = GetSelectedCounters().ToHashSet();
        
        // 現在のチャートタイプに応じてカウンターリストを取得
        var currentChartCounters = _currentChartType == ChartType.LineChart 
            ? _chartSeries.Keys.ToList() 
            : _areaChartSeries.Keys.ToList();
        
        // 選択されていないカウンターをグラフから削除
        foreach (var counter in currentChartCounters)
        {
            if (!selectedCounters.Contains(counter))
            {
                System.Diagnostics.Debug.WriteLine($"Removing unselected counter from chart: {counter}");
                RemoveCounterFromChart(counter);
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"Chart initialized with {selectedCounters.Count} selected counters");
    }

    /// <summary>
    /// 現在のグラフタイプで選択されているカウンターを再描画
    /// </summary>
    private void RefreshChartWithCurrentType()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"RefreshChartWithCurrentType called with type: {_currentChartType}");
            
            // 現在のグラフをクリア
            PerformanceChart.Plot.Clear();
            _chartSeries.Clear();
            _areaChartSeries.Clear();
            
            // 凡例をクリア
            ClearLegendItems(false);
            
            // グラフの基本設定を再初期化
            PerformanceChart.Plot.XLabel("時間");
            PerformanceChart.Plot.YLabel(GetCurrentYAxisLabel());
            PerformanceChart.Plot.Axes.DateTimeTicksBottom();
            PerformanceChart.Plot.Axes.Left.IsVisible = true;
            
            // 凡例を無効化（独立した凡例コンポーネントを使用）
            PerformanceChart.Plot.Legend.IsVisible = false;
            
            // 選択されているカウンターを取得
            var selectedCounters = GetSelectedCounters();
            
            if (!selectedCounters.Any())
            {
                System.Diagnostics.Debug.WriteLine("No selected counters found");
                PerformanceChart.Refresh();
                UpdateChartVisibility();
                return;
            }
            
            // グラフタイプに応じて描画
            switch (_currentChartType)
            {
                case ChartType.LineChart:
                    DrawLineChart(selectedCounters);
                    break;
                case ChartType.StackedAreaChart:
                    DrawStackedAreaChart(selectedCounters);
                    break;
            }

            // 凡例ハイライト状態を反映
            ApplyLineSeriesHighlight(refresh: false);
            
            // グラフを更新（Y軸固定範囲なのでAutoScaleは使わない）
            EnsureYAxisFixedRange();
            
            // X軸の範囲を選択された時間範囲に設定
            UpdateChartXAxisRange();
            
            PerformanceChart.Refresh();
            UpdateChartVisibility();
            
            // 凡例の現在値を更新
            UpdateLegendCurrentValues();
            
            System.Diagnostics.Debug.WriteLine($"Chart refreshed with {selectedCounters.Count} counters");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in RefreshChartWithCurrentType: {ex.Message}");
            LogError($"グラフ再描画エラー: {ex}");
        }
    }

    /// <summary>
    /// 折れ線グラフを描画
    /// </summary>
    private void DrawLineChart(List<string> selectedCounters)
    {
        System.Diagnostics.Debug.WriteLine($"Drawing line chart for {selectedCounters.Count} counters");
        
        foreach (var counter in selectedCounters)
        {
            var lineSeries = BuildLineSeries(counter);
            if (lineSeries is null)
            {
                continue;
            }

            AddBuiltLineSeriesToChart(lineSeries);

            System.Diagnostics.Debug.WriteLine($"Added line series for: {counter}");
        }

        // 凡例ハイライト状態を反映
        ApplyLineSeriesHighlight(refresh: false);
        
        // 凡例の現在値を更新
        UpdateLegendCurrentValues();
    }

    /// <summary>
    /// 積み重ね面グラフを描画
    /// </summary>
    private void DrawStackedAreaChart(List<string> selectedCounters)
    {
        System.Diagnostics.Debug.WriteLine($"Drawing stacked area chart for {selectedCounters.Count} counters");
        
        if (!selectedCounters.Any())
        {
            return;
        }

        // 凡例は選択中カウンター全体を維持（表示/非表示の再切替を可能にする）
        var counterColors = new Dictionary<string, ScottPlot.Color>(StringComparer.Ordinal);
        for (int i = 0; i < selectedCounters.Count; i++)
        {
            var counter = selectedCounters[i];
            var scottPlotColor = GetNextColor(i);
            counterColors[counter] = scottPlotColor;
            AddLegendItem(counter, GetCounterDisplayName(counter), ConvertToMediaColor(scottPlotColor));
        }

        // 積み上げ計算は「表示中（チェック有効）」の項目のみ対象
        var visibleCounters = selectedCounters
            .Where(counter => _seriesVisibility.GetValueOrDefault(counter, true))
            .ToList();

        if (!visibleCounters.Any())
        {
            UpdateLegendCurrentValues();
            return;
        }
        
        // 全カウンターの共通のタイムスタンプを取得
        var displayedCounterData = new Dictionary<string, List<PerformanceDataPoint>>();
        var allTimestamps = new SortedSet<DateTime>();
        foreach (var counter in visibleCounters)
        {
            var dataPoints = GetDisplayDataPoints(counter);
            if (dataPoints.Any())
            {
                displayedCounterData[counter] = dataPoints;
                foreach (var dp in dataPoints)
                {
                    allTimestamps.Add(dp.Timestamp);
                }
            }
        }

        if (!allTimestamps.Any())
        {
            UpdateLegendCurrentValues();
            return;
        }
        
        var timeArray = allTimestamps.ToArray();
        var xValues = timeArray.Select(t => t.ToOADate()).ToArray();
        
        // 積み重ねのベースライン（前回の累積値）
        var baseline = new double[timeArray.Length];
        
        foreach (var counter in visibleCounters)
        {
            if (!displayedCounterData.TryGetValue(counter, out var dataPoints))
            {
                continue;
            }
            
            // 手動スケールを取得（デフォルトは1.0）
            var manualScale = _counterScales.GetValueOrDefault(counter, 1.0);
            
            // 最終スケール = 手動スケール
            var finalScale = manualScale;
            
            // データポイントを辞書化（高速検索用）
            var dataDict = dataPoints.ToDictionary(dp => dp.Timestamp, dp => dp.Value * finalScale);
            
            // タイムスタンプごとの値を補間
            var yValues = new double[timeArray.Length];
            for (int i = 0; i < timeArray.Length; i++)
            {
                var timestamp = timeArray[i];
                if (dataDict.TryGetValue(timestamp, out var value))
                {
                    yValues[i] = value;
                }
                else
                {
                    // 補間：前後の値から線形補間
                    yValues[i] = InterpolateValue(dataPoints, timestamp, finalScale);
                }
            }
            
            // 積み重ねのために現在の値とベースラインを加算
            var topValues = new double[timeArray.Length];
            for (int i = 0; i < timeArray.Length; i++)
            {
                topValues[i] = baseline[i] + yValues[i];
            }
            
            // FillYプロットを作成（ベースラインから現在の値まで）
            var fillY = PerformanceChart.Plot.Add.FillY(xValues, baseline, topValues);
            fillY.LegendText = GetCounterDisplayName(counter);
            var scottPlotColor = counterColors[counter];
            fillY.FillStyle.Color = scottPlotColor;
            fillY.LineColor = scottPlotColor;
            fillY.LineWidth = StackedAreaOutlineWidth;
            fillY.LineStyle.Width = StackedAreaOutlineWidth;
            fillY.IsVisible = true;
            
            _areaChartSeries[counter] = fillY;
            
            // 次のカウンター用にベースラインを更新
            baseline = topValues;
            
            System.Diagnostics.Debug.WriteLine($"Added stacked area series for: {counter}");
        }
        
        // 凡例の現在値を更新
        UpdateLegendCurrentValues();
    }

    /// <summary>
    /// データポイント間の値を線形補間
    /// </summary>
    private double InterpolateValue(List<PerformanceDataPoint> dataPoints, DateTime targetTime, double scale)
    {
        if (!dataPoints.Any())
            return 0;
        
        // 目標時間より前の最新のポイント
        var before = dataPoints.Where(dp => dp.Timestamp <= targetTime).LastOrDefault();
        // 目標時間より後の最初のポイント
        var after = dataPoints.Where(dp => dp.Timestamp >= targetTime).FirstOrDefault();
        
        if (before == null && after == null)
            return 0;
        
        if (before == null)
            return after!.Value * scale;
        
        if (after == null)
            return before.Value * scale;
        
        if (before.Timestamp == after.Timestamp)
            return before.Value * scale;
        
        // 線形補間
        var totalTicks = (after.Timestamp - before.Timestamp).Ticks;
        var targetTicks = (targetTime - before.Timestamp).Ticks;
        var ratio = (double)targetTicks / totalTicks;
        
        return (before.Value + (after.Value - before.Value) * ratio) * scale;
    }

    /// <summary>
    /// カウンター順に色を取得
    /// </summary>
    private ScottPlot.Color GetNextColor(int index)
    {
        var colors = new[]
        {
            ScottPlot.Colors.Blue,
            ScottPlot.Colors.Red,
            ScottPlot.Colors.Green,
            ScottPlot.Colors.Orange,
            ScottPlot.Colors.Purple,
            ScottPlot.Colors.Brown,
            ScottPlot.Colors.Pink,
            ScottPlot.Colors.Gray,
            ScottPlot.Colors.Olive,
            ScottPlot.Colors.Cyan
        };
        
        return colors[index % colors.Length];
    }

    /// <summary>
    /// カウンターごとの折れ線色を取得（未割り当て時は新規採番）
    /// </summary>
    private ScottPlot.Color GetOrCreateCounterColor(string counter)
    {
        if (_counterLineColors.TryGetValue(counter, out var color))
        {
            return color;
        }

        color = GetNextColor(_counterLineColors.Count);
        _counterLineColors[counter] = color;
        return color;
    }
    
    /// <summary>
    /// ScottPlot.ColorをSystem.Windows.Media.Colorに変換
    /// </summary>
    private System.Windows.Media.Color ConvertToMediaColor(ScottPlot.Color scottPlotColor)
    {
        return System.Windows.Media.Color.FromArgb(
            scottPlotColor.A,
            scottPlotColor.R,
            scottPlotColor.G,
            scottPlotColor.B
        );
    }
    
    private void UpdateChartVisibility()
    {
        // シリーズがある場合はメッセージを非表示、ない場合は表示
        var hasVisibleData = HasVisibleChartData();
        var hasPanelContext = HasChartPanelContext();
        NoDataMessagePanel.Visibility = hasVisibleData ? Visibility.Collapsed : Visibility.Visible;
        UpdateLegendPanelControls(hasPanelContext);
        System.Diagnostics.Debug.WriteLine($"Chart visibility updated: hasVisibleData={hasVisibleData}, hasPanelContext={hasPanelContext}");
        
        // 統計情報表示の更新
        UpdateStatisticsDisplay();
    }

    /// <summary>
    /// 統計情報表示を更新
    /// </summary>
    private void UpdateStatisticsDisplay()
    {
        try
        {
            var hasVisibleData = HasVisibleChartData();
            var hasPanelContext = HasChartPanelContext();
            UpdateStatisticsPanelControls(hasPanelContext);
             
            // グラフコントロールパネルの表示制御
            GraphControlPanel.Visibility = hasPanelContext ? Visibility.Visible : Visibility.Collapsed;
            GraphControlPanel.IsEnabled = hasVisibleData;
             
            // グラフメニューの有効/無効制御
            GraphMenu.IsEnabled = hasVisibleData;
             
            // コンテキストメニューの有効/無効制御
            if (ContextMenuCopyGraph != null)
            {
                ContextMenuCopyGraph.IsEnabled = hasVisibleData;
            }
             
            if (!hasVisibleData)
            {
                StatisticsDataGrid.ItemsSource = null;
                return;
            }
            
            // 統計情報のコレクションを作成
            var statisticsItems = new List<CounterStatisticsItem>();
            
            // 現在のチャートタイプに応じて統計情報を計算
            var currentCounters = _currentChartType == ChartType.LineChart 
                ? (IEnumerable<string>)_chartSeries.Keys 
                : (IEnumerable<string>)_areaChartSeries.Keys;
            
            // 各カウンターの統計情報を計算
            foreach (var counterName in currentCounters.OrderBy(c => c))
            {
                var dataPoints = GetDisplayDataPoints(counterName);
                if (dataPoints.Any())
                {
                    // PerformanceDataPointから(DateTime, double)タプルに変換
                    var tupleDataPoints = dataPoints.Select(dp => (dp.Timestamp, dp.Value)).ToList();
                    var statisticsItem = CreateStatisticsItem(counterName, tupleDataPoints);
                    if (statisticsItem != null)
                    {
                        statisticsItems.Add(statisticsItem);
                    }
                }
            }
            
            // DataGridに統計情報を設定
            StatisticsDataGrid.ItemsSource = statisticsItems;
            
            System.Diagnostics.Debug.WriteLine($"Statistics display updated for {statisticsItems.Count} counters");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating statistics display: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 特定カウンターの統計情報アイテムを作成
    /// </summary>
    private CounterStatisticsItem? CreateStatisticsItem(string counterName, List<(DateTime Timestamp, double Value)> dataPoints)
    {
        try
        {
            // PDH風の統計計算を実装
            var statistics = ComputeCounterStatistics(counterName, dataPoints);
            
            // 単位情報を取得（既存のフォーマット機能を使用）
            var unit = EstimateUnit(counterName);
            
            // フォーマットされた値を作成
            var minFormatted = FormatValueWithUnit(statistics.Minimum, unit);
            var maxFormatted = FormatValueWithUnit(statistics.Maximum, unit);
            var avgFormatted = FormatValueWithUnit(statistics.Average, unit);
            
            return new CounterStatisticsItem
            {
                CounterName = GetCounterDisplayName(counterName),
                Average = avgFormatted,
                Maximum = maxFormatted,
                Minimum = minFormatted
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating statistics item for {counterName}: {ex.Message}");
            
            // エラー時は簡単なアイテムを返す
            return new CounterStatisticsItem
            {
                CounterName = GetCounterDisplayName(counterName),
                Average = "エラー",
                Maximum = "エラー",
                Minimum = "エラー"
            };
        }
    }
    
    /// <summary>
    /// フォーマットされた値から単位部分を抽出
    /// </summary>
    private string ExtractUnit(string formattedValue)
    {
        // 数値以外の部分を単位として抽出
        var match = System.Text.RegularExpressions.Regex.Match(formattedValue, @"[^\d\.,\-\s]+");
        return match.Success ? match.Value : "";
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

            var displayDataPoints = GetDisplayDataPoints(counter);

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
                ItemsSource = displayDataPoints,
                AlternatingRowBackground = System.Windows.Media.Brushes.AliceBlue,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HeadersVisibility = DataGridHeadersVisibility.Column
            };

            System.Diagnostics.Debug.WriteLine($"Data points count: {displayDataPoints.Count}");

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
            var statisticsPanel = CreateStatisticsPanel(counter, displayDataPoints);
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
            AddOperationLog(LogLevel.Error, $"タブ作成でエラーが発生しました: {ex.Message}");
            MessageBox.Show($"タブ作成でエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 指定されたカウンターのデータテーブルタブを表示し、必要に応じて下部エリアを展開する
    /// </summary>
    private void ShowDataTablesForCounters(IEnumerable<string> counters)
    {
        var loadedCounters = counters
            .Where(counter => _counterData.ContainsKey(counter))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!loadedCounters.Any())
        {
            return;
        }

        EnsureBottomPanelVisible();

        var loadedCounterSet = new HashSet<string>(loadedCounters, StringComparer.Ordinal);
        var tabsToRemove = DataTabControl.Items
            .Cast<TabItem>()
            .Where(tab => tab.Tag is string counter && !loadedCounterSet.Contains(counter))
            .ToList();

        foreach (var tabToRemove in tabsToRemove)
        {
            DataTabControl.Items.Remove(tabToRemove);
        }

        var currentSelectedCounter = (DataTabControl.SelectedItem as TabItem)?.Tag as string;

        foreach (var counter in loadedCounters)
        {
            AddCounterTab(counter);
        }

        var preferredCounter = !string.IsNullOrEmpty(currentSelectedCounter) && loadedCounterSet.Contains(currentSelectedCounter)
            ? currentSelectedCounter
            : loadedCounters[0];

        var preferredTab = DataTabControl.Items
            .Cast<TabItem>()
            .FirstOrDefault(tab => string.Equals(tab.Tag as string, preferredCounter, StringComparison.Ordinal));

        if (preferredTab != null)
        {
            DataTabControl.SelectedItem = preferredTab;
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
            Orientation = System.Windows.Controls.Orientation.Horizontal
        };

        var periodText = statistics.DataPointCount > 0
            ? $"{statistics.FirstTimestamp:MM/dd HH:mm} - {statistics.LastTimestamp:MM/dd HH:mm}"
            : "-";

        // 統計情報を表示するテキストブロックを作成
        var statisticsItems = new[]
        {
            $"データ数: {statistics.DataPointCount}",
            $"平均: {statistics.FormattedAverage}",
            $"最大: {statistics.FormattedMaximum}",
            $"最小: {statistics.FormattedMinimum}",
            $"標準偏差: {statistics.FormattedStandardDeviation}",
            $"期間: {periodText}"
        };

        foreach (var item in statisticsItems)
        {
            var textBlock = new TextBlock
            {
                Text = item,
                Margin = new Thickness(0, 0, 20, 0),
                FontSize = 12,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            stackPanel.Children.Add(textBlock);
        }

        // エクスポートボタンを追加
        var exportButton = new Button
        {
            Content = "CSV出力",
            Padding = new Thickness(10, 2, 10, 2),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
        exportButton.Click += (sender, e) => ExportCounterDataToCsv(counter, dataPoints);
        stackPanel.Children.Add(exportButton);

        border.Child = stackPanel;
        return border;
    }

    /// <summary>
    /// PDH風の統計計算（PDH_STATISTICSの形式に合わせた実装）
    /// </summary>
    private CounterStatistics ComputeCounterStatistics(string counterName, List<(DateTime Timestamp, double Value)> dataPoints)
    {
        if (!dataPoints.Any())
        {
            return new CounterStatistics
            {
                CounterName = counterName,
                DataPointCount = 0,
                Average = 0,
                Maximum = 0,
                Minimum = 0,
                StandardDeviation = 0,
                Unit = EstimateUnit(counterName)
            };
        }

        // 統計情報は元の値で計算（スケールは適用しない）
        // これにより、実際のデータ値の統計が表示される
        var values = dataPoints.Select(dp => dp.Value).ToArray();
        
        // PDH_STATISTICSの計算ロジックを模倣
        // PDHでは内部的に以下の統計を計算します
        var count = (uint)values.Length;
        var sum = values.Sum();
        var mean = sum / count;
        
        // PDHの統計計算アルゴリズムに従った実装
        var min = values.Min();
        var max = values.Max();
        
        // 標準偏差の計算（PDH風）
        var variance = values.Select(v => Math.Pow(v - mean, 2)).Sum() / count;
        var standardDeviation = Math.Sqrt(variance);

        return new CounterStatistics
        {
            CounterName = counterName,
            DataPointCount = dataPoints.Count,
            Average = mean,
            Maximum = max,
            Minimum = min,
            StandardDeviation = standardDeviation,
            FirstTimestamp = dataPoints.Min(dp => dp.Timestamp),
            LastTimestamp = dataPoints.Max(dp => dp.Timestamp),
            Unit = EstimateUnit(counterName)
        };
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
                
                AddOperationLog(LogLevel.Success, $"CSVファイルが保存されました。\n{saveFileDialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            AddOperationLog(LogLevel.Error, $"CSVファイルの保存に失敗しました: {ex.Message}");
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
        var parts = counter.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
        {
            var objectName = parts[1];
            var counterName = parts[2];
            
            // インスタンス名を抽出
            var instanceName = "";
            if (objectName.Contains('(') && objectName.Contains(')'))
            {
                var startIndex = objectName.IndexOf('(');
                var endIndex = objectName.IndexOf(')');
                instanceName = objectName.Substring(startIndex + 1, endIndex - startIndex - 1);
                objectName = objectName.Substring(0, startIndex);
                
                // インスタンス名がある場合は含める
                return $"{objectName}({instanceName}) - {counterName}";
            }
            
            return $"{objectName} - {counterName}";
        }
        else if (parts.Length >= 2) // ローカルコンピューターの場合
        {
            var objectName = parts[0];
            var counterName = parts[1];
            
            // インスタンス名を抽出
            var instanceName = "";
            if (objectName.Contains('(') && objectName.Contains(')'))
            {
                var startIndex = objectName.IndexOf('(');
                var endIndex = objectName.IndexOf(')');
                instanceName = objectName.Substring(startIndex + 1, endIndex - startIndex - 1);
                objectName = objectName.Substring(0, startIndex);
                
                // インスタンス名がある場合は含める
                return $"{objectName}({instanceName}) - {counterName}";
            }
            
            return $"{objectName} - {counterName}";
        }
        return counter;
    }

    /// <summary>
    /// カウンターパスからコンピューター名を抽出
    /// </summary>
    private string GetComputerNameFromCounterPath(string counter)
    {
        var parts = counter.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        
        // \\コンピューター名\オブジェクト\カウンター の場合
        if (counter.StartsWith("\\\\") && parts.Length >= 3)
        {
            return parts[0]; // コンピューター名
        }
        
        // \オブジェクト\カウンター の場合（ローカルコンピューター）
        // BLGファイルから抽出した実際のコンピューター名があれば使用
        if (!string.IsNullOrEmpty(_actualComputerName))
        {
            return _actualComputerName;
        }
        
        return "ローカルコンピューター";
    }

    /// <summary>
    /// サンプリング間隔を分かりやすい形式にフォーマット
    /// </summary>
    private string FormatSamplingInterval(TimeSpan interval)
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

    private void UnselectAll_Click(object sender, RoutedEventArgs e)
    {
        SetAllCheckBoxes(false);
    }
    
    private void SetAllCheckBoxes(bool isChecked)
    {
        foreach (var objectNode in _counterTreeNodes)
        {
            objectNode.IsChecked = isChecked;
        }

        UpdateRelogCommandDisplay();
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
            // 全てのデータタブを削除（ログタブは別のTabControlにあるため影響なし）
            // カウンターの選択状態やグラフ表示は保持する
            DataTabControl.Items.Clear();
            
            AddOperationLog(LogLevel.Success, "全てのデータテーブルタブを閉じました。カウンターの選択状態は保持されています。");
        }
        catch (Exception ex)
        {
            AddOperationLog(LogLevel.Error, $"タブのクリアに失敗しました: {ex.Message}");
            MessageBox.Show($"タブのクリアに失敗しました: {ex.Message}", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            LogError($"Failed to close all tabs: {ex}");
        }
    }

    private void ExportAllDataToCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exportCounterData = _counterData.Keys
                .Select(counter => new
                {
                    Counter = counter,
                    DataPoints = GetDisplayDataPoints(counter)
                })
                .Where(x => x.DataPoints.Any())
                .ToList();

            if (!exportCounterData.Any())
            {
                AddOperationLog(LogLevel.Warning, "エクスポートするデータがありません。BLGファイルを読み込んでカウンターを選択してください。");
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
                
                foreach (var counterPair in exportCounterData)
                {
                    foreach (var dataPoint in counterPair.DataPoints)
                    {
                        allData.Add((dataPoint.Timestamp, counterPair.Counter, dataPoint));
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
                
                AddOperationLog(LogLevel.Success, $"全データがCSVファイルに保存されました。\n" +
                              $"ファイル: {saveFileDialog.FileName}\n" +
                              $"カウンター数: {exportCounterData.Count}個\n" +
                              $"データポイント数: {allData.Count}個");
            }
        }
        catch (Exception ex)
        {
            AddOperationLog(LogLevel.Error, $"CSVファイルの保存に失敗しました: {ex.Message}");
            MessageBox.Show($"CSVファイルの保存に失敗しました: {ex.Message}", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            LogError($"All data CSV export failed: {ex}");
        }
    }

    /// <summary>
    /// BLGファイルの時間範囲を検出する（PDH API使用）
    /// </summary>
    private async Task<bool> DetectTimeRangeAsync(string blgFilePath, IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("PDH APIを使用してBLGファイルの時間範囲を検出中...");
            
            using var analyzer = new BlgFileAnalyzer();
            var opened = await analyzer.OpenBlgFileAsync(blgFilePath, progress);
            
            if (!opened)
            {
                ApplyFallbackTimeRange("PDH APIでBLGファイルを開けませんでした");
                return true;
            }

            // PDH APIの直接的な時間範囲取得を使用
            try
            {
                var (startTime, endTime) = await analyzer.GetTimeRangeAsync(progress);
                
                _fileStartTime = startTime;
                _fileEndTime = endTime;
                _timeRangeDetected = true;
                
                // UIを更新
                UpdateTimeRangeUI();
                
                LogError($"PDH APIで時間範囲を検出: {_fileStartTime:yyyy-MM-dd HH:mm:ss} - {_fileEndTime:yyyy-MM-dd HH:mm:ss}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"PDH APIによる直接時間範囲取得に失敗、フォールバック方法を試行: {ex.Message}");
                
                // フォールバック: GetTimeRangeAsyncメソッドを使用（データポイントを読み込まずに時間範囲を取得）
                try
                {
                    progress?.Report("フォールバック方法で時間範囲を取得中...");
                    var (startTime, endTime) = await analyzer.GetTimeRangeAsync(progress);
                    
                    _fileStartTime = startTime;
                    _fileEndTime = endTime;
                    _timeRangeDetected = true;
                    
                    // UIを更新
                    UpdateTimeRangeUI();
                    
                    LogError($"フォールバック方法で時間範囲を検出: {_fileStartTime:yyyy-MM-dd HH:mm:ss} - {_fileEndTime:yyyy-MM-dd HH:mm:ss}");
                    return true;
                }
                catch (Exception fallbackEx)
                {
                    LogError($"フォールバック方法も失敗: {fallbackEx.Message}");
                }
                
                // 最後のフォールバック: 現在時刻を基準にした仮の時間範囲を設定
                _fileStartTime = DateTime.Now.AddHours(-24);
                _fileEndTime = DateTime.Now;
                _timeRangeDetected = true;
                
                UpdateTimeRangeUI();
                
                LogError($"最終フォールバック時間範囲を設定: {_fileStartTime:yyyy-MM-dd HH:mm:ss} - {_fileEndTime:yyyy-MM-dd HH:mm:ss}");
                return true;
            }
        }
        catch (Exception ex)
        {
            ApplyFallbackTimeRange($"PDH APIによる時間範囲検出に失敗: {ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// 時間範囲検出失敗時のフォールバック時間範囲を設定してUIを有効化
    /// </summary>
    private void ApplyFallbackTimeRange(string reason)
    {
        _fileStartTime = DateTime.Now.AddHours(-24);
        _fileEndTime = DateTime.Now;
        _timeRangeDetected = true;
        UpdateTimeRangeUI();
        LogError($"時間範囲フォールバックを適用: {reason} / {_fileStartTime:yyyy-MM-dd HH:mm:ss} - {_fileEndTime:yyyy-MM-dd HH:mm:ss}");
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
            
            // グラフのX軸範囲を初期化
            UpdateChartXAxisRange();
        }
        else
        {
            TimeRangeExpander.Visibility = Visibility.Collapsed;
            ExecuteButton.IsEnabled = !string.IsNullOrEmpty(_currentBlgFile);
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
            
            // relog.exe情報表示を更新
            UpdateRelogCommandDisplay();
            
            // グラフのX軸範囲を更新
            UpdateChartXAxisRange();
        }
    }

    /// <summary>
    /// 選択されたカウンターを実行するボタンのクリックイベント
    /// </summary>
    private async void ExecuteSelectedCounters_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentBlgFile))
        {
            AddOperationLog(LogLevel.Warning, "BLGファイルが読み込まれていません。");
            MessageBox.Show("BLGファイルが読み込まれていません。", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedCounters = GetSelectedCounters();
        if (selectedCounters.Count == 0)
        {
            AddOperationLog(LogLevel.Warning, "実行するカウンターが選択されていません。チェックボックスを選択してからボタンを押してください。");
            MessageBox.Show("実行するカウンターが選択されていません。\nチェックボックスを選択してからボタンを押してください。", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // グラフ表示エリアを現在のチェック内容で初期化
        InitializeChartWithSelectedCounters();

        try
        {
            // プログレスバーを表示
            ProgressGrid.Visibility = Visibility.Visible;
            ProgressStatusText.Text = $"選択されたカウンター ({selectedCounters.Count}個) を処理中...";
            
            ExecuteButton.IsEnabled = false;
            
            var progress = new Progress<string>(status => ProgressStatusText.Text = status);
            
            // 選択された時間範囲を計算（時間範囲未検出時は全期間扱い）
            DateTime selectedStartTime;
            DateTime selectedEndTime;
            if (_timeRangeDetected)
            {
                var totalDuration = _fileEndTime - _fileStartTime;
                var startOffset = TimeSpan.FromTicks((long)(totalDuration.Ticks * StartTimeSlider.Value / 100));
                var endOffset = TimeSpan.FromTicks((long)(totalDuration.Ticks * EndTimeSlider.Value / 100));

                selectedStartTime = _fileStartTime + startOffset;
                selectedEndTime = _fileStartTime + endOffset;
            }
            else
            {
                selectedStartTime = _fileStartTime;
                selectedEndTime = _fileEndTime;
            }
            
            await ExecuteRelogForSelectedCounters(selectedCounters, selectedStartTime, selectedEndTime, progress);
            ResetBulkScaleSelectorAfterCounterLoad();
            ShowDataTablesForCounters(selectedCounters);

            var timeRangeMessage = _timeRangeDetected
                ? $"時間範囲: {selectedStartTime:yyyy/MM/dd HH:mm:ss} ～ {selectedEndTime:yyyy/MM/dd HH:mm:ss}"
                : "時間範囲: 全期間（時間範囲の自動検出なし）";

            AddOperationLog(LogLevel.Success, $"カウンターデータの読み込みが完了しました。\n" +
                           $"処理されたカウンター数: {selectedCounters.Count}個\n" +
                           $"{timeRangeMessage}");
        }
        catch (Exception ex)
        {
            AddOperationLog(LogLevel.Error, $"カウンターデータの処理に失敗しました: {ex.Message}");
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
            foreach (var selectedCounter in objectNode.GetSelectedCounters())
            {
                selected.Add(selectedCounter.FullPath);
            }
        }
        
        return selected;
    }

    /// <summary>
    /// 選択されたカウンターに対してPDH APIを使用してデータを読み込み
    /// </summary>
    private async Task ExecuteRelogForSelectedCounters(List<string> counters, DateTime startTime, DateTime endTime, IProgress<string>? progress)
    {
        try
        {
            progress?.Report("PDH APIを使用してカウンターデータを読み込み中...");
            
            // 時間制約有効かどうかを判定（スライダーが初期値でない場合は時間制約を適用）
            bool useTimeConstraints = StartTimeSlider.Value > 0 || EndTimeSlider.Value < 100;
            
            // relog.exeコマンドライン文字列を生成
            string relogCommand = GenerateRelogCommand(_currentBlgFile!, counters, useTimeConstraints ? startTime : (DateTime?)null, useTimeConstraints ? endTime : (DateTime?)null);
            
            // UI表示を更新（relog.exe情報のみ表示）
            await Dispatcher.InvokeAsync(() =>
            {
                // relog.exe同等コマンドの表示
                RelogCommandExpander.Visibility = Visibility.Visible;
                RelogCommandDisplay.Text = relogCommand;
            });
            
            // PDH API実行状況を操作ログに出力
            var pdhApiInfo = useTimeConstraints 
                ? $"📊 PDH API: {counters.Count}個のカウンターを時間範囲で読み込み（⏰ 時間範囲: {startTime:yyyy-MM-dd HH:mm:ss} ～ {endTime:yyyy-MM-dd HH:mm:ss}）"
                : $"📊 PDH API: {counters.Count}個のカウンターを読み込み（時間制約なし）";
            AddOperationLog(LogLevel.Info, pdhApiInfo);
            
            // デバッグ情報をログに出力
            LogInfo($"PDH APIを使用してカウンターデータを読み込み中");
            LogInfo($"Use time constraints: {useTimeConstraints}");
            LogInfo($"Start time: {startTime:yyyy-MM-dd HH:mm:ss}, End time: {endTime:yyyy-MM-dd HH:mm:ss}");
            LogInfo($"Selected counters count: {counters.Count}");
            
            
            using var analyzer = new BlgFileAnalyzer();
            var opened = await analyzer.OpenBlgFileAsync(_currentBlgFile!, progress);
            
            if (!opened)
            {
                throw new Exception("PDH APIでBLGファイルを開けませんでした");
            }

            await Dispatcher.InvokeAsync(() =>
            {
                AddOperationLog(LogLevel.Info, "BLGファイルを正常に開きました - カウンターデータを読み込み中...");
            });

            int processedCount = 0;
            int successCount = 0;
            var errors = new List<string>();

            // 各カウンターのデータを読み込み
            foreach (var counterPath in counters)
            {
                try
                {
                    progress?.Report($"カウンター読み込み中: {counterPath} ({processedCount + 1}/{counters.Count})");
                    
                    // 時間制約がある場合は、時間制約付きの読み込みメソッドを使用
                    BlgFileAnalyzer.CounterInfo counterInfo;
                    if (useTimeConstraints)
                    {
                        counterInfo = await analyzer.LoadCounterDataAsync(counterPath, startTime, endTime, progress);
                    }
                    else
                    {
                        counterInfo = await analyzer.LoadCounterDataAsync(counterPath, progress);
                    }
                    
                    if (counterInfo.DataPoints.Count > 0)
                    {
                        var dataPoints = new List<PerformanceDataPoint>();
                        
                        foreach (var dataPoint in counterInfo.DataPoints)
                        {
                            // NaN値をスキップ
                            if (double.IsNaN(dataPoint.Value))
                                continue;
                            
                            var unit = EstimateUnit(counterPath);
                            var formattedValue = FormatValueWithUnit(dataPoint.Value, unit);
                            
                            dataPoints.Add(new PerformanceDataPoint
                            {
                                Counter = counterPath,
                                Value = dataPoint.Value,
                                Timestamp = dataPoint.Timestamp,
                                FormattedValue = formattedValue,
                                Unit = unit
                            });
                        }
                        
                        if (dataPoints.Count > 0)
                        {
                            _counterData[counterPath] = dataPoints;
                            successCount++;
                            
                            // UIスレッドでデータテーブルを更新
                            await Dispatcher.InvokeAsync(() =>
                            {
                                // グラフとデータテーブルの両方を更新
                                AddCounterToChart(counterPath);
                            });
                        }
                        else
                        {
                            errors.Add($"{counterPath}: 有効なデータポイントが見つかりませんでした");
                        }
                    }
                    else
                    {
                        errors.Add($"{counterPath}: データポイントが見つかりませんでした");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{counterPath}: {ex.Message}");
                    LogError($"カウンター '{counterPath}' の読み込みに失敗: {ex.Message}");
                }
                
                processedCount++;
                
                // 進行状況を操作ログに出力
                if (processedCount % 10 == 0 || processedCount == counters.Count) // 10個ごと、または最後に出力
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        AddOperationLog(LogLevel.Info, $"PDH API処理進行: {processedCount}/{counters.Count} - 成功 {successCount}個、エラー {errors.Count}個");
                    });
                }
            }
            
            // 最終結果を操作ログに出力
            await Dispatcher.InvokeAsync(() =>
            {
                var resultText = $"PDH API実行結果: 処理したカウンター {processedCount}個、成功 {successCount}個、エラー {errors.Count}個";
                AddOperationLog(LogLevel.Info, resultText);
                
                if (errors.Count > 0)
                {
                    // エラーの詳細も操作ログに出力（最初の3個のエラーのみ）
                    foreach (var error in errors.Take(3))
                    {
                        AddOperationLog(LogLevel.Warning, $"PDH APIエラー: {error}");
                    }
                    if (errors.Count > 3)
                    {
                        AddOperationLog(LogLevel.Warning, $"その他 {errors.Count - 3} 個のPDH APIエラーが発生しました");
                    }
                }
            });
            
            if (successCount > 0)
            {
                LogInfo($"PDH APIによるデータ読み込み完了。成功: {successCount}/{processedCount}");
                progress?.Report("データテーブルへの読み込み完了");
            }
            else
            {
                throw new Exception($"すべてのカウンターの読み込みに失敗しました。エラー: {errors.Count}");
            }
        }
        catch (Exception ex)
        {
            LogError($"PDH APIによるデータ読み込みに失敗: {ex.Message}");
            
            await Dispatcher.InvokeAsync(() =>
            {
                AddOperationLog(LogLevel.Error, $"PDH API実行エラー: {ex.Message}");
            });
            
            throw;
        }
    }
            
    /// <summary>
    /// CSVファイルから選択されたカウンターのデータを読み込み（PDH API用は使用しない）
    /// </summary>
    private async Task LoadSelectedCountersFromCsv(string csvPath, List<string> selectedCounters, IProgress<string>? progress)
    {
        await Task.Run(async () =>
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
                    await Dispatcher.InvokeAsync(() =>
                    {
                        // グラフとデータテーブルの両方を更新
                        AddCounterToChart(counterPath);
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

    #region スケール変更機能

    /// <summary>
    /// 一括スケール選択コンボボックスを初期化
    /// </summary>
    private void InitializeBulkScaleComboBox()
    {
        if (BulkScaleComboBox == null)
        {
            return;
        }

        _isInitializingBulkScaleComboBox = true;
        BulkScaleComboBox.Items.Clear();

        AddSupportedScaleItems(BulkScaleComboBox);

        var defaultScaleLabel = ScaleCatalog.GetLabel(1.0);
        var defaultItem = BulkScaleComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), defaultScaleLabel, StringComparison.Ordinal));
        if (defaultItem != null)
        {
            BulkScaleComboBox.SelectedItem = defaultItem;
        }

        _isInitializingBulkScaleComboBox = false;
    }

    /// <summary>
    /// 一括スケール選択変更時に即時適用
    /// </summary>
    private void BulkScaleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingBulkScaleComboBox || !IsLoaded)
        {
            return;
        }

        if (!GetCurrentChartCounters().Any())
        {
            return;
        }

        ApplyScaleToAllCounters_Click(sender, new RoutedEventArgs());
    }

    /// <summary>
    /// 現在のチャートタイプで表示中のカウンター一覧を取得
    /// </summary>
    private List<string> GetCurrentChartCounters()
    {
        return _currentChartType == ChartType.LineChart
            ? _chartSeries.Keys.OrderBy(c => c).ToList()
            : _areaChartSeries.Keys.OrderBy(c => c).ToList();
    }

    private static ComboBoxItem CreateScaleComboBoxItem(string scaleLabel)
    {
        return new ComboBoxItem
        {
            Content = scaleLabel,
            Tag = scaleLabel
        };
    }

    private static void AddSupportedScaleItems(ComboBox comboBox)
    {
        foreach (var scaleOption in ScaleCatalog.SupportedOptions)
        {
            comboBox.Items.Add(CreateScaleComboBoxItem(scaleOption.Label));
        }
    }

    /// <summary>
    /// カウンター別スケール設定コントロールを作成
    /// </summary>
    private static bool TrySelectScaleComboBoxItem(ComboBox scaleComboBox, double targetScale)
    {
        foreach (ComboBoxItem item in scaleComboBox.Items)
        {
            if (item.Tag?.ToString() is string tagValue &&
                double.TryParse(tagValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double itemScale) &&
                Math.Abs(itemScale - targetScale) < 1e-12)
            {
                scaleComboBox.SelectedItem = item;
                return true;
            }
        }

        return false;
    }

    private static string FormatScaleValue(double scale)
    {
        return ScaleCatalog.GetLabel(scale);
    }

    private static void EnsureScaleComboBoxContainsScale(ComboBox scaleComboBox, double scale)
    {
        if (TrySelectScaleComboBoxItem(scaleComboBox, scale))
        {
            return;
        }

        var scaleLabel = FormatScaleValue(scale);
        scaleComboBox.Items.Add(CreateScaleComboBoxItem(scaleLabel));
    }

    /// <summary>
    /// 表示中のカウンタースケールプルダウンを指定スケールに同期
    /// </summary>
    private void SyncVisibleCounterScaleComboBoxes(double targetScale)
    {
        _isUpdatingScaleControls = true;
        try
        {
            foreach (var border in CounterScaleStackPanel.Children.OfType<Border>())
            {
                if (border.Child is not Grid grid)
                {
                    continue;
                }

                var comboBox = grid.Children.OfType<ComboBox>().FirstOrDefault();
                if (comboBox == null)
                {
                    continue;
                }

                _ = TrySelectScaleComboBoxItem(comboBox, targetScale);
            }
        }
        finally
        {
            _isUpdatingScaleControls = false;
        }
    }

    /// <summary>
    /// カウンター別スケール設定コントロールを作成
    /// </summary>
    private Border CreateCounterScaleControl(string counter)
    {
        var border = new Border
        {
            BorderBrush = System.Windows.Media.Brushes.LightGray,
            BorderThickness = new Thickness(1, 1, 1, 1),
            Margin = new Thickness(0, 2, 0, 0),
            Padding = new Thickness(5, 5, 5, 5),
            Background = System.Windows.Media.Brushes.White
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // カウンター名表示
        var counterLabel = new TextBlock
        {
            Text = GetCounterDisplayName(counter),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = System.Windows.Media.Brushes.DarkBlue,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 3)
        };
        Grid.SetRow(counterLabel, 0);
        grid.Children.Add(counterLabel);

        // スケール選択コンボボックス
        var scaleComboBox = new ComboBox
        {
            Width = 160,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Tag = counter
        };

        // スケール値を追加
        AddSupportedScaleItems(scaleComboBox);

        // 現在のスケール値を選択
        var currentScale = _counterScales.GetValueOrDefault(counter, 1.0);
        EnsureScaleComboBoxContainsScale(scaleComboBox, currentScale);
        var selected = TrySelectScaleComboBoxItem(scaleComboBox, currentScale);
        if (!selected)
        {
            selected = TrySelectScaleComboBoxItem(scaleComboBox, 1.0);
        }
        if (!selected && scaleComboBox.Items.Count > 0)
        {
            scaleComboBox.SelectedIndex = 0;
        }

        // イベントハンドラー追加
        scaleComboBox.SelectionChanged += (sender, e) =>
        {
            if (_isUpdatingScaleControls)
            {
                return;
            }

            if (sender is ComboBox comboBox && 
                comboBox.Tag is string counterName &&
                comboBox.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Tag is string scaleString)
            {
                if (double.TryParse(scaleString, NumberStyles.Float, CultureInfo.InvariantCulture, out double newScale))
                {
                    var oldScale = _counterScales.GetValueOrDefault(counterName, 1.0);
                    _counterScales[counterName] = newScale;
                    
                    // グラフを即座に更新
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        RefreshCounterInChart(counterName);
                    }), System.Windows.Threading.DispatcherPriority.Normal);
                    
                    LogError($"Counter '{counterName}' scale changed from {oldScale} to {newScale} (グラフ表示位置のみ変更、実際のデータ値は保持)");
                }
            }
        };

        Grid.SetRow(scaleComboBox, 1);
        grid.Children.Add(scaleComboBox);

        border.Child = grid;
        return border;
    }

    /// <summary>
    /// スケールコントロールパネルの表示/非表示を更新
    /// </summary>
    private void UpdateScaleControlVisibility()
    {
        // スケールコントロール更新中はスキップ
        if (_isUpdatingScaleControls)
        {
            return;
        }
        
        bool hasChartData = HasChartPanelContext();
        if (!hasChartData)
        {
            ScaleControlGroupBox.Visibility = Visibility.Collapsed;
            ScalePanelDividerHost.Visibility = Visibility.Collapsed;

            if (ScalePanelColumn.Width.Value > 0)
            {
                _lastScalePanelWidth = ScalePanelColumn.Width;
            }

            ScalePanelColumn.MinWidth = 0;
            ScalePanelColumn.Width = new GridLength(0);
            ScalePanelSplitterColumn.Width = new GridLength(0);
            return;
        }

        ScalePanelDividerHost.Visibility = Visibility.Visible;
        ScalePanelToggleButton.Content = _isScalePanelCollapsed ? "◀" : "▶";
        ScalePanelToggleButton.ToolTip = _isScalePanelCollapsed
            ? "クリックでスケール設定エリアを表示します"
            : "クリックでスケール設定エリアを非表示にします";

        if (_isScalePanelCollapsed)
        {
            if (ScalePanelColumn.Width.Value > 0)
            {
                _lastScalePanelWidth = ScalePanelColumn.Width;
            }

            ScaleControlGroupBox.Visibility = Visibility.Collapsed;
            ScalePanelColumn.MinWidth = 0;
            ScalePanelColumn.Width = new GridLength(0);
            ScalePanelSplitterColumn.Width = new GridLength(22);
            return;
        }

        ScaleControlGroupBox.Visibility = Visibility.Visible;
        ScalePanelColumn.MinWidth = 210;
        ScalePanelSplitterColumn.Width = new GridLength(22);

        if (ScalePanelColumn.Width.Value <= 0)
        {
            ScalePanelColumn.Width = _lastScalePanelWidth.Value > 0 ? _lastScalePanelWidth : new GridLength(230);
        }

        // 既存のコントロールをクリア
        CounterScaleStackPanel.Children.Clear();
        
        // 現在のチャートタイプに応じてカウンターを取得
        var currentCounters = GetCurrentChartCounters();
        
        // 各カウンターのスケール設定コントロールを追加
        foreach (var counter in currentCounters)
        {
            var control = CreateCounterScaleControl(counter);
            CounterScaleStackPanel.Children.Add(control);
        }
    }

    /// <summary>
    /// 現在表示中の全カウンターに同一スケールを一括適用
    /// </summary>
    private void ApplyScaleToAllCounters_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (BulkScaleComboBox.SelectedItem is not ComboBoxItem selectedItem ||
                selectedItem.Tag is not string scaleString ||
                !double.TryParse(scaleString, NumberStyles.Float, CultureInfo.InvariantCulture, out double newScale))
            {
                AddOperationLog(LogLevel.Warning, "一括適用するスケール値を選択してください。");
                return;
            }

            var currentCounters = GetCurrentChartCounters();
            if (!currentCounters.Any())
            {
                AddOperationLog(LogLevel.Warning, "一括適用の対象カウンターがありません。");
                return;
            }

            foreach (var counter in currentCounters)
            {
                _counterScales[counter] = newScale;
            }

            if (_currentChartType == ChartType.LineChart)
            {
                foreach (var counter in currentCounters)
                {
                    RefreshCounterInChart(counter);
                }
            }
            else
            {
                RefreshChartWithCurrentType();
            }

            UpdateScaleControlVisibility();
            SyncVisibleCounterScaleComboBoxes(newScale);

            AddOperationLog(LogLevel.Success, $"全{currentCounters.Count}カウンターにスケール {scaleString} を適用しました。");
        }
        catch (Exception ex)
        {
            AddOperationLog(LogLevel.Error, $"一括スケール適用に失敗しました: {ex.Message}");
            LogError($"Bulk scale apply failed: {ex}");
        }
    }

    /// <summary>
    /// 選択カウンター読み込み後に一括スケールのUI選択だけを既定値(1.0)へ戻す
    /// </summary>
    private void ResetBulkScaleSelectorAfterCounterLoad()
    {
        const double defaultScale = 1.0;

        if (BulkScaleComboBox != null)
        {
            _isInitializingBulkScaleComboBox = true;
            try
            {
                _ = TrySelectScaleComboBoxItem(BulkScaleComboBox, defaultScale);
            }
            finally
            {
                _isInitializingBulkScaleComboBox = false;
            }
        }

        UpdateScaleControlVisibility();
    }

    /// <summary>
    /// 特定のカウンターのグラフ表示を更新
    /// </summary>
    private void RefreshCounterInChart(string counter)
    {
        // 現在のチャートタイプに応じてシリーズの存在をチェック
        bool hasLineChart = _chartSeries.ContainsKey(counter);
        bool hasAreaChart = _areaChartSeries.ContainsKey(counter);
        
        if (hasLineChart || hasAreaChart)
        {
            System.Diagnostics.Debug.WriteLine($"Refreshing chart for counter: {counter}");
            
            // スケールコントロールの更新を一時的に無効化
            _isUpdatingScaleControls = true;
            
            try
            {
                // 積み重ね面グラフの場合は全体を再描画
                if (_currentChartType == ChartType.StackedAreaChart)
                {
                    System.Diagnostics.Debug.WriteLine("Stacked area chart requires full redraw for display scale change");
                    RefreshChartWithCurrentType();
                }
                else
                {
                    // 折れ線グラフの場合は個別に更新
                    if (_chartSeries.TryGetValue(counter, out var scatter))
                    {
                        PerformanceChart.Plot.Remove(scatter);
                        _chartSeries.Remove(counter);
                        PerformanceChart.Refresh();
                        
                        // X軸の範囲を選択された時間範囲に設定
                        UpdateChartXAxisRange();
                        
                        System.Diagnostics.Debug.WriteLine($"Removed line series from chart for: {counter}");
                    }
                    
                    // 新しいスケールで再追加（スケールコントロール更新なし）
                    AddCounterToChartInternal(counter);
                }
            }
            finally
            {
                // スケールコントロールの更新を再有効化
                _isUpdatingScaleControls = false;
                
                // 最後に一度だけスケールコントロールを更新
                UpdateScaleControlVisibility();
                
                // 統計情報も更新
                UpdateStatisticsDisplay();
            }
            
            System.Diagnostics.Debug.WriteLine($"Chart refreshed for counter: {counter}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"Counter not found in chart series: {counter}");
        }
    }

    #endregion

    #region パターン管理機能

    /// <summary>
    /// パターン管理機能の初期化
    /// </summary>
    private async Task InitializePatternManagerAsync()
    {
        try
        {
            _patternManager = new CounterPatternManager();
            var loaded = await _patternManager.LoadConfigAsync();
            
            if (loaded)
            {
                await RefreshPatternMenuAsync();
                await LogErrorAsync("パターン設定が正常に読み込まれました。");
            }
            else
            {
                await LogErrorAsync("パターン設定の読み込みに失敗しました。");
            }
        }
        catch (Exception ex)
        {
            await LogErrorAsync($"パターン管理機能の初期化エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// パターンメニューの更新
    /// </summary>
    private async Task RefreshPatternMenuAsync()
    {
        try
        {
            if (_patternManager == null) return;

            // コンボボックスの更新
            var patterns = _patternManager.GetAvailablePatterns().ToList();
            PatternComboBox.ItemsSource = patterns;
            
            // メニューアイテムの更新
            PatternMenu.Items.Clear();
            
            if (patterns.Any())
            {
                foreach (var pattern in patterns)
                {
                    var menuItem = new MenuItem
                    {
                        Header = pattern.Name,
                        ToolTip = pattern.Description,
                        Tag = pattern
                    };
                    menuItem.Click += PatternMenuItem_Click;
                    PatternMenu.Items.Add(menuItem);
                }
            }
            else
            {
                var noPatternItem = new MenuItem
                {
                    Header = "利用可能なパターンがありません",
                    IsEnabled = false
                };
                PatternMenu.Items.Add(noPatternItem);
            }
        }
        catch (Exception ex)
        {
            await LogErrorAsync($"パターンメニューの更新エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// パターンコンボボックスの選択変更イベント
    /// </summary>
    private void PatternComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyPatternButton.IsEnabled = PatternComboBox.SelectedItem != null;
    }

    /// <summary>
    /// パターン適用ボタンのクリックイベント
    /// </summary>
    private async void ApplyPattern_Click(object sender, RoutedEventArgs e)
    {
        if (PatternComboBox.SelectedItem is CounterPattern pattern)
        {
            await ApplyCounterPatternAsync(pattern);
        }
    }

    /// <summary>
    /// パターンメニューアイテムのクリックイベント
    /// </summary>
    private async void PatternMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is CounterPattern pattern)
        {
            await ApplyCounterPatternAsync(pattern);
        }
    }

    /// <summary>
    /// カウンターパターンの適用
    /// </summary>
    private async Task ApplyCounterPatternAsync(CounterPattern pattern)
    {
        try
        {
            if (_counterTreeNodes.Count == 0)
            {
                AddOperationLog(LogLevel.Warning, "BLGファイルが読み込まれていません。まずBLGファイルを開いてください。");
                MessageBox.Show("BLGファイルが読み込まれていません。まずBLGファイルを開いてください。", 
                              "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 現在の選択をすべて解除（リーフノードのみ操作して、親ノードは自動更新させる）
            foreach (var objNode in _counterTreeNodes)
            {
                foreach (var instNode in objNode.Children)
                {
                    foreach (var counterNode in instNode.Children)
                    {
                        // カウンターノード（リーフノード）のみを解除
                        // 親ノードの状態は自動的に更新される
                        counterNode.IsChecked = false;
                    }
                }
            }

            var appliedCounters = new List<string>();
            var notFoundCounters = new List<string>();

            // パターンに含まれる各カウンターを検索・選択
            foreach (var counterDef in pattern.Counters.Where(c => c.Enabled))
            {
                var found = await SelectCounterByPatternAsync(counterDef.Name, counterDef.Scale);
                if (found)
                {
                    appliedCounters.Add(counterDef.Name);
                }
                else
                {
                    notFoundCounters.Add(counterDef.Name);
                }
            }

            // パターン適用後、親ノードの状態を階層的に更新
            // まず最下位（カウンター）から最上位（オブジェクト）へ順番に更新
            foreach (var objNode in _counterTreeNodes)
            {
                foreach (var instNode in objNode.Children)
                {
                    // インスタンス レベルの状態更新
                    instNode.UpdateParentStateFromChild();
                }
                // オブジェクト レベルの状態更新
                objNode.UpdateParentStateFromChild();
            }

            var chartTypeChanged = ApplyChartType(pattern.GraphType, refreshChart: false);
            var valueModeChanged = ApplyValueMode(pattern.ValueMode, refreshDisplays: false, logChange: false);
            if (chartTypeChanged || valueModeChanged)
            {
                RefreshChartWithCurrentType();
                RefreshAllDataTabsForCurrentMode();
            }

            // パターン適用後の選択状態でrelog表示を更新
            UpdateRelogCommandDisplay();

            // 結果の表示
            var message = $"パターン「{pattern.Name}」を適用しました。\n" +
                         $"✅ 適用されたカウンター: {appliedCounters.Count}個\n" +
                         $"📊 グラフタイプ: {GetChartTypeDisplayName(pattern.GraphType)}\n" +
                         $"📉 値モード: {GetValueModeDisplayName(pattern.ValueMode)}";
            
            if (notFoundCounters.Any())
            {
                message += $"\n⚠️ 見つからなかったカウンター: {notFoundCounters.Count}個\n\n";
                message += "見つからなかったカウンター:\n";
                message += string.Join("\n", notFoundCounters.Take(5));
                if (notFoundCounters.Count > 5)
                {
                    message += $"\n...他 {notFoundCounters.Count - 5}個";
                }
            }
            
            AddOperationLog(LogLevel.Success, message);
            
            await LogErrorAsync($"パターン「{pattern.Name}」が適用されました。適用: {appliedCounters.Count}個, 未検出: {notFoundCounters.Count}個");
        }
        catch (Exception ex)
        {
            await LogErrorAsync($"パターン適用エラー: {ex.Message}");
            AddOperationLog(LogLevel.Error, $"パターンの適用中にエラーが発生しました: {ex.Message}");
            MessageBox.Show($"パターンの適用中にエラーが発生しました: {ex.Message}", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }



    /// <summary>
    /// パターンに基づいてカウンターを検索・選択
    /// </summary>
    private async Task<bool> SelectCounterByPatternAsync(string counterPattern, double scale)
    {
        try
        {
            // 完全一致を試行
            var exactMatch = await FindCounterNodeAsync(counterPattern);
            if (exactMatch != null)
            {
                exactMatch.IsChecked = true;
                _counterScales[exactMatch.FullPath] = scale;
                return true;
            }

            // パターンマッチング（ワイルドカード対応）
            var patternMatches = await FindCountersByPatternAsync(counterPattern);
            if (patternMatches.Any())
            {
                foreach (var match in patternMatches)
                {
                    match.IsChecked = true;
                    _counterScales[match.FullPath] = scale;
                }
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            await LogErrorAsync($"カウンター検索エラー（パターン: {counterPattern}）: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 完全一致でカウンターノードを検索
    /// </summary>
    private async Task<CounterTreeNode?> FindCounterNodeAsync(string fullPath)
    {
        return await Task.Run(() =>
        {
            foreach (var objNode in _counterTreeNodes)
            {
                foreach (var instNode in objNode.Children)
                {
                    foreach (var counterNode in instNode.Children)
                    {
                        if (counterNode.IsWildCard)
                        {
                            continue;
                        }

                        if (counterNode.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            return counterNode;
                        }
                    }
                }
            }
            return null;
        });
    }

    /// <summary>
    /// パターンマッチングでカウンターノードを検索（ワイルドカード対応）
    /// \Object(instance)\* のような指定で該当オブジェクト配下の全カウンターを選択できる。
    /// </summary>
    private async Task<List<CounterTreeNode>> FindCountersByPatternAsync(string pattern)
    {
        return await Task.Run(() =>
        {
            var matches = new List<CounterTreeNode>();

            foreach (var objNode in _counterTreeNodes)
            {
                foreach (var instNode in objNode.Children)
                {
                    foreach (var counterNode in instNode.Children)
                    {
                        if (counterNode.IsWildCard)
                        {
                            continue;
                        }

                        if (CounterPathPatternMatcher.MatchesPattern(pattern, counterNode.FullPath))
                        {
                            matches.Add(counterNode);
                        }
                    }
                }
            }

            return matches;
        });
    }

    /// <summary>
    /// パターン設定ファイルを開く
    /// </summary>
    private async void OpenPatternConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_patternManager != null)
            {
                var configPath = _patternManager.ConfigFilePath;
                
                if (File.Exists(configPath))
                {
                    var result = MessageBox.Show(
                        $"パターン設定ファイルを開きますか？\n\n場所: {configPath}\n\n" +
                        "ファイルを編集した後は「パターン設定を再読み込み」メニューから設定を更新してください。",
                        "設定ファイルを開く", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = configPath,
                            UseShellExecute = true
                        });
                    }
                }
                else
                {
                    AddOperationLog(LogLevel.Warning, $"設定ファイルが見つかりません: {configPath}");
                    MessageBox.Show($"設定ファイルが見つかりません: {configPath}", 
                                  "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            await LogErrorAsync($"設定ファイルオープンエラー: {ex.Message}");
            AddOperationLog(LogLevel.Error, $"設定ファイルを開けませんでした: {ex.Message}");
            MessageBox.Show($"設定ファイルを開けませんでした: {ex.Message}", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// パターン設定の再読み込み
    /// </summary>
    private async void ReloadPatternConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_patternManager == null)
            {
                _patternManager = new CounterPatternManager();
            }
            
            var loaded = await _patternManager.LoadConfigAsync();
            
            if (loaded)
            {
                await RefreshPatternMenuAsync();
                AddOperationLog(LogLevel.Success, "パターン設定を再読み込みしました。");
                await LogErrorAsync("パターン設定が再読み込みされました。");
            }
            else
            {
                AddOperationLog(LogLevel.Error, "パターン設定の再読み込みに失敗しました。エラーログを確認してください。");
                MessageBox.Show("パターン設定の再読み込みに失敗しました。エラーログを確認してください。", 
                              "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            await LogErrorAsync($"パターン設定再読み込みエラー: {ex.Message}");
            AddOperationLog(LogLevel.Error, $"パターン設定の再読み込み中にエラーが発生しました: {ex.Message}");
            MessageBox.Show($"パターン設定の再読み込み中にエラーが発生しました: {ex.Message}", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 非同期エラーログ出力
    /// </summary>
    private async Task LogErrorAsync(string message)
    {
        await Task.Run(() => LogError(message));
    }

    #endregion

    #region ヘルパーメソッド

    /// <summary>
    /// relog.exeコマンドライン文字列を生成
    /// </summary>
    private string GenerateRelogCommand(string blgFilePath, List<string> counters, DateTime? startTime, DateTime? endTime)
    {
        try
        {
            var commandBuilder = new StringBuilder();
            commandBuilder.AppendLine("relog.exe `");
            
            // 入力ファイル（引用符で囲む）
            commandBuilder.AppendLine($"  \"{blgFilePath}\" `");
            
            // 出力ファイル
            var outputFileName = Path.GetFileNameWithoutExtension(blgFilePath) + "_output.blg";
            commandBuilder.AppendLine($"  -o \"{outputFileName}\" `");
            
            // バイナリフォーマット出力を明示的に指定
            commandBuilder.AppendLine("  -f BIN `");
            
            // 時間範囲指定（時間制約が指定されていない場合でもファイル内の開始/終了時間を使用）
            DateTime effectiveStartTime = startTime ?? _fileStartTime;
            DateTime effectiveEndTime = endTime ?? _fileEndTime;
            
            commandBuilder.AppendLine($"  -b \"{effectiveStartTime:yyyy/MM/dd HH:mm:ss}\" `");
            commandBuilder.Append($"  -e \"{effectiveEndTime:yyyy/MM/dd HH:mm:ss}\"");
            
            return commandBuilder.ToString();
        }
        catch (Exception ex)
        {
            LogError($"relog.exeコマンド生成エラー: {ex.Message}");
            return $"relog.exe コマンド生成エラー: {ex.Message}";
        }
    }

    /// <summary>
    /// relog.exe情報表示を更新
    /// </summary>
    private void UpdateRelogCommandDisplay()
    {
        try
        {
            // 現在選択されているカウンターを取得
            var selectedCounters = GetSelectedCounters();
            
            if (selectedCounters.Count == 0 || string.IsNullOrEmpty(_currentBlgFile))
            {
                // 選択されたカウンターまたはBLGファイルがない場合は非表示
                RelogCommandExpander.Visibility = Visibility.Collapsed;
                return;
            }
            
            // 時間制約の有効性を判定
            bool useTimeConstraints = _timeRangeDetected && (StartTimeSlider.Value > 0 || EndTimeSlider.Value < 100);
            DateTime? startTime = null;
            DateTime? endTime = null;
            
            if (useTimeConstraints)
            {
                var totalDuration = _fileEndTime - _fileStartTime;
                startTime = _fileStartTime.AddMilliseconds(totalDuration.TotalMilliseconds * StartTimeSlider.Value / 100);
                endTime = _fileStartTime.AddMilliseconds(totalDuration.TotalMilliseconds * EndTimeSlider.Value / 100);
            }
            
            // relog.exeコマンドを生成
            string relogCommand = GenerateRelogCommand(_currentBlgFile, selectedCounters, startTime, endTime);
            
            // UI表示を更新
            RelogCommandExpander.Visibility = Visibility.Visible;
            RelogCommandDisplay.Text = relogCommand;
        }
        catch (Exception ex)
        {
            LogError($"relog.exe情報表示の更新エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// 時間スライダーの値から実際のDateTime値を計算する
    /// </summary>
    private (DateTime startTime, DateTime endTime) GetSelectedTimeRange()
    {
        if (!_timeRangeDetected)
        {
            return (_fileStartTime, _fileEndTime);
        }

        var totalDuration = _fileEndTime - _fileStartTime;
        var startTime = _fileStartTime.AddMilliseconds(totalDuration.TotalMilliseconds * StartTimeSlider.Value / 100);
        var endTime = _fileStartTime.AddMilliseconds(totalDuration.TotalMilliseconds * EndTimeSlider.Value / 100);
        
        return (startTime, endTime);
    }

    /// <summary>
    /// グラフのX軸範囲を選択された時間範囲に設定する
    /// </summary>
    private void UpdateChartXAxisRange()
    {
        try
        {
            if (!_timeRangeDetected)
            {
                return;
            }

            var (startTime, endTime) = GetSelectedTimeRange();
            
            // ScottPlotでX軸の範囲を設定
            PerformanceChart.Plot.Axes.Bottom.Min = startTime.ToOADate();
            PerformanceChart.Plot.Axes.Bottom.Max = endTime.ToOADate();
            
            // グラフを更新
            PerformanceChart.Refresh();
            
            System.Diagnostics.Debug.WriteLine($"X軸範囲を更新: {startTime:yyyy-MM-dd HH:mm:ss} ～ {endTime:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex)
        {
            LogError($"グラフX軸範囲の更新エラー: {ex.Message}");
        }
    }

    #endregion

    #region ログ管理機能

    /// <summary>
    /// 操作ログを追加
    /// </summary>
    private void AddOperationLog(LogLevel level, string message)
    {
        try
        {
            var logEntry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            };
            
            // UIスレッドで実行
            if (Dispatcher.CheckAccess())
            {
                _operationLogs.Insert(0, logEntry); // 新しいログを先頭に追加
                
                // 最大1000件で古いログを削除
                while (_operationLogs.Count > 1000)
                {
                    _operationLogs.RemoveAt(_operationLogs.Count - 1);
                }
            }
            else
            {
                Dispatcher.Invoke(() => AddOperationLog(level, message));
            }
            
            // ファイルにも記録
            LogInfo($"[{level}] {message}");
        }
        catch (Exception ex)
        {
            LogError($"操作ログの追加に失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// エラーログをファイルから読み込み
    /// </summary>
    private void LoadErrorLogFromFile()
    {
        try
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            
            if (File.Exists(logPath))
            {
                _errorLogs.Clear();
                
                var lines = File.ReadAllLines(logPath, Encoding.UTF8);
                
                foreach (var line in lines.Reverse().Take(1000)) // 最新1000件のみ
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    var logEntry = ParseLogLine(line);
                    if (logEntry != null)
                    {
                        _errorLogs.Add(logEntry);
                    }
                }
                
                AddOperationLog(LogLevel.Info, $"エラーログファイルから {_errorLogs.Count} 件のログを読み込みました。");
            }
            else
            {
                AddOperationLog(LogLevel.Info, "エラーログファイルが見つかりません。");
            }
        }
        catch (Exception ex)
        {
            AddOperationLog(LogLevel.Error, $"エラーログの読み込みに失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// ログ行を解析してLogEntryを作成
    /// </summary>
    private LogEntry? ParseLogLine(string line)
    {
        try
        {
            // フォーマット: [yyyy-MM-dd HH:mm:ss] メッセージ
            if (line.StartsWith("[") && line.Contains("] "))
            {
                var endBracket = line.IndexOf("] ");
                if (endBracket > 0)
                {
                    var timestampStr = line.Substring(1, endBracket - 1);
                    var message = line.Substring(endBracket + 2);
                    
                    if (DateTime.TryParse(timestampStr, out var timestamp))
                    {
                        // メッセージからレベルを推定
                        var level = LogLevel.Info;
                        if (message.Contains("ERROR") || message.Contains("エラー") || message.Contains("失敗"))
                            level = LogLevel.Error;
                        else if (message.Contains("WARNING") || message.Contains("警告"))
                            level = LogLevel.Warning;
                        else if (message.Contains("INFO"))
                            level = LogLevel.Info;
                        
                        return new LogEntry
                        {
                            Timestamp = timestamp,
                            Level = level,
                            Message = message
                        };
                    }
                }
            }
            
            // タイムスタンプがない場合は現在時刻を使用
            return new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Info,
                Message = line
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 操作ログをクリア
    /// </summary>
    private void ClearOperationLogs()
    {
        try
        {
            _operationLogs.Clear();
            AddOperationLog(LogLevel.Info, "操作ログがクリアされました。");
        }
        catch (Exception ex)
        {
            LogError($"操作ログのクリアに失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// エラーログをクリア
    /// </summary>
    private void ClearErrorLogs()
    {
        try
        {
            _errorLogs.Clear();
            
            // ファイルもクリア
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            if (File.Exists(logPath))
            {
                File.WriteAllText(logPath, string.Empty);
            }
            
            AddOperationLog(LogLevel.Info, "エラーログがクリアされました。");
        }
        catch (Exception ex)
        {
            AddOperationLog(LogLevel.Error, $"エラーログのクリアに失敗: {ex.Message}");
        }
    }

    #endregion

    #region グラフ操作メソッド

    /// <summary>
    /// キーボードショートカットの処理
    /// </summary>
    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+C でグラフをクリップボードにコピー
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (GraphMenu.IsEnabled) // グラフが表示されている場合のみ実行
            {
                CopyGraphToClipboardInternal();
                e.Handled = true;
            }
        }
    }

    /// <summary>
    /// グラフ領域の右クリック時にアプリ独自コンテキストメニューを表示
    /// </summary>
    private void PerformanceChart_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (PerformanceChart.ContextMenu is null)
            {
                return;
            }

            PerformanceChart.ContextMenu.PlacementTarget = PerformanceChart;
            PerformanceChart.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            PerformanceChart.ContextMenu.IsOpen = true;
            e.Handled = true;
        }
        catch (Exception ex)
        {
            LogError($"グラフコンテキストメニュー表示エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// グラフをクリップボードにコピーする（UI イベント）
    /// </summary>
    private void CopyGraphToClipboard_Click(object sender, RoutedEventArgs e)
    {
        CopyGraphToClipboardInternal();
    }

    /// <summary>
    /// Y軸範囲入力値を適用する
    /// </summary>
    private void ApplyYAxisRange_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!TryParseYAxisInput(YAxisMinTextBox.Text, out double yAxisMin) ||
                !TryParseYAxisInput(YAxisMaxTextBox.Text, out double yAxisMax))
            {
                AddOperationLog(LogLevel.Warning, "Y軸範囲の入力値が不正です。数値を入力してください。");
                return;
            }

            if (yAxisMax <= yAxisMin)
            {
                AddOperationLog(LogLevel.Warning, "Y軸範囲の入力値が不正です。上限は下限より大きい値を指定してください。");
                return;
            }

            _manualYAxisMin = yAxisMin;
            _manualYAxisMax = yAxisMax;
            _isManualYAxisRangeEnabled = true;

            EnsureYAxisFixedRange();
            PerformanceChart.Refresh();
            AddOperationLog(LogLevel.Info, $"Y軸範囲を手動設定しました: {_manualYAxisMin} ～ {_manualYAxisMax}");
        }
        catch (Exception ex)
        {
            LogError($"Y軸範囲適用エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// Y軸範囲入力のEnterキー操作
    /// </summary>
    private void YAxisRangeTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        ApplyYAxisRange_Click(sender, new RoutedEventArgs());
        e.Handled = true;
    }

    /// <summary>
    /// Y軸範囲の自動設定に戻す
    /// </summary>
    private void ResetYAxisRange_Click(object sender, RoutedEventArgs e)
    {
        _isManualYAxisRangeEnabled = false;
        _manualYAxisMin = 0;
        _manualYAxisMax = 100;
        YAxisMinTextBox.Text = "0";
        YAxisMaxTextBox.Text = "100";

        EnsureYAxisFixedRange();
        PerformanceChart.Refresh();
        AddOperationLog(LogLevel.Info, "Y軸範囲を自動設定に戻しました。");
    }

    /// <summary>
    /// Y軸範囲入力の数値解析
    /// </summary>
    private static bool TryParseYAxisInput(string? input, out double value)
    {
        var text = input?.Trim() ?? string.Empty;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// ズームリセットボタンのクリックイベント
    /// </summary>
    private void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 時間範囲スライダーを初期状態にリセット
            StartTimeSlider.Value = 0;
            EndTimeSlider.Value = 100;
            
            // スライダーテキストを更新
            UpdateTimeSliderTexts();
            
            // Y軸範囲を現在データに合わせて再設定
            EnsureYAxisFixedRange();
            
            // X軸範囲を更新（全体範囲に戻す）
            UpdateChartXAxisRange();
            
            // グラフを更新
            PerformanceChart.Refresh();
            
            AddOperationLog(LogLevel.Info, "ズームと時間範囲をリセットしました。");
            System.Diagnostics.Debug.WriteLine("ズームリセット: 時間範囲とズーム倍率を初期状態に戻しました");
        }
        catch (Exception ex)
        {
            LogError($"ズームリセットエラー: {ex.Message}");
            AddOperationLog(LogLevel.Error, $"ズームリセット中にエラーが発生しました: {ex.Message}");
        }
    }

    /// <summary>
    /// グラフをクリップボードにコピーする内部実装
    /// </summary>
    private void CopyGraphToClipboardInternal()
    {
        try
        {
            // グラフにデータがあるかチェック
            if (PerformanceChart.Plot.PlottableList.Count == 0)
            {
                MessageBox.Show("コピーするグラフデータがありません。\nカウンターを選択してグラフを表示してからコピーしてください。", 
                               "グラフコピー", MessageBoxButton.OK, MessageBoxImage.Information);
                AddOperationLog(LogLevel.Warning, "グラフコピー: 表示されているグラフがありません");
                return;
            }

            // WPF コントロールから画像を取得
            int width = (int)PerformanceChart.ActualWidth;
            int height = (int)PerformanceChart.ActualHeight;
            
            if (width <= 0 || height <= 0)
            {
                width = 800;
                height = 600;
            }

            var renderTargetBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(PerformanceChart);
            
            // クリップボードにコピー
            Clipboard.SetImage(renderTargetBitmap);
            
            AddOperationLog(LogLevel.Info, "グラフをクリップボードにコピーしました");
            
            // ユーザーに成功を通知（オプション）
            // MessageBox.Show("グラフをクリップボードにコピーしました。", "グラフコピー", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AddOperationLog(LogLevel.Error, $"グラフのクリップボードコピーに失敗: {ex.Message}");
            MessageBox.Show($"グラフのコピーに失敗しました。\n{ex.Message}", 
                           "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region ヘルパーメソッド（ファイル情報）

    /// <summary>
    /// ファイルサイズを読みやすい形式にフォーマット
    /// </summary>
    /// <param name="bytes">バイト数</param>
    /// <returns>フォーマットされたファイルサイズ文字列</returns>
    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    /// <summary>
    /// 凡例の初期化
    /// </summary>
    private void InitializeLegend()
    {
        LegendItemsControl.ItemsSource = _legendItems;
    }

    private bool IsLineCounterHighlighted(string counterPath)
    {
        return _currentChartType == ChartType.LineChart
            && _highlightedLegendCounterPaths.Contains(counterPath);
    }

    private void ApplyLineSeriesHighlight(bool refresh = true)
    {
        _highlightedLegendCounterPaths.RemoveWhere(path => !_chartSeries.ContainsKey(path));

        foreach (var item in _legendItems)
        {
            item.IsHighlighted = IsLineCounterHighlighted(item.CounterPath);
        }

        foreach (var kvp in _chartSeries)
        {
            var lineWidth = IsLineCounterHighlighted(kvp.Key) ? HighlightedLineWidth : DefaultLineWidth;
            kvp.Value.LineWidth = lineWidth;
            kvp.Value.LineStyle.Width = lineWidth;
        }

        if (refresh)
        {
            PerformanceChart.Refresh();
        }
    }

    /// <summary>
    /// 凡例から折れ線のハイライトを切り替え
    /// </summary>
    private void LegendHighlight_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string counterPath })
        {
            return;
        }

        if (_currentChartType != ChartType.LineChart)
        {
            AddOperationLog(LogLevel.Info, "凡例ハイライトは折れ線グラフでのみ使用できます。");
            return;
        }

        if (_highlightedLegendCounterPaths.Contains(counterPath))
        {
            _highlightedLegendCounterPaths.Remove(counterPath);
        }
        else
        {
            _highlightedLegendCounterPaths.Add(counterPath);
        }
        ApplyLineSeriesHighlight();
    }

    /// <summary>
    /// すべてのシリーズを表示
    /// </summary>
    private void ShowAllSeries_Click(object sender, RoutedEventArgs e)
    {
        _isBulkLegendVisibilityUpdate = true;
        try
        {
            foreach (var item in _legendItems)
            {
                item.IsVisible = true;
                _seriesVisibility[item.CounterPath] = true;
            }
        }
        finally
        {
            _isBulkLegendVisibilityUpdate = false;
        }

        UpdateChartSeriesVisibility();
    }
    
    /// <summary>
    /// すべてのシリーズを非表示
    /// </summary>
    private void HideAllSeries_Click(object sender, RoutedEventArgs e)
    {
        _highlightedLegendCounterPaths.Clear();
        _isBulkLegendVisibilityUpdate = true;
        try
        {
            foreach (var item in _legendItems)
            {
                item.IsVisible = false;
                _seriesVisibility[item.CounterPath] = false;
            }
        }
        finally
        {
            _isBulkLegendVisibilityUpdate = false;
        }

        UpdateChartSeriesVisibility();
    }
    
    /// <summary>
    /// 凡例アイテムのチェック状態変更
    /// </summary>
    private void LegendItem_Checked(object sender, RoutedEventArgs e)
    {
        if (_isBulkLegendVisibilityUpdate)
        {
            return;
        }

        if (sender is CheckBox checkBox && checkBox.Tag is string counterPath)
        {
            _seriesVisibility[counterPath] = true;
            UpdateChartSeriesVisibility();
        }
    }
    
    /// <summary>
    /// 凡例アイテムのチェック状態変更
    /// </summary>
    private void LegendItem_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isBulkLegendVisibilityUpdate)
        {
            return;
        }

        if (sender is CheckBox checkBox && checkBox.Tag is string counterPath)
        {
            _seriesVisibility[counterPath] = false;
            _highlightedLegendCounterPaths.Remove(counterPath);
            UpdateChartSeriesVisibility();
        }
    }
    
    /// <summary>
    /// グラフシリーズの表示/非表示を更新
    /// </summary>
    private void UpdateChartSeriesVisibility()
    {
        try
        {
            if (_currentChartType == ChartType.StackedAreaChart)
            {
                RefreshChartWithCurrentType();
                return;
            }

            // 折れ線グラフの場合
            foreach (var kvp in _chartSeries)
            {
                var counterPath = kvp.Key;
                var series = kvp.Value;
                var isVisible = _seriesVisibility.GetValueOrDefault(counterPath, true);
                
                series.IsVisible = isVisible;
            }
            
            // 積み重ね面グラフの場合
            foreach (var kvp in _areaChartSeries)
            {
                var counterPath = kvp.Key;
                var series = kvp.Value;
                var isVisible = _seriesVisibility.GetValueOrDefault(counterPath, true);
                
                series.IsVisible = isVisible;
            }

            ApplyLineSeriesHighlight(refresh: false);
            PerformanceChart.Refresh();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"シリーズ表示更新エラー: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 凡例アイテムを追加
    /// </summary>
    private void AddLegendItem(string counterPath, string counterName, System.Windows.Media.Color color)
    {
        var existingItem = _legendItems.FirstOrDefault(item => item.CounterPath == counterPath);
        if (existingItem != null)
        {
            // 既存のアイテムを更新
            existingItem.Color = color;
            existingItem.IsVisible = _seriesVisibility.GetValueOrDefault(counterPath, true);
            existingItem.IsHighlighted = IsLineCounterHighlighted(counterPath);
            return;
        }
        
        var legendItem = new LegendItem
        {
            CounterPath = counterPath,
            CounterName = counterName,
            Color = color,
            IsVisible = _seriesVisibility.GetValueOrDefault(counterPath, true),
            IsHighlighted = IsLineCounterHighlighted(counterPath),
            CurrentValue = "-"
        };
        
        _legendItems.Add(legendItem);
    }
    
    /// <summary>
    /// 凡例アイテムを削除
    /// </summary>
    private void RemoveLegendItem(string counterPath)
    {
        var item = _legendItems.FirstOrDefault(item => item.CounterPath == counterPath);
        if (item != null)
        {
            _legendItems.Remove(item);
        }

        _highlightedLegendCounterPaths.Remove(counterPath);
    }
    
    /// <summary>
    /// すべての凡例アイテムをクリア
    /// </summary>
    private void ClearLegendItems(bool clearVisibility = true)
    {
        _legendItems.Clear();
        if (clearVisibility)
        {
            _seriesVisibility.Clear();
            _highlightedLegendCounterPaths.Clear();
        }
    }
    
    /// <summary>
    /// 凡例アイテムの現在値を更新
    /// </summary>
    private void UpdateLegendCurrentValues()
    {
        try
        {
            foreach (var item in _legendItems)
            {
                if (TryGetLatestDisplayValue(item.CounterPath, out var latestValue))
                {
                    // 値をフォーマット
                    item.CurrentValue = FormatCounterValue(latestValue);
                }
                else
                {
                    item.CurrentValue = "-";
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"凡例の現在値更新エラー: {ex.Message}");
        }
    }
    
    /// <summary>
    /// カウンター値をフォーマット
    /// </summary>
    private string FormatCounterValue(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return "-";
            
        if (Math.Abs(value) >= 1000000)
            return $"{value / 1000000:F2}M";
        if (Math.Abs(value) >= 1000)
            return $"{value / 1000:F2}K";
        if (Math.Abs(value) >= 1)
            return $"{value:F2}";
        
        return $"{value:F4}";
    }

    #endregion

    #region グラフサイズ関連

    /// <summary>
    /// グラフサイズ監視の初期化
    /// </summary>
    private void InitializeGraphSizeTracking()
    {
        // 初期グラフサイズの表示
        UpdateGraphSizeDisplay();
        
        // PerformanceChartのサイズ変更イベントの監視
        if (PerformanceChart != null)
        {
            PerformanceChart.SizeChanged += PerformanceChart_SizeChanged;
        }
        
        // リサイズハンドルの初期化
        InitializeResizeHandle();
    }

    /// <summary>
    /// リサイズハンドルの初期化
    /// </summary>
    private void InitializeResizeHandle()
    {
        try
        {
            if (ResizeHandle != null)
            {
                // マウスイベントの設定
                ResizeHandle.MouseDown += ResizeHandle_MouseDown;
                ResizeHandle.MouseMove += ResizeHandle_MouseMove;
                ResizeHandle.MouseUp += ResizeHandle_MouseUp;
                ResizeHandle.MouseLeave += ResizeHandle_MouseLeave;
                
                // マウスエンター/リーブイベントでハンドルの表示/非表示
                if (GraphContainer != null)
                {
                    GraphContainer.MouseEnter += GraphContainer_MouseEnter;
                    GraphContainer.MouseLeave += GraphContainer_MouseLeave;
                }
                
                LogInfo("グラフリサイズハンドルを初期化しました");
            }
        }
        catch (Exception ex)
        {
            LogError($"リサイズハンドルの初期化中にエラーが発生しました: {ex.Message}");
        }
    }

    /// <summary>
    /// PerformanceChartサイズ変更イベントハンドラー
    /// </summary>
    private void PerformanceChart_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateGraphSizeDisplay();
    }

    /// <summary>
    /// グラフサイズ表示の更新
    /// </summary>
    private void UpdateGraphSizeDisplay()
    {
        try
        {
            if (GraphSizeText != null && PerformanceChart != null)
            {
                // 固定サイズが設定されているかチェック
                if (!double.IsNaN(PerformanceChart.Width) && !double.IsNaN(PerformanceChart.Height))
                {
                    // 固定サイズの場合
                    GraphSizeText.Text = $"グラフサイズ: {PerformanceChart.Width:F0}×{PerformanceChart.Height:F0} (固定)";
                }
                else
                {
                    // 自動サイズの場合はActualWidthとActualHeightを表示
                    var width = PerformanceChart.ActualWidth > 0 ? PerformanceChart.ActualWidth : 800;
                    var height = PerformanceChart.ActualHeight > 0 ? PerformanceChart.ActualHeight : 400;
                    
                    GraphSizeText.Text = $"グラフサイズ: {width:F0}×{height:F0} (自動)";
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"グラフサイズ表示の更新中にエラーが発生しました: {ex.Message}");
        }
    }

    #region グラフリサイズハンドル機能

    /// <summary>
    /// グラフコンテナーへのマウス進入イベント
    /// </summary>
    private void GraphContainer_MouseEnter(object sender, MouseEventArgs e)
    {
        try
        {
            // 固定サイズのグラフの場合のみハンドルを表示
            if (ResizeHandle != null && !double.IsNaN(PerformanceChart.Width) && !double.IsNaN(PerformanceChart.Height))
            {
                ResizeHandle.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            LogError($"グラフコンテナーマウス進入処理中にエラー: {ex.Message}");
        }
    }

    /// <summary>
    /// グラフコンテナーからのマウス退出イベント
    /// </summary>
    private void GraphContainer_MouseLeave(object sender, MouseEventArgs e)
    {
        try
        {
            if (ResizeHandle != null && !_isResizing)
            {
                ResizeHandle.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            LogError($"グラフコンテナーマウス退出処理中にエラー: {ex.Message}");
        }
    }

    /// <summary>
    /// リサイズハンドルのマウスダウンイベント
    /// </summary>
    private void ResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (e.LeftButton == MouseButtonState.Pressed && ResizeHandle != null)
            {
                _isResizing = true;
                _resizeStartPoint = e.GetPosition(GraphContainer);
                _resizeStartSize = new Size(PerformanceChart.Width, PerformanceChart.Height);
                
                ResizeHandle.CaptureMouse();
                LogInfo("グラフリサイズを開始しました");
            }
        }
        catch (Exception ex)
        {
            LogError($"リサイズ開始処理中にエラー: {ex.Message}");
        }
    }

    /// <summary>
    /// リサイズハンドルのマウス移動イベント
    /// </summary>
    private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
    {
        try
        {
            if (_isResizing && e.LeftButton == MouseButtonState.Pressed && GraphContainer != null)
            {
                Point currentPoint = e.GetPosition(GraphContainer);
                
                double deltaX = currentPoint.X - _resizeStartPoint.X;
                double deltaY = currentPoint.Y - _resizeStartPoint.Y;
                
                double newWidth = Math.Max(200, _resizeStartSize.Width + deltaX);
                double newHeight = Math.Max(150, _resizeStartSize.Height + deltaY);
                
                // 最大サイズの制限
                double maxWidth = GraphContainer.ActualWidth - 20;
                double maxHeight = GraphContainer.ActualHeight - 20;
                
                if (maxWidth > 0) newWidth = Math.Min(newWidth, maxWidth);
                if (maxHeight > 0) newHeight = Math.Min(newHeight, maxHeight);
                
                // サイズを設定
                PerformanceChart.Width = newWidth;
                PerformanceChart.Height = newHeight;
                PerformanceChart.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                PerformanceChart.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                
                UpdateGraphSizeDisplay();
            }
        }
        catch (Exception ex)
        {
            LogError($"リサイズ処理中にエラー: {ex.Message}");
        }
    }

    /// <summary>
    /// リサイズハンドルのマウスアップイベント
    /// </summary>
    private void ResizeHandle_MouseUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (_isResizing && ResizeHandle != null)
            {
                _isResizing = false;
                ResizeHandle.ReleaseMouseCapture();
                LogInfo($"グラフリサイズを完了しました: {PerformanceChart.Width:F0}×{PerformanceChart.Height:F0}");
            }
        }
        catch (Exception ex)
        {
            LogError($"リサイズ完了処理中にエラー: {ex.Message}");
        }
    }

    /// <summary>
    /// リサイズハンドルからのマウス退出イベント
    /// </summary>
    private void ResizeHandle_MouseLeave(object sender, MouseEventArgs e)
    {
        try
        {
            if (!_isResizing && ResizeHandle != null)
            {
                ResizeHandle.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            LogError($"リサイズハンドルマウス退出処理中にエラー: {ex.Message}");
        }
    }

    #endregion

    /// <summary>
    /// グラフサイズ手動設定クリックイベント
    /// </summary>
    private void GraphSizeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ShowGraphSizeSettingDialog();
    }

    /// <summary>
    /// グラフサイズ設定ダイアログの表示
    /// </summary>
    private void ShowGraphSizeSettingDialog()
    {
        try
        {
            // グラフサイズ表示テキストが存在するかチェック
            if (GraphSizeText == null)
            {
                LogError("GraphSizeText が初期化されていません");
                MessageBox.Show(
                    "グラフサイズ表示コンポーネントが初期化されていません。",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            if (PerformanceChart == null)
            {
                LogError("PerformanceChart が初期化されていません");
                MessageBox.Show(
                    "グラフコンポーネントが初期化されていません。",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var currentWidth = PerformanceChart.ActualWidth > 0 ? PerformanceChart.ActualWidth : PerformanceChart.Width;
            var currentHeight = PerformanceChart.ActualHeight > 0 ? PerformanceChart.ActualHeight : PerformanceChart.Height;
            
            // NaNやInfinityをチェック
            if (double.IsNaN(currentWidth) || double.IsInfinity(currentWidth) || currentWidth <= 0)
                currentWidth = 800; // デフォルト値
            if (double.IsNaN(currentHeight) || double.IsInfinity(currentHeight) || currentHeight <= 0)
                currentHeight = 400; // デフォルト値

            var dialog = new GraphSizeSettingDialog(currentWidth, currentHeight)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            // グラフ表示領域のサイズを取得して最大化サイズとして設定
            try
            {
                // PerformanceChartの親となるGridの実際のサイズを取得
                var parentGrid = PerformanceChart.Parent as Grid;
                if (parentGrid != null)
                {
                    double maxWidth = parentGrid.ActualWidth;
                    double maxHeight = parentGrid.ActualHeight;
                    
                    // マージンを考慮して少し小さめにする
                    if (maxWidth > 50) maxWidth -= 20;
                    if (maxHeight > 50) maxHeight -= 20;
                    
                    dialog.MaximizeSize = new Size(maxWidth, maxHeight);
                    LogInfo($"グラフ最大化サイズを設定しました: {maxWidth:F0}×{maxHeight:F0}");
                }
            }
            catch (Exception ex)
            {
                LogError($"最大化サイズの取得に失敗しました: {ex.Message}");
            }

            if (dialog.ShowDialog() == true && dialog.IsApplied)
            {
                // 自動サイズの場合（NaN値）
                if (double.IsNaN(dialog.GraphWidth) && double.IsNaN(dialog.GraphHeight))
                {
                    // PerformanceChartを自動サイズに戻す
                    PerformanceChart.Width = double.NaN;
                    PerformanceChart.Height = double.NaN;
                    PerformanceChart.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                    PerformanceChart.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
                    
                    LogInfo("グラフサイズを自動サイズに変更しました");
                }
                else
                {
                    // PerformanceChart自体のサイズを直接変更
                    PerformanceChart.Width = dialog.GraphWidth;
                    PerformanceChart.Height = dialog.GraphHeight;
                    
                    // HorizontalAlignmentとVerticalAlignmentを設定して固定サイズにする
                    PerformanceChart.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                    PerformanceChart.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                    
                    LogInfo($"グラフサイズを変更しました: {dialog.GraphWidth:F0}×{dialog.GraphHeight:F0}");
                }
                
                // 表示を更新
                UpdateGraphSizeDisplay();
            }
        }
        catch (Exception ex)
        {
            LogError($"グラフサイズ設定ダイアログの表示中にエラーが発生しました: {ex.Message}");
            MessageBox.Show(
                $"グラフサイズ設定中にエラーが発生しました。\n\n{ex.Message}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    #endregion

    #region ウィンドウサイズ管理機能

    /// <summary>
    /// ウィンドウサイズ監視の初期化
    /// </summary>
    private void InitializeWindowSizeTracking()
    {
        // 初期ウィンドウサイズの表示
        UpdateWindowSizeDisplay();
        
        // ウィンドウサイズ変更イベントの監視
        this.SizeChanged += MainWindow_SizeChanged;
        
        // ウィンドウステート変更イベントの監視
        this.StateChanged += MainWindow_StateChanged;
    }

    /// <summary>
    /// ウィンドウサイズ変更イベントハンドラー
    /// </summary>
    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWindowSizeDisplay();
    }

    /// <summary>
    /// ウィンドウステート変更イベントハンドラー
    /// </summary>
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        UpdateWindowSizeDisplay();
    }

    /// <summary>
    /// ウィンドウサイズ表示の更新
    /// </summary>
    private void UpdateWindowSizeDisplay()
    {
        try
        {
            if (WindowSizeText != null)
            {
                var width = this.ActualWidth > 0 ? this.ActualWidth : this.Width;
                var height = this.ActualHeight > 0 ? this.ActualHeight : this.Height;
                
                string stateText = this.WindowState switch
                {
                    WindowState.Maximized => " (最大化)",
                    WindowState.Minimized => " (最小化)",
                    _ => ""
                };
                
                WindowSizeText.Text = $"ウィンドウサイズ: {width:F0}×{height:F0}{stateText}";
            }
        }
        catch (Exception ex)
        {
            LogError($"ウィンドウサイズ表示の更新中にエラーが発生しました: {ex.Message}");
        }
    }

    /// <summary>
    /// ウィンドウサイズ手動設定クリックイベント
    /// </summary>
    private void WindowSizeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ShowWindowSizeSettingDialog();
    }

    /// <summary>
    /// ウィンドウサイズ設定ダイアログの表示
    /// </summary>
    private void ShowWindowSizeSettingDialog()
    {
        try
        {
            // ウィンドウサイズ表示テキストが存在するかチェック
            if (WindowSizeText == null)
            {
                LogError("WindowSizeText が初期化されていません");
                MessageBox.Show(
                    "ウィンドウサイズ表示コンポーネントが初期化されていません。",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var dialog = new WindowSizeSettingDialog
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CurrentWidth = this.ActualWidth > 0 ? this.ActualWidth : this.Width,
                CurrentHeight = this.ActualHeight > 0 ? this.ActualHeight : this.Height,
                CurrentWindowState = this.WindowState
            };

            if (dialog.ShowDialog() == true)
            {
                // ウィンドウステートを適用
                this.WindowState = dialog.SelectedWindowState;
                
                // サイズが指定されている場合は適用
                if (dialog.SelectedWindowState == WindowState.Normal && 
                    dialog.NewWidth.HasValue && dialog.NewHeight.HasValue)
                {
                    this.Width = dialog.NewWidth.Value;
                    this.Height = dialog.NewHeight.Value;
                }
                
                LogInfo($"ウィンドウサイズを変更しました: {dialog.NewWidth}×{dialog.NewHeight} ({dialog.SelectedWindowState})");
            }
        }
        catch (Exception ex)
        {
            LogError($"ウィンドウサイズ設定ダイアログの表示中にエラーが発生しました: {ex.Message}");
            MessageBox.Show(
                $"ウィンドウサイズ設定中にエラーが発生しました。\n\n{ex.Message}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    #endregion
}

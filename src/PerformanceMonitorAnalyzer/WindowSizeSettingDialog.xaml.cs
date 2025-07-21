using System.Windows;
using System.Windows.Controls;

namespace PerformanceMonitorAnalyzer
{
    /// <summary>
    /// WindowSizeSettingDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class WindowSizeSettingDialog : Window
    {
        #region プロパティ

        /// <summary>
        /// 現在の幅
        /// </summary>
        public double CurrentWidth { get; set; }

        /// <summary>
        /// 現在の高さ
        /// </summary>
        public double CurrentHeight { get; set; }

        /// <summary>
        /// 現在のウィンドウ状態
        /// </summary>
        public WindowState CurrentWindowState { get; set; }

        /// <summary>
        /// 新しい幅（null可能）
        /// </summary>
        public double? NewWidth { get; private set; }

        /// <summary>
        /// 新しい高さ（null可能）
        /// </summary>
        public double? NewHeight { get; private set; }

        /// <summary>
        /// 選択されたウィンドウ状態
        /// </summary>
        public WindowState SelectedWindowState { get; private set; }

        #endregion

        #region 定数

        private const double MIN_WIDTH = 400;
        private const double MAX_WIDTH = 3840;
        private const double MIN_HEIGHT = 300;
        private const double MAX_HEIGHT = 2160;

        #endregion

        #region コンストラクタ

        public WindowSizeSettingDialog()
        {
            InitializeComponent();
            this.Loaded += WindowSizeSettingDialog_Loaded;
        }

        #endregion

        #region イベントハンドラー

        /// <summary>
        /// ダイアログ読み込み完了
        /// </summary>
        private void WindowSizeSettingDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // テキストボックスの変更イベントを設定（Loaded後に安全に設定）
            WidthTextBox.TextChanged += SizeTextBox_TextChanged;
            HeightTextBox.TextChanged += SizeTextBox_TextChanged;
            
            // 現在の値を表示
            CurrentSizeText.Text = $"{CurrentWidth:F0}×{CurrentHeight:F0}";
            CurrentStateText.Text = CurrentWindowState switch
            {
                WindowState.Maximized => "最大化",
                WindowState.Minimized => "最小化",
                _ => "通常"
            };

            // 現在の状態に基づいてラジオボタンを設定
            switch (CurrentWindowState)
            {
                case WindowState.Maximized:
                    MaximizedStateRadio.IsChecked = true;
                    break;
                case WindowState.Minimized:
                    MinimizedStateRadio.IsChecked = true;
                    break;
                default:
                    NormalStateRadio.IsChecked = true;
                    break;
            }

            // 現在の値をテキストボックスに設定
            WidthTextBox.Text = CurrentWidth.ToString("F0");
            HeightTextBox.Text = CurrentHeight.ToString("F0");

            // 初期状態の更新
            UpdateSizeSettingVisibility();
            UpdatePreview();
        }

        /// <summary>
        /// ウィンドウ状態変更
        /// </summary>
        private void WindowState_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSizeSettingVisibility();
            UpdatePreview();
        }

        /// <summary>
        /// サイズテキストボックス変更
        /// </summary>
        private void SizeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        /// <summary>
        /// プリセットサイズクリック
        /// </summary>
        private void PresetSize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string sizeString)
            {
                var sizes = sizeString.Split(',');
                if (sizes.Length == 2)
                {
                    WidthTextBox.Text = sizes[0];
                    HeightTextBox.Text = sizes[1];
                }
            }
        }

        /// <summary>
        /// 適用ボタンクリック
        /// </summary>
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // ウィンドウ状態の取得
                SelectedWindowState = GetSelectedWindowState();

                // サイズの検証と取得
                if (SelectedWindowState == WindowState.Normal)
                {
                    if (TryParseAndValidateSize(WidthTextBox.Text, HeightTextBox.Text, out double width, out double height))
                    {
                        NewWidth = width;
                        NewHeight = height;
                    }
                    else
                    {
                        MessageBox.Show(
                            $"無効なサイズが指定されました。\n\n" +
                            $"有効範囲: 幅 {MIN_WIDTH}-{MAX_WIDTH}、高さ {MIN_HEIGHT}-{MAX_HEIGHT}",
                            "入力エラー",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    // 最大化/最小化の場合はサイズを null に設定
                    NewWidth = null;
                    NewHeight = null;
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"設定の適用中にエラーが発生しました。\n\n{ex.Message}",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// キャンセルボタンクリック
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        #endregion

        #region プライベートメソッド

        /// <summary>
        /// サイズ設定の表示/非表示を更新
        /// </summary>
        private void UpdateSizeSettingVisibility()
        {
            try
            {
                if (NormalStateRadio != null && SizeSettingGroup != null)
                {
                    bool isNormalState = NormalStateRadio.IsChecked == true;
                    SizeSettingGroup.IsEnabled = isNormalState;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSizeSettingVisibility エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// プレビューテキストの更新
        /// </summary>
        private void UpdatePreview()
        {
            try
            {
                if (PreviewText == null || WidthTextBox == null || HeightTextBox == null)
                {
                    return;
                }

                var selectedState = GetSelectedWindowState();
                string stateText = selectedState switch
                {
                    WindowState.Maximized => "最大化",
                    WindowState.Minimized => "最小化",
                    _ => "通常"
                };

                if (selectedState == WindowState.Normal && 
                    TryParseAndValidateSize(WidthTextBox.Text, HeightTextBox.Text, out double width, out double height))
                {
                    PreviewText.Text = $"新しいサイズ: {width:F0}×{height:F0} ({stateText})";
                }
                else if (selectedState != WindowState.Normal)
                {
                    PreviewText.Text = $"新しいサイズ: {stateText}";
                }
                else
                {
                    PreviewText.Text = $"新しいサイズ: 無効な値 ({stateText})";
                }
            }
            catch (Exception ex)
            {
                if (PreviewText != null)
                {
                    PreviewText.Text = "新しいサイズ: プレビュー取得エラー";
                }
                System.Diagnostics.Debug.WriteLine($"UpdatePreview エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 選択されたウィンドウ状態を取得
        /// </summary>
        private WindowState GetSelectedWindowState()
        {
            try
            {
                if (MaximizedStateRadio?.IsChecked == true)
                    return WindowState.Maximized;
                if (MinimizedStateRadio?.IsChecked == true)
                    return WindowState.Minimized;
                return WindowState.Normal;
            }
            catch
            {
                return WindowState.Normal;
            }
        }

        /// <summary>
        /// サイズの解析と検証
        /// </summary>
        private bool TryParseAndValidateSize(string widthText, string heightText, out double width, out double height)
        {
            width = 0;
            height = 0;

            // 数値解析
            if (!double.TryParse(widthText, out width) || !double.TryParse(heightText, out height))
            {
                return false;
            }

            // 範囲チェック
            if (width < MIN_WIDTH || width > MAX_WIDTH || height < MIN_HEIGHT || height > MAX_HEIGHT)
            {
                return false;
            }

            return true;
        }

        #endregion
    }
}
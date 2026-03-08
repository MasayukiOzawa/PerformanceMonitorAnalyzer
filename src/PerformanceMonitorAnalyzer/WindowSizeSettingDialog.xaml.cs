using System.Windows;
using System.Windows.Controls;

namespace PerformanceMonitorAnalyzer
{
    /// <summary>
    /// WindowSizeSettingDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class WindowSizeSettingDialog : Window
    {
        private static readonly DialogSizeInputHelper.ValidationOptions WindowSizeValidationOptions =
            new(400, 3840, 300, 2160, DialogSizeInputHelper.NumericMode.DecimalAllowed);

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

            // 常に「通常」をデフォルトで選択
            NormalStateRadio.IsChecked = true;

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
            if (sender is Button button &&
                button.Tag is string sizeString &&
                DialogSizeInputHelper.TryParsePreset(
                    sizeString,
                    DialogSizeInputHelper.NumericMode.DecimalAllowed,
                    out string widthText,
                    out string heightText))
            {
                WidthTextBox.Text = widthText;
                HeightTextBox.Text = heightText;
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
                    var validation = DialogSizeInputHelper.Evaluate(
                        WidthTextBox.Text,
                        HeightTextBox.Text,
                        WindowSizeValidationOptions);

                    if (validation.State == DialogSizeInputHelper.ValidationState.Valid)
                    {
                        NewWidth = validation.Width;
                        NewHeight = validation.Height;
                    }
                    else
                    {
                        MessageBox.Show(
                            $"無効なサイズが指定されました。\n\n" +
                            $"有効範囲: 幅 {WindowSizeValidationOptions.MinWidth:F0}-{WindowSizeValidationOptions.MaxWidth:F0}、高さ {WindowSizeValidationOptions.MinHeight:F0}-{WindowSizeValidationOptions.MaxHeight:F0}",
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

                if (selectedState == WindowState.Normal)
                {
                    var validation = DialogSizeInputHelper.Evaluate(
                        WidthTextBox.Text,
                        HeightTextBox.Text,
                        WindowSizeValidationOptions);

                    PreviewText.Text = validation.State == DialogSizeInputHelper.ValidationState.Valid
                        ? $"新しいサイズ: {validation.Width:F0}×{validation.Height:F0} ({stateText})"
                        : $"新しいサイズ: 無効な値 ({stateText})";
                }
                else
                {
                    PreviewText.Text = $"新しいサイズ: {stateText}";
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

        #endregion
    }
}

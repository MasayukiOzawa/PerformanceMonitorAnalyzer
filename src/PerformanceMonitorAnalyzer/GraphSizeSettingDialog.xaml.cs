using System;
using System.Windows;
using System.Windows.Controls;

namespace PerformanceMonitorAnalyzer
{
    /// <summary>
    /// グラフサイズ設定ダイアログ
    /// </summary>
    public partial class GraphSizeSettingDialog : Window
    {
        private static readonly DialogSizeInputHelper.ValidationOptions GraphSizeValidationOptions =
            new(200, 3840, 150, 2160, DialogSizeInputHelper.NumericMode.IntegerOnly, allowAuto: true);

        /// <summary>
        /// 設定されたグラフの幅
        /// </summary>
        public double GraphWidth { get; private set; }

        /// <summary>
        /// 設定されたグラフの高さ
        /// </summary>
        public double GraphHeight { get; private set; }

        /// <summary>
        /// 設定が適用されたかどうか
        /// </summary>
        public bool IsApplied { get; private set; }

        /// <summary>
        /// 最大化サイズ（親領域のサイズ）
        /// </summary>
        public Size MaximizeSize { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="currentWidth">現在のグラフ幅</param>
        /// <param name="currentHeight">現在のグラフ高さ</param>
        public GraphSizeSettingDialog(double currentWidth, double currentHeight)
        {
            InitializeComponent();
            Loaded += GraphSizeSettingDialog_Loaded;
            
            GraphWidth = currentWidth;
            GraphHeight = currentHeight;
            
            // 現在のサイズを表示
            if (CurrentSizeText != null)
            {
                CurrentSizeText.Text = $"{currentWidth:F0}×{currentHeight:F0}";
            }
            
            // テキストボックスに現在の値を設定
            if (WidthTextBox != null && HeightTextBox != null)
            {
                WidthTextBox.Text = ((int)currentWidth).ToString();
                HeightTextBox.Text = ((int)currentHeight).ToString();
            }
            
            UpdatePreview();
        }

        /// <summary>
        /// ウィンドウロード時の処理
        /// </summary>
        private void GraphSizeSettingDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // イベントハンドラーの設定
                if (WidthTextBox != null)
                {
                    WidthTextBox.TextChanged += SizeTextBox_TextChanged;
                }
                if (HeightTextBox != null)
                {
                    HeightTextBox.TextChanged += SizeTextBox_TextChanged;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ダイアログの初期化中にエラーが発生しました。\n\n{ex.Message}", "エラー", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// サイズテキストボックスの変更イベント
        /// </summary>
        private void SizeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                UpdatePreview();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"プレビュー更新エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 最大化ボタンのクリックイベント
        /// </summary>
        private void MaximizeSize_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WidthTextBox != null && HeightTextBox != null)
                {
                    // 最大化サイズが設定されている場合はそれを使用
                    if (MaximizeSize.Width > 0 && MaximizeSize.Height > 0)
                    {
                        int maxWidth = (int)Math.Floor(MaximizeSize.Width);
                        int maxHeight = (int)Math.Floor(MaximizeSize.Height);
                        
                        WidthTextBox.Text = maxWidth.ToString();
                        HeightTextBox.Text = maxHeight.ToString();
                    }
                    else
                    {
                        // デフォルトの最大化サイズ
                        WidthTextBox.Text = "1400";
                        HeightTextBox.Text = "700";
                    }
                    UpdatePreview();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"最大化サイズの設定中にエラーが発生しました。\n\n{ex.Message}", "エラー", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 自動サイズボタンのクリックイベント
        /// </summary>
        private void AutoSize_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WidthTextBox != null && HeightTextBox != null)
                {
                    // 自動サイズの場合は特別な値を設定
                    WidthTextBox.Text = "自動";
                    HeightTextBox.Text = "自動";
                    UpdatePreview();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"自動サイズの設定中にエラーが発生しました。\n\n{ex.Message}", "エラー", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// プリセットサイズボタンのクリックイベント
        /// </summary>
        private void PresetSize_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button &&
                    button.Tag is string sizeStr &&
                    WidthTextBox != null &&
                    HeightTextBox != null &&
                    DialogSizeInputHelper.TryParsePreset(
                        sizeStr,
                        DialogSizeInputHelper.NumericMode.IntegerOnly,
                        out string widthText,
                        out string heightText))
                {
                    WidthTextBox.Text = widthText;
                    HeightTextBox.Text = heightText;
                    UpdatePreview();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"プリセットサイズの設定中にエラーが発生しました。\n\n{ex.Message}", "エラー", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// プレビューを更新
        /// </summary>
        private void UpdatePreview()
        {
            try
            {
                if (PreviewText == null || WidthTextBox == null || HeightTextBox == null)
                    return;

                var validation = DialogSizeInputHelper.Evaluate(
                    WidthTextBox.Text,
                    HeightTextBox.Text,
                    GraphSizeValidationOptions);

                if (validation.State == DialogSizeInputHelper.ValidationState.Auto)
                {
                    PreviewText.Text = "新しいサイズ: 自動 (親コンテナに合わせて調整)";
                    PreviewText.Foreground = System.Windows.Media.Brushes.DarkGreen;
                    return;
                }

                if (validation.State == DialogSizeInputHelper.ValidationState.Valid)
                {
                    PreviewText.Text = $"新しいサイズ: {validation.Width:F0}×{validation.Height:F0}";
                    PreviewText.Foreground = System.Windows.Media.Brushes.DarkBlue;
                }
                else if (validation.State == DialogSizeInputHelper.ValidationState.OutOfRange)
                {
                    PreviewText.Text = $"無効なサイズ: {validation.Width:F0}×{validation.Height:F0} (範囲外)";
                    PreviewText.Foreground = System.Windows.Media.Brushes.Red;
                }
                else
                {
                    PreviewText.Text = "無効なサイズ: 数値を入力してください";
                    PreviewText.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"プレビュー更新エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 適用ボタンのクリックイベント
        /// </summary>
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WidthTextBox == null || HeightTextBox == null)
                {
                    DialogResult = false;
                    return;
                }

                var validation = DialogSizeInputHelper.Evaluate(
                    WidthTextBox.Text,
                    HeightTextBox.Text,
                    GraphSizeValidationOptions);

                switch (validation.State)
                {
                    case DialogSizeInputHelper.ValidationState.Auto:
                        GraphWidth = double.NaN; // 自動サイズを示すためにNaNを使用
                        GraphHeight = double.NaN;
                        IsApplied = true;
                        DialogResult = true;
                        return;
                    case DialogSizeInputHelper.ValidationState.Valid:
                        GraphWidth = validation.Width;
                        GraphHeight = validation.Height;
                        IsApplied = true;
                        DialogResult = true;
                        return;
                    case DialogSizeInputHelper.ValidationState.OutOfRange:
                        MessageBox.Show("サイズが有効範囲外です。\n\n有効範囲: 幅 200-3840、高さ 150-2160", 
                            "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    default:
                        MessageBox.Show("無効な数値が入力されています。\n正しい数値を入力してください。", 
                            "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"グラフサイズ設定中にエラーが発生しました。\n\n{ex.Message}", "エラー", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// キャンセルボタンのクリックイベント
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsApplied = false;
            DialogResult = false;
        }
    }
}

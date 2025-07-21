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
                if (sender is Button button && button.Tag is string sizeStr)
                {
                    var sizes = sizeStr.Split(',');
                    if (sizes.Length == 2 && 
                        int.TryParse(sizes[0], out int width) && 
                        int.TryParse(sizes[1], out int height))
                    {
                        if (WidthTextBox != null && HeightTextBox != null)
                        {
                            WidthTextBox.Text = width.ToString();
                            HeightTextBox.Text = height.ToString();
                            UpdatePreview();
                        }
                    }
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

                // 自動サイズの場合
                if (WidthTextBox.Text == "自動" && HeightTextBox.Text == "自動")
                {
                    PreviewText.Text = "新しいサイズ: 自動 (親コンテナに合わせて調整)";
                    PreviewText.Foreground = System.Windows.Media.Brushes.DarkGreen;
                    return;
                }

                if (int.TryParse(WidthTextBox.Text, out int width) && 
                    int.TryParse(HeightTextBox.Text, out int height))
                {
                    // 有効範囲をチェック
                    bool isValidWidth = width >= 200 && width <= 3840;
                    bool isValidHeight = height >= 150 && height <= 2160;
                    
                    if (isValidWidth && isValidHeight)
                    {
                        PreviewText.Text = $"新しいサイズ: {width}×{height}";
                        PreviewText.Foreground = System.Windows.Media.Brushes.DarkBlue;
                    }
                    else
                    {
                        PreviewText.Text = $"無効なサイズ: {width}×{height} (範囲外)";
                        PreviewText.Foreground = System.Windows.Media.Brushes.Red;
                    }
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

                // 自動サイズの場合
                if (WidthTextBox.Text == "自動" && HeightTextBox.Text == "自動")
                {
                    GraphWidth = double.NaN; // 自動サイズを示すためにNaNを使用
                    GraphHeight = double.NaN;
                    IsApplied = true;
                    DialogResult = true;
                    return;
                }

                if (int.TryParse(WidthTextBox.Text, out int width) && 
                    int.TryParse(HeightTextBox.Text, out int height))
                {
                    // 有効範囲をチェック
                    if (width >= 200 && width <= 3840 && height >= 150 && height <= 2160)
                    {
                        GraphWidth = width;
                        GraphHeight = height;
                        IsApplied = true;
                        DialogResult = true;
                    }
                    else
                    {
                        MessageBox.Show("サイズが有効範囲外です。\n\n有効範囲: 幅 200-3840、高さ 150-2160", 
                            "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("無効な数値が入力されています。\n正しい数値を入力してください。", 
                        "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
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
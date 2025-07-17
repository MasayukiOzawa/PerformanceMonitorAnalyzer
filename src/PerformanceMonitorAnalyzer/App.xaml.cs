using System.Windows;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // メインウィンドウを作成
        var mainWindow = new MainWindow();
        
        // コマンドライン引数があればBLGファイルとして読み込み
        if (e.Args.Length > 0)
        {
            string blgFilePath = e.Args[0];
            if (System.IO.File.Exists(blgFilePath))
            {
                // メインウィンドウを表示してからBLGファイルを読み込み
                mainWindow.Show();
                _ = mainWindow.LoadBlgFileFromCommandLineAsync(blgFilePath);
            }
            else
            {
                MessageBox.Show($"指定されたBLGファイルが見つかりません: {blgFilePath}", 
                               "ファイルエラー", MessageBoxButton.OK, MessageBoxImage.Error);
                mainWindow.Show();
            }
        }
        else
        {
            mainWindow.Show();
        }
        
        MainWindow = mainWindow;
    }
}

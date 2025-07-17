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
        
        try
        {
            // メインウィンドウを作成
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            
            // メインウィンドウを表示
            mainWindow.Show();
            
            // コマンドライン引数があればBLGファイルとして読み込み
            if (e.Args.Length > 0)
            {
                string blgFilePath = e.Args[0];
                if (System.IO.File.Exists(blgFilePath))
                {
                    // BLGファイルを非同期で読み込み
                    _ = Task.Run(async () => 
                    {
                        await Task.Delay(500); // UI初期化を待つ
                        await Dispatcher.InvokeAsync(async () => 
                        {
                            await mainWindow.LoadBlgFileFromCommandLineAsync(blgFilePath);
                        });
                    });
                }
                else
                {
                    // トースト通知でエラーを表示（MessageBoxの代わり）
                    _ = Task.Run(async () => 
                    {
                        await Task.Delay(1000); // UI初期化を待つ
                        await Dispatcher.InvokeAsync(() => 
                        {
                            mainWindow.ShowToastNotification(
                                "ファイルエラー",
                                $"指定されたBLGファイルが見つかりません: {blgFilePath}",
                                MainWindow.ToastType.Error,
                                7000);
                        });
                    });
                }
            }
        }
        catch (Exception ex)
        {
            // 起動時のエラーをファイルに記録
            var errorLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log");
            File.WriteAllText(errorLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Application startup error: {ex}\n");
            MessageBox.Show($"アプリケーションの起動中にエラーが発生しました。詳細は startup_error.log を確認してください。\n\nエラー: {ex.Message}", 
                           "起動エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(1);
        }
    }
}

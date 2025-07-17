using System;
using System.IO;
using System.Threading.Tasks;

namespace PerformanceMonitorAnalyzer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Performance Monitor BLG File Analyzer - Console Version");
            Console.WriteLine("========================================================");

            string blgFilePath = "";

            // 引数またはサンプルファイルを使用
            if (args.Length > 0)
            {
                blgFilePath = args[0];
            }
            else
            {
                // サンプルファイルのパスを試す
                var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "sample", "DataCollector01.blg");
                samplePath = Path.GetFullPath(samplePath);
                
                if (File.Exists(samplePath))
                {
                    blgFilePath = samplePath;
                    Console.WriteLine($"サンプルファイルを使用: {blgFilePath}");
                }
                else
                {
                    Console.WriteLine("使用方法: dotnet run [BLGファイルパス]");
                    Console.WriteLine("BLGファイルが指定されていません。");
                    return;
                }
            }

            if (!File.Exists(blgFilePath))
            {
                Console.WriteLine($"エラー: ファイルが見つかりません: {blgFilePath}");
                return;
            }

            try
            {
                var analyzer = new BlgFileAnalyzer();
                
                Console.WriteLine($"BLGファイルを読み込み中: {blgFilePath}");
                
                // プログレス表示用
                var progress = new Progress<string>(message => Console.WriteLine($"[進行状況] {message}"));
                
                // BLGファイルを開く
                bool opened = await analyzer.OpenBlgFileAsync(blgFilePath, progress);
                
                if (opened)
                {
                    Console.WriteLine("BLGファイルが正常に開かれました。");
                    
                    // オブジェクトを列挙
                    var objects = await analyzer.EnumerateObjectsAsync(progress);
                    Console.WriteLine($"\n検出されたパフォーマンスオブジェクト数: {objects.Count}");
                    
                    foreach (var obj in objects)
                    {
                        Console.WriteLine($"- {obj}");
                        
                        // 各オブジェクトのカウンターとインスタンスを列挙
                        var (counters, instances) = await analyzer.EnumerateCountersAndInstancesAsync(obj, progress);
                        Console.WriteLine($"  カウンター数: {counters.Count}, インスタンス数: {instances.Count}");
                    }
                }
                else
                {
                    Console.WriteLine("BLGファイルの読み込みに失敗しました。");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"エラーが発生しました: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"詳細: {ex.InnerException.Message}");
                }
            }
            
            Console.WriteLine("\nエンターキーで終了...");
            Console.ReadLine();
        }
    }
}
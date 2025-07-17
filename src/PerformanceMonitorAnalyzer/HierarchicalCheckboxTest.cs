using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace PerformanceMonitorAnalyzer
{
    /// <summary>
    /// 階層チェックボックス機能のテストクラス
    /// </summary>
    public class HierarchicalCheckboxTest
    {
        public static void RunTests()
        {
            Console.WriteLine("=== 階層チェックボックス機能テスト開始 ===");
            
            // テストデータの作成
            var testNodes = CreateTestTree();
            
            // テスト1: オブジェクトレベルでの全選択
            Test1_ObjectLevelSelection(testNodes);
            
            // テスト2: インスタンスレベルでの選択
            Test2_InstanceLevelSelection(testNodes);
            
            // テスト3: カウンターレベルでの個別選択
            Test3_CounterLevelSelection(testNodes);
            
            // テスト4: 部分選択状態のテスト
            Test4_PartialSelection(testNodes);
            
            Console.WriteLine("=== 階層チェックボックス機能テスト完了 ===");
        }
        
        private static ObservableCollection<CounterTreeNode> CreateTestTree()
        {
            var nodes = new ObservableCollection<CounterTreeNode>();
            
            // Processorオブジェクトの作成
            var processorObject = new CounterTreeNode
            {
                DisplayName = "Processor",
                Type = NodeType.Object
            };
            
            // _Totalインスタンスの作成
            var totalInstance = new CounterTreeNode
            {
                DisplayName = "_Total",
                Type = NodeType.Instance,
                Parent = processorObject
            };
            
            // カウンターの作成
            var cpuTimeCounter = new CounterTreeNode
            {
                DisplayName = "% Processor Time",
                FullPath = "\\Processor(_Total)\\% Processor Time",
                Type = NodeType.Counter,
                Parent = totalInstance
            };
            
            var userTimeCounter = new CounterTreeNode
            {
                DisplayName = "% User Time",
                FullPath = "\\Processor(_Total)\\% User Time",
                Type = NodeType.Counter,
                Parent = totalInstance
            };
            
            totalInstance.Children.Add(cpuTimeCounter);
            totalInstance.Children.Add(userTimeCounter);
            processorObject.Children.Add(totalInstance);
            nodes.Add(processorObject);
            
            return nodes;
        }
        
        private static void Test1_ObjectLevelSelection(ObservableCollection<CounterTreeNode> nodes)
        {
            Console.WriteLine("テスト1: オブジェクトレベルでの全選択");
            
            var processorObject = nodes[0];
            processorObject.IsChecked = true;
            
            // 全ての子ノードが選択されているかチェック
            var selectedCounters = processorObject.GetSelectedCounters().ToList();
            Console.WriteLine($"選択されたカウンター数: {selectedCounters.Count}");
            
            foreach (var counter in selectedCounters)
            {
                Console.WriteLine($"  - {counter.FullPath}");
            }
            
            Console.WriteLine($"テスト1結果: {(selectedCounters.Count == 2 ? "成功" : "失敗")}");
            Console.WriteLine();
        }
        
        private static void Test2_InstanceLevelSelection(ObservableCollection<CounterTreeNode> nodes)
        {
            Console.WriteLine("テスト2: インスタンスレベルでの選択");
            
            // 前のテストで選択されたものをリセット
            var processorObject = nodes[0];
            processorObject.IsChecked = false;
            
            // インスタンスレベルで選択
            var totalInstance = processorObject.Children[0];
            totalInstance.IsChecked = true;
            
            var selectedCounters = processorObject.GetSelectedCounters().ToList();
            Console.WriteLine($"選択されたカウンター数: {selectedCounters.Count}");
            Console.WriteLine($"オブジェクトの状態: {processorObject.IsChecked}");
            Console.WriteLine($"テスト2結果: {(selectedCounters.Count == 2 && processorObject.IsChecked == true ? "成功" : "失敗")}");
            Console.WriteLine();
        }
        
        private static void Test3_CounterLevelSelection(ObservableCollection<CounterTreeNode> nodes)
        {
            Console.WriteLine("テスト3: カウンターレベルでの個別選択");
            
            // 前のテストで選択されたものをリセット
            var processorObject = nodes[0];
            processorObject.IsChecked = false;
            
            // 1つのカウンターのみ選択
            var totalInstance = processorObject.Children[0];
            var cpuTimeCounter = totalInstance.Children[0];
            cpuTimeCounter.IsChecked = true;
            
            var selectedCounters = processorObject.GetSelectedCounters().ToList();
            Console.WriteLine($"選択されたカウンター数: {selectedCounters.Count}");
            Console.WriteLine($"インスタンスの状態: {totalInstance.IsChecked}");
            Console.WriteLine($"オブジェクトの状態: {processorObject.IsChecked}");
            Console.WriteLine($"テスト3結果: {(selectedCounters.Count == 1 && totalInstance.IsChecked == null && processorObject.IsChecked == null ? "成功" : "失敗")}");
            Console.WriteLine();
        }
        
        private static void Test4_PartialSelection(ObservableCollection<CounterTreeNode> nodes)
        {
            Console.WriteLine("テスト4: 部分選択状態のテスト");
            
            // 前のテストで選択されたものをリセット
            var processorObject = nodes[0];
            processorObject.IsChecked = false;
            
            // 一部のカウンターのみ選択
            var totalInstance = processorObject.Children[0];
            var cpuTimeCounter = totalInstance.Children[0];
            var userTimeCounter = totalInstance.Children[1];
            
            cpuTimeCounter.IsChecked = true;
            // userTimeCounterは選択しない
            
            Console.WriteLine($"CPU Time Counter: {cpuTimeCounter.IsChecked}");
            Console.WriteLine($"User Time Counter: {userTimeCounter.IsChecked}");
            Console.WriteLine($"Instance状態: {totalInstance.IsChecked}");
            Console.WriteLine($"Object状態: {processorObject.IsChecked}");
            
            var selectedCounters = processorObject.GetSelectedCounters().ToList();
            Console.WriteLine($"選択されたカウンター数: {selectedCounters.Count}");
            Console.WriteLine($"テスト4結果: {(selectedCounters.Count == 1 && totalInstance.IsChecked == null ? "成功" : "失敗")}");
            Console.WriteLine();
        }
    }
}
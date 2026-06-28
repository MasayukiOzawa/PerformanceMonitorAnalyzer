# 77-MainWindowリファクタリング

## 概要

MainWindow の肥大化を抑えるため、UI 非依存ロジックと状態管理を段階的に抽出した。挙動保存を基本方針とし、WPF コードビハインド構造は維持した。

## 抽出したクラス

- `CounterPathFormatter`: カウンターパスの表示名、コンピューター名、正規化、オブジェクト/インスタンス/カウンター名抽出。
- `ValueFormatHelper`: 単位推定、単位付き値、サンプリング間隔、ファイルサイズ、凡例値のフォーマット。
- `CsvLineParser`: ダブルクォートとエスケープに対応した CSV 行パース。
- `RelogCommandBuilder`: relog.exe 相当コマンド文字列の構築。
- `TimeRangeCalculator`: スライダー割合から選択時間範囲への変換。
- `CounterCsvBuilder`: 単一カウンターおよび全カウンターの CSV 文字列構築と既定ファイル名生成。
- `CounterStatisticsCalculator`: 統計計算。`Compute` と `Calculate` は計算方法が異なるため統合していない。
- `LogLineParser`: error.log 行のタイムスタンプ解析とログレベル推定。
- `OperationLogStore`: 操作ログ/エラーログのコレクション操作と上限トリム。
- `PanelLayoutState`: カウンター、凡例、統計、下部、スケール各パネルの折りたたみ状態と復元サイズ。
- `CounterValueModeConverter`: Raw/Delta 表示用データ変換、最新値取得、最大絶対値取得。
- `ChartColorPalette`: ScottPlot 色パレットと WPF 色変換。
- `StackedAreaSeriesBuilder`: 積み上げ面グラフ用の時刻統合、線形補間、ベースライン累積。
- `LineSeriesDataBuilder`: 折れ線グラフ用の X/Y 配列生成。
- `HierarchicalCheckboxTest` は製品コードから削除し、`CounterTreeNodeHierarchyTests` に xUnit テストとして移行した。

## 削減実績

- `MainWindow.xaml.cs`: 約 6,300 行から約 5,200 行へ削減。
- テスト件数: 95 件から 150 件へ増加。

## 意図的な挙動変更

- relog コマンド Expander は、BLG 読み込み済みであればカウンター未選択でも表示する。
- `MainWindow.xaml` で「読み込み」と「カウンターを追加」ボタンの配置を入れ替え、凡例パネルのドラッグリサイズ用 `LegendGridSplitter` を追加した。
- Release ビルド時に `publish\win-x64\` へシングルファイル発行を自動実行する。必要な場合は `-p:PublishSingleFileOnBuild=false` で抑止できる。

## 統合しなかった点

`CounterStatisticsCalculator.Compute` と `CounterStatisticsCalculator.Calculate` はどちらも統計計算だが、元実装の計算経路と分散計算の扱いが異なるため、挙動保存を優先して両方を保持した。`Compute` は母分散ベース、`Calculate` はデータテーブル表示側の既存計算を維持する。

## 時間範囲計算

時間範囲計算は `TimeRangeCalculator.CalculateRange` に統一した。`UpdateTimeRangeDisplay` 相当の表示更新処理も Ticks ベースからミリ秒ベースの計算に寄せたが、差はサブミリ秒で UI 表示には影響しない。

## 検証

- `dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`


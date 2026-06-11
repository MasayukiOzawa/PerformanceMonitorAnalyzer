# MainWindow リファクタリング実装計画

## 背景と目的

本リポジトリは WPF (.NET 10) の Performance Monitor BLG ファイル解析ツールである。機能追加を重ねた結果、[MainWindow.xaml.cs](../src/PerformanceMonitorAnalyzer/MainWindow.xaml.cs) が約 6,300 行・フィールド約 60 個に肥大化し、ファイル読み込み・グラフ描画・統計計算・ログ管理・パネル制御などの責務が 1 ファイルに集中している。

一方で、本リポジトリには既に確立された切り出しスタイルがある:

- **純粋ロジック → `public static` クラス**(例: `ScaleCatalog`, `StatisticsGridSorter`, `CounterTreeBuilder`, `DialogSizeInputHelper`)
- **UI バインディング → `INotifyPropertyChanged` モデル**(例: `LegendItem`, `CounterTreeNode`, `LogEntry`)
- **抽出クラスへの xUnit テスト**(`tests/PerformanceMonitorAnalyzer.Tests`、xunit 2.9.3、`InternalsVisibleTo` 設定済み)

本計画はこのスタイルを踏襲し、MainWindow から UI 非依存ロジックを**段階的に分離**する。

## 方針

- **挙動は一切変えない**(behavior-preserving)。メッセージ文言・数値フォーマット・計算結果はすべて現状維持
- MVVM 全面移行は**しない**。コードビハインド構造は維持する
- 抽出は「コピー」ではなく「移動」。MainWindow 側は新クラスの呼び出しに置換する
- 抽出した各クラスに xUnit テストを追加し、リファクタリングの安全網とする
- **1 フェーズ = 1 PR**。各フェーズは独立してビルド・テスト・手動確認が可能
- コミットメッセージは日本語。完了したフェーズの知見は必要に応じて `knowledge/` に記録する

## フェーズ一覧(リスクの低い順)

| フェーズ | 内容 | リスク | MainWindow 削減見込み |
|---|---|---|---|
| 0 | HierarchicalCheckboxTest の xUnit 化 | 極小 | 製品コードからテスト混在を除去 |
| 1 | カウンターパス・値フォーマットの純粋関数抽出 | 小 | 約 250 行 |
| 2 | CSV / relog / 時間範囲ロジック抽出 | 小 | 約 150 行 |
| 3 | 統計計算の抽出 | 小〜中 | 約 100 行 |
| 4 | ログ行パースとログストアの抽出 | 中 | 約 120 行 |
| 5 | パネルレイアウト状態クラスの導入 | 中 | フィールド 26 個 + 約 150 行 |
| 6 | 値モード変換とグラフ用データ準備の抽出 | 中〜高 | 約 200 行 |

---

## フェーズ 0: HierarchicalCheckboxTest の xUnit 化

**課題**: `src/PerformanceMonitorAnalyzer/HierarchicalCheckboxTest.cs`(164 行)は Console 出力ベースの手動テストコードで、製品コードに混在している。`RunTests()` への参照は製品コード内に存在せず、削除しても安全。

**作業**:

- テスト 4 本(オブジェクトレベル全選択 / インスタンスレベル選択 / カウンターレベル個別選択 / 部分選択状態)を `[Fact]` に書き換え、`Console.WriteLine` による目視確認を `Assert` に変換
- 新規: `tests/PerformanceMonitorAnalyzer.Tests/CounterTreeNodeHierarchyTests.cs`
- 削除: `src/PerformanceMonitorAnalyzer/HierarchicalCheckboxTest.cs`

**検証**: ビルド + `dotnet test`。製品コードの挙動変更がないため手動確認は不要。

---

## フェーズ 1: カウンターパス・値フォーマットの純粋関数抽出

MainWindow 内のフィールド非依存(または引数化で切り離せる)文字列処理を 2 クラスに抽出する。

**新規 `src/PerformanceMonitorAnalyzer/CounterPathFormatter.cs`(public static)**:

| 移動元(MainWindow.xaml.cs) | 行 | 抽出後 |
|---|---|---|
| `GetCounterDisplayName(string)` | 3240 | `GetDisplayName(string counterPath)` |
| `GetComputerNameFromCounterPath(string)` | 3289 | `GetComputerName(string counterPath, string? actualComputerName)` — `_actualComputerName` フィールドを引数化 |
| `NormalizeCounterPath(string)` | 4114 | `Normalize(string path)` |
| `ExtractCounterDisplayName` / `ExtractObjectDisplayName` / `ExtractInstanceDisplayName`(既に static) | 2051-2079 | そのまま移動 |

**新規 `src/PerformanceMonitorAnalyzer/ValueFormatHelper.cs`(public static)**:

| 移動元 | 行 | 内容 |
|---|---|---|
| `EstimateUnit(string)` | 3174 | カウンター名から単位を推定 |
| `FormatValueWithUnit(double, string)` | 3373 | 単位付きフォーマット(Bytes の GB/MB/KB 段階変換を含む) |
| `ExtractUnit(string)` | 2891 | FormattedValue から単位を抽出 |
| `FormatSamplingInterval(TimeSpan)` | 3312 | 取得間隔の日本語表記 |
| `FormatFileSize(long)` | 5576 | ファイルサイズ表記 |
| `FormatCounterValue(double)` | 5859 | 凡例の現在値表記 |

**テスト**:

- `CounterPathFormatterTests.cs`: `\\PC\Processor(_Total)\% Processor Time` 形式 / ローカル 2 要素形式 / インスタンスなし / 不正パス
- `ValueFormatHelperTests.cs`: 単位推定の各分岐、Bytes 閾値境界(1024 / 1048576 / 1073741824)、取得間隔の時間・分・秒・ミリ秒の境界

**手動確認**: `sample\DataCollector01.blg` を読み込み、凡例の表示名・統計グリッドの単位列・ファイル情報表示(サイズ・取得間隔)・データテーブルの FormattedValue が従来どおりであること。

---

## フェーズ 2: CSV / relog / 時間範囲ロジック抽出

**新規 `src/PerformanceMonitorAnalyzer/CsvLineParser.cs`(public static)**:

- `ParseCsvLine`(4122、ダブルクォート対応の CSV 分割)→ `Parse(string csvLine)`
- `ParseTimestampFromCsvLine`(3579)→ `TryParseTimestamp(string csvLine, out DateTime timestamp)`

**新規 `src/PerformanceMonitorAnalyzer/RelogCommandBuilder.cs`(public static)**:

- `GenerateRelogCommand`(5068)→ `Build(string blgFilePath, DateTime effectiveStartTime, DateTime effectiveEndTime)`
- `_fileStartTime` / `_fileEndTime` のフォールバックは呼び出し元で解決して渡す。try/catch + `LogError` は MainWindow 側に残す
- ※ 現実装は `counters` 引数を未使用 — 挙動保存のためそのまま維持し、コメントで明記する

**新規 `src/PerformanceMonitorAnalyzer/TimeRangeCalculator.cs`(public static)**:

- スライダー % → DateTime 変換式(`GetSelectedTimeRange`(5148)と `UpdateRelogCommandDisplay` 内に同一式が重複)→ `CalculateRange(DateTime fileStart, DateTime fileEnd, double startPercent, double endPercent)` に一本化

**新規 `src/PerformanceMonitorAnalyzer/CounterCsvBuilder.cs`(public static)**:

- `ExportCounterDataToCsv`(3195)の CSV 文字列構築部 → `BuildSingleCounterCsv(IEnumerable<PerformanceDataPoint>)`
- `ExportAllDataToCsv_Click`(3414)内の全カウンター CSV 構築部 → `BuildAllCountersCsv(...)`
- 既定ファイル名生成も `BuildDefaultFileName(string counterPath)` として抽出
- `SaveFileDialog`・`File.WriteAllText`・`MessageBox`・操作ログは MainWindow に残す

**テスト**:

- `CsvLineParserTests`: クォート内カンマ、エスケープ `""`、空フィールド
- `RelogCommandBuilderTests`: バッククォート行継続、日付フォーマット `yyyy/MM/dd HH:mm:ss`、出力ファイル名 `_output.blg`
- `TimeRangeCalculatorTests`: 0% / 100% / 中間値
- `CounterCsvBuilderTests`: ヘッダー行、タイムスタンプ順ソート、クォート付与

**手動確認**: relog コマンド表示が従来と一致、時間スライダー操作で `-b` / `-e` が変化、CSV エクスポート(単一・全体)の出力をリファクタ前後でファイル diff 比較。

---

## フェーズ 3: 統計計算の抽出

**新規 `src/PerformanceMonitorAnalyzer/CounterStatisticsCalculator.cs`(public static)**:

- `ComputeCounterStatistics`(3087、PDH 風・母分散の標準偏差)→ `Compute(string counterName, List<(DateTime, double)> dataPoints, string unit)`
- `CalculateStatistics`(3138、類似実装)→ `Calculate(string counter, List<PerformanceDataPoint> dataPoints, string unit)`
- ※ 2 つは分散の計算方法が微妙に異なるため、**統合せず両方そのまま移動**する(挙動保存を優先)
- 単位はフェーズ 1 の `ValueFormatHelper.EstimateUnit` を利用
- `CreateStatisticsItem` の UI 項目生成自体は MainWindow に残す

**テスト**: `CounterStatisticsCalculatorTests.cs` — 空データ、単一点、既知データでの平均・最小・最大・標準偏差(母分散であることをテストで固定化)、FirstTimestamp / LastTimestamp。

**手動確認**: 統計情報パネルの値、データテーブルタブ内の統計表示がリファクタ前と一致すること。

---

## フェーズ 4: ログ行パースとログストアの抽出

**新規 `src/PerformanceMonitorAnalyzer/LogLineParser.cs`(public static)**:

- `ParseLogLine`(5276)→ `Parse(string line)`。`[timestamp] message` 形式のパースと、メッセージからのレベル推定(ERROR / エラー / 失敗 / WARNING / 警告 / INFO)

**新規 `src/PerformanceMonitorAnalyzer/OperationLogStore.cs`(プレーンクラス)**:

- `_operationLogs` / `_errorLogs`(ObservableCollection)を保持し、`AddOperationLog`(5198)のエントリ生成・コレクション操作・上限トリムを担当
- `LoadErrorLogFromFile`(5237)は行リストを受け取る `LoadErrorLogLines(IEnumerable<string>)` に分割し、`File.ReadLines` は MainWindow 側に残す(テスト容易性のため)
- `ClearOperationLogs` / `ClearErrorLogs` のコレクション操作部もストアへ
- `Dispatcher` ディスパッチ・自動スクロール等の UI 処理は MainWindow に残す

**テスト**:

- `LogLineParserTests`: レベル推定の各分岐、タイムスタンプなし行、不正行 → null
- `OperationLogStoreTests`: 追加・クリア・行ロード

**手動確認**: 操作ログ / エラーログタブへの出力、クリアボタン、起動時のエラーログファイル読み込み。

---

## フェーズ 5: パネルレイアウト状態クラスの導入

**新規 `src/PerformanceMonitorAnalyzer/PanelLayoutState.cs`(プレーンクラス、INotifyPropertyChanged 不要)**:

- 5 パネル分の状態フィールドをグルーピング: `_isCounterPanelVisible` / `_lastCounterPanelWidth`、`_isLegendPanelCollapsed` / `_lastLegendPanelWidth`、`_isStatisticsPanelCollapsed` / `_lastStatisticsPanelHeight`、`_isBottomPanelCollapsed` / `_lastBottomPanelHeight`、`_isScalePanelCollapsed` / `_lastScalePanelWidth`(行 78-87)
- 関連定数(`StatisticsPanelMinHeight` 等、行 67-70)も移動
- 状態遷移を純粋メソッド化: 「折りたたみ時に現在の GridLength を記憶し、展開時に復元値を返す」を `GridLength? ToggleCounterPanel(GridLength currentWidth)` のような入出力型で実装。`Grid.ColumnDefinitions` への適用(副作用)は MainWindow の各イベントハンドラに残す
- 「すべて折りたたむ / 展開」メニュー(1002-1063)の判定ロジックも State 側へ

**MainWindow に残すフィールド(グルーピングしないもの)**:

- UI 再入ガードフラグ 5 個(`_isUpdatingScaleControls` 等)— イベントハンドラと不可分
- ScottPlot Plottable 辞書(`_chartSeries`、`_areaChartSeries`)— Plot オブジェクトと同一生存期間
- XAML バインド先の ObservableCollection 群
- ファイル / データ系 7 フィールド(`_counterData`、`_currentBlgFile` 等)— フェーズ 1〜4 で引数渡しに切り替わる。引数が煩雑化した場合のみ将来 `BlgFileContext` の導入を検討(本計画ではスコープ外)
- **全 60 フィールドを包む巨大な状態クラスは作らない**

**テスト**: `PanelLayoutStateTests.cs` — トグル往復で幅が復元される、全折りたたみ → 全展開、初期値。

**手動確認**: 各パネルの折りたたみ / 展開と幅・高さの復元、「すべて折りたたむ / 展開」メニュー、スプリッタでリサイズ後のトグル復元。

---

## フェーズ 6: 値モード変換とグラフ用データ準備の抽出

**ScottPlot との線引き方針**:

- **分離する**: データ点列 → 配列変換、Raw / Delta 値モード変換、線形補間、積み上げベースライン累積、色パレット決定。いずれも入力 → 出力が決まる純粋計算
- **MainWindow に残す**: `Plot.Add.Scatter` / `FillY`、軸範囲設定、`Refresh()`、Plottable の保持辞書、凡例 UI 操作。Plot オブジェクト操作は WPF コントロールと不可分のため分離しない

**新規 `src/PerformanceMonitorAnalyzer/CounterValueModeConverter.cs`(public static)**:

- `GetDisplayDataPoints`(423)の Raw → Delta 変換 → `ToDisplayDataPoints(string counter, List<PerformanceDataPoint> rawData, CounterValueMode mode)`
- `TryGetLatestDisplayValue`(484)→ `TryGetLatestValue(...)`
- `TryGetMaximumAbsoluteValue`(462、既に static)→ 移動

**新規 `src/PerformanceMonitorAnalyzer/ChartColorPalette.cs`(public static)**:

- `GetNextColor(int index)`(2663)、`ConvertToMediaColor`(2700)。ScottPlot.Color 型に依存するが UI コントロール非依存のためテスト可能

**新規 `src/PerformanceMonitorAnalyzer/StackedAreaSeriesBuilder.cs`(public static)**:

- `DrawStackedAreaChart`(2508)のデータ計算部(全カウンターのタイムスタンプ統合、`ToOADate` 変換、欠損点の線形補間、ベースライン累積)→ `(string Counter, double[] XValues, double[] Baseline, double[] Top)` のリストを返す `Build(...)` に抽出
- `InterpolateValue`(2630)→ Builder 内部の static メソッドへ
- `Plot.Add.FillY` とスタイル設定、凡例追加は MainWindow に残す
- `BuildLineSeries` は既にデータ準備が分離された構造のため、X/Y 配列生成部を新クラスへ寄せる軽微な整理に留める

**テスト**:

- `CounterValueModeConverterTests`: Raw モード素通し、Delta の差分計算と件数 -1、2 点未満で空
- `StackedAreaSeriesBuilderTests`: 補間の境界(前後欠損 / 同一タイムスタンプ)、ベースライン累積順序、スケール適用
- `ChartColorPaletteTests`: インデックス循環

**手動確認**: 折れ線 ↔ 積み上げ切替、値モード(Raw / Delta)切替、凡例チェックの表示 / 非表示で積み上げが再計算されること、スケール変更の反映、自動スケール、グラフ画像コピー。リファクタ前後でスクリーンショット比較を推奨。

---

## スケール機能について(追加抽出はしない)

スケール計算ロジックは既に [ScaleCatalog.cs](../src/PerformanceMonitorAnalyzer/ScaleCatalog.cs) へ抽出済み。スケール変更 region(4197-4696)の残りは ComboBox 生成・同期・可視性制御の WPF コードが大半のため、本計画では抽出対象としない。

## やらないこと

- **MVVM 全面移行 / ViewModel 導入**: コードビハインド構造は維持する
- **XAML の大規模変更**: MainWindow.xaml(約 1,200 行)は変更しない。コードビハインドから参照するコントロール名も不変
- **BlgFileAnalyzer.cs / PdhApi.cs のリファクタリング**: PDH API 依存でユニットテストが困難。エラーメッセージ整形の static 抽出は将来候補として `knowledge/` にメモのみ残す
- **partial class による MainWindow の機械的ファイル分割**: diff が巨大化しレビューが困難になるため。ロジック抽出を優先する
- **DI コンテナ導入、async フローの変更、例外処理ポリシーの変更**
- **機能追加・性能改善・ログ / メッセージ文言の変更**(挙動保存に反するため)
- **test_epsilon.csx / test_format.csx の削除**(開発用スクリプトとして残す)

## 各フェーズ共通の検証手順

1. `dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
2. `dotnet test tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`(既存テスト全パス + 新規テスト)
3. `build-and-run.bat` で起動 → `sample\DataCollector01.blg` を読み込み、各フェーズ固有の確認ポイントをチェック
4. 共通スモーク: BLG 読み込み → カウンター選択 → グラフ描画 → 統計表示 → CSV エクスポート → ログタブ確認

## フェーズ間の依存関係

- フェーズ 1(`ValueFormatHelper.EstimateUnit`)← フェーズ 3・6 が依存
- フェーズ 0・2・4・5 は相互に独立(順序の入れ替え可)
- 推奨順序はリスク昇順の **0 → 1 → 2 → 3 → 4 → 5 → 6**

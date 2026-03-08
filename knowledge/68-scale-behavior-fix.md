# Issue #68: スケール変更時の動作修正

## 問題の詳細

### 問題
グラフのスケールを変更した場合、プロット上の位置を変えたいが、Y軸の値は実際に変更したくない。
スケール変更はプロットの値の位置を変えるための機能として使用したい。

### 既存の実装の問題点
- スケール変更時に実際のデータ値に対して `dp.Value * scale` を適用
- 統計情報（平均、最大、最小、標準偏差）もスケール適用後の値で計算
- 凡例の現在値もスケール適用後の値を表示
- これにより実際のデータ値が見えなくなってしまう

## 修正内容

### 1. 統計情報の計算修正
**変更前:**
```csharp
// スケールを適用（PDHカウンターのスケール処理を模倣）
var scale = _counterScales.TryGetValue(counterName, out var scaleValue) ? scaleValue : 1.0;
var scaledValues = dataPoints.Select(dp => dp.Value * scale).ToArray();

// 統計計算にスケール適用後の値を使用
var count = (uint)scaledValues.Length;
var sum = scaledValues.Sum();
var min = scaledValues.Min();
var max = scaledValues.Max();
```

**変更後:**
```csharp
// 統計情報は元の値で計算（スケールは適用しない）
// これにより、実際のデータ値の統計が表示される
var values = dataPoints.Select(dp => dp.Value).ToArray();

// 統計計算に元の値を使用
var count = (uint)values.Length;
var sum = values.Sum();
var min = values.Min();
var max = values.Max();
```

### 2. 凡例の現在値表示修正
**変更前:**
```csharp
var latestValue = dataPoints.Last().Value;
var scale = _counterScales.GetValueOrDefault(item.CounterPath, 1.0);
var scaledValue = latestValue * scale;
item.CurrentValue = FormatCounterValue(scaledValue);
```

**変更後:**
```csharp
var latestValue = dataPoints.Last().Value;
// 凡例では元の値を表示（スケールは適用しない）
// スケールはグラフの表示位置のみに影響する
item.CurrentValue = FormatCounterValue(latestValue);
```

### 3. UI説明の改善
**XAML修正:**
```xml
<!-- 説明テキスト -->
<TextBlock Text="各カウンターのグラフ表示位置を調整:" />
<TextBlock Text="※実際のデータ値は変更されません" FontStyle="Italic" />
```

### 4. ログメッセージの明確化
**変更前:**
```csharp
LogError($"Counter '{counterName}' scale changed from {oldScale} to {newScale}");
```

**変更後:**
```csharp
LogError($"Counter '{counterName}' scale changed from {oldScale} to {newScale} (グラフ表示位置のみ変更、実際のデータ値は保持)");
```

## 修正後の動作

### 各表示での値の扱い
| 表示箇所 | 使用する値 | 説明 |
|---------|-----------|------|
| **グラフプロット** | `dp.Value * scale` | スケール調整後の位置で表示（視覚的比較のため） |
| **データテーブル** | `dp.Value` | 元の実際の値を表示 |
| **統計情報** | `dp.Value` | 元の実際の値で計算（平均、最大、最小、標準偏差） |
| **凡例の現在値** | `dp.Value` | 元の実際の値を表示 |

### スケール機能の目的
1. **視覚的比較**: 異なる単位や範囲のカウンターを同一グラフ上で比較
2. **表示位置調整**: グラフの見やすさを向上
3. **データ保持**: 実際のデータ値や統計は元の値を維持

## テストシナリオ

### シナリオ1: 基本的なスケール変更
1. BLGファイルを読み込む
2. 複数のカウンターを選択してグラフ表示
3. 右側のスケール設定で任意のカウンターのスケールを変更（例：1.0 → 0.1）
4. 確認項目：
   - グラフ上のプロット位置が変更されている
   - データテーブルの値は元のまま
   - 統計情報は元の値で計算されている
   - 凡例の現在値は元の値のまま

### シナリオ2: 異なる単位のカウンター比較
1. CPU使用率（%）とメモリ使用量（MB）など異なる単位のカウンターを選択
2. スケール調整により視覚的に比較しやすく調整
3. 確認項目：
   - 両方のカウンターが同一グラフ上で比較可能
   - 各カウンターの実際の値と単位は保持されている

### シナリオ3: スケール変更の影響範囲確認
1. カウンターを選択してデータテーブルも表示
2. スケールを大幅に変更（例：1.0 → 1000.0）
3. 確認項目：
   - グラフのY軸スケールが自動調整される
   - データテーブルの値は変更されない
   - CSVエクスポート時も元の値が出力される

## 技術的詳細

### 修正されたメソッド
- `CalculateCounterStatistics()` - 統計計算でスケール適用を除去
- `UpdateLegendCurrentValues()` - 凡例表示でスケール適用を除去
- `CreateCounterScaleControl()` - ログメッセージを明確化

### 保持されたメソッド（正しい動作）
- `AddLineChartSeries()` - グラフプロット時のスケール適用は維持
- `AddCounterToChartInternal()` - グラフプロット時のスケール適用は維持
- `DrawStackedAreaChart()` - 積み重ね面グラフでのスケール適用は維持

## 影響確認事項

### 想定される正常動作
- グラフの視覚的比較機能は維持
- データの整合性が向上
- ユーザーの混乱が軽減

### 注意事項
- 既存のスケール設定がある場合、統計情報の値が変更される可能性
- ユーザーには新しい動作についての説明が必要

## まとめ

この修正により、スケール機能は純粋に「グラフ表示位置の調整」機能となり、実際のデータ値は常に保持されるようになりました。これにより、ユーザーは視覚的な比較と正確なデータ分析の両方を同時に行うことができるようになります。

---

## 追補: 一括スケールと下部プルダウンの同期改善

### 追加で発生した問題
- 一括スケール適用後、下部の各カウンタースケールプルダウンの選択表示が更新されないケースがある。

### 対応内容
- スケール項目選択ロジックを `TrySelectScaleComboBoxItem()` として共通化。
- 一括スケール適用後に `SyncVisibleCounterScaleComboBoxes()` で表示中の各プルダウンを明示同期。
- 同期時は `_isUpdatingScaleControls` を使って `SelectionChanged` の再入を抑止し、不要な再描画ループを回避。

### 検証
- `dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

### 追加改善（操作性）
- 一括スケールのプルダウン変更時にも即時適用されるよう `BulkScaleComboBox_SelectionChanged` を追加。
- 従来の「全カウンターに適用」ボタン操作も継続して利用可能。

### 追加改善（積み重ね面グラフの可視項目再スタック）
- 問題: 積み重ね面グラフで凡例チェックをOFFにしても、面の表示が消えるだけで積み上げ計算自体は再構成されず、下段だけが欠けた表示になる。
- 対応:
  - 積み重ね計算対象を `_seriesVisibility == true` のカウンターに限定。
  - `UpdateChartSeriesVisibility()` で積み重ね面グラフ時は `RefreshChartWithCurrentType()` を呼び、可視状態変更のたびに再描画。
  - 凡例は選択中カウンター全体を維持し、再表示操作を継続可能にした。
  - 「すべて表示/非表示」時の再入を防ぐため `_isBulkLegendVisibilityUpdate` フラグでイベント連鎖を抑止。

### 検証（追加）
- `dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: 選択カウンター読み込み時の一括スケールUI初期化

### 追加で発生した問題
- 「選択されたカウンターを読み込み」を実行した際、一括スケールのUI選択は初期値へ戻したい一方で、パターンや個別設定で指定したカウンタースケールまで `1.0` に上書きされてしまう。

### 対応内容
- `ExecuteSelectedCounters_Click` の読み込み完了後に `ResetBulkScaleSelectorAfterCounterLoad()` を呼び出すよう変更。
- `ResetBulkScaleSelectorAfterCounterLoad()` で以下を実施:
  - 一括スケールのプルダウン選択を `1.0` に更新
  - 現在表示中カウンターの `_counterScales` は保持
  - 下部の個別スケールプルダウンは保持されたスケール値をそのまま表示

## 追補: 選択カウンター読み込み後のデータテーブル再表示

### 追加で発生した問題
- 下部のデータテーブル / ログ領域を折りたたんだ状態で「選択されたカウンターを読み込み」を実行しても、読込済みのカウンタータブが見えず、処理結果をすぐ確認しづらい。

### 対応内容
- `ShowDataTablesForCounters(IEnumerable<string>)` を追加し、読み込み完了後に読込済みの選択カウンターに対応するデータテーブルタブを必ず表示するようにした。
- 下部領域が折りたたまれている場合は `EnsureBottomPanelVisible()` で自動再表示する。
- 既存タブのうち今回未選択のものは除外し、読込済みの選択カウンターだけがタブに並ぶよう整理した。

### 検証（追加2）
- 実行中プロセス停止後、`dotnet build --nologo -v q -r win-x64 -p:OutputPath=%TEMP%\copilot-pattern-scale-preserve-build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。
- `dotnet test --nologo -v q -p:OutputPath=%TEMP%\copilot-pattern-scale-preserve-test -p:SelfContained=false -p:UseAppHost=false tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj` でテスト成功。

## 追補: 「すべてのタブを閉じる」でチェック状態を保持

### 追加で発生した問題
- 「すべてのタブを閉じる」を実行すると、データテーブルタブを閉じるだけでなく、選択済みカウンターのチェックまで外れてしまう。

### 対応内容
- `CloseAllTabs_Click()` から `SetAllCheckBoxes(false)` を削除し、データテーブルタブのみを閉じる動作へ変更した。
- 操作ログは「タブを閉じたが、カウンターの選択状態は保持される」ことが分かる文言に更新した。

## 追補: Process `% Processor Time` が同値化して見える問題の是正

### 問題
- Process の `% Processor Time` を複数表示した際、系列ごとの差が見えず、同じ値に見えるケースがある。

### 原因
- 自動スケール処理 `CalculateAutoScale()` が各系列を個別に正規化していたため、%系カウンターでも最大値がほぼ同じ高さに揃って表示されていた。

### 対応
- `EstimateUnit(counter) == "%"` の場合は自動スケールを `1.0` 固定にし、%系カウンターは実値のまま描画するよう変更。

### 検証（追加3）
- 実行中プロセス停止後、`dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: Process `% Processor Time` が 100 固定に見える問題

### 問題
- `%` カウンターを実値表示（自動スケール1.0）にした際、Y軸が 0-100 固定のままのため高値が上端でクリップし、100固定に見える。

### 対応
- `EnsureYAxisFixedRange()` を動的上限対応に変更。
  - 既定下限は `0`、既定上限は `100` を維持。
  - `TryGetCurrentDisplayMax()` で現在表示中データ（手動/自動スケール反映後）の最大値を算出し、100を超える場合は上限を拡張（`ceil(max * 1.1)`）。
- 折れ線グラフと積み重ね面グラフの両方で上限算出ロジックを適用。

### 検証（追加4）
- `dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: `% Processor Time` がデータテーブルで100固定になる問題

### 問題
- Process `% Processor Time` のデータテーブル値がすべて `100` になるケースがある。

### 原因
- `PdhGetFormattedCounterValue` を `PDH_FMT_DOUBLE` のみで取得していたため、PDHの100上限キャップが有効になり、100超過値が100へ丸められていた。

### 対応
- `PdhApi` に `PDH_FMT_NOCAP100` 定数を追加。
- `BlgFileAnalyzer.LoadCounterDataAsync`（通常/時間制約付きの両方）で `PDH_FMT_DOUBLE | PDH_FMT_NOCAP100` を指定して取得するよう変更。

### 検証（追加5）
- `dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: 複数折れ線が同色になる問題

### 問題
- 複数カウンターを追加した際に、折れ線グラフが同じ色で描画されるケースがある。

### 対応
- 折れ線シリーズの色指定を `scatter.LineStyle.Color` から `scatter.LineColor` に変更。
- 対象箇所:
  - `AddLineChartSeries`
  - `AddCounterToChartInternal`
  - `DrawLineChart`

### 検証（追加6）
- 実行中プロセス停止後、`dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: 折れ線色の再描画時同色化対策

### 問題
- 複数カウンター読み込み後、再描画（スケール変更や読み込み後処理）で折れ線色が同色になるケースがある。

### 対応
- `_counterLineColors` を追加し、カウンターごとの色を永続管理。
- `GetOrCreateCounterColor()` を導入し、折れ線色決定を共通化。
- `AddLineChartSeries` / `AddCounterToChartInternal` / `DrawLineChart` で共通色解決を使用。
- BLG再読み込み時は `_counterLineColors.Clear()` で色割り当てをリセット。

### 検証（追加7）
- `dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: UIラベル文言整理

### 対応
- スケール設定の注釈ラベル（「Y軸固定範囲...」「※データは...」）を削除。
- relog表示の文言を「relog.exe同等コマンド」から「relog.exe コマンド」に変更。

### 検証（追加8）
- `dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: 「選択されたカウンターを読み込み」ボタンが押せない問題

### 問題
- BLG読み込み後に時間範囲検出が失敗した場合、`ExecuteButton` が無効のままとなり押せない。

### 対応
- `UpdateTimeRangeUI()` の `_timeRangeDetected == false` 分岐で、BLG読み込み済みなら `ExecuteButton` を有効化。
- `LoadBlgFileAsync()` で `DetectTimeRangeAsync()` が失敗した場合も `ExecuteButton` を有効化。
- `ExecuteSelectedCounters_Click()` から `_timeRangeDetected` 必須条件を外し、時間範囲未検出時は全期間扱いで処理可能に変更。

### 検証（追加9）
- `dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: 時間範囲選択UIが表示されない問題

### 問題
- 時間範囲検出失敗時に `TimeRangeExpander` が非表示のままとなり、時間範囲選択UIが使えない。

### 対応
- `DetectTimeRangeAsync()` の失敗経路でも `ApplyFallbackTimeRange()` を呼び、フォールバック時間範囲を設定して `_timeRangeDetected=true` とする。
- `ApplyFallbackTimeRange()` で `UpdateTimeRangeUI()` を実行し、UI表示を必ず更新。

### 検証（追加10）
- `dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: `PDH_CSTATUS_BAD_COUNTERNAME` でBLGオープン失敗

### 問題
- 「選択されたカウンターを読み込み」実行時に `BLGファイルを開けませんでした: PDH_CSTATUS_BAD_COUNTERNAME (0xC0000BBD)` が発生するケースがある。

### 対応
- `BlgFileAnalyzer.OpenBlgFileAsync()` のフォールバックを強化。
  - `PdhBindInputDataSource` 失敗時に、`PdhOpenQuery(filePath, ...)` を追加試行。
  - 追加試行に失敗した場合のみ `PdhOpenLog` 経路へフォールバック。
  - 失敗した `_query` ハンドルは都度クリーンアップしてから次経路を試行。

### 検証（追加11）
- 実行中の `PerformanceMonitorAnalyzer.exe`（PID 23264）停止後、`dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: マシン名重複による `PDH_CSTATUS_BAD_COUNTERNAME` 対策

### 問題
- 一部環境でカウンターパスが `Machine\Object\Counter` 形式で渡される際、`EnsureMachineNameInPath()` がさらにマシン名を付与し、`Machine\Machine\Object\Counter` となって `PDH_CSTATUS_BAD_COUNTERNAME` が発生する。

### 対応
- `EnsureMachineNameInPath()` を以下の正規化仕様に変更:
  - `\\Machine\...` 形式はそのまま利用。
  - `Machine\Object\Counter` 形式は「既にマシン名付き」と判定し、`\\Machine\Object\Counter` に正規化。
  - マシン名未付与パスのみBLGのマシン名を1回だけ付与。

### 検証（追加12）
- 実行中の `PerformanceMonitorAnalyzer.exe`（PID 8140）停止後、`dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: relog コマンドの事前表示

### 問題
- 「選択されたカウンターを読み込み」を実行しないと、`relog.exe コマンド` が更新・表示されない操作経路がある。

### 対応
- チェックボックス変更時の `UpdateRelogCommandDisplay()` 呼び出しをリーフノード限定から全ノード変更後へ移動。
- 「すべて選択/すべて解除」（`SetAllCheckBoxes`）実行後にも `UpdateRelogCommandDisplay()` を実行。
- パターン適用（`ApplyCounterPatternAsync`）完了後にも `UpdateRelogCommandDisplay()` を実行。

### 検証（追加13）
- `dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: グラフ右クリックのコンテキストメニュー表示不具合

### 問題
- グラフ領域を右クリックした際に、アプリ側で定義したコンテキストメニューが期待どおり表示されない。

### 対応
- `InitializeChart()` で `PerformanceChart.PreviewMouseRightButtonUp` を購読。
- `PerformanceChart_PreviewMouseRightButtonUp` を追加し、`PerformanceChart.ContextMenu` を `MousePoint` 位置で明示表示。
- 既存の ScottPlot メニュー無効化（`WpfPlotMenu.Clear()`）を維持したまま、右クリック時はアプリ独自メニューを確実に表示。

### 検証（追加14）
- 実行中の `PerformanceMonitorAnalyzer.exe`（PID 37800）停止後、`dotnet build -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: グラフ領域カーソル移動時の意図しないズーム挙動

### 問題
- グラフ領域で右クリック操作時に、コンテキストメニュー操作と干渉してズーム動作が発生することがある。

### 対応
- `InitializeChart()` で `PerformanceChart.UserInputProcessor.RightClickDragZoom(false)` を設定し、右ドラッグズームを無効化。
- 右クリックはアプリ独自コンテキストメニュー表示用途に限定。

### 検証（追加15）
- 実行中の `PerformanceMonitorAnalyzer.exe`（PID 67868）停止後、`dotnet build -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: 積み重ね面グラフの境界線を細線化

### 要望
- 積み重ね面グラフの系列境界線を、現状より細くして見やすくしたい。

### 対応
- `MainWindow.xaml.cs` に `StackedAreaOutlineWidth` 定数を追加。
- `DrawStackedAreaChart()` で `FillY` 作成時に `LineColor` を系列色へ合わせ、`LineWidth` / `LineStyle.Width` を `1f` に明示設定。
- 面の塗りつぶし色は維持しつつ、境界線だけを折れ線グラフ（2px）より細く調整。

### 検証（追加）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`

## 追補: 凡例境界線のトグルボタン追加

### 要望
- 凡例領域についても、境界線から表示/非表示を切り替えたい。

### 対応
- `MainWindow.xaml` の凡例境界列を、`GridSplitter` とトグルボタンを重ねた `LegendDividerHost` に変更。
- `LegendPanelToggleButton_Click()` / `UpdateLegendPanelControls()` を追加し、凡例の折りたたみ時は境界列だけ残して再表示しやすくした。
- `UpdateChartVisibility()` から凡例境界UIの表示状態も更新し、グラフデータがないときはトグルごと非表示にするよう調整。

### 検証（追加）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`

### 最新仕様の調整
- 凡例境界のドラッグによる幅変更は廃止し、境界中央のトグルボタンによる表示/非表示切り替えに特化した。
- `MainWindow.xaml.cs` では `LegendGridSplitter` の表示制御を削除し、トグル状態に応じた列幅の復元だけを継続する構成へ整理した。

## 追補: 統計情報エリア境界のトグル

### 要望
- グラフ下部の統計情報エリアについて、中央のトグルアイコンで最小化できるようにしたい。

### 対応
- `MainWindow.xaml` の統計領域境界を `StatisticsDividerHost` とし、中央トグルボタンで表示/非表示を切り替える構成にした。
- `MainWindow.xaml.cs` に `_isStatisticsPanelCollapsed` / `_lastStatisticsPanelHeight` を追加し、統計情報エリアの表示高さを保持したまま折りたたみ/再表示できるようにした。
- `UpdateStatisticsDisplay()` から `UpdateStatisticsPanelControls()` を呼び出し、データ有無に応じて境界・統計領域の表示状態を一元管理。

### 検証（追加）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`

## 追補: 統計情報エリアのドラッグリサイズを一時オミット

### 要望
- 統計情報エリアについて、境界線のドラッグ＆ドロップによる高さ変更機能は一度外したい。

### 対応
- `MainWindow.xaml` の `StatisticsGridSplitter` を通常の境界表示へ置き換え、ドラッグで高さ変更できないようにした。
- トグルボタンによる最小化/再表示は維持し、統計情報の開閉操作だけは継続可能にした。

### 検証（追加）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`

## 追補: 統計情報エリアのドラッグリサイズを再有効化

### 要望
- 統計情報エリアの表示領域の高さを、再度境界線のドラッグ＆ドロップで変更できるようにしたい。

### 対応
- `MainWindow.xaml` の統計境界を、メイン `Grid` の直接子である `StatisticsGridSplitter` として再配置し、`ResizeDirection="Rows"` / `ResizeBehavior="PreviousAndNext"` を明示した。
- トグルボタンは同じ行にオーバーレイ配置し、ドラッグリサイズと最小化/再表示の両方が使えるようにした。
- `MainWindow.xaml.cs` では表示制御対象を `StatisticsDividerBorder` / `StatisticsGridSplitter` / `StatisticsPanelToggleButton` に整理した。

## 追補: 統計情報境界機能の復旧

### 問題
- 統計情報エリアの境界について、ドラッグリサイズやトグルが動作しなくなる回帰が発生した。

### 対応
- 境界UIを再度見直し、`StatisticsGridSplitter` をネイティブなWPFの行リサイズとして動作させる構成へ戻した。
- 境界線・`GridSplitter`・トグルボタンをそれぞれ独立要素として配置し、表示/非表示制御も個別に行うよう修正した。

### 検証（追加）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`

## 追補: 統計情報境界のリサイズ対象を修正

### 問題
- 統計情報境界の `GridSplitter` が「ズームをリセット / クリップボードにコピー」行と統計情報行をリサイズ対象として扱っており、ドラッグ時に統計情報ではなくボタン行の高さが広がっていた。

### 対応
- グラフ表示エリアを「グラフ・凡例」と「グラフ操作ボタン」の2段構成の内部 `Grid` にまとめ、外側の統計情報用 `GridSplitter` がそのまとまり全体と統計情報エリアをリサイズするように変更した。
- これにより、操作ボタン行は内部 `Grid` の `Auto` 行として自然高さを維持し、境界ドラッグ時は統計情報エリアの高さだけが期待どおりに変化する。

### 検証
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`

## 追補: データテーブル / ログ表示境界のトグル追加

### 要望
- データテーブル / ログ表示エリアの上端境界にも、中央のトグルアイコンを追加して最小化できるようにしたい。

### 対応
- ルート `Grid` の下部境界行を `BottomPanelDividerRowDefinition` として再定義し、`BottomPanelGridSplitter` と `BottomPanelToggleButton` を重ねて配置した。
- `MainWindow.xaml.cs` に `_isBottomPanelCollapsed` / `_lastBottomPanelHeight` と `InitializeBottomPanelControls()` / `BottomPanelToggleButton_Click()` / `UpdateBottomPanelControls()` を追加し、リサイズ後の高さを保持したまま折りたたみ / 再表示できるようにした。
- 下部エリア本体は `BottomPanelGrid` としてまとめ、折りたたみ時は `Visibility.Collapsed` と行高 `0` を同時に適用して不要な余白を残さないようにした。

### 検証
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`

## 追補: Y軸の上限/下限を手動変更可能にする

### 要望
- Y軸の上限・下限を任意の値に変更できるようにしたい。

### 対応
- グラフ操作パネルに Y軸範囲入力UIを追加（`下限` / `上限` / `適用` / `自動`）。
- `ApplyYAxisRange_Click` を追加し、入力値を検証して手動Y軸モードを有効化。
- `ResetYAxisRange_Click` を追加し、手動Y軸モードを解除して自動範囲へ復帰。
- `EnsureYAxisFixedRange()` を拡張し、手動モード時は入力した上限/下限を優先、通常時は既存の自動範囲ロジックを適用。

### 検証（追加16）
- 実行中の `PerformanceMonitorAnalyzer.exe`（PID 45792）停止後、`dotnet build -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: Y軸設定UIの配置変更（左側）

### 要望
- Y軸設定UIをグラフ表示の左側に配置したい。

### 対応
- `MainWindow.xaml` のグラフ表示ヘッダー（グラフタイプ/値モード行）へ Y軸設定UIを移動。
- 既存のグラフ操作ボタン行（ズームリセット/コピー）から Y軸設定UIを削除。
- 結果として、Y軸設定は「グラフ表示上部の左側」に常時表示される構成に変更。

### 検証（追加17）
- 実行中の `PerformanceMonitorAnalyzer.exe`（PID 66952）停止後、`dotnet build -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: 自動スケール機能の削除（手動スケールのみ）

### 要求
- グラフ描画時の自動スケール（`_autoCalculatedScales` / `CalculateAutoScale()`）依存をなくし、手動スケールのみで描画したい。

### 対応
- `MainWindow.xaml.cs` から `_autoCalculatedScales` フィールドと `CalculateAutoScale()` を削除。
- 折れ線グラフ・積み重ね面グラフ・Y軸上限算出でのスケール計算を `manualScale`（`_counterScales`）のみへ統一。
- 値モード切替時の自動スケールキャッシュクリア処理を削除。

### 検証（追加18）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功（既存警告: `CS0162` は継続）。

## 追補: Y軸の `Value` ラベルを非表示化

### 要望
- Y軸の `Value` ラベルは不要。

### 対応
- `MainWindow.xaml.cs` の `GetCurrentYAxisLabel()` を変更。
  - `string.Empty` を返し、Y軸ラベルを非表示化。

### 検証（追加19）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功（既存警告: `CS0162` は継続）。

## 追補: 折れ線グラフ凡例のハイライト機能

### 要望
- 折れ線グラフ表示時に、特定の凡例項目の線だけを太くして強調したい。

### 対応
- 凡例行に `☆/★` ボタンを追加し、クリックで対象系列のハイライトを切り替え可能にした。
- `MainWindow.xaml.cs` に `_highlightedLegendCounterPath` を追加し、選択中系列を管理。
- 折れ線再描画時・可視状態更新時に `ApplyLineSeriesHighlight()` を実行し、対象系列のみ `LineWidth=4`（他は `LineWidth=2`）を適用。
- 凡例モデル `LegendItem` に `IsHighlighted` を追加し、凡例側の視覚状態（★表示/背景色/太字）を同期。

### 備考
- ハイライト操作は折れ線グラフ時のみ有効（積み重ね面グラフでは無効）。

### 検証（追加20）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: 折れ線凡例ハイライトの複数選択対応

### 要望
- 複数の系列を同時にハイライトできるようにしたい。

### 対応
- 単一選択の `_highlightedLegendCounterPath` を、複数選択用の `HashSet<string>`（`_highlightedLegendCounterPaths`）へ変更。
- `LegendHighlight_Click` をトグル方式に変更し、クリックした系列をセットへ追加/削除。
- `ApplyLineSeriesHighlight()` でセット内の全系列に `LineWidth=4` を適用し、それ以外は `LineWidth=2` を適用。
- 系列非表示・系列削除・凡例クリア時に対象パスをセットから除去し、状態の整合性を維持。

### 検証（追加21）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

## 追補: MainWindow.xaml.cs のリファクタリング

### 目的
- `MainWindow.xaml.cs` に集中していた補助型定義と折れ線系列生成/ハイライト処理の重複を整理し、挙動を変えずに保守しやすくしたい。

### 対応
- `CounterStatisticsItem` / `LegendItem` / `CounterTreeNode` / `PerformanceDataPoint` / `LogEntry` と関連 enum を `src\PerformanceMonitorAnalyzer\*.cs` の個別ファイルへ抽出し、`MainWindow.xaml.cs` から定義を削除。
- 折れ線系列生成で重複していたデータ点取得・色解決・Scatter設定・凡例追加を `BuildLineSeries()` / `AddBuiltLineSeriesToChart()` に集約。
- 複数ハイライト用 `HashSet<string>` の追加/削除/クリーンアップ処理を、専用ヘルパー経由に寄せて重複を削減。
- 並列作業時に補助型が `MainWindow.xaml.cs` に残ったため、最終検証で重複定義エラーを検知し、不要定義を削除して整合性を回復。

### 検証（追加22）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q --no-build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`

## 追補: 値モードラベル文言の調整

### 要望
- 値モードのラベルを `Raw / 差分` に変更したい。

### 対応
- `MainWindow.xaml` の値モードラジオボタン文言を `Raw` / `差分` に変更。
- `MainWindow.xaml.cs` の操作ログ文言も同じ表示名へ統一。
- README / wiki の値モード説明も `Raw / 差分` 表記へ更新。

### 検証（追加23）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`

## 追補: 階層チェックボックスのクリック遷移修正

### 問題
- 親/インスタンスの三状態チェックボックスをユーザーが直接クリックすると、`チェック -> 部分選択 -> 無効` の順に循環してしまう。

### 対応
- `CounterTreeNode` に `ToggleFromUserInteraction()` を追加し、ユーザー操作時は `true / false` のみを切り替えるように変更。
- `MainWindow.xaml` のツリー用チェックボックスに `PreviewMouseLeftButtonDown` / `PreviewKeyDown` を追加し、親ノードは部分選択を経由せず全選択/全解除へ遷移させるよう修正。
- `CounterTreeNodeTests` を追加し、親子同期・部分選択からの全選択・チェック済みからの全解除・ワイルドカード除外を自動テスト化。

### 検証（追加24）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`

## 追補: 値モード配置の調整と積み重ね差分時のY軸ラベル抑制

### 要望
- 値モードのラジオボタンをグラフタイプのすぐ右側に移動したい。
- 積み重ね面グラフ + 差分モードで表示される Y 軸ラベルは不要。

### 対応
- `MainWindow.xaml` のグラフ表示ヘッダーを調整し、グラフタイプと値モードを同じ横並び領域に集約した。
- `MainWindow.xaml.cs` の `GetCurrentYAxisLabel()` を見直し、`Delta (Prev)` は **折れ線グラフ + 差分モード** の組み合わせでのみ返すようにした。
- README / wiki の説明も、新しい操作位置と Y 軸ラベル条件に合わせて更新した。

### 検証（追加25）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`

## 追補: グラフのY軸表示そのものを非表示化

### 要望
- グラフの Y 軸は、差分モードのラベルだけでなく表示自体を削除して問題ない。

### 対応
- `MainWindow.xaml.cs` の `InitializeChart()` と `RefreshChartWithCurrentType()` で `PerformanceChart.Plot.Axes.Left.IsVisible = false` を設定し、Y軸の目盛り・ラベル・軸線をまとめて非表示化した。
- README / wiki も、Y軸は表示せず上部入力欄から範囲のみ調整する説明へ更新した。

### 検証（追加26）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`

## 追補: Y軸表示の復元

### 要望
- Y軸の表示は必要なので、目盛りと軸線は元に戻したい。

### 対応
- `MainWindow.xaml.cs` の `InitializeChart()` と `RefreshChartWithCurrentType()` で `PerformanceChart.Plot.Axes.Left.IsVisible = true` を設定し、左軸を再表示した。
- 左軸の目盛り文字サイズも復元しつつ、`GetCurrentYAxisLabel()` は空文字のまま維持して Y 軸ラベルだけ非表示にした。
- README / wiki の Y軸説明も「軸は表示、ラベルは非表示」に合わせて更新した。

### 検証（追加27）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`

## 追補: 選択カウンター読み込みエリアの「すべて選択」ボタン削除

### 要望
- 「選択されたカウンターを読み込み」付近の「すべて選択」ボタンは不要。

### 対応
- `MainWindow.xaml` から `すべて選択` ボタンを削除し、`MainWindow.xaml.cs` の未使用になった `SelectAll_Click()` も削除した。
- 一括解除は従来どおり `すべて解除` ボタンと `SetAllCheckBoxes(false)` で維持し、親/インスタンスのチェックボックスによる配下一括選択はそのまま利用できるようにした。
- README / wiki の操作説明も現在の UI に合わせて更新した。

### 検証（追加28）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`

## 追補: 選択カウンター読み込みボタン列の左揃え

### 要望
- 「選択されたカウンターを読み込み」のボタン位置を左揃えにしたい。

### 対応
- `MainWindow.xaml` のボタン行 `StackPanel` の `HorizontalAlignment` を `Left` に変更し、実行ボタンと `すべて解除` ボタンが左寄せで並ぶようにした。

### 検証（追加29）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`

## 追補: 積み重ね面グラフでの「凡例のすべて非表示」回帰修正

### 問題
- 差分 + 積み重ね面グラフで「すべて非表示」を押すと、凡例チェックの一括オフだけでなく、凡例や関連パネルまで非表示になってしまう。

### 対応
- `MainWindow.xaml.cs` に「表示中シリーズの有無」と「凡例パネルを維持すべき文脈」を分けるヘルパーを追加した。
- `UpdateLegendPanelControls()` / `UpdateStatisticsPanelControls()` / `UpdateScaleControlVisibility()` の呼び出し元を見直し、積み重ね面グラフで全系列が非表示でも `_legendItems` が残っている間はパネルを維持するようにした。
- `GraphControlPanel` は表示を維持しつつ、表示系列がないときだけ無効化するように調整した。

### 検証（追加30）
- `dotnet build --nologo -v q src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`

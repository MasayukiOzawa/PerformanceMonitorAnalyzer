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

## 追補: 選択カウンター読み込み時の一括スケール初期化

### 追加で発生した問題
- 「選択されたカウンターを読み込み」を実行した際、直前の一括スケール値が残るため、新規読み込み時の比較基準が一定にならない。

### 対応内容
- `ExecuteSelectedCounters_Click` の読み込み完了後に `ResetBulkScaleToDefaultAfterCounterLoad()` を呼び出すよう変更。
- `ResetBulkScaleToDefaultAfterCounterLoad()` で以下を実施:
  - 一括スケールのプルダウン選択を `1.0` に更新
  - 現在表示中カウンターの `_counterScales` を `1.0` に上書き
  - チャートを再描画し、下部の個別スケールプルダウン表示も `1.0` に同期

### 検証（追加2）
- 実行中プロセス停止後、`dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj` でビルド成功。

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

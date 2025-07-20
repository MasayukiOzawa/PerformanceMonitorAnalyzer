# 46-モダンUI改善とfeature/01統合

## 概要

Issue #46として、feature/01ブランチの内容を現在のブランチに統合し、モダンなUIデザインへの改善を実装した。主にMaterial Design風のスタイルを採用し、アニメーション効果やダークテーマ対応を追加した。

## 要件

- feature/01ブランチのグラフタイプ切り替え機能の統合
- モダンなUIデザインの実装
- アニメーション効果の追加
- ダークテーマ対応

## 実装したファイルと変更内容

### 1. feature/01ブランチ統合

#### MainWindow.xaml の変更
- グラフタイプ切り替えUI（ラジオボタン）の追加
- Grid.Row構造の調整（グラフタイプ選択行を追加）

#### MainWindow.xaml.cs の変更
- `ChartType` enumの追加（LineChart, StackedAreaChart）
- 新しいフィールドの追加：
  ```csharp
  private readonly Dictionary<string, ScottPlot.Plottables.FillY> _areaChartSeries = new();
  private ChartType _currentChartType = ChartType.LineChart;
  ```
- 主要な新規メソッド：
  - `ChartType_Changed` - グラフタイプ変更イベントハンドラー
  - `RefreshChartWithCurrentType` - 現在のタイプでチャート全体を再描画
  - `DrawLineChart` - 折れ線グラフ描画
  - `DrawStackedAreaChart` - 積み重ね面グラフ描画
  - `InterpolateValue` - データポイント間の線形補間
  - `GetNextColor` - 積み重ね面グラフ用の色管理
  - `AddLineChartSeries` - 個別の折れ線シリーズ追加

### 2. モダンUIデザインの実装

#### App.xaml の更新
- **Material Design風カラーパレット**：
  ```xml
  <SolidColorBrush x:Key="PrimaryColor" Color="#6750A4"/>
  <SolidColorBrush x:Key="SecondaryColor" Color="#625B71"/>
  <SolidColorBrush x:Key="SurfaceColor" Color="#FFFBFE"/>
  <SolidColorBrush x:Key="BackgroundColor" Color="#FFFBFE"/>
  ```

- **モダンコントロールスタイル**：
  - `ModernCard` - 角丸・影効果のカードスタイル
  - `ModernGroupBox` - ヘッダー付きカードデザイン
  - `ModernButton` - 角丸・ホバー効果・アニメーション付きボタン
  - `ModernSecondaryButton` - アウトライン型ボタン
  - `ModernTextBox` - 角丸・フォーカス効果付き入力フィールド
  - `ModernComboBox` - ドロップダウン付きモダンスタイル
  - `ModernRadioButton` - カスタムデザインのラジオボタン
  - `ModernProgressBar` - シンプルなプログレスバー

#### 主要なスタイル特徴
1. **角の丸み**: CornerRadius="12" (カード), CornerRadius="20" (ボタン)
2. **影効果**: DropShadowEffect (BlurRadius="8", ShadowDepth="2")
3. **アニメーション効果**: 
   - ホバー時のスケール変更（1.05倍）
   - カラーアニメーション
   - シャドウエフェクトの変化
4. **統一されたパディング・マージン**: 一貫した16px基準

### 3. ダークテーマ対応

#### ダークテーマカラーパレット
```xml
<SolidColorBrush x:Key="DarkPrimaryColor" Color="#D0BCFF"/>
<SolidColorBrush x:Key="DarkSurfaceColor" Color="#1C1B1F"/>
<SolidColorBrush x:Key="DarkBackgroundColor" Color="#121212"/>
```

#### テーマ切り替え機能
- メニューバーにテーマ切り替えオプション追加
- `ApplyTheme(bool isDark)` メソッドでリアルタイム切り替え
- チェックマーク付きメニュー項目で現在のテーマ表示

### 4. アニメーション効果

#### ボタンアニメーション
```xml
<DoubleAnimation Storyboard.TargetName="ButtonScale"
               Storyboard.TargetProperty="ScaleX"
               To="1.05" Duration="{StaticResource FastAnimation}"/>
```

- ホバー時のスケール変更（1.05倍）
- カラーアニメーション（Primary → PrimaryVariant）
- シャドウ効果の変化
- プレス時のスケール縮小（0.95倍）

#### アニメーション時間定義
```xml
<Duration x:Key="FastAnimation">0:0:0.2</Duration>
<Duration x:Key="MediumAnimation">0:0:0.3</Duration>
<Duration x:Key="SlowAnimation">0:0:0.5</Duration>
```

## 技術的な実装詳細

### グラフタイプ切り替えロジック

1. **折れ線グラフ（LineChart）**:
   - `ScottPlot.Plot.Add.Scatter()` を使用
   - 個別のシリーズとして管理
   - カウンターの追加・削除が個別に可能

2. **積み重ね面グラフ（StackedAreaChart）**:
   - `ScottPlot.Plot.Add.FillY()` を使用
   - ベースライン累積方式で実装
   - 全体再描画が必要（一つのカウンターの変更で全て再計算）

### データ補間機能
```csharp
private double InterpolateValue(List<PerformanceDataPoint> dataPoints, DateTime targetTime, double scale)
{
    // 目標時間の前後のデータポイントを見つけて線形補間
    var before = dataPoints.Where(dp => dp.Timestamp <= targetTime).LastOrDefault();
    var after = dataPoints.Where(dp => dp.Timestamp >= targetTime).FirstOrDefault();
    
    // 線形補間の計算...
}
```

### 色管理システム
10色のカラーパレットを定義し、カウンター数に応じて循環使用：
```csharp
var colors = new[]
{
    ScottPlot.Colors.Blue, ScottPlot.Colors.Red, ScottPlot.Colors.Green,
    ScottPlot.Colors.Orange, ScottPlot.Colors.Purple, ScottPlot.Colors.Brown,
    ScottPlot.Colors.Pink, ScottPlot.Colors.Gray, ScottPlot.Colors.Olive,
    ScottPlot.Colors.Cyan
};
```

## UIコンポーネントの変更

### ビフォー・アフター

| コンポーネント | 変更前 | 変更後 |
|---|---|---|
| GroupBox | 標準の矩形デザイン | 角丸カード + ヘッダー色付き |
| Button | 標準ボタン | 角丸 + アニメーション + 影効果 |
| TextBox | 標準入力フィールド | 角丸 + フォーカス効果 |
| RadioButton | 標準ラジオボタン | カスタムデザイン + 色変更 |
| ProgressBar | 標準プログレスバー | 細いモダンデザイン |

### アイコン追加
- 📊 パフォーマンスカウンター
- 📈 グラフ表示
- ⚙️ スケール設定
- 🚀 選択されたカウンターを読み込み
- ✨ パターンを適用
- 🌞 ライトテーマ
- 🌙 ダークテーマ

## パフォーマンス考慮事項

### アニメーション最適化
- GPU加速を利用したRenderTransform使用
- 短時間アニメーション（0.2秒）でレスポンシブ感を維持
- 必要最小限のプロパティ変更に限定

### 積み重ね面グラフの制約
- 全体再描画が必要なため、大量のカウンターでは重い
- データ補間処理によりCPU使用量が増加する可能性
- 時間間隔が大きい場合は補間の精度が落ちる

## 今後の改善案

### 1. パフォーマンス最適化
- 積み重ね面グラフの部分更新機能
- データ補間のキャッシュ機能
- 仮想化によるUI描画最適化

### 2. UI/UX改善
- グラフタイプのプレビュー機能
- カラーパレットの拡張または設定可能化
- アニメーションの設定オプション

### 3. アクセシビリティ
- ハイコントラストテーマ対応
- キーボードナビゲーション改善
- スクリーンリーダー対応

### 4. 追加機能
- 設定の永続化（テーマ選択等）
- カスタムテーマ作成機能
- より多くのアニメーション効果

## まとめ

feature/01ブランチのグラフタイプ切り替え機能とモダンUIデザインの実装により、以下の改善を実現した：

1. **機能拡張**: 折れ線グラフと積み重ね面グラフの切り替え
2. **視覚的改善**: Material Design風の統一されたデザイン
3. **ユーザビリティ向上**: アニメーション効果とダークテーマ対応
4. **保守性向上**: 統一されたスタイルシステムとリソース管理

最小限のコード変更で最大限の視覚的インパクトを実現し、モダンなアプリケーションとしての品質を大幅に向上させることができた。
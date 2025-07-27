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
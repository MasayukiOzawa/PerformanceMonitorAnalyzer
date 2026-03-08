# 72-CounterPatternのscaleテスト追加

## タスク概要

`CounterPatternManager` が読み込む YAML 設定において、`scale` の明示指定、未指定時の既定値 `1.0`、無効値の補正を自動テストで保証するようにしました。

## 追加したテスト

対象ファイル: `tests\PerformanceMonitorAnalyzer.Tests\CounterPatternTests.cs`

### 1. 明示的な `scale` の読み込み
- YAML 内に `scale: 0.25` を記述した設定ファイルを一時ディレクトリへ出力
- `CounterPatternManager.LoadConfigAsync()` 実行後、対象カウンターの `Scale` が `0.25` になることを検証

### 2. `scale` 未指定時の既定値維持
- `scale` を含まない YAML を読み込み
- 対象カウンターの `Scale` が `1.0` のまま維持されることを検証
- 併せて `enabled` の読み込みも確認

### 3. 無効な `scale` の補正
- YAML 内に `scale: 0` を記述した設定ファイルを読み込み
- `CounterPatternManager.LoadConfigAsync()` 実行後、対象カウンターの `Scale` が既定値 `1.0` に補正されることを検証

### 4. 既定設定ファイルの互換性確認
- 設定ファイルが存在しない状態で `LoadConfigAsync()` を実行し、既定の `counter-patterns.yaml` が生成されることを確認
- 生成された YAML に `scale:` のサンプルが含まれることを確認
- 生成直後の in-memory 設定と、生成済み YAML を再読み込みした結果の両方で、`scale: 0.000001` の例と既定値 `1.0` が共存したまま読み込めることを検証

## 調査メモ

- `src\PerformanceMonitorAnalyzer\CounterPattern.cs` の `CounterDefinition.Scale` は既定値 `1.0`
- YAML 読み込みは `YamlDotNet` の `CamelCaseNamingConvention` を使っており、`scale` は明示指定時に `Scale` プロパティへマッピングされる
- `CounterDefinition.Normalize()` により、`0` 以下や `NaN` / `Infinity` のような無効値は `1.0` へ補正される
- `scale` を YAML に書かない場合でも、オブジェクト初期値の `1.0` が維持される

## 検証コマンド

```powershell
dotnet test --nologo -v q -p:OutputPath=<temp path> -p:SelfContained=false -p:UseAppHost=false tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj
```

## 結果

- 上記コマンドでテスト成功
- `CounterPattern` の scale サポートに関する回帰テストが追加され、今後の仕様変更時に破壊的変更を検知しやすくなった
- 既定設定に含めた `scale: 0.000001` のサンプルと、未指定時の `1.0` の両方を自動検証できるようになった

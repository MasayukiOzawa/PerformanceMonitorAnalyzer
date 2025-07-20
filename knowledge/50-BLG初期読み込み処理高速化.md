# BLGファイル初期読み込み時の処理高速化

## 概要
PR #50で実装された、BLGファイル選択時の初期読み込み処理の高速化について説明します。

## 問題の背景
- BLGファイルを選択した際に、パフォーマンスカウンターのツリー構造構築のために**すべてのデータポイント**が読み取られていた
- 実際には、ツリー構造構築にはメタデータ（オブジェクト名、カウンター名、インスタンス名）のみが必要
- 全データポイントの読み取りは「選択されたカウンターを読み込み」実行時のみ必要

## 実装した修正

### 1. 初期読み込み処理の最適化

**修正ファイル**: `src/PerformanceMonitorAnalyzer/MainWindow.xaml.cs`

**修正箇所**: `ParseBlgFileWithPdhApiAsync`メソッド（line 779）
```csharp
// 修正前
if (counters.Count > 0)
{
    progress?.Report("カウンターデータを読み込み中...");
    // 実際のカウンターデータを読み込み（最初の10個のみテスト用）
    await LoadCounterDataWithPdhApiAsync(analyzer, counters, progress);
    return counters;
}

// 修正後
if (counters.Count > 0)
{
    // カウンターパスの生成が完了 - データポイントの読み込みは選択時に実行
    progress?.Report($"カウンター構造を構築しました（{counters.Count}個のカウンター）");
    return counters;
}
```

**効果**:
- 最初の10個のカウンターの全データポイント読み込み（最大100,000サンプル×10）を削除
- 初期読み込み時間の大幅短縮
- メモリ使用量の削減

### 2. 時間範囲取得処理の最適化

**修正箇所**: `DetectTimeRangeAsync`メソッドのフォールバック処理（line 2022-2036）
```csharp
// 修正前: サンプルカウンターから全データポイントを読み込んで時間範囲を取得
var counterInfo = await analyzer.LoadCounterDataAsync(sampleCounterPath, progress);
if (counterInfo.DataPoints.Count > 0)
{
    _fileStartTime = counterInfo.DataPoints.First().Timestamp;
    _fileEndTime = counterInfo.DataPoints.Last().Timestamp;
    // ...
}

// 修正後: GetTimeRangeAsyncメソッドを使用（データポイントを読み込まずに時間範囲を取得）
var (startTime, endTime) = await analyzer.GetTimeRangeAsync(progress);
_fileStartTime = startTime;
_fileEndTime = endTime;
```

**効果**:
- PDH APIの`PdhGetDataSourceTimeRange`を使用してメタデータのみから時間範囲を取得
- サンプルカウンターの全データポイント読み込みを回避

### 3. 不要メソッドの無効化

**修正内容**: `LoadCounterDataWithPdhApiAsync`メソッドをコメントアウト
- 将来的な利用の可能性を考慮して削除ではなくコメントアウト
- 初期読み込み時の不要なデータ読み込みを防止

## 技術詳細

### PDH API使用方法の最適化
1. **メタデータ取得**: `GenerateAllCounterPathsAsync`
   - `PdhEnumObjects`, `PdhEnumObjectItems`を使用
   - データポイントを読み込まずにカウンター構造を構築

2. **時間範囲取得**: `GetTimeRangeAsync`
   - `PdhGetDataSourceTimeRange`を使用
   - データポイントを読み込まずに時間情報を取得

3. **実データ読み込み**: `LoadCounterDataAsync`
   - ユーザーがカウンターを選択した時のみ実行
   - `PdhCollectQueryDataWithTime`で実際のデータポイントを読み込み

### パフォーマンス改善効果
- **初期読み込み時間**: 数十秒 → 数秒（推定）
- **メモリ使用量**: 大容量BLGファイルでの初期メモリ消費を大幅削減
- **ユーザー体験**: カウンターツリーが即座に表示される

## テスト項目
- [ ] 大容量BLGファイル（数GB）での初期読み込み時間測定
- [ ] カウンター選択後のデータ読み込み機能が正常動作することを確認
- [ ] 時間範囲検出機能が正常動作することを確認
- [ ] メモリ使用量の改善を測定

## 今後の改善案
1. **プログレッシブローディング**: カウンターツリーの段階的表示
2. **バックグラウンドキャッシング**: 頻繁にアクセスされるカウンターの事前読み込み
3. **インデックス機能**: BLGファイルのインデックス作成による高速検索

## 関連ファイル
- `src/PerformanceMonitorAnalyzer/MainWindow.xaml.cs`
- `src/PerformanceMonitorAnalyzer/BlgFileAnalyzer.cs`
- `src/PerformanceMonitorAnalyzer/PdhApi.cs`
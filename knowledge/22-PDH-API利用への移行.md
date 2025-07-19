# 22-PDH API利用への移行

## 概要
パフォーマンスモニターのカウンター値の読み込みを `relog.exe` から PDH (Performance Data Helper) API を使用した実装に変更しました。

## 変更内容

### 1. 時間範囲検出の変更
**ファイル**: `src/PerformanceMonitorAnalyzer/MainWindow.xaml.cs`
**メソッド**: `DetectTimeRangeAsync`

**変更前**:
- `relog.exe` を実行してCSVファイルを生成
- CSVファイルを解析して時間範囲を取得

**変更後**:
- `BlgFileAnalyzer` クラスを使用してPDH APIでBLGファイルを直接読み込み
- サンプルカウンターからタイムスタンプを取得して時間範囲を決定

### 2. カウンターデータ読み込みの変更
**ファイル**: `src/PerformanceMonitorAnalyzer/MainWindow.xaml.cs`
**メソッド**: `ExecuteRelogForSelectedCounters`

**変更前**:
- `relog.exe` を実行してCSVファイルを生成
- 複数の時間形式でリトライ機能
- CSVファイルを解析してデータ読み込み

**変更後**:
- `BlgFileAnalyzer` クラスを使用してPDH APIで各カウンターを直接読み込み
- 時間制約をアプリケーションレベルで適用
- エラーハンドリングとプログレス表示の改善

### 3. UI表示の更新
**ファイル**: `src/PerformanceMonitorAnalyzer/MainWindow.xaml`

**変更点**:
- Expanderのヘッダーを "relog.exe実行状況" から "PDH API実行状況" に変更
- "実行コマンド:" を "実行内容:" に変更

## 技術的な詳細

### 使用した既存クラス
1. **BlgFileAnalyzer**: BLGファイルの解析を行うクラス
   - `OpenBlgFileAsync`: BLGファイルを開く
   - `EnumerateObjectsAsync`: オブジェクトの列挙
   - `EnumerateCountersAndInstancesAsync`: カウンターとインスタンスの列挙
   - `LoadCounterDataAsync`: カウンターデータの読み込み

2. **PdhApi**: PDH APIのP/Invoke定義

### 主要な改善点
1. **パフォーマンス向上**: 外部プロセス実行が不要
2. **エラーハンドリング**: 各カウンター個別のエラー処理
3. **リアルタイム進捗**: カウンター毎の進捗表示
4. **依存関係削減**: `relog.exe` への依存を除去

## 実装時の課題と解決策

### 課題1: 時間範囲の取得
**問題**: PDH APIには直接時間範囲を取得するAPIがない
**解決策**: サンプルカウンターからデータポイントを読み込んで時間範囲を推定

### 課題2: 既存コードの複雑さ
**問題**: 大きなメソッドに複雑な `relog.exe` 実行ロジックが含まれていた
**解決策**: 段階的にPDH APIベースの実装に置き換え

### 課題3: UIとの整合性
**問題**: `relog.exe` の実行状況表示との整合性
**解決策**: PDH API用に表示内容を調整しつつ既存UIを活用

## テスト観点

### 基本機能テスト
- [ ] BLGファイルの読み込み
- [ ] 時間範囲の検出
- [ ] カウンターデータの読み込み
- [ ] 時間制約の適用

### エラーハンドリングテスト
- [ ] 存在しないBLGファイル
- [ ] 破損したBLGファイル
- [ ] 存在しないカウンター
- [ ] ネットワークファイルの読み込み

### パフォーマンステスト
- [ ] 大きなBLGファイルの処理
- [ ] 多数のカウンターの同時読み込み

## 今後の改善案

1. **非同期処理の最適化**: 複数カウンターの並列読み込み
2. **キャッシュ機能**: 読み込み済みデータのキャッシュ
3. **プログレス表示の改善**: より詳細な進捗情報
4. **エラー回復**: 一部カウンターの失敗時の継続処理

## 関連ファイル
- `src/PerformanceMonitorAnalyzer/MainWindow.xaml.cs` - メインロジック
- `src/PerformanceMonitorAnalyzer/MainWindow.xaml` - UI定義
- `src/PerformanceMonitorAnalyzer/BlgFileAnalyzer.cs` - PDH API実装
- `src/PerformanceMonitorAnalyzer/PdhApi.cs` - PDH API定義
# 22-PDH-API利用への移行

## 概要
パフォーマンスモニターのカウンター値読み込み処理を外部プロセス(relog.exe)からWindows標準のPDH (Performance Data Helper) APIを使用した実装に移行し、PDH_NO_DIALOG_DATA エラーを修正しました。

## 実施した改善

### 1. PerformanceCounters リポジトリの解析
DevelopersCommunity/PerformanceCounters リポジトリの実装を参考にして、PDH APIの正しい使用方法を学習しました。

### 2. PdhApi.cs の大幅な拡張
- 不足していたPDH API定義を追加
- `PDH_FMT_COUNTERVALUE` 構造体を union 形式に修正
- `PdhCollectQueryDataWithTime` でタイムスタンプ付きデータ収集を追加
- `PdhGetDataSourceTimeRange` で直接的な時間範囲取得を追加
- `PdhExpandWildCardPath` でワイルドカード展開を追加
- エラーコード定数とメッセージの拡充

### 3. BlgFileAnalyzer.cs の完全な書き直し
**旧実装の問題:**
- 複雑なデータソース/ログハンドル管理
- PDH_NO_DIALOG_DATA エラーの原因となる不適切なAPI使用

**新実装のアプローチ:**
- シンプルな `PdhOpenQuery(filePath)` による直接BLGファイル読み込み
- PerformanceCounters リポジトリの PCReaderEnumerator パターンを採用
- 時間範囲の直接取得: `GetTimeRangeAsync()`
- ワイルドカード展開: `ExpandWildCardPathAsync()`
- 時間制約付きデータ読み込み: `LoadCounterDataAsync(counterPath, startTime, endTime)`

### 4. MainWindow.xaml.cs の改善
- `DetectTimeRangeAsync()` で直接的な時間範囲取得を使用
- フォールバック機能の充実
- 時間制約付きカウンターデータ読み込みの実装
- NaN値のフィルタリング追加

## 技術的な改善点

### PDH_NO_DIALOG_DATA エラーの解決
**原因:** 不適切なPDH API使用パターン（データソース処理の複雑さ）
**解決策:** PerformanceCounters リポジトリの確立されたパターンを採用

### パフォーマンス向上
- 外部プロセス実行のオーバーヘッドを除去
- 中間CSVファイル生成が不要
- 直接的なバイナリデータアクセス

### エラーハンドリングの改善
- カウンター毎の個別エラー処理
- 段階的フォールバック機能
- 詳細なプログレス表示

## テスト結果
- BLGファイルの読み込み: ✅ 
- 時間範囲の検出: ✅ 
- カウンターデータの読み込み: ✅ 
- UI表示の更新: ✅ 
- PDH_NO_DIALOG_DATA エラー: ✅ 解決

## 参考リポジトリ
- [DevelopersCommunity/PerformanceCounters](https://github.com/DevelopersCommunity/PerformanceCounters)
  - 特に `PCReader.cs` と `PCReaderEnumerator.cs` の実装パターンを参考

## 関連ファイル
- `src/PerformanceMonitorAnalyzer/MainWindow.xaml.cs` - メインロジック
- `src/PerformanceMonitorAnalyzer/BlgFileAnalyzer.cs` - PDH API実装（完全書き直し）
- `src/PerformanceMonitorAnalyzer/PdhApi.cs` - PDH API定義（大幅拡張）
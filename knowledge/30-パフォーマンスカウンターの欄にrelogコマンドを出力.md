# PR30-パフォーマンスカウンターの欄にrelogコマンドを出力

## タスク概要
Issue #30の要求に対応し、パフォーマンスカウンターの欄にPDH API実行状況と同様のフォーマットで現在の時刻範囲でrelog.exeを実行した場合のコマンドを表示する機能を実装。

## 実装内容

### 追加された機能
1. **GenerateRelogCommand メソッド**
   - BLGファイルパス、カウンターリスト、時間範囲を基にrelog.exeコマンドライン生成
   - 複数行フォーマットでバックスラッシュ継続を使用
   - 長いカウンターリストの省略表示（5個まで表示）
   - 時間制約の有無を自動判定

2. **UI表示の拡張**
   - 既存のRelogCommandDisplayエリアを拡張
   - PDH API実行情報とrelog.exeコマンドを同時表示
   - 絵文字アイコンによる視認性向上（📊 📝 ⏰）

### 技術的詳細

#### 変更ファイル
- `src/PerformanceMonitorAnalyzer/MainWindow.xaml.cs`

#### 主要変更箇所
1. **ExecuteRelogForSelectedCounters メソッド（行2048-）**
   - relog.exeコマンド生成処理を追加
   - RelogCommandDisplay表示内容を拡張

2. **新規メソッド: GenerateRelogCommand（行2981-）**
   - relog.exeコマンドライン文字列生成
   - エラーハンドリング付き

#### relog.exeコマンド形式
```
relog.exe \
  "入力ファイル.blg" \
  -o "出力ファイル_output.csv" \
  -f CSV \
  -b "開始時刻" \
  -e "終了時刻" \
  -c \
    "カウンター1" \
    "カウンター2" \
    ... (他 N 個のカウンター)
```

## 実装アプローチ

### 1. 分析フェーズ
- 既存のPDH API実行状況表示機能の調査
- RelogCommandDisplay UIコンポーネントの特定
- ExecuteRelogForSelectedCountersメソッドの詳細分析

### 2. 設計フェーズ
- 最小限の変更で実現する方針を決定
- 既存UIコンポーネントの活用
- relog.exeコマンド生成ロジックの設計

### 3. 実装フェーズ
- GenerateRelogCommandメソッドの実装
- UI表示ロジックの修正
- フォーマット改善

### 4. テストフェーズ
- 独立したテストプログラムによる動作確認
- 時間制約あり/なしのテストケース
- カウンター数による表示変化の確認

## テスト結果

### テストケース1: 時間制約ありの場合
```
📊 PDH API: 6個のカウンターを時間範囲で読み込み
⏰ 時間範囲: 2024-01-15 10:00:00 ～ 2024-01-15 12:00:00

📝 同等のrelog.exeコマンド:
relog.exe \
  "C:\sample\DataCollector01.blg" \
  -o "C:\sample\DataCollector01_output.csv" \
  -f CSV \
  -b "2024/01/15 10:00:00" \
  -e "2024/01/15 12:00:00" \
  -c \
    "\Processor(_Total)\% Processor Time" \
    "\Memory\Available MBytes" \
    "\PhysicalDisk(_Total)\Disk Reads/sec" \
    "\Network Interface(*)\Bytes Total/sec" \
    "\System\Context Switches/sec" \
    ... (他 1 個のカウンター)
```

### テストケース2: 時間制約なしの場合
```
📊 PDH API: 6個のカウンターを読み込み（時間制約なし）

📝 同等のrelog.exeコマンド:
relog.exe \
  "C:\sample\DataCollector01.blg" \
  -o "C:\sample\DataCollector01_output.csv" \
  -f CSV \
  -c \
    "\Processor(_Total)\% Processor Time" \
    "\Memory\Available MBytes" \
    "\PhysicalDisk(_Total)\Disk Reads/sec" \
    "\Network Interface(*)\Bytes Total/sec" \
    "\System\Context Switches/sec" \
    ... (他 1 個のカウンター)
```

## 学んだこと

### 技術的洞察
1. **WPFアプリケーションの理解**: Windows専用のUIフレームワークの特徴
2. **既存コードベース活用**: 最小限の変更で最大の効果を得る方法
3. **非同期UI更新**: Dispatcher.InvokeAsyncを使ったUI更新パターン
4. **コマンドライン生成**: 可読性とメンテナンス性を両立したフォーマット

### 設計パターン
1. **単一責任の原則**: GenerateRelogCommandメソッドを独立した機能として分離
2. **既存パターンの踏襲**: PDH API表示機能と同様の形式を採用
3. **エラーハンドリング**: 例外発生時の適切な対応とログ出力

### 開発プロセス
1. **段階的実装**: 分析→設計→実装→テストの順序立てた進行
2. **独立テスト**: 本体とは別のテストプログラムによる検証
3. **最小限の変更**: 既存機能への影響を最小化

## 今後の改善可能性

1. **relog.exe実際実行機能**: 実際にrelog.exeを実行してCSV出力する機能
2. **コマンド履歴保存**: 生成されたコマンドの保存・再利用機能
3. **カスタマイズ設定**: 出力ファイル名やパラメータのユーザー設定
4. **バッチファイル生成**: 生成されたコマンドをバッチファイルとして保存

## 結論

Issue #30の要求を満たすrelog.exeコマンド表示機能を成功裏に実装。既存のPDH API実行状況表示と統一されたフォーマットで、現在の時刻範囲でのrelog.exeコマンドを見やすく表示する機能を追加した。最小限の変更でありながら、ユーザビリティの向上に貢献する機能となった。

## 関連資料

- Issue #30: パフォーマンスカウンターの欄にrelog コマンドを出力
- relog.exe公式ドキュメント: https://docs.microsoft.com/en-us/windows-server/administration/windows-commands/relog
- PDH API リファレンス: https://docs.microsoft.com/en-us/windows/win32/perfctrs/performance-counters-portal
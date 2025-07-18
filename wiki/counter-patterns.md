# カウンターパターン機能ガイド

## 概要

カウンターパターン機能は、事前定義されたカウンター選択パターンを使用して効率的にパフォーマンス分析を開始できる機能です。YAMLファイルベースの設定により、目的に応じたカウンターセットを素早く適用できます。

## 機能の特徴

### 🎯 事前定義パターン
- **6つの定義済みパターン**: 様々な分析シナリオに対応
- **ワンクリック適用**: パターン選択による瞬時のカウンター選択
- **目的別最適化**: 分析目的に応じた最適なカウンター組み合わせ

### ⚙️ 柔軟な設定
- **YAML設定ファイル**: `counter-patterns.yaml` による設定管理
- **カスタマイズ可能**: 独自パターンの追加・編集
- **スケール設定**: カウンターごとのスケール値指定
- **有効無効切り替え**: パターン内の個別カウンターの制御

### 🔍 インテリジェント検索
- **完全一致検索**: カウンター名の厳密マッチング
- **ワイルドカード対応**: `*`（任意文字列）、`?`（単一文字）サポート
- **フォールバック検索**: 部分一致による柔軟な検索

## 使用方法

### 基本的な使用手順

1. **BLGファイルの読み込み**
   ```
   ファイル → BLGファイルを開く（relog.exe使用）
   ```

2. **パターンの選択**
   - 左側パネルの「カウンターパターン」コンボボックス
   - 目的に応じたパターンを選択

3. **パターンの適用**
   - 「パターンを適用」ボタンをクリック
   - または「パターン」メニューから選択

4. **結果の確認**
   - 自動選択されたカウンターを確認
   - 適用結果ダイアログで成功・失敗を確認

### メニューからの適用

```
パターン → カウンターパターンを適用 → [目的のパターン]
```

## 定義済みパターン

### 1. 基本システム監視
**目的**: システム全体の基本的な監視
**カウンター**:
- `\Processor(_Total)\% Processor Time` - CPU使用率
- `\Memory\Available MBytes` - 利用可能メモリ
- `\PhysicalDisk(_Total)\Disk Reads/sec` - ディスク読み取り
- `\PhysicalDisk(_Total)\Disk Writes/sec` - ディスク書き込み

### 2. ネットワーク監視
**目的**: ネットワークトラフィックの分析
**カウンター**:
- `\Network Interface(*)\Bytes Total/sec` - 総バイト数/秒
- `\Network Interface(*)\Packets Total/sec` - 総パケット数/秒
- `\Network Interface(*)\Current Bandwidth` - 現在の帯域幅

### 3. 詳細システム監視
**目的**: 詳細なシステム分析
**カウンター**:
- `\System\Context Switches/sec` - コンテキストスイッチ/秒
- `\System\System Calls/sec` - システムコール/秒
- `\Process(_Total)\Working Set` - プロセス作業セット
- `\Process(_Total)\Private Bytes` - プライベートバイト
- `\Paging File(_Total)\% Usage` - ページファイル使用率

### 4. SQLサーバー監視
**目的**: SQL Serverパフォーマンスの分析
**カウンター**:
- `\SQLServer:General Statistics\User Connections` - ユーザー接続数
- `\SQLServer:Buffer Manager\Buffer cache hit ratio` - バッファキャッシュヒット率
- `\SQLServer:SQL Statistics\Batch Requests/sec` - バッチリクエスト/秒
- `\SQLServer:SQL Statistics\SQL Compilations/sec` - SQLコンパイル/秒
- `\SQLServer:Locks(_Total)\Lock Waits/sec` - ロック待機/秒

### 5. Webサーバー監視
**目的**: IIS Webサーバーの分析
**カウンター**:
- `\Web Service(_Total)\Current Connections` - 現在の接続数
- `\Web Service(_Total)\Get Requests/sec` - GETリクエスト/秒
- `\Web Service(_Total)\Post Requests/sec` - POSTリクエスト/秒
- `\Web Service(_Total)\Bytes Total/sec` - 総バイト/秒
- `\ASP.NET Applications(__Total__)\Requests/Sec` - ASP.NET リクエスト/秒

### 6. 高負荷診断
**目的**: システム高負荷時の詳細診断
**カウンター**:
- `\Processor(*)\% Processor Time` - 全CPUの使用率
- `\Process(*)\% Processor Time` - 全プロセスのCPU使用率
- `\Memory\Pages/sec` - ページ/秒
- `\Memory\Page Faults/sec` - ページフォルト/秒
- `\PhysicalDisk(*)\Avg. Disk Queue Length` - 平均ディスクキュー長
- `\PhysicalDisk(*)\% Disk Time` - ディスク時間率
- `\System\Processor Queue Length` - プロセッサキュー長

## カスタムパターンの作成

### 設定ファイルの場所
```
sample/counter-patterns.yaml
```

### 設定ファイルの編集
```
パターン → パターン設定ファイルを開く
```

### YAML形式の例
```yaml
patterns:
  マイカスタムパターン:
    name: マイカスタムパターン
    description: 独自の監視項目
    counters:
      - name: \カウンター\パス1
        scale: 1.0
        enabled: true
      - name: \カウンター\パス2
        scale: 0.1
        enabled: true
```

### 設定の再読み込み
```
パターン → パターン設定を再読み込み
```

## ワイルドカードパターン

### サポートする記号
- `*`: 任意の文字列にマッチ
- `?`: 単一文字にマッチ

### 使用例
```yaml
counters:
  - name: \Processor(*)\% Processor Time  # 全CPUコア
  - name: \Network Interface(*)\*         # 全ネットワークカウンター
  - name: \Process(?)\Working Set         # 単一文字プロセス名
```

## 分析ワークフローの例

### 1. 初回分析フロー
```
1. BLGファイル読み込み
2. 「基本システム監視」パターン適用
3. 全体的なリソース使用状況を把握
4. 問題がある領域を特定
```

### 2. 詳細分析フロー
```
1. 問題領域に応じて専門パターンを選択
   - CPU問題 → 「高負荷診断」
   - ネットワーク → 「ネットワーク監視」
   - SQL Server → 「SQLサーバー監視」
2. 詳細データを分析
3. 必要に応じて個別カウンターを追加
```

### 3. カスタム分析フロー
```
1. 頻繁に使用するカウンター組み合わせを
   カスタムパターンとして保存
2. 案件固有のパターンを作成
3. チーム内でパターンを共有
```

## トラブルシューティング

### パターンが適用されない
- **原因**: BLGファイルが読み込まれていない
- **対策**: まずBLGファイルを読み込んでからパターンを適用

### 一部のカウンターが見つからない
- **原因**: BLGファイルに該当カウンターが含まれていない
- **対策**: 
  - 適用結果ダイアログで未検出カウンターを確認
  - ワイルドカードパターンの使用を検討
  - カウンター名の表記を確認

### 設定ファイルが読み込まれない
- **原因**: YAML形式のエラー
- **対策**:
  - YAML形式の構文を確認
  - インデントは半角スペースを使用
  - 特殊文字はクォートで囲む

### パターンメニューが表示されない
- **原因**: 設定ファイルの読み込みエラー
- **対策**:
  - `error.log` ファイルでエラー内容を確認
  - デフォルト設定ファイルの再作成
  - 「パターン設定を再読み込み」を実行

## 最新情報

詳細な実装情報は以下のナレッジベースを参照してください：
- [YAMLパターン機能実装詳細](../knowledge/18-構成ファイルによる選択されたチェックボックスのパターン化.md)

## 関連機能

- [階層チェックボックス機能](./hierarchical-checkboxes.md) - 手動選択との組み合わせ
- [使用方法ガイド](./user-guide.md) - 基本的な操作方法
- [グラフ表示機能](../knowledge/13-グラフ表示機能の実装.md) - 選択カウンターの可視化

---

最終更新: 2024年12月19日
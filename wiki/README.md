# Performance Monitor Analyzer Wiki

## 概要

Performance Monitor Analyzer は、Windows Performance Monitor の .blg ファイルを読み込み、解析・可視化するC#デスクトップアプリケーションです。

## ドキュメント一覧

### ユーザーガイド
- [使用方法ガイド](./user-guide.md) - アプリケーションの基本的な使用方法
- [階層チェックボックス機能](./hierarchical-checkboxes.md) - オブジェクト/インスタンス単位での効率的な選択方法
- [🎯 カウンターパターン機能](./counter-patterns.md) - YAMLベースの事前定義パターンによる効率的分析
- [インストールガイド](./installation-guide.md) - インストールと環境設定
- [🚀 ビルドとデプロイメントガイド](./build-deployment-guide.md) - シングルバイナリビルドと配布方法
- [🔧 トラブルシューティング](./troubleshooting.md) - よくある問題と解決方法

### 開発者向けドキュメント
- [開発環境構築](./development-setup.md) - モダンな開発環境の構築手順（Dev Container対応）
- [アーキテクチャ概要](./architecture.md) - システムアーキテクチャと設計思想
- [API リファレンス](./api-reference.md) - クラスとメソッドのリファレンス

### ナレッジベース
- [🆕 モダン構成への改善実装](../knowledge/44-モダン構成への改善実装.md) - Dev Container、CI/CD、品質管理体制の整備
- [PDH API利用への移行](../knowledge/22-PDH-API利用への移行.md) - relog.exeからPDH APIへの移行実装詳細
- [🔧 SQLサーバー監視パターンでのエラー修正](../knowledge/26-SQLサーバー監視パターンでのログ読み込みエラー修正.md) - Index out of rangeエラーの原因分析と修正実装
- [🔧 パフォーマンスカウンターの欄にrelogコマンドを出力](../knowledge/30-パフォーマンスカウンターの欄にrelogコマンドを出力.md) - PDH API実行状況と同様の形式でrelog.exeコマンド表示機能実装
- [🆕 統計情報表示領域の追加](../knowledge/28-統計情報表示領域の追加.md) - グラフ下部への統計情報表示機能実装
- [relog.exe を使用したCSV変換機能](../knowledge/11-relog.exe-csv-conversion.md) - relog.exe実装の詳細解説（レガシー実装）
- [グラフ表示機能の実装](../knowledge/13-グラフ表示機能の実装.md) - ScottPlotによる時系列グラフ表示（.NET 8.0対応）
- [階層チェックボックス機能](../knowledge/16-オブジェクト-インスタンス単位の選択チェックボックスの実装.md) - オブジェクト/インスタンス単位での一括選択機能
- [🎯 YAMLパターン機能](../knowledge/18-構成ファイルによる選択されたチェックボックスのパターン化.md) - YAML設定ファイルによるカウンターパターン化実装
- [🚀 シングルバイナリ化実装](../knowledge/20-ビルド時のシングルバイナリ化.md) - .NET 8.0での自己完結型シングルバイナリ設定
- [トラブルシューティング](./troubleshooting.md) - よくある問題と解決方法
- [パフォーマンスカウンター一覧](./performance-counters.md) - サポートしているカウンターの詳細

## クイックスタート

### コンソール版（推奨）
```bash
cd src/PerformanceMonitorAnalyzer
dotnet run
```

### WPF版（Windows専用）
```bash
cd src/PerformanceMonitorAnalyzer
dotnet build -p:BuildWindowsWpf=true -f net8.0-windows
dotnet run -p:BuildWindowsWpf=true -f net8.0-windows
```

## 主な機能

- **BLGファイル読み込み**: Windows Performance Monitor のバイナリログファイルを解析
  - **⭐PDH API方式**: Windows標準PDH APIを使用した高速・直接解析（推奨）
  - **relog.exe方式**: relog.exeを使用したCSV変換解析（レガシー）
  - **⭐時間範囲選択**: 指定した時間範囲での部分解析が可能
- **🎯 カウンターパターン機能**: YAMLベースの事前定義パターンによる効率的分析
  - **6つの定義済みパターン**: 基本システム監視、ネットワーク監視、SQLサーバー監視等
  - **ワンクリック適用**: パターン選択による自動カウンター選択
  - **カスタマイズ可能**: YAML設定ファイルによる独自パターン作成
- **📊 グラフ表示**: ScottPlot.WPFによる時系列データの可視化
  - **複数カウンター同時表示**: 異なる色の線で複数カウンターを同時表示
  - **リアルタイム連携**: チェックボックス選択と連動したグラフ更新
  - **高性能描画**: .NET 8.0対応の高速チャートライブラリ
  - **🆕 統計情報表示**: グラフ下部に平均・最大・最小値を自動表示
- **データ可視化**: パフォーマンスデータの階層表示とタブ形式データテーブル
  - **⭐階層チェックボックス**: オブジェクト/インスタンス単位での一括選択機能
  - **三状態チェックボックス**: 全選択/部分選択/全解除の直感的な表示
- **マルチカウンター表示**: 複数のパフォーマンスカウンターを同時に監視
- **統計分析**: 平均、最大、最小、標準偏差の自動計算
- **データエクスポート**: CSV形式での個別・一括データ出力
- **動的データ読み込み**: カウンター選択時の効率的なデータ読み込み

## サポート環境

| 機能 | Windows | Linux | macOS |
|------|---------|-------|-------|
| コンソール版 | ✅ | ✅ | ✅ |
| WPF版 | ✅ | ❌ | ❌ |
| BLG解析 | ✅ | ❌ | ❌ |
| サンプルデータ | ✅ | ✅ | ✅ |

## 貢献

このプロジェクトへの貢献を歓迎します。モダンな開発環境が整備されているため、以下の手順で簡単に参加できます：

### 🚀 クイックスタート（Dev Container使用）
1. リポジトリをフォーク・クローン
2. VS Codeで開く
3. "Reopen in Container"を選択
4. 自動的に完全な開発環境が構築される

### 📋 貢献プロセス
1. GitHub Issue テンプレートを使用してイシューを作成
2. 適切なブランチ（`feature/*`、`fix/*`）を作成
3. Dev Container環境で開発・テスト
4. PR テンプレートを使用してプルリクエストを作成
5. 自動CI/CDチェック通過後、レビュー・マージ

詳細は [コントリビューションガイドライン](../CONTRIBUTING.md) をご覧ください。

## ライセンス

MIT License - 詳細は [LICENSE](../LICENSE) ファイルをご覧ください。

## 連絡先

- 問題報告: GitHub Issues
- 機能要求: GitHub Discussions
- 一般的な質問: GitHub Discussions

---

最終更新: 2025年1月16日
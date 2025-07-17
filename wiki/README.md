# Performance Monitor Analyzer Wiki

## 概要

Performance Monitor Analyzer は、Windows Performance Monitor の .blg ファイルを読み込み、解析・可視化するC#デスクトップアプリケーションです。

## ドキュメント一覧

### ユーザーガイド
- [使用方法ガイド](./user-guide.md) - アプリケーションの基本的な使用方法
- [階層チェックボックス機能](./hierarchical-checkboxes.md) - オブジェクト/インスタンス単位での効率的な選択方法
- [インストールガイド](./installation-guide.md) - インストールと環境設定

### 開発者向けドキュメント
- [開発環境構築](./development-setup.md) - 開発環境の構築手順
- [アーキテクチャ概要](./architecture.md) - システムアーキテクチャと設計思想
- [API リファレンス](./api-reference.md) - クラスとメソッドのリファレンス

### ナレッジベース
- [relog.exe を使用したCSV変換機能](../knowledge/11-relog.exe-csv-conversion.md) - relog.exe実装の詳細解説
- [グラフ表示機能の実装](../knowledge/13-グラフ表示機能の実装.md) - ScottPlotによる時系列グラフ表示（.NET 8.0対応）
- [階層チェックボックス機能](../knowledge/16-オブジェクト-インスタンス単位の選択チェックボックスの実装.md) - オブジェクト/インスタンス単位での一括選択機能
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
  - **PDH API方式**: Windows標準APIを使用した直接解析
  - **relog.exe方式**: relog.exeを使用した安定したCSV変換解析（推奨）
  - **⭐時間範囲選択**: relog.exe方式で時間範囲を指定した部分解析が可能
- **📊 グラフ表示**: ScottPlot.WPFによる時系列データの可視化
  - **複数カウンター同時表示**: 異なる色の線で複数カウンターを同時表示
  - **リアルタイム連携**: チェックボックス選択と連動したグラフ更新
  - **高性能描画**: .NET 8.0対応の高速チャートライブラリ
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

このプロジェクトへの貢献を歓迎します。以下の手順でご参加ください：

1. リポジトリをフォーク
2. 機能ブランチを作成
3. 変更をコミット
4. プルリクエストを作成

詳細は [開発者ガイド](./development-setup.md) をご覧ください。

## ライセンス

MIT License - 詳細は [LICENSE](../LICENSE) ファイルをご覧ください。

## 連絡先

- 問題報告: GitHub Issues
- 機能要求: GitHub Discussions
- 一般的な質問: GitHub Discussions

---

最終更新: 2025年7月16日
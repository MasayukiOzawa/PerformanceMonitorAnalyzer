# Performance Monitor Analyzer Wiki

## 概要

Performance Monitor Analyzer は、Windows Performance Monitor の .blg ファイルを読み込み、解析・可視化するC#デスクトップアプリケーションです。

## ドキュメント一覧

### ユーザーガイド
- [使用方法ガイド](./user-guide.md) - アプリケーションの基本的な使用方法
- [インストールガイド](./installation-guide.md) - インストールと環境設定

### 開発者向けドキュメント
- [開発環境構築](./development-setup.md) - 開発環境の構築手順
- [アーキテクチャ概要](./architecture.md) - システムアーキテクチャと設計思想
- [API リファレンス](./api-reference.md) - クラスとメソッドのリファレンス

### ナレッジベース
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
- **データ可視化**: リアルタイムグラフによるパフォーマンスデータの視覚化
- **マルチカウンター表示**: 複数のパフォーマンスカウンターを同時に監視
- **データエクスポート**: JSON形式でのデータ出力
- **統計分析**: 基本的な統計情報の計算と表示

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
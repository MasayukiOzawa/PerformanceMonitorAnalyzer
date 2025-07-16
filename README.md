# Performance Monitor Analyzer

Windows Performance Monitor の .blg ファイルを読み込み、解析・可視化するC# WPFデスクトップアプリケーションです。

## 機能

- ✅ BLGファイルの読み込み（Windows環境のみ）
- ✅ パフォーマンスカウンターの一覧表示
- ✅ 複数カウンターの選択機能
- ✅ カウンターごとのタブ形式データテーブル表示
- ✅ JSONファイルへのデータエクスポート
- ✅ 統計情報の表示
- ✅ エラーログ機能
- 🚧 データのグラフ化（現在開発中）

## プロジェクト構成

```
src/
└── PerformanceMonitorAnalyzer/
    ├── App.xaml                   # WPFアプリケーション設定
    ├── App.xaml.cs
    ├── MainWindow.xaml            # メインウィンドウUI
    ├── MainWindow.xaml.cs         # メインウィンドウロジック
    └── PerformanceMonitorAnalyzer.csproj
```

## 必要な環境

- .NET 8.0以降
- Windows環境必須
- Visual Studio 2022 または Visual Studio Code

## ビルドと実行

### Windows環境（推奨）

```bash
cd src/PerformanceMonitorAnalyzer
dotnet build
dotnet run
```

### 引数でBLGファイルを指定（Windows環境のみ）

```bash
dotnet run "C:\path\to\your\file.blg"
```

## 使用方法

### WPF GUI アプリケーション
1. アプリケーションを実行
2. メニューから「ファイル」→「BLGファイルを開く」を選択
3. 左側のカウンター一覧から表示したいカウンターにチェック
4. 下部のタブでカウンターごとの詳細データを確認
5. グラフ機能は現在開発中です

## サポートするカウンター例

- `\Processor(_Total)\% Processor Time` - CPU使用率
- `\Memory\Available MBytes` - 利用可能メモリ
- `\PhysicalDisk(_Total)\Disk Reads/sec` - ディスク読み取り速度
- `\PhysicalDisk(_Total)\Disk Writes/sec` - ディスク書き込み速度
- `\Network Interface(*)\Bytes Total/sec` - ネットワーク総バイト数
- `\System\Context Switches/sec` - コンテキストスイッチ/秒
- `\Process(_Total)\Working Set` - プロセス作業セット

## エラーログ

アプリケーションのエラーは `error.log` ファイルに出力されます。

## 開発環境

### Dev Container
プロジェクトにはDev Container設定が含まれています。Visual Studio Codeで開くことで、自動的に適切な開発環境が構築されます。

### VS Code設定
- デバッグ設定（`.vscode/launch.json`）
- ビルドタスク（`.vscode/tasks.json`）

## 技術仕様

### 使用技術
- **フロントエンド**: WPF (Windows Presentation Foundation)
- **グラフライブラリ**: LiveCharts.Wpf
- **データ形式**: JSON (Newtonsoft.Json)
- **BLG解析**: PDH API（将来実装予定）

### アーキテクチャ
- MVVMパターン（WPF版）
- 非同期処理対応
- クロスプラットフォーム対応（コンソール版）

## 今後の実装予定

- [ ] 実際のBLGファイル解析（PDH API使用）
- [ ] カウンターフィルタリング機能
- [ ] グラフの時間範囲指定
- [ ] CSVエクスポート機能
- [ ] リアルタイム監視機能
- [ ] カスタムカウンター追加

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。

## 貢献

プルリクエストやイシューの報告をお待ちしています。開発に参加される場合は、Dev Containerを使用することを推奨します。
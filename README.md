# Performance Monitor Analyzer

Windows Performance Monitor の .blg ファイルを読み込み、解析・可視化するC# WPFデスクトップアプリケーションです。

## 機能

- ✅ BLGファイルの読み込み（Windows環境のみ）
- ✅ パフォーマンスカウンターの一覧表示（階層構造表示）
- ✅ 複数カウンターの選択機能
- ✅ カウンターごとのタブ形式データテーブル表示
- ✅ **詳細統計情報の表示**（平均、最大、最小、標準偏差）
- ✅ **単位認識とインテリジェントフォーマット**
- ✅ **CSVファイルへのデータエクスポート**（個別・一括）
- ✅ **サンプルBLGファイルの読み込み機能**
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
sample/
└── DataCollector01.blg            # サンプルBLGファイル
```

## 🚨 実行環境要件

**このアプリケーションはWindows専用です**

- ✅ Windows 10/11
- ✅ .NET 8.0 Windows Desktop Runtime
- ✅ PowerShell（BLGファイル解析用）
- ❌ Linux/macOS（WPFのため実行不可）

## 必要な環境

- .NET 8.0以降
- Windows 10/11（WPF必須）
- PowerShell（BLGファイル解析用）

## 使用方法

### Windows環境での実行

```bash
cd src/PerformanceMonitorAnalyzer
dotnet build
dotnet run
```

### ファイル読み込み

1. **ファイル > BLGファイルを開く** - 任意のBLGファイルを選択
2. **ファイル > サンプルBLGファイルを読み込み** - 付属のサンプルファイルを読み込み

### カウンター表示と操作

- オブジェクト > インスタンス > カウンター の3階層で表示
- チェックボックスで選択したカウンターがデータテーブルに表示
- 「すべて選択」「すべて解除」ボタンで一括操作可能

### データテーブル機能

- **詳細な統計情報**: 各カウンターの平均、最大、最小、標準偏差を自動計算
- **インテリジェントフォーマット**: カウンターの種類に応じた単位の自動認識（%、MB、/sec等）
- **CSVエクスポート**: 
  - 個別カウンターのデータをCSV出力
  - 全カウンターのデータを一括CSV出力
- **データ表示**: タイムスタンプ、生値、フォーマット済み値、単位を表示
- **タブ管理**: 複数カウンターを同時に表示、一括クリア機能

## BLGファイル解析機能

### Windows環境
- PowerShellの`Import-Counter`コマンドを使用した実際のBLG解析
- COM経由でのPDH API使用（実装予定）

### Linux/macOS環境
- WPFアプリケーションのため実行不可
- エラーメッセージを表示してビルドを停止

## ログ機能

エラーログは実行ファイルと同じディレクトリの`error.log`に出力されます。
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

## トラブルシューティング

### アプリケーションが起動しない場合

1. **Windows環境の確認**
   - このアプリケーションはWindows専用（WPF使用）です
   - Linux/macOSでは実行できません

2. **起動時エラーの確認**
   - 起動エラーが発生した場合、実行ディレクトリに `startup_error.log` が作成されます
   - ログファイルの内容を確認してください

3. **コマンドライン引数の使用**
   ```bash
   # 正しい例
   dotnet run "C:\path\to\file.blg"
   
   # パスにスペースが含まれる場合は引用符で囲む
   dotnet run "C:\Program Files\My Files\data.blg"
   ```

4. **必要な依存関係**
   - .NET 8.0 Windows Desktop Runtime が必要です
   - PowerShell（BLGファイル解析で使用）

### よくある問題

- **ファイルが見つからないエラー**: ファイルパスが正しいか確認してください
- **権限エラー**: 管理者権限で実行してみてください
- **BLGファイル解析エラー**: PowerShellが利用可能か確認してください

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
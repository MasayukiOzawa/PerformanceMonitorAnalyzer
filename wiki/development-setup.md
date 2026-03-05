# 開発環境構築ガイド

## 必要な環境

### 基本要件
- **.NET 10.0 SDK** 以降
- **Visual Studio Code** または **Visual Studio 2022**
- **Git**

### プラットフォーム別要件

#### Windows環境（推奨）
- Windows 10/11
- Visual Studio 2022（推奨）または Visual Studio Code
- Windows SDK（WPF開発用）

#### Linux/macOS環境
- Docker（Dev Container使用時）
- Visual Studio Code with Dev Containers extension
- **注意**: WPFアプリケーションはWindows専用のため、実際の動作はWindowsでのみ可能

## セットアップ手順

### 1. リポジトリのクローン
```bash
git clone https://github.com/MasayukiOzawa/PerformanceMonitorAnalyzer.git
cd PerformanceMonitorAnalyzer
```

### 2. Dev Container を使用する場合（推奨）

#### 前提条件
- Docker Desktop
- Visual Studio Code
- Dev Containers extension

#### 手順
1. Visual Studio Code でプロジェクトを開く
2. コマンドパレット（Ctrl+Shift+P）を開く
3. "Dev Containers: Reopen in Container" を選択
4. 自動的に開発環境が構築される

### 3. ローカル環境でのセットアップ

#### .NET SDK のインストール
```bash
# Windows (winget)
winget install Microsoft.DotNet.SDK.10

# Ubuntu/Debian
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0

# macOS (Homebrew)
brew install dotnet
```

#### 依存関係の復元
```bash
cd src/PerformanceMonitorAnalyzer
dotnet restore
```

## ビルドとテスト

### コンソール版のビルド
```bash
cd src/PerformanceMonitorAnalyzer
dotnet build
dotnet run
```

### WPF版のビルド（Windows環境）
```bash
cd src/PerformanceMonitorAnalyzer
dotnet build -p:BuildWindowsWpf=true -f net10.0-windows
dotnet run -p:BuildWindowsWpf=true -f net10.0-windows
```

### テストの実行
```bash
# 単体テストを追加した場合
dotnet test
```

## 開発ツールの設定

### Visual Studio Code

#### 推奨拡張機能
- C# (ms-dotnettools.csharp)
- .NET Core Extension Pack
- GitLens
- Prettier - Code formatter

#### デバッグ設定
プロジェクトには `.vscode/launch.json` が含まれており、F5でデバッグ実行が可能です。

### Visual Studio 2022

#### 必要なワークロード
- .NET desktop development
- .NET Cross-platform development

## コーディング規約

### C# コーディングスタイル
- Microsoft の C# コーディング規約に準拠
- PascalCase: クラス名、メソッド名、プロパティ名
- camelCase: 変数名、フィールド名（private）
- インデント: 4スペース

### ファイル構成
```
src/
└── PerformanceMonitorAnalyzer/
    ├── Models/              # データモデル
    ├── Views/               # UIコンポーネント（WPF）
    ├── ViewModels/          # ビューモデル（MVVM）
    ├── Services/            # ビジネスロジック
    ├── Utilities/           # ユーティリティクラス
    └── wpf-version/         # WPF固有ファイル
```

## 開発フロー

### 1. ブランチ戦略
- `main`: 本番用安定版
- `release`: リリース準備用
- `feature/*`: 機能開発用
- `copilot/*`: Copilot による変更用

### 2. 開発プロセス
1. イシューの作成
2. 機能ブランチの作成
3. 開発・テスト
4. プルリクエストの作成
5. コードレビュー
6. マージ

### 3. コミットメッセージ
```
type(scope): description

例:
feat(ui): add performance counter selection
fix(parser): resolve BLG file parsing error
docs(wiki): update user guide
```

## デバッグとトラブルシューティング

### よくある問題

#### 1. WPFビルドエラー
```
error MSB4019: Microsoft.NET.Sdk.WindowsDesktop.targets was not found
```
**解決方法**: Windows環境で実行するか、コンソール版を使用

#### 2. パッケージ復元エラー
```bash
dotnet restore --force
dotnet clean
dotnet build
```

#### 3. Dev Container が起動しない
- Docker Desktop が実行中であることを確認
- Dev Containers extension がインストールされていることを確認

### ログとデバッグ
- アプリケーションログ: `error.log`
- VS Code デバッグコンソール: デバッグ実行時に使用
- .NET ログ: `dotnet --info` で環境情報確認

## UI 拡張とカスタマイズ

### レイアウト構造
Performance Monitor Analyzer は WPF の Grid レイアウトを使用し、以下の領域に分かれています：

1. **メニューバー**（Row 0）: ファイル操作とヘルプ
2. **プログレスバー**（Row 1）: ファイル読み込み進行状況
3. **メイン表示エリア**（Row 2）: カウンター選択とグラフ表示
4. **GridSplitter**（Row 3）: リサイズ用の分割線
5. **データテーブル**（Row 4）: タブ形式のデータ表示

### 動的リサイズ機能
- Row 3 に配置された GridSplitter により、データテーブル領域の高さを動的に調整可能
- 最小100ピクセル、最大600ピクセルの制約あり
- マウスオーバー時の視覚的フィードバック機能

```xml
<GridSplitter Grid.Row="3" 
              Height="5" 
              HorizontalAlignment="Stretch" 
              VerticalAlignment="Stretch"
              Background="#E0E0E0"
              Cursor="SizeNS"
              ToolTip="ドラッグしてデータテーブル領域の高さを調整"/>
```

### UI カスタマイズのガイドライン
- WPF の MVVM パターンに従った実装
- データバインディングによる動的な表示更新
- ユーザビリティを重視したインタラクティブな要素

## パフォーマンス測定

### ベンチマーク
```bash
# BenchmarkDotNet を使用した場合
dotnet run -c Release --project Benchmarks
```

### プロファイリング
- Visual Studio Diagnostic Tools
- dotMemory（JetBrains）
- PerfView（Microsoft）

## 貢献ガイドライン

### プルリクエスト
1. 機能説明を明確に記載
2. テストコードを含める
3. ドキュメントの更新
4. Breaking Changes の明示

### イシューの報告
- 再現手順の明記
- 環境情報の提供
- 期待する動作の説明

## 参考資料

- [.NET 10.0 Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [WPF Documentation](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
- [LiveCharts Documentation](https://lvcharts.net/)
- [Performance Monitor API](https://docs.microsoft.com/en-us/windows/win32/perfctrs/performance-counters-portal)

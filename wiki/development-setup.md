# 開発環境構築ガイド

## 必要な環境

### 基本要件
- **.NET 8.0 SDK** 以降
- **Visual Studio Code** または **Visual Studio 2022**
- **Git**

### プラットフォーム別要件

#### Windows環境（推奨）
- Windows 10/11
- Visual Studio 2022（推奨）または Visual Studio Code
- Windows SDK（WPF開発用）

#### Linux/macOS環境
- **制約**: PDH API はWindows専用のため、実行不可
- **可能な作業**: コード編集、静的解析、コンパイル
- **実行テスト**: Windows環境でのみ可能
- **開発用途**: UI以外のロジック開発（限定的）

### 🐳 Dev Container 対応（Windows専用）
プロジェクトは **Windows ベースの Dev Container** に対応しており、以下が自動設定されます：
- .NET 8.0 SDK（Windows Server Core）
- 必要なVS Code拡張機能
- C#開発ツール（OmniSharp、CodeLens等）
- **PDH API サポート**（Windows専用）
- YAML、JSON、PowerShell サポート

**重要**: このアプリケーションは Windows 専用の PDH (Performance Data Helper) API を使用するため、**Linux ベースの Dev Container では実行できません**。

## セットアップ手順

### 1. リポジトリのクローン
```bash
git clone https://github.com/MasayukiOzawa/PerformanceMonitorAnalyzer.git
cd PerformanceMonitorAnalyzer
```

### 2. Dev Container を使用する場合（Windows環境のみ）

#### 前提条件
- **Windows 11** または **Windows Server 2022**
- **Docker Desktop for Windows**（Windows コンテナーモード）
- Visual Studio Code
- Dev Containers extension

#### Windows PDH API の制約
このアプリケーションは `pdh.dll` を使用するため：
- **Windows コンテナーが必須**
- Linux/macOS の Dev Container では実行不可
- コンパイルは可能だが、実行時に `DllNotFoundException` が発生

#### 手順
1. Docker Desktop を **Windows コンテナーモード** に切り替え
2. Visual Studio Code でプロジェクトを開く
3. コマンドパレット（Ctrl+Shift+P）を開く
4. "Dev Containers: Reopen in Container" を選択
5. Windows Server Core イメージのダウンロード（初回は5-10分）
6. 自動的に開発環境が構築される

詳細は [.devcontainer/README.md](.devcontainer/README.md) を参照してください。

### 3. ローカル環境でのセットアップ

#### .NET SDK のインストール
```bash
# Windows (winget)
winget install Microsoft.DotNet.SDK.8

# Ubuntu/Debian
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0

# macOS (Homebrew)
brew install dotnet
```

#### 依存関係の復元
```bash
cd src/PerformanceMonitorAnalyzer
dotnet restore
```

## ビルドとテスト

### プロジェクト構造
```
PerformanceMonitorAnalyzer/
├── src/
│   └── PerformanceMonitorAnalyzer/
│       └── PerformanceMonitorAnalyzer.csproj
├── tests/
│   └── PerformanceMonitorAnalyzer.Tests/
│       └── PerformanceMonitorAnalyzer.Tests.csproj
├── .devcontainer/
│   └── devcontainer.json
├── .vscode/
│   ├── launch.json
│   ├── tasks.json
│   └── settings.json
├── .github/
│   └── workflows/
│       ├── build.yml
│       └── release.yml
└── PerformanceMonitorAnalyzer.sln
```

### ソリューション全体のビルド
```bash
# ルートディレクトリから
dotnet build PerformanceMonitorAnalyzer.sln
```

### WPFアプリケーションのビルド（Windows環境）
```bash
cd src/PerformanceMonitorAnalyzer
dotnet build
dotnet run
```

### テストの実行
```bash
# 全てのテストを実行
dotnet test

# カバレッジレポート付きでテスト実行
dotnet test --collect:"XPlat Code Coverage"

# 特定のテストプロジェクトのみ実行
dotnet test tests/PerformanceMonitorAnalyzer.Tests/
```

### VS Codeでのデバッグ
1. F5キーを押す、または
2. 「実行とデバッグ」ビューから構成を選択：
   - `Launch PerformanceMonitorAnalyzer`: 通常の起動
   - `Launch PerformanceMonitorAnalyzer with BLG file`: サンプルファイル付きで起動

## 開発ツールの設定

### Visual Studio Code（推奨）

#### 自動設定される拡張機能
Dev Containerを使用する場合、以下の拡張機能が自動でインストールされます：
- **C# (ms-dotnettools.csharp)**: C#言語サポート
- **C# Dev Kit (ms-dotnettools.csdevkit)**: 強化されたC#開発体験
- **.NET Core Extension Pack**: .NET開発用の包括的なツールセット
- **YAML (redhat.vscode-yaml)**: YAML ファイルサポート
- **GitHub Copilot**: AI ペアプログラミング
- **PowerShell**: PowerShell スクリプトサポート

#### 自動設定されるVS Code設定
- **フォーマット**: 保存時自動フォーマット
- **インポート整理**: 保存時自動インポート整理
- **EditorConfig サポート**: 統一されたコードスタイル
- **Roslyn Analyzers**: コード品質チェック

#### デバッグ設定
プロジェクトには包括的な `.vscode/launch.json` が含まれており、以下が可能：
- **F5キー**: 即座にデバッグ実行
- **BLGファイル付き実行**: サンプルファイルでの自動テスト
- **プロセスアタッチ**: 実行中のプロセスへのデバッグ接続

### Visual Studio 2022

#### 必要なワークロード
- **.NET desktop development**: WPF開発用
- **.NET Cross-platform development**: 将来のクロスプラットフォーム対応用

### 品質管理ツール

#### EditorConfig
プロジェクト全体で統一されたコードスタイル設定：
- インデント: 4スペース
- エンコーディング: UTF-8 with BOM
- 改行コード: CRLF (Windows準拠)
- C# specific: ファイルスコープ名前空間、var使用規則等

#### 静的解析
- **Roslyn Analyzers**: 組み込み済み
- **Security rules**: セキュリティベストプラクティス強制
- **Performance rules**: パフォーマンス問題の早期発見

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
- `release`: リリース準備用（mainへの自動マージ対象）
- `feature/*`: 機能開発用
- `fix/*`: バグ修正用  
- `copilot/*`: GitHub Copilot による変更用

### 2. 自動化されたCI/CD
#### GitHub Actions Workflows
- **Build and Test** (`build.yml`):
  - Push/PR時の自動ビルド・テスト
  - 複数プラットフォーム対応（win-x64, win-x86, win-arm64）
  - アーティファクトの自動アップロード

- **Release from main** (`release.yml`):
  - `release`ブランチから`main`への自動マージ
  - バージョンタグの自動作成
  - GitHub Release の自動作成

#### Dependabot
- 週次での依存関係自動更新
- .NET パッケージとGitHub Actions両方に対応
- 自動ラベリングとアサイン設定

### 3. 開発プロセス
1. **イシューの作成**: GitHub Issue テンプレート使用
2. **機能ブランチの作成**: 適切なプレフィックス使用
3. **開発・テスト**: ローカルまたはDev Container環境で
4. **プルリクエストの作成**: PRテンプレート使用
5. **自動チェック**: CI/CDパイプラインによる自動検証
6. **コードレビュー**: レビュー後の承認
7. **マージ**: 自動またはメンテナによるマージ

### 4. 品質保証
- **自動テスト**: xUnit + FluentAssertions + Moq
- **静的解析**: Roslyn Analyzers
- **依存関係スキャン**: Dependabot
- **セキュリティ**: Security.md準拠の脆弱性管理

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

- [.NET 8.0 Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [WPF Documentation](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
- [LiveCharts Documentation](https://lvcharts.net/)
- [Performance Monitor API](https://docs.microsoft.com/en-us/windows/win32/perfctrs/performance-counters-portal)
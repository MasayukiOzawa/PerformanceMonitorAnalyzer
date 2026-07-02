# 開発環境構築ガイド

## 必要な環境

- Windows 10/11
- .NET 10.0 SDK 以降
- Visual Studio 2022 または Visual Studio Code
- Git

WPF アプリケーションのため、実行と画面確認は Windows 環境を前提にしています。

## セットアップ手順

### 1. リポジトリのクローン

```bash
git clone https://github.com/MasayukiOzawa/PerformanceMonitorAnalyzer.git
cd PerformanceMonitorAnalyzer
```

### 2. 依存関係の復元

```bash
dotnet restore src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.sln
```

### 3. ビルド

```bash
dotnet build src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.sln -c Debug
```

### 4. 実行

```bash
dotnet run --project src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.csproj
```

BLG ファイルを起動時に指定する場合:

```bash
dotnet run --project src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.csproj -- "C:\Logs\your-file.blg"
```

## テスト

```bash
dotnet test src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.sln
```

## Release ビルド

通常の Release ビルド:

```bash
dotnet build src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.sln -c Release
```

シングルバイナリ発行:

```bash
publish.bat
```

生成物は `publish/win-x64/` と `publish/win-arm64/` に出力されます。`publish/` 配下の成果物は Git 管理対象外です。

## 開発ツール

### Visual Studio 2022

推奨ワークロード:

- .NET desktop development
- .NET Cross-platform development

### Visual Studio Code

推奨拡張機能:

- C# Dev Kit
- C# extension
- GitLens

## ブランチ運用

- `main`: 公開・安定版
- `release`: リリース準備用
- `feature/*`: 機能開発用
- `codex/*`: Codex による作業用

作業開始前に目的別のブランチを作成し、`main` へ直接コミットしない運用を推奨します。

## コーディング規約

- C# は Microsoft の一般的なコーディング規約に準拠します。
- `PascalCase`: クラス名、メソッド名、プロパティ名
- `camelCase`: ローカル変数、private フィールド
- インデントは 4 スペースです。

## 主な構成

```text
src/PerformanceMonitorAnalyzer/
  App.xaml
  MainWindow.xaml
  MainWindow.xaml.cs
  BlgFileAnalyzer.cs
  PdhApi.cs
  CounterPattern.cs
  RelogCommandBuilder.cs
tests/PerformanceMonitorAnalyzer.Tests/
config/counter-patterns.yaml
```

## トラブルシューティング

### WPF ビルドで Windows SDK 関連のエラーが出る

Visual Studio Installer で .NET desktop development ワークロードと Windows SDK が入っていることを確認してください。

### パッケージ復元に失敗する

```bash
dotnet restore src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.sln --force
dotnet clean src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.sln
dotnet build src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.sln
```

### BLG ファイルを読み込めない

- Windows 環境で実行していることを確認してください。
- 対象ファイルが `.blg` 形式で、現在のユーザーから読み取れる場所にあることを確認してください。
- 詳細は実行ディレクトリの `error.log` を確認してください。

## 参考資料

- [.NET Documentation](https://learn.microsoft.com/dotnet/)
- [WPF Documentation](https://learn.microsoft.com/dotnet/desktop/wpf/)
- [ScottPlot Documentation](https://scottplot.net/)
- [Performance Counters](https://learn.microsoft.com/windows/win32/perfctrs/performance-counters-portal)

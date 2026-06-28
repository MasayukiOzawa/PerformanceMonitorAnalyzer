---
applyTo: "**"
---

# ビルド Instructions

## Debug / Release の扱い

- Debug は開発時の通常ビルド・実行確認に使用してください。
  - 例: `dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.sln -c Debug`
  - 通常ビルド出力: `src\PerformanceMonitorAnalyzer\bin\Debug\net10.0-windows\win-x64\PerformanceMonitorAnalyzer.exe`
  - Debug ビルドはシングルバイナリ成果物の生成対象ではありません。
- Release は配布・成果物確認に使用してください。
  - 例: `dotnet build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.sln -c Release`
  - 通常ビルド出力: `src\PerformanceMonitorAnalyzer\bin\Release\net10.0-windows\win-x64\PerformanceMonitorAnalyzer.exe`
  - 現在のプロジェクト設定では、Release ビルド時に `publish\win-x64\PerformanceMonitorAnalyzer.exe` へシングルバイナリ発行が自動実行されます。
  - Release ビルド時の自動発行を抑止する必要がある場合は `-p:PublishSingleFileOnBuild=false` を指定してください。

## 生成されるバイナリのパス

- Debug 通常ビルド: `src\PerformanceMonitorAnalyzer\bin\Debug\net10.0-windows\win-x64\PerformanceMonitorAnalyzer.exe`
- Release 通常ビルド: `src\PerformanceMonitorAnalyzer\bin\Release\net10.0-windows\win-x64\PerformanceMonitorAnalyzer.exe`
- Release single-file 自動発行: `publish\win-x64\PerformanceMonitorAnalyzer.exe`
- single-file 発行対象:
  - `publish\win-x64\PerformanceMonitorAnalyzer.exe`
  - `publish\win-arm64\PerformanceMonitorAnalyzer.exe`

## ビルド後のモジュールのアーティファクトへの格納

- ビルド後が必要なリポジトリの場合、ビルド後のモジュールを、GitHub Actions のアーティファクトとして格納してください。
  - このリポジトリでは Release ビルドまたは `publish.bat` で生成される `publish\**\PerformanceMonitorAnalyzer.exe` を成果物として扱ってください。

## シングルバイナリでのビルド

- 使用する言語でシングルバイナリでビルドが可能な場合、シングルバイナリでビルドしてください。
- このリポジトリでは、シングルバイナリ生成は Release / publish の運用です。
  - `dotnet build -c Release` は既定で `publish\win-x64\` へシングルバイナリを生成します。
  - 手動で発行する場合は `win-x64` / `win-arm64` を対象にしてください。
  - Debug ビルドでシングルバイナリ成果物を生成する運用にはしないでください。

## シングルバイナリビルドを手動実行する場合

- Windows x64 向け:
  ```cmd
  dotnet publish src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj --configuration Release --runtime win-x64 --self-contained true --output publish\win-x64
  ```
- Windows ARM64 向け:
  ```cmd
  dotnet publish src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj --configuration Release --runtime win-arm64 --self-contained true --output publish\win-arm64
  ```

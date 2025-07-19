# ビルドとデプロイメントガイド

## 概要

Performance Monitor Analyzerは、.NET 8.0を使用したWPFデスクトップアプリケーションです。シングルバイナリ配布に対応しており、.NET Frameworkがインストールされていない環境でも動作します。

## 開発環境でのビルド

### 前提条件

- .NET 8.0 SDK以降
- Windows 10/11（WPF機能のため）
- Visual Studio 2022またはVisual Studio Code

### 開発時のビルドと実行

```bash
cd src/PerformanceMonitorAnalyzer
dotnet restore
dotnet build
dotnet run
```

### デバッグビルド

```bash
dotnet build --configuration Debug
```

### リリースビルド

```bash
dotnet build --configuration Release
```

## プロダクション向けシングルバイナリビルド

本アプリケーションは自己完結型のシングルバイナリとしてビルド可能です。生成される実行ファイルは.NET Frameworkが未インストールの環境でも単独で動作します。

### Windows x64向けビルド（推奨）

```bash
cd src/PerformanceMonitorAnalyzer
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output ../../publish/win-x64
```

### Windows x86向けビルド

```bash
cd src/PerformanceMonitorAnalyzer
dotnet publish --configuration Release --runtime win-x86 --self-contained true --output ../../publish/win-x86
```

### Windows ARM64向けビルド

```bash
cd src/PerformanceMonitorAnalyzer
dotnet publish --configuration Release --runtime win-arm64 --self-contained true --output ../../publish/win-arm64
```

### ビルド結果

各コマンド実行後、以下のファイル構成で実行可能ファイルが生成されます：

```
publish/
├── win-x64/
│   ├── PerformanceMonitorAnalyzer.exe   # メイン実行ファイル
│   └── config/                          # 設定ファイル
├── win-x86/
│   ├── PerformanceMonitorAnalyzer.exe
│   └── config/
└── win-arm64/
    ├── PerformanceMonitorAnalyzer.exe
    └── config/
```

## シングルバイナリビルドの設定

プロジェクトファイル（`PerformanceMonitorAnalyzer.csproj`）には以下の設定が含まれています：

```xml
<PropertyGroup>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <PublishTrimmed>true</PublishTrimmed>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
</PropertyGroup>
```

### 設定の説明

- **`PublishSingleFile=true`**: すべての依存関係を含む単一の実行ファイルを生成
- **`SelfContained=true`**: .NET ランタイムを含む自己完結型パッケージ
- **`PublishTrimmed=true`**: 未使用アセンブリの除去によるファイルサイズ削減
- **`IncludeNativeLibrariesForSelfExtract=true`**: ネイティブライブラリの自動展開

## デプロイメント

### スタンドアロン配布

生成された実行ファイル（`PerformanceMonitorAnalyzer.exe`）を以下の方法で配布可能：

1. **直接配布**: .exeファイルを単体でコピー
2. **アーカイブ配布**: configディレクトリと共にZIPファイル化
3. **インストーラー**: WiXツールセットでMSIパッケージ作成（今後の実装予定）

### 設定ファイル

アプリケーションには以下の設定ファイルが必要です：

```
config/
└── counter-patterns.yaml    # カウンターパターン定義
```

### システム要件

**最小要件**:
- Windows 10 以降
- メモリ: 4GB以上
- ストレージ: 100MB以上の空き容量

**推奨要件**:
- Windows 11
- メモリ: 8GB以上
- ストレージ: 500MB以上の空き容量

## トラブルシューティング

### ビルドエラー

#### Linux/macOS環境でのビルドエラー
**エラー**: `Microsoft.NET.Sdk.WindowsDesktop.targets was not found`

**解決策**: WPFアプリケーションはWindows専用のため、Windows環境での開発・ビルドが必要です。

#### 依存関係の解決エラー
**エラー**: パッケージの復元に失敗

**解決策**:
```bash
dotnet clean
dotnet restore --force
dotnet build
```

### パフォーマンスの最適化

#### ファイルサイズの削減
`PublishTrimmed=true`により未使用アセンブリが自動的に除去されますが、さらなる最適化が必要な場合：

```bash
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output publish --p:PublishTrimmed=true --p:TrimMode=partial
```

#### 起動時間の改善
Ready2Run (R2R) コンパイルを有効にする場合：

```xml
<PropertyGroup>
  <PublishReadyToRun>true</PublishReadyToRun>
</PropertyGroup>
```

## 参考資料

- [.NET Single-file deployment](https://docs.microsoft.com/en-us/dotnet/core/deploying/single-file)
- [Self-contained deployment](https://docs.microsoft.com/en-us/dotnet/core/deploying/#publish-self-contained)
- [Assembly trimming](https://docs.microsoft.com/en-us/dotnet/core/deploying/trimming/)
- [Ready2Run compilation](https://docs.microsoft.com/en-us/dotnet/core/deploying/ready-to-run)
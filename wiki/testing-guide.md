# テストガイド

## 概要

このリポジトリには Windows 向けの xUnit テストプロジェクトとして `tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj` を追加しています。

## テストプロジェクト

- プロジェクト: `tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`
- ターゲットフレームワーク: `net10.0-windows`
- 参照先: `src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`

## 使用パッケージ

- `Microsoft.NET.Test.Sdk` `17.14.1`
- `xunit` `2.9.3`
- `xunit.runner.visualstudio` `3.1.4`
- `coverlet.collector` `6.0.4`

すべて固定バージョンで指定しており、浮動バージョンは使用していません。

## テスト実行方法

リポジトリのルートディレクトリで次のコマンドを実行します。

```powershell
dotnet test tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj -v minimal
```

## 現在のスモークテスト

初期セットアップ確認用として、アプリ本体の `PerformanceDataPoint` 型を参照できることを確認する最小限のスモークテストを配置しています。

今後はこのプロジェクトに機能単位のユニットテストを追加してください。

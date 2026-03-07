# 自動テストプロジェクト作成

## 概要

Windows 環境で `dotnet test tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj` を実行できるように、xUnit ベースの自動テストプロジェクトを追加しました。

## 実施内容

1. 既存のアプリ本体 (`src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`) が `dotnet build` できることを確認しました。
2. `dotnet new xunit` で `tests\PerformanceMonitorAnalyzer.Tests` を作成し、以下の設定に調整しました。
   - ターゲットフレームワーク: `net10.0-windows`
   - `UseWPF: true`
   - 本体プロジェクトへの `ProjectReference`
3. 最小構成のスモークテストを追加し、本体プロジェクトの型 (`PerformanceDataPoint`) を参照できることを検証しました。
4. `dotnet test tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj -v minimal` を実行し、1 件のテスト成功を確認しました。

## 使用パッケージ

- `Microsoft.NET.Test.Sdk` `17.14.1`
- `xunit` `2.9.3`
- `xunit.runner.visualstudio` `3.1.4`
- `coverlet.collector` `6.0.4`

## 補足

- 本体コードは変更せず、テストプロジェクト追加のみに留めています。
- 今後はこのプロジェクト配下に実際の単体テストを追加していけば、Windows 上で継続的に `dotnet test` を実行できます。

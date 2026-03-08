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

## CounterPattern の YAML 読み込みテスト

`tests\PerformanceMonitorAnalyzer.Tests\CounterPatternTests.cs` では、カウンターパターン設定の `scale` / `graphType` / `valueMode` 読み込みを次の観点で検証しています。

- YAML に明示した `scale` / `graphType` / `valueMode` が `CounterPattern` / `CounterDefinition` に反映されること
- YAML で `scale` / `graphType` / `valueMode` を省略した場合でも既定値 `1.0` / `lineChart` / `rawValue` が維持されること
- `0` 以下などの無効な `scale` が指定された場合は既定値 `1.0` に補正されること
- 設定ファイル未配置時に生成される既定設定で、明示的な `scale` サンプルと `graphType` / `valueMode` を含めた既定値が両立したまま再読み込みできること

検証時は出力先の競合を避けるため、必要に応じて `OutputPath` を一時ディレクトリへ切り替えて実行します。

```powershell
dotnet test --nologo -v q -p:OutputPath=<temp path> -p:SelfContained=false -p:UseAppHost=false tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj
```

`tests\PerformanceMonitorAnalyzer.Tests\CounterPathPatternMatcherTests.cs` では、`\オブジェクト(インスタンス)\*` 形式のパターンが該当オブジェクト配下の全カウンターへ一致することと、SQL Server のようにインスタンス名内にかっこを含むケースでも正しく扱えることを検証しています。

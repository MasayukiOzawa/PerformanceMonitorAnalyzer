# Performance Monitor Analyzer

Windows Performance Monitor の `.blg` ファイルを読み込み、必要なカウンターだけを選んで解析・可視化する C# / WPF デスクトップアプリケーションです。

BLG の解析は Windows 標準の PDH API を使用します。画面に表示される `relog.exe` 相当コマンドは、同じ条件を外部ツールで確認するための参考情報です。

## 主な機能

- BLG ファイル読み込み
  - メニューからのファイル選択
  - グラフ領域への `.blg` ドラッグアンドドロップ
  - コマンドライン引数での起動時読み込み
- BLG メタデータ表示
  - 読み込みファイルのフルパス、ファイルサイズ、コンピューター名、取得間隔、時間範囲
- 非同期読み込みと中止
  - BLG 初期読み込み、カウンターデータ読み込み中の進捗表示
  - `読み込みを中止` によるキャンセル
  - 取得済みカウンターの段階的なグラフ・データテーブル反映
- PerfMon 風カウンター選択
  - オブジェクト、カウンター、インスタンスを選ぶモーダルダイアログ
  - `<すべてのインスタンス>` による一括追加
  - 追加済みカウンターの削除、クリア、設定コピー
- YAML ベースのカウンターパターン
  - `config/counter-patterns.yaml` で定義
  - ワイルドカード、個別 `scale`、`graphType`、`valueMode` に対応
  - メニューまたは左ペインからワンクリック適用
- グラフ表示
  - ScottPlot.WPF による時系列グラフ
  - 折れ線グラフ / 積み重ね面グラフの切り替え
  - Raw / 差分値モードの切り替え
  - X 軸の時間範囲表示、Y 軸範囲の手動指定
  - カウンター別スケール設定と自動スケール
  - 独立凡例パネル、系列の表示切り替え、折れ線ハイライト
  - ホバー位置の値表示とクリックによる固定表示
  - グラフ画像のクリップボードコピー
- データテーブルと統計
  - カウンター一覧 + 詳細ビュー
  - 平均、最大、最小、標準偏差、取得期間の表示
  - 列ソート、選択行コピー、TSV コピー
  - カウンター別 CSV エクスポート
  - 詳細チャートのクリップボードコピー
- UI / 操作補助
  - 共通 ResourceDictionary によるモダンテーマ
  - カウンター、凡例、スケール設定、下部データテーブル / ログ領域の表示切り替え
  - 表示メニューからの一括折りたたみ / 展開
  - グラフサイズ、ウィンドウサイズの表示と手動設定
  - `error.log` とアプリ内ログ表示

## クイックスタート

### 必要な環境

- Windows 10 / 11
- .NET 10.0 SDK 以降
- Visual Studio 2022 または Visual Studio Code

WPF と BLG 解析の都合上、アプリケーションの実行は Windows 専用です。

### 実行

```cmd
dotnet restore src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.sln
dotnet run --project src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.csproj
```

BLG ファイルを起動時に指定する場合:

```cmd
dotnet run --project src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.csproj -- "C:\Logs\your-file.blg"
```

リポジトリルートからバッチファイルで起動することもできます。

```cmd
build-and-run.bat
build-and-run.bat "C:\Logs\your-file.blg"
```

## 基本的な使い方

1. `ファイル` -> `BLGファイルを開く`、またはグラフ領域へのドラッグアンドドロップで `.blg` ファイルを読み込みます。
2. 左ペインの `カウンターを追加...` から対象のオブジェクト、カウンター、インスタンスを追加します。
3. 既定パターンを使う場合は、左ペインまたは `パターン` メニューからカウンターパターンを適用します。
4. `選択されたカウンターを読み込み` を実行します。
5. グラフ、凡例、スケール設定、データテーブル、統計情報を使って時系列の傾向を確認します。
6. 必要に応じて、グラフ画像のコピー、CSV 出力、表データのコピーを行います。

## カウンターパターン

カウンターパターンは `config/counter-patterns.yaml` に定義します。パターンごとにカウンター、表示モード、値モード、スケールをまとめて指定できます。

```yaml
patterns:
  - name: 詳細システム監視
    description: 詳細なシステム分析のための監視項目
    graphType: lineChart
    valueMode: rawValue
    counters:
      - name: \System\Context Switches/sec
        enabled: true
        scale: 1.0
      - name: \Process(_Total)\Working Set
        enabled: true
        scale: 0.000001
```

同梱パターン:

- `基本システム監視`
- `クエリ実行時間`
- `詳細システム監視`
- `SQL Server Monitoring`
- `SQL Server Lock Info`

`name` には `*` や `?` を使えます。たとえば `\SQLServer:Batch Resp Statistics(Elapsed Time:Total(ms))\*` のように指定すると、該当オブジェクト配下のカウンターをまとめて選択できます。

## ビルドと配布

### 開発ビルド

```cmd
dotnet build src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.sln -c Debug
```

### シングルバイナリ生成

リポジトリルートで実行します。

```cmd
publish.bat
```

出力先:

- `publish\win-x64\PerformanceMonitorAnalyzer.exe`
- `publish\win-arm64\PerformanceMonitorAnalyzer.exe`

`config\counter-patterns.yaml` は publish 出力に同梱されます。

手動で publish する場合:

```cmd
cd src\PerformanceMonitorAnalyzer
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output ..\..\publish\win-x64
dotnet publish --configuration Release --runtime win-arm64 --self-contained true --output ..\..\publish\win-arm64
```

## テスト

```cmd
dotnet test src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.sln
```

テストは solution に含まれる xUnit プロジェクトをまとめて実行します。カウンターパターン、パス整形、統計計算、グラフ系列生成、クリップボード用文字列生成などのロジックを検証します。

## プロジェクト構成

```text
config/
  counter-patterns.yaml
src/
  PerformanceMonitorAnalyzer/
    App.xaml
    MainWindow.xaml
    MainWindow.xaml.cs
    BlgFileAnalyzer.cs
    PdhApi.cs
    CounterAddDialog.xaml
    CounterPattern.cs
    CounterTreeBuilder.cs
    CounterStatisticsCalculator.cs
    LineSeriesDataBuilder.cs
    StackedAreaSeriesBuilder.cs
    Styles/
tests/
  PerformanceMonitorAnalyzer.Tests/
wiki/
  user-guide.md
  development-setup.md
  testing-guide.md
  build-deployment-guide.md
  troubleshooting.md
```

## 技術スタック

- .NET 10.0 / `net10.0-windows`
- WPF
- PDH API
- ScottPlot.WPF 5.0.41
- YamlDotNet 15.1.1
- Newtonsoft.Json 13.0.3
- xUnit

## 詳細ドキュメント

- [使用方法ガイド](./wiki/user-guide.md)
- [カウンターパターン機能](./wiki/counter-patterns.md)
- [開発環境構築](./wiki/development-setup.md)
- [テストガイド](./wiki/testing-guide.md)
- [ビルドとデプロイメントガイド](./wiki/build-deployment-guide.md)
- [トラブルシューティング](./wiki/troubleshooting.md)

## 制限事項

- WPF アプリケーションのため、Linux / macOS では実行できません。
- BLG 解析は Windows の PDH API に依存します。
- 非常に大きな BLG ファイルでは、読み込むカウンター数とメモリ使用量に注意してください。
- リアルタイム監視ではなく、保存済み BLG ファイルの解析を対象にしています。

## ライセンス

このプロジェクトは [MIT License](./LICENSE) の下で公開されています。

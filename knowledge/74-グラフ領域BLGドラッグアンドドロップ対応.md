# グラフ領域 BLG ドラッグアンドドロップ対応

## 概要

`copilot/graph-blg-dragdrop` ブランチで、グラフ表示領域に `.blg` ファイルをドラッグアンドドロップして読み込めるようにした。  
あわせて、未使用だったグラフ / ヘルプメニューと、データ未表示時の補助メッセージのうち不要になった案内文を整理した。

## 実装内容

- `MainWindow.xaml`
  - `GraphContainer` に `AllowDrop` と DragEnter / DragOver / DragLeave / Drop を追加。
  - ドラッグ中に受け入れ可否が分かる `GraphDropOverlay` を追加。
  - 未使用の `グラフ` / `ヘルプ` メニューを削除。
  - `NoDataMessagePanel` 内の「グラフ機能が実装されました！」表示を削除。
- `MainWindow.xaml.cs`
  - 単一 `.blg` ファイルのみ受け付けるドロップ検証処理を追加。
  - ドロップ時は既存の `LoadBlgFileAsync()` を共通エラーハンドリング経由で呼び出すよう統一。
  - `Ctrl+C` によるグラフコピー可否判定を、削除したメニュー状態ではなく `HasVisibleChartData()` ベースへ変更。
- `Assets\app-icon.ico` / `PerformanceMonitorAnalyzer.csproj` / `MainWindow.xaml`
  - 独自のアプリケーションアイコンを追加し、実行ファイルとメインウィンドウの両方で同じアイコンを使うよう設定。
- `README.md` / `wiki\user-guide.md`
  - グラフ領域へのドラッグアンドドロップ読込手順を追記。
- `PerformanceMonitorAnalyzer.csproj`
  - Release publish では `DebugType=embedded` を使い、sidecar PDB を出さないように調整。
- `wiki\build-deployment-guide.md`
  - publish の成果物に `config\counter-patterns.yaml` が同梱される前提で構成例を更新。

### 追加実装: 統計情報グリッドの列ソート対応

- `CounterStatisticsItem.cs`
  - 表示用文字列とは別に、`AverageValue` / `MaximumValue` / `MinimumValue` の数値プロパティを追加。
- `StatisticsGridSorter.cs`
  - 統計情報グリッド専用の並び替え helper を追加し、`カウンター名` / `平均` / `最大` / `最小` を昇順 / 降順で並べ替えられるようにした。
- `MainWindow.xaml`
  - `StatisticsDataGrid` の列ソートを有効化し、`カウンター名` / `平均` / `最大` / `最小` 列へ `HeaderTemplate` を設定。
  - 現在のソート方向を `▲` / `▼` で表示するアイコンをヘッダー右側へ追加。
- `MainWindow.xaml.cs`
  - ヘッダークリック時のソート方向トグル、現在のソート列 / 方向の保持、統計再描画時のソート再適用を追加。
- `tests\PerformanceMonitorAnalyzer.Tests\StatisticsGridSorterTests.cs`
  - 平均昇順、最大降順、同値時のカウンター名タイブレーク、カウンター名降順を自動テストで検証。

## 仕様メモ

- ドロップ対象は 1 ファイルのみ。
- `.blg` 以外、複数ファイル、処理中ドロップは受け付けず、警告メッセージを表示する。
- 実際の BLG 読み込み、UI リセット、時間範囲検出は既存の読み込み導線を再利用する。
- `counter-patterns.yaml` は既存の `config\counter-patterns.yaml` を利用し、追加のソースコピーは作成しない。
- publish 時は `PerformanceMonitorAnalyzer.exe` に加えて `config\counter-patterns.yaml` を同梱する。

## 検証

- `dotnet build --nologo -v q -p:OutputPath=%TEMP%\copilot-graphdragdrop-build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q -p:OutputPath=%TEMP%\copilot-graphdragdrop-test -p:SelfContained=false -p:UseAppHost=false tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`
- `dotnet publish --nologo -v q -c Release -r win-x64 --self-contained true -o %TEMP%\copilot-singlefile-check src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet build --nologo -v q -r win-x64 -p:OutputPath=%TEMP%\pma-post-build src\PerformanceMonitorAnalyzer\PerformanceMonitorAnalyzer.csproj`
- `dotnet test --nologo -v q -p:OutputPath=%TEMP%\pma-post-test -p:SelfContained=false -p:UseAppHost=false tests\PerformanceMonitorAnalyzer.Tests\PerformanceMonitorAnalyzer.Tests.csproj`

# Performance Monitor Analyzer

Windows Performance Monitor の `.blg` ファイルを読み込み、必要なカウンターだけを選んで解析・可視化する C# / WPF デスクトップアプリケーションです。

BLG の解析は Windows 標準の PDH API を使用します。画面に表示される `relog.exe` 相当コマンドは、同じ条件を外部ツールで確認するための参考情報です。

<<<<<<< HEAD
- ✅ BLGファイルの読み込み（Windows環境のみ、メニュー / グラフ領域へのドラッグアンドドロップ対応）
- ✅ パフォーマンスカウンターの一覧表示（階層構造表示）
- ✅ 複数カウンターの選択機能
- ✅ **🎯 YAMLベースのカウンターパターン機能**（事前定義された選択パターン）
- ✅ カウンターごとの一覧 + 詳細ビュー形式データテーブル表示
- ✅ **詳細統計情報の表示**（平均、最大、最小、標準偏差）
- ✅ **CSVファイルへのデータエクスポート**（個別カウンター）
- ✅ **PDH API を使用した BLG 解析**
- ✅ **relog.exe 相当コマンドの表示**（再現・外部確認用）
- ✅ エラーログ機能
- ✅ **データのグラフ化機能**（ScottPlot.WPF使用）
- ✅ **グラフスケール調整機能**（自動スケール / 表示位置調整、実データ値は保持）
- ✅ **値モード切り替え機能**（Raw / 差分）
- ✅ **2軸グラフ機能**（凡例から系列を右軸のRaw折れ線へ切り替え）
- ✅ **折れ線凡例ハイライト機能**（凡例から複数系列を太線強調）
- ✅ **モダンUIテーマ**（共通 ResourceDictionary / カードベースレイアウト）
- ✅ **カウンター選択エリアのUI制御**（トグルによる表示・非表示）
- ✅ **凡例 / スケール設定パネルのUI制御**（トグルによる表示・非表示）
- ✅ **下部データテーブル / ログ領域のUI制御**（トグル + 高さ調整）
- ✅ **表示メニューからの一括折りたたみ / 展開**
=======
## 主な機能
>>>>>>> origin/main

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

<<<<<<< HEAD
- 左ペインの「カウンターを追加...」からモーダルダイアログを開き、標準のパフォーマンスモニターに近い形式でオブジェクト、カウンター、インスタンスを選択
- ダイアログ内でオブジェクトを選択し、カウンターとインスタンスをチェックして「追加」を押すことで、左ペインの追加済みカウンター一覧に登録
- 「追加」後もダイアログは開いたままになるため、別のオブジェクトやインスタンスを続けて追加可能
- 複数のカウンターやインスタンスを持つオブジェクトでは、ダイアログ内の `<すべてのカウンター>` と `<すべてのインスタンス>` で各チェックを一括操作して追加可能
- 左ペインの追加済みカウンターは「削除(<<)」または「クリア」で読み込み対象から除外可能
- 「選択されたカウンターを読み込み」実行後は、読込済みの選択カウンターに対応するデータテーブル一覧と詳細が表示されます
- 「すべて解除」ボタンで全カウンターを一括解除可能
- カウンター選択エリアは境界線中央のトグルアイコン（◀/▶）で表示/非表示を切り替え可能
- カウンター選択エリアとグラフ領域の境界はドラッグ調整せず、トグル操作のみを提供
- メニューバーの **表示** → **すべて折りたたむ / すべて展開する**、または **グラフ表示上段のボタン** から、各トグル対応エリアを一括制御可能
=======
### 開発ビルド
>>>>>>> origin/main

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

<<<<<<< HEAD
### 🎨 グラフ機能の詳細

#### スケール調整機能
- **グラフ表示位置の調整**: 右側の「スケール設定」パネルで各カウンターの表示位置を調整可能
- **パネル開閉**: 境界中央のトグルアイコンでスケール設定パネルの表示/非表示を切り替え可能
- **自動スケール**: `自動スケール` ボタンで、表示中の各カウンターの最大絶対値が約 100 になるようカウンター別倍率を自動設定。倍率は `1` / `2` / `5` × 10 の累乗に丸められます
- **実際のデータ値は保持**: スケール変更はグラフのプロット位置のみに影響し、実際のデータ値や統計情報は変更されません
- **柔軟な比較**: 異なる単位や範囲のカウンターを同じグラフ上で視覚的に比較可能
- **読み込み時の既定化**: 「選択されたカウンターを読み込み」実行後は一括スケールのUI選択のみ `1.0` に戻り、各カウンターの個別スケール値は保持されます

#### グラフ表示オプション
- **折れ線グラフ**: 各カウンターを個別の線として表示
- **積み重ね面グラフ**: カウンター値を積み重ねて表示し、全体の傾向を把握
- **値モード切り替え**: グラフタイプの右側で Raw と差分（current - previous）を切り替え可能
- **2軸表示**: 凡例の `2軸` を有効にした系列を、右軸のRaw折れ線として表示。積み重ね面グラフにも重ねて比較可能
- **Y軸表示**: 左右の目盛りと軸線を表示し、軸ラベルは表示しません
- **凡例機能**: 各カウンターの色、名前、現在値を表示（個別表示/非表示切り替え可能）
- **凡例パネル開閉**: 境界中央のトグルアイコンで凡例パネルの表示/非表示を切り替え可能

#### 表示される値の種類
- **グラフプロット**: 選択中の値モード（Raw / 差分）で表示
- **データテーブル**: 選択中の値モードに連動して表示
- **統計情報**: 選択中の値モードに連動して計算（平均、最大、最小、標準偏差）
- **凡例の現在値**: 選択中の値モードに連動して表示
- **2軸系列**: 全体が差分モードの場合でも、グラフ、ホバー、データテーブル、統計情報、凡例現在値をRawで表示

### 推奨ワークフロー
1. **relog.exe方式でBLGファイルを読み込み**（安定性重視）
2. **カウンターパターンの適用**（効率的な分析開始）
3. **必要に応じて個別カウンターを追加選択**
4. **グラフとデータテーブルで総合分析**
5. **必要に応じてCSVエクスポート**

### 🎯 カウンターパターンの活用方法
- **初回分析**: 「基本システム監視」パターンで全体把握
- **問題特定**: 「詳細システム監視」パターンでプロセスやページングを詳細調査
- **特定サービス**: 「SQLサーバー監視」で専門分析
- **カスタムパターン**: `config/counter-patterns.yaml` を編集して独自パターン作成

## サポートするカウンター例

- `\Processor(_Total)\% Processor Time` - CPU使用率
- `\Memory\Available MBytes` - 利用可能メモリ
- `\PhysicalDisk(_Total)\Disk Reads/sec` - ディスク読み取り速度
- `\PhysicalDisk(_Total)\Disk Writes/sec` - ディスク書き込み速度
- `\Network Interface(*)\Bytes Total/sec` - ネットワーク総バイト数
- `\System\Context Switches/sec` - コンテキストスイッチ/秒
- `\Process(_Total)\Working Set` - プロセス作業セット

## エラーログ

アプリケーションのエラーは `error.log` ファイルに出力されます。

## 開発環境

- Windows 10/11
- .NET 10.0 SDK 以降
- Visual Studio 2022（.NET desktop development ワークロード）または Visual Studio Code
- 詳細は [wiki/development-setup.md](./wiki/development-setup.md) を参照してください。

## 技術仕様

### 使用技術
- **フロントエンド**: WPF (Windows Presentation Foundation)
- **グラフライブラリ**: ScottPlot.WPF
- **設定管理**: YamlDotNet (YAML設定ファイル)
- **データ形式**: JSON (Newtonsoft.Json), CSV, YAML
- **BLG解析**: 
  - relog.exe (Windows標準ツール)
  - PDH API (Performance Data Helper)
- **データ処理**: 非同期処理、動的読み込み

### アーキテクチャ
- MVVMパターン（WPF版）
- 非同期処理対応
- PDH API ベースの BLG 解析
- YAML設定ベースのパターン管理システム
- リソース管理（一時ファイル自動クリーンアップ）

## 今後の実装予定

- [x] relog.exe を使用したBLG解析
- [x] CSVエクスポート機能
- [x] **データのグラフ化機能**（ScottPlot.WPF使用）
- [x] **YAMLベースのカウンターパターン機能**
- [ ] カウンターフィルタリング機能
- [ ] グラフの時間範囲指定
- [ ] リアルタイム監視機能
- [ ] カスタムカウンター追加
=======
- WPF アプリケーションのため、Linux / macOS では実行できません。
- BLG 解析は Windows の PDH API に依存します。
- 非常に大きな BLG ファイルでは、読み込むカウンター数とメモリ使用量に注意してください。
- リアルタイム監視ではなく、保存済み BLG ファイルの解析を対象にしています。
>>>>>>> origin/main

## ライセンス

このプロジェクトは [MIT License](./LICENSE) の下で公開されています。

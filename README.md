# Performance Monitor Analyzer

Windows Performance Monitor の .blg ファイルを読み込み、解析・可視化するC# WPFデスクトップアプリケーションです。

## 機能

- ✅ BLGファイルの読み込み（Windows環境のみ）
- ✅ パフォーマンスカウンターの一覧表示（階層構造表示）
- ✅ 複数カウンターの選択機能
- ✅ **🎯 YAMLベースのカウンターパターン機能**（事前定義された選択パターン）
- ✅ カウンターごとのタブ形式データテーブル表示
- ✅ **詳細統計情報の表示**（平均、最大、最小、標準偏差）
- ✅ **単位認識とインテリジェントフォーマット**
- ✅ **CSVファイルへのデータエクスポート**（個別・一括）
- ✅ **サンプルBLGファイルの読み込み機能**
- ✅ **relog.exe を使用したBLG解析**（PDH APIの代替手段）
- ✅ エラーログ機能
- ✅ **データのグラフ化機能**（LiveCharts.Wpf使用）

## プロジェクト構成

```
src/
└── PerformanceMonitorAnalyzer/
    ├── App.xaml                   # WPFアプリケーション設定
    ├── App.xaml.cs
    ├── MainWindow.xaml            # メインウィンドウUI
    ├── MainWindow.xaml.cs         # メインウィンドウロジック
    ├── BlgFileAnalyzer.cs         # PDH APIを使用したBLG解析
    ├── PdhApi.cs                  # PDH API P/Invoke宣言
    ├── RelogCsvAnalyzer.cs        # relog.exe を使用したCSV変換・解析
    ├── CounterPattern.cs          # YAMLパターン機能（NEW）
    └── PerformanceMonitorAnalyzer.csproj
sample/
├── DataCollector01.blg            # サンプルBLGファイル
└── counter-patterns.yaml          # カウンターパターン設定ファイル（NEW）
```

## 必要な環境

- .NET 8.0以降
- Windows 10/11（WPF必須）
- PowerShell（BLGファイル解析用）

## 使用方法

### Windows環境での実行

```bash
cd src/PerformanceMonitorAnalyzer
dotnet build
dotnet run
```

### ファイル読み込み

1. **ファイル > BLGファイルを開く** - PDH APIを使用した従来の方法
2. **ファイル > BLGファイルを開く（relog.exe使用）** - より安定したrelog.exe を使用した方法（**時間範囲選択対応**）
3. **ファイル > サンプルBLGファイルを読み込み（PDH API）** - 付属のサンプルファイルをPDH APIで読み込み
4. **ファイル > サンプルBLGファイルを読み込み（relog.exe）** - 付属のサンプルファイルをrelog.exe で読み込み（**時間範囲選択対応**）

### 🔥 新機能: 時間範囲選択とrelog実行制御

relog.exe方式でBLGファイルを開くと、以下の高度な機能が利用できます：

#### 📅 時間範囲選択機能
- **📊 自動時間範囲検出**: BLGファイルの全期間を自動で取得・表示
- **🎚️ 直感的スライダー操作**: デュアルスライダーで開始・終了時刻を簡単選択
- **⏱️ リアルタイム表示**: 選択中の時間範囲と期間を即座に表示
- **🔄 即座に再読み込み**: 「この時間範囲で再読み込み」ボタンで選択範囲のデータのみ解析
- **💡 パフォーマンス向上**: 大容量BLGファイルも必要な時間範囲のみ処理で高速化

#### 🚀 relog実行制御機能
- **🔄 実行状況表示**: 現在処理中のカウンター名と進捗詳細をリアルタイム表示
- **☑️ チェックボックス選択**: カウンターをチェックするだけで自動データ読み込み開始
- **🎯 手動実行ボタン**: 「選択されたカウンターでrelog実行」で選択済みカウンターを一括処理
- **📊 タブ形式表示**: CSVデータを基にカウンター名別タブでデータテーブル表示
- **✅ バッチ処理**: 複数カウンターの効率的な並列処理とエラーハンドリング

### 🎯 NEW: カウンターパターン機能

効率的な分析のため、事前定義されたカウンターパターンが利用できます：

#### 📝 YAML設定ファイル
- **設定ファイル**: `config/counter-patterns.yaml` 
- **カスタマイズ可能**: パターン名、説明、カウンターリスト、スケール値を自由に設定
- **メニュー統合**: 「パターン」メニューから設定ファイルの開き・再読み込み

#### 🚀 使用方法
1. **パターン選択**: 左側パネルのコンボボックスから目的のパターンを選択
2. **ワンクリック適用**: 「パターンを適用」ボタンで該当カウンターを自動選択
3. **メニューからも選択可能**: 「パターン」→「カウンターパターンを適用」

#### 📊 事前定義パターン
- **基本システム監視**: CPU、メモリ、ディスクの基本項目
- **ネットワーク監視**: ネットワークトラフィック関連
- **詳細システム監視**: 詳細なシステム分析項目
- **SQLサーバー監視**: SQL Server特有のパフォーマンス項目
- **Webサーバー監視**: IIS関連の監視項目
- **高負荷診断**: システム高負荷時の詳細診断

#### 🎛️ 高度な機能
- **ワイルドカード対応**: `*` や `?` を使用したパターンマッチング
- **スケール設定**: カウンターごとのスケール値指定
- **部分マッチ**: 完全一致しない場合の柔軟な検索
- **リアルタイムフィードバック**: 適用結果と未検出カウンターの表示

### BLG解析方法の比較

#### PDH API方式
- **利点**: Windows標準API、直接的なデータアクセス
- **欠点**: 複雑な実装、環境依存性が高い、エラーが発生しやすい

#### relog.exe方式（推奨）
- **利点**: 
  - Windowsに標準で付属するツール
  - 安定性が高い
  - すべてのカウンターデータを確実に読み取り可能
  - 実装がシンプル
- **欠点**: 一時CSVファイルの作成が必要

### カウンター表示と操作

- オブジェクト > インスタンス > カウンター の3階層で表示
- チェックボックスで選択したカウンターがデータテーブルに表示
- 「すべて選択」「すべて解除」ボタンで一括操作可能

### データテーブル機能

- **詳細な統計情報**: 各カウンターの平均、最大、最小、標準偏差を自動計算
- **インテリジェントフォーマット**: カウンターの種類に応じた単位の自動認識（%、MB、/sec等）
- **CSVエクスポート**: 
  - 個別カウンターのデータをCSV出力
  - 全カウンターのデータを一括CSV出力
- **データ表示**: タイムスタンプ、生値、フォーマット済み値、単位を表示
- **タブ管理**: 複数カウンターを同時に表示、一括クリア機能

## BLGファイル解析機能

### relog.exe 方式（推奨）
- relog.exe を使用してBLGファイルをCSVに変換
- 高い安定性と互換性
- 全カウンターデータの確実な読み取り
- カウンター選択時の動的データ読み込み
- **📅 時間範囲選択機能**: 
  - BLGファイルの全時間範囲を自動検出
  - スライダーバーによる直感的な時間範囲選択
  - 選択された時間範囲のみでデータ解析
  - relog.exe の `-b` (begin) と `-e` (end) パラメータを使用

### PDH API方式
- PowerShellの`Import-Counter`コマンドを使用した実際のBLG解析
- COM経由でのPDH API使用（実装予定）

### Linux/macOS環境
- WPFアプリケーションのため実行不可
- エラーメッセージを表示してビルドを停止

## ログ機能

エラーログは実行ファイルと同じディレクトリの`error.log`に出力されます。
- Windows環境必須
- Visual Studio 2022 または Visual Studio Code

## ビルドと実行

### 開発時の実行（Windows環境推奨）

```bash
cd src/PerformanceMonitorAnalyzer
dotnet build
dotnet run
```

### シングルバイナリとしてビルド

本プロジェクトは自己完結型のシングルバイナリとしてビルド可能です：

#### Windows x64向け
```bash
cd src/PerformanceMonitorAnalyzer
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output ../../publish/win-x64
```

#### Windows x86向け
```bash
cd src/PerformanceMonitorAnalyzer
dotnet publish --configuration Release --runtime win-x86 --self-contained true --output ../../publish/win-x86
```

#### Windows ARM64向け
```bash
cd src/PerformanceMonitorAnalyzer
dotnet publish --configuration Release --runtime win-arm64 --self-contained true --output ../../publish/win-arm64
```

ビルド後の実行ファイル（`PerformanceMonitorAnalyzer.exe`）は.NET Frameworkが未インストールの環境でも単独で動作します。

### 引数でBLGファイルを指定（Windows環境のみ）

```bash
dotnet run "C:\path\to\your\file.blg"
```

### GitHub Actionsでのビルド

プッシュ時に自動的にシングルバイナリがビルドされ、GitHubアーティファクトとして保存されます：
- `PerformanceMonitorAnalyzer-win-x64`
- `PerformanceMonitorAnalyzer-win-x86` 
- `PerformanceMonitorAnalyzer-win-arm64`

## 使用方法

### WPF GUI アプリケーション
1. アプリケーションを実行
2. メニューから以下のいずれかを選択：
   - 「ファイル」→「BLGファイルを開く」（PDH API使用）
   - 「ファイル」→「BLGファイルを開く（relog.exe使用）」（推奨）
   - 「ファイル」→「サンプルBLGファイルを読み込み」
3. **カウンター選択**（以下のいずれかの方法）：
   - **🎯 パターン適用**: 左側パネルでパターンを選択し「パターンを適用」ボタンをクリック
   - **手動選択**: 左側のカウンター一覧から表示したいカウンターにチェック
   - **メニューから**: 「パターン」→「カウンターパターンを適用」→目的のパターンを選択
4. 下部のタブでカウンターごとの詳細データを確認
   - 統計情報（平均、最大、最小、標準偏差）
   - 全データポイントの時系列表示
   - CSVエクスポート機能
5. **グラフ表示**: 左側でチェックしたカウンターが右側のグラフエリアに時系列グラフとして表示
6. グラフ機能により、複数のパフォーマンスカウンターの推移を視覚的に分析可能

### 推奨ワークフロー
1. **relog.exe方式でBLGファイルを読み込み**（安定性重視）
2. **カウンターパターンの適用**（効率的な分析開始）
3. **必要に応じて個別カウンターを追加選択**
4. **グラフとデータテーブルで総合分析**
5. **必要に応じてCSVエクスポート**

### 🎯 カウンターパターンの活用方法
- **初回分析**: 「基本システム監視」パターンで全体把握
- **問題特定**: 「高負荷診断」パターンで詳細調査
- **特定サービス**: 「SQLサーバー監視」「Webサーバー監視」で専門分析
- **カスタムパターン**: `counter-patterns.yaml` を編集して独自パターン作成

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

### Dev Container
プロジェクトにはDev Container設定が含まれています。Visual Studio Codeで開くことで、自動的に適切な開発環境が構築されます。

### VS Code設定
- デバッグ設定（`.vscode/launch.json`）
- ビルドタスク（`.vscode/tasks.json`）

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
- プラガブル解析エンジン（PDH API / relog.exe）
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

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。

## 貢献

プルリクエストやイシューの報告をお待ちしています。開発に参加される場合は、Dev Containerを使用することを推奨します。
# Performance Monitor Analyzer (MAUI版)

Windows Performance Monitor の .blg ファイルを読み込み、解析・可視化するクロスプラットフォーム .NET MAUIアプリケーションです。

## 📱 新機能: MAUI (Multi-platform App UI) 対応

本プロジェクトはWPFからMAUIに移行し、以下のプラットフォームに対応しました：

- ✅ **Windows** (Windows 10/11)
- ✅ **macOS** (macOS 10.15以降)
- ✅ **iOS** (iOS 11.0以降) 
- ✅ **Android** (API Level 21以降)

### 🎯 移行の利点

1. **クロスプラットフォーム対応**: Windows以外のプラットフォームでもPerformance Monitorデータを分析可能
2. **モダンUI**: 最新のMAUIコントロールによる統一されたユーザーエクスペリエンス
3. **タッチ対応**: モバイルデバイスでのタッチ操作に最適化
4. **パフォーマンス向上**: ネイティブコントロールによる高速な描画とレスポンス

## 機能

- ✅ BLGファイルの読み込み（Windows環境推奨、他プラットフォームでも基本機能利用可能）
- ✅ パフォーマンスカウンターの一覧表示（階層構造表示をCollectionViewで実装）
- ✅ 複数カウンターの選択機能
- ✅ **🎯 YAMLベースのカウンターパターン機能**（事前定義された選択パターン）
- ✅ カウンターごとのデータ表示（タブ形式からスタック形式に変更）
- ✅ **詳細統計情報の表示**（平均、最大、最小、標準偏差）
- ✅ **単位認識とインテリジェントフォーマット**
- ✅ **CSVファイルへのデータエクスポート**（ファイルピッカー使用）
- ✅ **サンプルBLGファイルの読み込み機能**
- ✅ **relog.exe を使用したBLG解析**（Windows環境）
- ✅ エラーログ機能
- ✅ **LiveCharts MAUI グラフ機能**（ScottPlot.WPFから移行）

## プロジェクト構成

```
src/
├── PerformanceMonitorAnalyzer/          # 元のWPFバージョン
│   ├── App.xaml                         # WPFアプリケーション設定
│   ├── MainWindow.xaml                  # WPFメインウィンドウ
│   └── ...
└── PerformanceMonitorAnalyzer.Maui/     # 新しいMAUIバージョン
    ├── App.xaml                         # MAUIアプリケーション設定
    ├── AppShell.xaml                    # MAUIシェル設定
    ├── MainPage.xaml                    # MAUIメインページ
    ├── MainPage.xaml.cs                 # MAUIメインページロジック
    ├── MauiProgram.cs                   # MAUIプログラムエントリポイント
    ├── BlgFileAnalyzer.cs               # 共通BLG解析ロジック
    ├── CounterPattern.cs                # 共通パターン管理
    ├── PdhApi.cs                        # Windows PDH API
    ├── Platforms/                       # プラットフォーム固有実装
    │   ├── Android/
    │   ├── iOS/
    │   ├── MacCatalyst/
    │   └── Windows/
    └── Resources/                       # MAUIリソース
        ├── AppIcon/
        ├── Splash/
        ├── Images/
        ├── Fonts/
        └── Styles/
```

## 必要な環境

### 開発環境
- .NET 8.0以降
- Visual Studio 2022 17.8以降 または Visual Studio Code
- .NET MAUI Workload

### 実行環境
- **Windows**: Windows 10 version 1809以降
- **macOS**: macOS 10.15以降  
- **iOS**: iOS 11.0以降
- **Android**: API Level 21 (Android 5.0)以降

## インストール方法

### .NET MAUI Workloadのインストール

```bash
dotnet workload install maui
```

### プロジェクトのビルド

```bash
cd src/PerformanceMonitorAnalyzer.Maui
dotnet build
```

### プラットフォーム別実行

#### Windows
```bash
dotnet build -f net8.0-windows10.0.19041.0
dotnet run --framework net8.0-windows10.0.19041.0
```

#### Android
```bash
dotnet build -f net8.0-android
# エミュレーターまたはデバイスでデプロイ
```

#### iOS
```bash
dotnet build -f net8.0-ios
# Xcodeまたはデバイスでデプロイ
```

#### macOS
```bash
dotnet build -f net8.0-maccatalyst
dotnet run --framework net8.0-maccatalyst
```

## 🆕 MAUI版での主な変更点

### UI要素の変換

| WPF | MAUI | 変更理由 |
|-----|------|----------|
| `Window` | `ContentPage` | MAUIページモデル |
| `Menu` | ツールバーボタン | クロスプラットフォーム対応 |
| `TreeView` | `CollectionView` | 階層データ表示の最適化 |
| `DataGrid` | カスタム`StackLayout` | プラットフォーム統一 |
| `GroupBox` | `Frame` | 視覚的グループ化 |
| `Expander` | `CommunityToolkit.Maui.Expander` | 拡張性向上 |
| `TabControl` | スタック形式 | モバイル最適化 |

### グラフ機能

- **ScottPlot.WPF** → **LiveChartsCore.SkiaSharpView.Maui**
- クロスプラットフォーム対応の高性能チャート
- タッチジェスチャー対応（ズーム、パン）
- カラフルな線グラフで複数カウンター同時表示

### ファイル操作

- **Microsoft.Win32.OpenFileDialog** → **Microsoft.Maui.Storage.FilePicker**
- プラットフォーム固有のファイルピッカー使用
- ネイティブファイルシステム統合

### メッセージ表示

- **MessageBox.Show** → **DisplayAlert**
- 非同期ダイアログ表示
- プラットフォーム固有のUIスタイル

### ナビゲーション

- **WPF Window** → **MAUI Shell**
- モダンナビゲーション体験
- タブベースレイアウト対応

## 使用技術

### フロントエンド
- **.NET MAUI**: クロスプラットフォームUIフレームワーク
- **LiveChartsCore**: 高性能グラフライブラリ
- **CommunityToolkit.Maui**: MAUI追加コントロール
- **SkiaSharp**: 2Dグラフィックスライブラリ

### バックエンド（共通）
- **YamlDotNet**: YAML設定ファイル
- **Newtonsoft.Json**: JSONデータ処理
- **System.Management**: Windowsパフォーマンス情報（Windows限定）

### BLG解析
- **relog.exe**: Windows標準BLG変換ツール
- **PDH API**: Performance Data Helper API（Windows限定）

## 制限事項

### プラットフォーム固有制限

- **Windows**: 全機能利用可能
- **macOS/iOS/Android**: 
  - BLG解析機能は制限される場合があります
  - Windows PDH APIは利用できません
  - relog.exeは利用できません
  - CSV読み込み機能やサンプルデータ表示は利用可能

### パフォーマンス

- モバイルデバイスでは大量のデータ処理に時間がかかる場合があります
- メモリ制限により、表示可能なデータポイント数に制限があります

## ビルドと配布

### プラットフォーム別パッケージング

#### Windows (MSIX)
```bash
dotnet publish -f net8.0-windows10.0.19041.0 -c Release
```

#### Android (APK)
```bash
dotnet publish -f net8.0-android -c Release
```

#### iOS (IPA)
```bash
dotnet publish -f net8.0-ios -c Release
```

#### macOS (PKG)
```bash
dotnet publish -f net8.0-maccatalyst -c Release
```

## マイグレーション済み機能

- [x] **基本UI構造**: WPF Window → MAUI ContentPage
- [x] **メニューシステム**: WPF Menu → ツールバーボタン
- [x] **カウンター表示**: TreeView → CollectionView（階層構造対応）
- [x] **データ表示**: DataGrid → カスタムレイアウト
- [x] **グラフ機能**: ScottPlot.WPF → LiveChartsCore.Maui
- [x] **ファイル操作**: WPF FileDialog → MAUI FilePicker
- [x] **プログレス表示**: WPF ProgressBar → MAUI ActivityIndicator
- [x] **パターン管理**: YAML設定システム（共通）
- [x] **統計計算**: データ解析ロジック（共通）

## 今後の実装予定

- [ ] **プラットフォーム固有BLG解析**: iOS/Android向けの代替実装
- [ ] **オフライン機能**: ローカルデータキャッシュとオフライン解析
- [ ] **クラウド連携**: OneDrive/iCloud等のクラウドストレージ対応
- [ ] **リアルタイム監視**: ライブデータ収集機能（Windows限定）
- [ ] **カスタムテーマ**: ダーク/ライトテーマ切り替え
- [ ] **多言語対応**: 国際化（i18n）サポート

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。

## 貢献

プルリクエストやイシューの報告をお待ちしています。MAUIマルチプラットフォーム開発に参加される場合は、適切な開発環境をご準備ください。

### 開発環境セットアップ

1. Visual Studio 2022 (17.8以降) のインストール
2. .NET MAUI workloadのインストール
3. 対象プラットフォームのSDKインストール（Android SDK、Xcodeなど）

---

**🎉 WPFからMAUIへのマイグレーション完了！**
クロスプラットフォーム対応により、より多くのユーザーがPerformance Monitor Analyzerを利用できるようになりました。
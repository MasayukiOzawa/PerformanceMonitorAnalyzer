# Dev Container での開発環境

## 概要

Performance Monitor Analyzer は Windows 専用の PDH (Performance Data Helper) API を使用するため、**Windows ベースの Dev Container** を使用します。

## 重要な制約

### Windows PDH API の要件
- このアプリケーションは `pdh.dll` を P/Invoke で呼び出します
- PDH API は **Windows 専用** であり、Linux/macOS では動作しません
- Dev Container は **Windows Server Core** ベースのイメージを使用

### システム要件
- Windows 11 または Windows Server 2022
- Docker Desktop for Windows (Windows コンテナーモード)
- Visual Studio Code with Dev Containers extension

## セットアップ手順

### 1. Docker の Windows コンテナーモード切り替え

```powershell
# Docker Desktop を Windows コンテナーモードに切り替え
# タスクトレイの Docker アイコンを右クリック → "Switch to Windows containers..."
```

### 2. Dev Container の起動

1. VS Code でリポジトリを開く
2. `Ctrl+Shift+P` → "Dev Containers: Reopen in Container"
3. 初回は Windows Server Core イメージのダウンロードに時間がかかります（約 5-10 分）

### 3. 動作確認

```powershell
# ソリューションのビルド
dotnet build PerformanceMonitorAnalyzer.sln

# テスト実行
dotnet test tests/PerformanceMonitorAnalyzer.Tests/

# アプリケーション実行
dotnet run --project src/PerformanceMonitorAnalyzer/
```

## 制約事項

### Linux/macOS での開発
- PDH API の制約により、Linux/macOS では**コンパイルのみ可能**です
- 実行時に `DllNotFoundException` が発生します
- **実行テストは Windows 環境でのみ可能**

### 代替開発環境
Dev Container が使用できない場合：

1. **Windows ネイティブ環境**
   - .NET 8.0 SDK をインストール
   - Visual Studio 2022 または VS Code を使用

2. **クロスプラットフォーム開発**
   - コード編集・静的解析のみ
   - 実行テストは Windows 環境で行う

## トラブルシューティング

### Docker が Windows コンテナーを認識しない
```powershell
# Docker の再起動
Restart-Service docker

# Docker Desktop の設定確認
docker info | findstr "OSType"
# 出力: OSType: windows
```

### Dev Container の起動に失敗する
1. Docker Desktop が Windows コンテナーモードになっているか確認
2. 十分なディスク容量があるか確認（最低 10GB）
3. Windows Defender の除外設定を確認

### PDH API エラー
```csharp
// PDH API の動作確認
var result = PdhApi.PdhOpenQuery(null, IntPtr.Zero, out var queryHandle);
if (result != PdhApi.ERROR_SUCCESS)
{
    Console.WriteLine($"PDH Error: {PdhApi.GetErrorMessage(result)}");
}
```

## 参考リンク

- [Dev Containers documentation](https://code.visualstudio.com/docs/devcontainers/containers)
- [Windows containers on Docker Desktop](https://docs.docker.com/desktop/windows/wsl/)
- [PDH API documentation](https://docs.microsoft.com/en-us/windows/win32/perfctrs/performance-data-helper-portal)
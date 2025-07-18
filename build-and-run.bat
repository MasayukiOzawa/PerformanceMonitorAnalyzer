@echo off
echo アプリケーションをビルドして実行中...
echo.

cd /d "%~dp0src\PerformanceMonitorAnalyzer"

echo プロジェクトをビルド中...
dotnet build

if %ERRORLEVEL% neq 0 (
    echo エラー: ビルドに失敗しました。
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo アプリケーションを実行中...
echo 引数: %*
echo.

if "%~1"=="" (
    echo 使用方法: build-and-run.bat [ファイルパス]
    echo 例: build-and-run.bat "D:\Git\PerformanceMonitorAnalyzer\sample\DataCollector01.blg"
    echo.
    echo 引数なしで実行します...
    dotnet run
) else (
    dotnet run "%*"
)

if %ERRORLEVEL% neq 0 (
    echo エラー: アプリケーションの実行に失敗しました。
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo アプリケーションが正常に終了しました。
pause
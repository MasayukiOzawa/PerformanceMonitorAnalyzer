@echo off
echo シングルバイナリを生成中...
echo.

cd /d "%~dp0src\PerformanceMonitorAnalyzer"

echo Windows x64向けシングルバイナリを生成中...
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output "..\..\publish\win-x64"

if %ERRORLEVEL% neq 0 (
    echo エラー: Windows x64向けビルドに失敗しました。
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Windows x86向けシングルバイナリを生成中...
dotnet publish --configuration Release --runtime win-x86 --self-contained true --output "..\..\publish\win-x86"

if %ERRORLEVEL% neq 0 (
    echo エラー: Windows x86向けビルドに失敗しました。
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Windows ARM64向けシングルバイナリを生成中...
dotnet publish --configuration Release --runtime win-arm64 --self-contained true --output "..\..\publish\win-arm64"

if %ERRORLEVEL% neq 0 (
    echo エラー: Windows ARM64向けビルドに失敗しました。
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo シングルバイナリの生成が完了しました。
echo 出力先:
echo - Windows x64: publish\win-x64\PerformanceMonitorAnalyzer.exe
echo - Windows x86: publish\win-x86\PerformanceMonitorAnalyzer.exe
echo - Windows ARM64: publish\win-arm64\PerformanceMonitorAnalyzer.exe
echo.
pause
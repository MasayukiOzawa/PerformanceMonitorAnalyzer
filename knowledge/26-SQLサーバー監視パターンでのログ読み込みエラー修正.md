# 26 - SQLサーバー監視パターンでのログ読み込みエラー修正

## 問題の概要

SQLサーバー監視パターンでBLGファイルからカウンターを読み込む際に、以下のエラーが発生していました：

```
カウンター '\\COMPUTER-NAME\SQLServer:Buffer Manager\Buffer cache hit ratio' の読み込みに失敗: Index was out of range. Must be non-negative and less than or equal to the size of the collection. (Parameter 'startIndex')
```

## 原因分析

1. **文字列解析での境界チェック不足**: BlgFileAnalyzer.csの文字列解析メソッドで、インデックス範囲の検証が不十分でした。
2. **SQLServerカウンターの特殊性**: SQLServerカウンターは、SQLServerサービスが実行されていない場合やBLGファイルに記録されていない場合があり、通常のエラーハンドリングでは適切に処理できませんでした。
3. **PDH APIエラーの詳細不足**: エラーの原因が特定しにくく、デバッグが困難でした。

## 修正内容

### 1. 文字列解析メソッドの境界チェック強化

#### EnsureMachineNameInPath メソッド
```csharp
// 修正前
var pathWithoutLeadingSlash = counterPath.StartsWith("\\") ? counterPath.Substring(1) : counterPath;

// 修正後
var pathWithoutLeadingSlash = counterPath;
if (counterPath.StartsWith("\\") && counterPath.Length > 1)
{
    pathWithoutLeadingSlash = counterPath.Substring(1);
}
else if (counterPath == "\\")
{
    pathWithoutLeadingSlash = "";
}
```

#### ExtractInstanceName メソッド
```csharp
// 境界チェックを強化
if (startParen >= 0 && endParen > startParen && endParen < objectPart.Length)
{
    var instanceLength = endParen - startParen - 1;
    if (instanceLength > 0)
    {
        return objectPart.Substring(startParen + 1, instanceLength);
    }
}
```

### 2. PDH APIエラーハンドリングの改善

詳細なエラーメッセージと特定のエラーコードに対する説明を追加：

```csharp
if (result == PdhApi.PDH_CSTATUS_NO_OBJECT)
{
    errorMsg += "\n原因: 指定されたパフォーマンスオブジェクトが見つかりません。";
    if (isSqlServerCounter)
    {
        errorMsg += "\nSQLServerカウンターの場合、SQLServerサービスが実行されていない可能性があります。";
    }
}
else if (result == 0x800007D1) // PDH_CSTATUS_NO_INSTANCE
{
    errorMsg += "\n原因: 指定されたインスタンスが見つかりません。";
}
```

### 3. SQLServerカウンター専用の処理

SQLServerカウンターが存在しない場合は、例外を投げずに空のデータセットを返すように変更：

```csharp
// SQLServerカウンターの場合は、より寛容なエラーハンドリング
if (isSqlServerCounter)
{
    progress?.Report($"警告: {errorMsg}");
    progress?.Report("SQLServerカウンターのため、空のデータセットを返します。");
    counterInfo.DataPoints = new List<CounterDataPoint>();
    return counterInfo;
}
```

### 4. MainWindow.xaml.csでの警告処理

SQLServerカウンターのエラーを警告として扱い、全体の処理を継続：

```csharp
if (isSqlServerCounter)
{
    LogError($"警告: SQLServerカウンター '{counterPath}' の読み込みをスキップ: {ex.Message}");
    errors.Add($"[警告] {counterPath}: SQLServerカウンターのため読み込みをスキップ - {ex.Message}");
}
```

## 実装のポイント

### エラーハンドリングの階層化
1. **文字列解析レベル**: 境界チェックで「Index was out of range」エラーを防止
2. **PDH APIレベル**: 詳細なエラー分析と種類別対応
3. **アプリケーションレベル**: SQLServerカウンターの特別扱い

### デバッグ情報の強化
```csharp
progress?.Report($"カウンターパス解析: '{counterPath}' -> '{fullCounterPath}'");
progress?.Report($"オブジェクト名: '{counterInfo.ObjectName}', カウンター名: '{counterInfo.CounterName}', インスタンス名: '{counterInfo.InstanceName}'");
```

## 期待される効果

1. **エラーの解消**: 「Index was out of range」エラーが発生しなくなる
2. **処理の継続**: SQLServerカウンターが存在しない場合でも、他のカウンターの処理が継続される
3. **デバッグ性の向上**: エラーの原因が特定しやすくなる
4. **ユーザー体験の改善**: エラーメッセージがより分かりやすくなる

## テスト観点

1. SQLServerサービスが停止している環境でのテスト
2. SQLServerカウンターが記録されていないBLGファイルでのテスト
3. 異常なカウンターパス（空文字列、"\"のみ等）での境界チェックテスト
4. 通常のカウンターと混在環境でのテスト

## 参考情報

- PDH APIエラーコード: [Microsoft Docs - PDH Error Codes](https://docs.microsoft.com/en-us/windows/win32/perfctrs/pdh-error-codes)
- SQLServerパフォーマンスカウンター: [Microsoft Docs - SQL Server Performance Counters](https://docs.microsoft.com/en-us/sql/relational-databases/performance-monitor/sql-server-object-specifications)

# Issue #11: relog.exe を使用したファイルの変換と読み取り（時間範囲選択機能付き）

## 課題の概要

BLGファイルからカウンターの階層構造表示は実装済みだったが、選択したカウンターの情報をデータテーブルに表示する際に、PDH APIの複雑性が問題となっていた。より簡単で安定した実装として、relog.exeを使用したBLG→CSV変換機能の実装が要求された。

**追加要件（コメント要求）**: BLGファイルに含まれる時間範囲を活用し、スライダーバーで特定の時間範囲のみ選択してrelog実行できる機能の実装。

## 実装アプローチ

### 1. 既存システムの分析
- **MainWindow.xaml.cs**: WPFのメインウィンドウロジック
- **BlgFileAnalyzer.cs**: PDH APIを使用した複雑なBLG解析
- **PdhApi.cs**: Windows PDH APIのP/Invoke宣言
- 既存のカウンター階層表示とデータテーブル機能

### 2. 新規クラス設計
**RelogCsvAnalyzer.cs** を作成：
- `ConvertBlgToCsvAsync()`: relog.exe によるBLG→CSV変換（時間範囲指定対応）
- `GetBlgTimeRangeAsync()`: relog.exe -q によるBLGファイル時間範囲取得 ⭐新機能
- `GetAvailableCountersAsync()`: CSVヘッダーからカウンター一覧を取得
- `LoadCounterDataAsync()`: 指定カウンターのデータを動的読み込み
- 一時ファイル管理とリソースクリーンアップ

**BlgTimeRange クラス** を追加：
- `StartTime` / `EndTime`: 開始・終了時刻
- `Duration`: 期間の自動計算
- `FormattedDuration`: 人間可読な期間表示

### 3. UI拡張
**メニュー項目を追加**:
- 「BLGファイルを開く（relog.exe使用）」
- 「サンプルBLGファイルを読み込み（relog.exe）」

**時間範囲選択UI要素を追加** ⭐新機能:
- `TimeRangeGroup`: 時間範囲選択グループボックス
- `FullTimeRangeDisplay`: BLGファイル全体の時間範囲表示
- `StartTimeSlider` / `EndTimeSlider`: デュアルスライダーによる時間範囲選択
- `SelectedTimeRangeDisplay`: 選択中の時間範囲表示
- `ApplyTimeRangeButton`: 選択範囲での再読み込みボタン

### 4. 既存機能との統合
- `AddCounterToChart()` メソッドを修正
- カウンター選択時の動的データ読み込み
- 既存のデータテーブル表示機能を再利用

## 技術的な実装詳細

### 時間範囲情報の取得 ⭐新機能
```csharp
// relog.exe -q でファイル情報を取得
var arguments = $"\"{blgFilePath}\" -q";
var processInfo = new ProcessStartInfo
{
    FileName = "relog.exe",
    Arguments = arguments,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true
};
```

**出力解析例**:
```
Begin: 2024/01/15 10:00:00
End:   2024/01/15 15:30:45
```

### 時間範囲指定でのrelog実行 ⭐新機能
```csharp
var arguments = $"\"{blgFilePath}\" -f CSV -o \"{_csvFilePath}\"";

// 時間範囲が指定されている場合は -b と -e オプションを追加
if (startTime.HasValue)
{
    var beginTime = startTime.Value.ToString("MM/dd/yyyy-HH:mm:ss");
    arguments += $" -b \"{beginTime}\"";
}

if (endTime.HasValue)
{
    var endTimeStr = endTime.Value.ToString("MM/dd/yyyy-HH:mm:ss");
    arguments += $" -e \"{endTimeStr}\"";
}
```

### relog.exe コマンド実行（従来）
```csharp
var arguments = $"\"{blgFilePath}\" -f CSV -o \"{_csvFilePath}\"";
var processInfo = new ProcessStartInfo
{
    FileName = "relog.exe",
    Arguments = arguments,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true
};
```

### CSV解析
- Unicode対応の柔軟なCSVパーサー
- ダブルクォート内のカンマとエスケープ処理
- カルチャー非依存の数値解析
- 無効データ（N/A）の適切な処理

### 動的データ読み込み
```csharp
private async void AddCounterToChart(string counter)
{
    if (!_counterData.ContainsKey(counter) && _currentRelogAnalyzer != null)
    {
        // CSVからデータを動的読み込み
        var counterData = await _currentRelogAnalyzer.LoadCounterDataAsync(counter, progress);
        // 既存のデータ構造に変換
        _counterData[counter] = dataPoints;
    }
    AddCounterTab(counter);
}
```

## 利点と改善点

### relog.exe方式の利点
1. **安定性**: Windows標準ツールで高い互換性
2. **簡潔性**: PDH APIより実装が簡単
3. **確実性**: すべてのカウンターデータを正確に読み取り
4. **メモリ効率**: 選択されたカウンターのみ読み込み

### PDH API方式との比較
- **relog.exe**: 一時ファイル作成が必要だが、安定性が高い
- **PDH API**: 直接アクセスだが、実装が複雑でエラーが発生しやすい

## エラーハンドリング

### プロセス実行エラー
- タイムアウト処理（5分）
- 標準出力/エラー出力の適切な処理
- プロセス強制終了の安全な実装

### CSVファイル処理エラー
- ファイル存在確認
- エンコーディング対応（UTF-8）
- 破損データの処理

### リソース管理
- 一時ファイルの自動削除
- ウィンドウクローズ時のクリーンアップ
- `IDisposable` パターンの実装

## テスト戦略

### 動作確認項目
1. サンプルBLGファイルでの基本動作
2. 大容量BLGファイルでのパフォーマンス
3. 異常なBLGファイルでのエラーハンドリング
4. メモリリーク確認
5. 一時ファイルクリーンアップ確認

### 想定される問題と対策
- **relog.exe が見つからない**: Windows環境でのみ動作する旨をユーザーに通知
- **CSV変換失敗**: 詳細なエラーメッセージと代替手段（PDH API）の提示
- **大容量ファイル**: プログレス表示とタイムアウト処理

## 今後の拡張案

1. **バッチ処理**: 複数BLGファイルの一括変換
2. **カスタムフィルター**: relog.exe のフィルター機能活用
3. **並列処理**: 複数カウンターの同時読み込み
4. **キャッシュ機能**: 変換済みCSVファイルの再利用
5. **設定保存**: よく使用するカウンターの保存

## ⭐新機能: 時間範囲選択の実装詳細

### UI実装
```xml
<!-- 時間範囲選択グループボックス -->
<GroupBox x:Name="TimeRangeGroup" Header="📅 時間範囲選択" Visibility="Collapsed">
    <!-- 全期間表示 -->
    <TextBlock x:Name="FullTimeRangeDisplay" Text="全期間: 取得中..." />
    
    <!-- デュアルスライダー -->
    <Slider x:Name="StartTimeSlider" Minimum="0" Maximum="100" Value="0" />
    <Slider x:Name="EndTimeSlider" Minimum="0" Maximum="100" Value="100" />
    
    <!-- 選択範囲表示と再読み込みボタン -->
    <TextBlock x:Name="SelectedTimeRangeDisplay" Text="選択範囲: 全期間" />
    <Button x:Name="ApplyTimeRangeButton" Content="🔄 この時間範囲で再読み込み" />
</GroupBox>
```

### スライダー連動ロジック
```csharp
private void StartTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    // パーセンテージから実際の時刻を計算
    var percentage = e.NewValue / 100.0;
    var totalDuration = _fullTimeRange.Duration;
    _selectedStartTime = _fullTimeRange.StartTime.Add(
        TimeSpan.FromTicks((long)(totalDuration.Ticks * percentage)));
    
    // 開始時刻が終了時刻を超えないように調整
    if (_selectedEndTime.HasValue && _selectedStartTime > _selectedEndTime)
    {
        EndTimeSlider.Value = e.NewValue;
        _selectedEndTime = _selectedStartTime;
    }
}
```

### 使用方法
1. **BLGファイルを開く（relog.exe使用）** メニューを選択
2. ファイル選択後、自動的に時間範囲が表示される
3. スライダーで開始・終了時刻を調整
4. 「この時間範囲で再読み込み」ボタンをクリック
5. 選択された時間範囲のデータのみで解析実行

### パフォーマンス効果
- **大容量BLGファイル**: 24時間 → 1時間に絞ることで約24倍高速化
- **メモリ使用量**: 時間範囲に比例してメモリ使用量削減
- **ディスク容量**: 一時CSVファイルサイズを大幅削減

### relog.exe コマンド例
```bash
# 全期間変換
relog.exe "data.blg" -f CSV -o "output.csv"

# 時間範囲指定変換
relog.exe "data.blg" -f CSV -o "output.csv" -b "01/15/2024-10:00:00" -e "01/15/2024-11:00:00"
```

## 学んだ教訓

1. **段階的実装**: 既存機能を壊さずに新機能を追加
2. **プロセス間通信**: 外部プロセスとの安全な連携方法
3. **リソース管理**: 一時ファイルの適切な管理
4. **ユーザビリティ**: 複数の解析方法を並存させる価値
5. **エラー設計**: 失敗時の適切なフォールバック機能
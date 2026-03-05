using System.Runtime.InteropServices;
using System.Text;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// Windows Performance Data Helper (PDH) APIのP/Invoke宣言
/// </summary>
public static class PdhApi
{
    private const string PdhDll = "pdh.dll";

    // PDH エラーコード
    public const uint PDH_CSTATUS_VALID_DATA = 0x00000000;
    public const uint PDH_CSTATUS_NEW_DATA = 0x00000001;
    public const uint PDH_MORE_DATA = 0x800007D2;
    public const uint PDH_NO_MORE_DATA = 0xC0000BCC;
    public const uint PDH_NO_DATA = 0x800007D5;
    public const uint PDH_RETRY = 0x800007D4;
    public const uint PDH_CALC_NEGATIVE_DENOMINATOR = 0x800007D6;
    public const uint PDH_CALC_NEGATIVE_VALUE = 0x800007D8;
    public const uint PDH_CALC_NEGATIVE_TIMEBASE = 0x800007D7;
    public const uint PDH_INVALID_DATA = 0x800007D0;
    public const uint PDH_ENTRY_NOT_IN_LOG_FILE = 0xC0000BCD;
    public const uint PDH_CSTATUS_NO_OBJECT = 0xC0000BB8;
    public const uint PDH_FUNCTION_NOT_FOUND = 0xC0000BC4;
    public const uint ERROR_SUCCESS = 0;

    // PDH データ形式
    public const uint PDH_FMT_DOUBLE = 0x00000200;
    public const uint PDH_FMT_LARGE = 0x00000400;
    public const uint PDH_FMT_LONG = 0x00000100;
    public const uint PDH_FMT_NOCAP100 = 0x00008000;

    // PDH ログタイプ
    public const uint PDH_LOG_TYPE_BINARY = 0x00000008;
    
    // ファイルアクセスモード（標準Windows定数）
    public const uint GENERIC_READ = 0x80000000;
    public const uint GENERIC_WRITE = 0x40000000;

    // PDH パフォーマンス詳細レベル
    public const uint PERF_DETAIL_NOVICE = 100;
    public const uint PERF_DETAIL_ADVANCED = 200;
    public const uint PERF_DETAIL_EXPERT = 300;
    public const uint PERF_DETAIL_WIZARD = 400;

    [StructLayout(LayoutKind.Explicit)]
    public struct PDH_FMT_COUNTERVALUE
    {
        [FieldOffset(0)]
        public uint CStatus;

        [FieldOffset(8)]
        public int longValue;

        [FieldOffset(8)]
        public double doubleValue;

        [FieldOffset(8)]
        public long largeValue;

        [FieldOffset(8)]
        public IntPtr AnsiStringValue;

        [FieldOffset(8)]
        public IntPtr WideStringValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PDH_TIME_INFO
    {
        public long StartTime;
        public long EndTime;
        public uint SampleCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PDH_COUNTER_INFO
    {
        public uint dwLength;
        public uint dwType;
        public uint CVersion;
        public uint CStatus;
        public int lScale;
        public int lDefaultScale;
        public IntPtr dwUserData;
        public IntPtr dwQueryUserData;
        public IntPtr szFullPath;
        public IntPtr szMachineName;
        public IntPtr szObjectName;
        public IntPtr szInstanceName;
        public IntPtr szParentInstance;
        public uint dwInstanceIndex;
        public IntPtr szCounterName;
        public IntPtr szExplainText;
        public IntPtr DataBuffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PDH_STATISTICS
    {
        public uint dwFormat;
        public uint count;
        public PDH_FMT_COUNTERVALUE min;
        public PDH_FMT_COUNTERVALUE max;
        public PDH_FMT_COUNTERVALUE mean;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PDH_RAW_COUNTER
    {
        public uint CStatus;
        public long TimeStamp;
        public long FirstValue;
        public long SecondValue;
        public uint MultiCount;
    }

    /// <summary>
    /// クエリハンドルを開く（データソース指定）
    /// </summary>
    [DllImport(PdhDll, CharSet = CharSet.Unicode)]
    public static extern uint PdhOpenQuery(
        string? szDataSource,
        IntPtr dwUserData,
        out IntPtr phQuery);

    /// <summary>
    /// クエリハンドルを閉じる
    /// </summary>
    [DllImport(PdhDll)]
    public static extern uint PdhCloseQuery(IntPtr hQuery);

    /// <summary>
    /// カウンターをクエリに追加
    /// </summary>
    [DllImport(PdhDll, CharSet = CharSet.Unicode)]
    public static extern uint PdhAddCounter(
        IntPtr hQuery,
        string szFullCounterPath,
        IntPtr dwUserData,
        out IntPtr phCounter);

    /// <summary>
    /// カウンターを削除
    /// </summary>
    [DllImport(PdhDll)]
    public static extern uint PdhRemoveCounter(IntPtr hCounter);

    /// <summary>
    /// クエリデータを収集
    /// </summary>
    [DllImport(PdhDll)]
    public static extern uint PdhCollectQueryData(IntPtr hQuery);

    /// <summary>
    /// タイムスタンプ付きでクエリデータを収集
    /// </summary>
    [DllImport(PdhDll)]
    public static extern uint PdhCollectQueryDataWithTime(IntPtr hQuery, out long pllTimeStamp);

    /// <summary>
    /// フォーマットされたカウンター値を取得
    /// </summary>
    [DllImport(PdhDll)]
    public static extern uint PdhGetFormattedCounterValue(
        IntPtr hCounter,
        uint dwFormat,
        IntPtr lpdwType,
        out PDH_FMT_COUNTERVALUE pValue);

    /// <summary>
    /// カウンターの統計情報を計算
    /// </summary>
    [DllImport(PdhDll)]
    public static extern uint PdhComputeCounterStatistics(
        IntPtr hCounter,
        uint dwFormat,
        uint dwFirstEntry,
        uint dwNumEntries,
        IntPtr lpRawValueArray,
        out PDH_STATISTICS data);

    /// <summary>
    /// 生の未処理カウンター値を取得
    /// </summary>
    [DllImport(PdhDll)]
    public static extern uint PdhGetRawCounterValue(
        IntPtr hCounter,
        IntPtr lpdwType,
        out PDH_RAW_COUNTER pValue);

    /// <summary>
    /// 配列形式でフォーマットされたカウンター値を取得
    /// </summary>
    [DllImport(PdhDll)]
    public static extern uint PdhGetFormattedCounterArray(
        IntPtr hCounter,
        uint dwFormat,
        ref uint lpdwBufferSize,
        out uint lpdwItemCount,
        IntPtr ItemBuffer);

    /// <summary>
    /// クエリの時間範囲を設定
    /// </summary>
    [DllImport(PdhDll)]
    public static extern uint PdhSetQueryTimeRange(
        IntPtr hQuery,
        ref PDH_TIME_INFO pInfo);

    /// <summary>
    /// データソースの時間範囲を取得
    /// </summary>
    [DllImport(PdhDll, CharSet = CharSet.Unicode)]
    public static extern uint PdhGetDataSourceTimeRange(
        string szDataSource,
        out uint pdwNumEntries,
        out PDH_TIME_INFO pInfo,
        ref uint pdwBufferSize);

    /// <summary>
    /// ワイルドカードパスを展開
    /// </summary>
    [DllImport(PdhDll, CharSet = CharSet.Unicode)]
    public static extern uint PdhExpandWildCardPath(
        string szDataSource,
        string szWildCardPath,
        char[]? mszExpandedPathList,
        ref uint pcchPathListLength,
        uint dwFlags);

    /// <summary>
    /// マシン名を列挙
    /// </summary>
    [DllImport(PdhDll, CharSet = CharSet.Unicode)]
    public static extern uint PdhEnumMachines(
        string? szDataSource,
        char[]? mszMachineList,
        ref uint pcchBufferSize);

    /// <summary>
    /// PDHエラーコードを文字列に変換
    /// </summary>
    public static string GetErrorMessage(uint errorCode)
    {
        return errorCode switch
        {
            ERROR_SUCCESS => "Success", // ERROR_SUCCESS = 0 = PDH_CSTATUS_VALID_DATA
            PDH_CSTATUS_NEW_DATA => "New data",
            PDH_MORE_DATA => "More data available", // PDH_MORE_DATA = 0x800007D2
            PDH_NO_DATA => "No data",
            PDH_RETRY => "Retry",
            PDH_CALC_NEGATIVE_DENOMINATOR => "Negative denominator",
            PDH_CALC_NEGATIVE_TIMEBASE => "Negative timebase",
            PDH_CALC_NEGATIVE_VALUE => "Negative value",
            PDH_INVALID_DATA => "Invalid data", // 0x800007D0
            PDH_NO_MORE_DATA => "No more data", // 0xC0000BCC
            PDH_ENTRY_NOT_IN_LOG_FILE => "Entry not in log file", // 0xC0000BCD  
            PDH_CSTATUS_NO_OBJECT => "No object", // 0xC0000BB8
            0x800007D1 => "PDH_CSTATUS_NO_INSTANCE",
            0x800007D3 => "PDH_CSTATUS_ITEM_NOT_VALIDATED",
            0xC0000BB9 => "PDH_INVALID_HANDLE",
            0xC0000BBA => "PDH_INVALID_DATA",
            0xC0000BBB => "PDH_INVALID_PATH",
            0xC0000BBC => "PDH_COUNTER_NOT_IN_QUERY",
            0xC0000BBD => "PDH_CSTATUS_BAD_COUNTERNAME",
            0xC0000BBE => "PDH_CSTATUS_NO_OBJECT",
            0xC0000BBF => "PDH_CSTATUS_NO_COUNTER",
            0xC0000BC0 => "PDH_CSTATUS_INVALID_DATA",
            0xC0000BC1 => "PDH_MEMORY_ALLOCATION_FAILURE",
            0xC0000BC2 => "PDH_INVALID_HANDLE",
            0xC0000BC3 => "PDH_INVALID_ARGUMENT",
            0xC0000BC4 => "PDH_FUNCTION_NOT_FOUND",
            0xC0000BC5 => "PDH_CSTATUS_NO_COUNTERNAME",
            0xC0000BC6 => "PDH_CSTATUS_BAD_COUNTERNAME",
            0xC0000BC7 => "PDH_INVALID_BUFFER",
            0xC0000BC8 => "PDH_INSUFFICIENT_BUFFER",
            0xC0000BC9 => "PDH_CANNOT_CONNECT_MACHINE",
            0xC0000BCA => "PDH_INVALID_PATH",
            0xC0000BCB => "PDH_INVALID_INSTANCE",
            0xC0000BCE => "PDH_CANNOT_READ_NAME_STRINGS",
            0xC0000BCF => "PDH_LOG_FILE_CREATE_ERROR",
            0xC0000BD0 => "PDH_LOG_FILE_OPEN_ERROR",
            0xC0000BD1 => "PDH_LOG_TYPE_NOT_FOUND",
            0xC0000BD2 => "PDH_NO_MORE_DATA",
            0xC0000BD3 => "PDH_ENTRY_NOT_IN_LOG_FILE",
            0xC0000BD4 => "PDH_DATA_SOURCE_IS_LOG_FILE",
            0xC0000BD5 => "PDH_DATA_SOURCE_IS_REAL_TIME",
            0xC0000BD6 => "PDH_UNABLE_READ_LOG_HEADER",
            0xC0000BD7 => "PDH_FILE_NOT_FOUND",
            0xC0000BD8 => "PDH_FILE_ALREADY_EXISTS",
            0xC0000BD9 => "PDH_NOT_IMPLEMENTED",
            0xC0000BDA => "PDH_STRING_NOT_FOUND",
            _ => $"Unknown PDH error: 0x{errorCode:X8}"
        };
    }

    /// <summary>
    /// FileTimeからDateTimeに変換
    /// </summary>
    public static DateTime DateTimeFromFileTime(long fileTime)
    {
        return DateTime.FromFileTimeUtc(fileTime);
    }

    /// <summary>
    /// DateTimeからFileTimeに変換
    /// </summary>
    public static long FileTimeFromDateTime(DateTime dateTime)
    {
        return dateTime.ToFileTimeUtc();
    }

    /// <summary>
    /// PDHエラーをチェックし、エラーの場合は例外をスロー
    /// </summary>
    public static void CheckPdhStatus(uint status)
    {
        if (status != ERROR_SUCCESS)
        {
            throw new Exception($"PDH error: {GetErrorMessage(status)} (0x{status:X8})");
        }
    }

    /// <summary>
    /// NULL区切り文字列を文字列リストに変換
    /// </summary>
    public static List<string> MultipleStringsToList(char[] multipleStrings)
    {
        var list = new List<string>();
        var item = new StringBuilder();

        for (int i = 0; i < multipleStrings.Length; i++)
        {
            if (multipleStrings[i] != '\0')
            {
                item.Append(multipleStrings[i]);
            }
            else
            {
                if (item.Length > 0)
                {
                    list.Add(item.ToString());
                    item.Length = 0;
                }
                if (i + 1 < multipleStrings.Length && multipleStrings[i + 1] == '\0')
                {
                    break;
                }
            }
        }

        return list;
    }

    // ==============================================
    // BLGファイル解析用の追加PDH API メソッド
    // ==============================================

    /// <summary>
    /// データソースのマシンを列挙
    /// </summary>
    [DllImport(PdhDll, CharSet = CharSet.Unicode)]
    public static extern uint PdhEnumMachinesH(
        IntPtr hDataSource,
        IntPtr mszMachineList,
        ref uint pcchBufferSize);

    /// <summary>
    /// データソースのオブジェクトを列挙（Unicode版）
    /// </summary>
    [DllImport(PdhDll, CharSet = CharSet.Unicode)]
    public static extern uint PdhEnumObjectsH(
        IntPtr hDataSource,
        string? szMachineName,
        IntPtr mszObjectList,
        ref uint pcchBufferSize,
        uint dwDetailLevel,
        bool bRefresh);

    /// <summary>
    /// データソースのオブジェクトを列挙（ANSI版）
    /// </summary>
    [DllImport(PdhDll, CharSet = CharSet.Ansi)]
    public static extern uint PdhEnumObjectsA(
        IntPtr hDataSource,
        string? szMachineName,
        IntPtr mszObjectList,
        ref uint pcchBufferSize,
        uint dwDetailLevel,
        bool bRefresh);

    /// <summary>
    /// オブジェクトアイテム（カウンターとインスタンス）を列挙（IntPtr版）
    /// </summary>
    [DllImport(PdhDll, CharSet = CharSet.Unicode)]
    public static extern uint PdhEnumObjectItemsH(
        IntPtr hDataSource,
        string? szMachineName,
        string szObjectName,
        IntPtr mszCounterList,
        ref uint pcchCounterListLength,
        IntPtr mszInstanceList,
        ref uint pcchInstanceListLength,
        uint dwDetailLevel,
        uint dwFlags);

    /// <summary>
    /// オブジェクトアイテム（カウンターとインスタンス）を列挙（StringBuilder版）
    /// </summary>
    [DllImport(PdhDll, CharSet = CharSet.Unicode, EntryPoint = "PdhEnumObjectItemsH")]
    public static extern uint PdhEnumObjectItemsHSB(
        IntPtr hDataSource,
        string? szMachineName,
        string szObjectName,
        StringBuilder? mszCounterList,
        ref uint pcchCounterListLength,
        StringBuilder? mszInstanceList,
        ref uint pcchInstanceListLength,
        uint dwDetailLevel,
        uint dwFlags);

    /// <summary>
    /// 入力データソースをバインド
    /// </summary>
    [DllImport(PdhDll, CharSet = CharSet.Unicode)]
    public static extern uint PdhBindInputDataSource(
        out IntPtr phDataSource,
        string szLogFileNameList);

    /// <summary>
    /// ログファイルを開く
    /// </summary>
    [DllImport(PdhDll, CharSet = CharSet.Unicode)]
    public static extern uint PdhOpenLog(
        string szLogFileName,
        uint dwAccessFlags,
        out uint lpdwLogType,
        IntPtr hQuery,
        uint dwMaxSize,
        string? szUserCaption,
        out IntPtr phLog);

    /// <summary>
    /// ログを閉じる
    /// </summary>
    [DllImport(PdhDll)]
    public static extern uint PdhCloseLog(IntPtr hLog, uint dwFlags);
}

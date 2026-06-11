using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class OperationLogStoreTests
{
    [Fact]
    public void AddOperationLog_InsertsNewestFirst()
    {
        var store = new OperationLogStore();

        store.AddOperationLog(LogLevel.Info, "first");
        store.AddOperationLog(LogLevel.Warning, "second");

        Assert.Equal("second", store.OperationLogs[0].Message);
        Assert.Equal("first", store.OperationLogs[1].Message);
    }

    [Fact]
    public void ClearMethods_ClearCollections()
    {
        var store = new OperationLogStore();
        store.AddOperationLog(LogLevel.Info, "message");
        store.LoadErrorLogLines(["[2026-01-01 00:00:00] ERROR failed"]);

        store.ClearOperationLogs();
        store.ClearErrorLogs();

        Assert.Empty(store.OperationLogs);
        Assert.Empty(store.ErrorLogs);
    }

    [Fact]
    public void LoadErrorLogLines_ParsesNewestLinesFirst()
    {
        var store = new OperationLogStore();

        var count = store.LoadErrorLogLines([
            "[2026-01-01 00:00:00] first",
            "[2026-01-01 00:00:01] ERROR second"
        ]);

        Assert.Equal(2, count);
        Assert.Equal("ERROR second", store.ErrorLogs[0].Message);
        Assert.Equal(LogLevel.Error, store.ErrorLogs[0].Level);
    }
}

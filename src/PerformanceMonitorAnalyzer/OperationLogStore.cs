using System.Collections.ObjectModel;

namespace PerformanceMonitorAnalyzer;

public sealed class OperationLogStore
{
    private const int MaxLogEntries = 1000;

    public ObservableCollection<LogEntry> OperationLogs { get; } = new();
    public ObservableCollection<LogEntry> ErrorLogs { get; } = new();

    public LogEntry AddOperationLog(LogLevel level, string message)
    {
        var logEntry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message
        };

        OperationLogs.Insert(0, logEntry);

        while (OperationLogs.Count > MaxLogEntries)
        {
            OperationLogs.RemoveAt(OperationLogs.Count - 1);
        }

        return logEntry;
    }

    public int LoadErrorLogLines(IEnumerable<string> lines)
    {
        ErrorLogs.Clear();

        foreach (var line in lines.Reverse().Take(MaxLogEntries))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var logEntry = LogLineParser.Parse(line);
            if (logEntry is not null)
            {
                ErrorLogs.Add(logEntry);
            }
        }

        return ErrorLogs.Count;
    }

    public void ClearOperationLogs()
    {
        OperationLogs.Clear();
    }

    public void ClearErrorLogs()
    {
        ErrorLogs.Clear();
    }
}

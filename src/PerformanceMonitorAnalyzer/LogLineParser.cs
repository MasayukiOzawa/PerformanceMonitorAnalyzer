namespace PerformanceMonitorAnalyzer;

public static class LogLineParser
{
    public static LogEntry? Parse(string line)
    {
        try
        {
            if (line.StartsWith("[", StringComparison.Ordinal) && line.Contains("] "))
            {
                var endBracket = line.IndexOf("] ", StringComparison.Ordinal);
                if (endBracket > 0)
                {
                    var timestampStr = line.Substring(1, endBracket - 1);
                    var message = line[(endBracket + 2)..];

                    if (DateTime.TryParse(timestampStr, out var timestamp))
                    {
                        return new LogEntry
                        {
                            Timestamp = timestamp,
                            Level = InferLevel(message),
                            Message = message
                        };
                    }
                }
            }

            return new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Info,
                Message = line
            };
        }
        catch
        {
            return null;
        }
    }

    private static LogLevel InferLevel(string message)
    {
        if (message.Contains("ERROR", StringComparison.Ordinal) ||
            message.Contains("エラー", StringComparison.Ordinal) ||
            message.Contains("失敗", StringComparison.Ordinal))
        {
            return LogLevel.Error;
        }

        if (message.Contains("WARNING", StringComparison.Ordinal) ||
            message.Contains("警告", StringComparison.Ordinal))
        {
            return LogLevel.Warning;
        }

        return LogLevel.Info;
    }
}

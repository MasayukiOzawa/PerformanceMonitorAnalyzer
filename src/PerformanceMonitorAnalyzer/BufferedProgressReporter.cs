using System.Threading;

namespace PerformanceMonitorAnalyzer;

internal sealed class BufferedProgressReporter : IProgress<string>
{
    private string? _latestMessage;

    public BufferedProgressReporter(string initialMessage)
    {
        _latestMessage = initialMessage;
    }

    public string? LatestMessage => Volatile.Read(ref _latestMessage);

    public void Report(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        Volatile.Write(ref _latestMessage, value);
    }
}

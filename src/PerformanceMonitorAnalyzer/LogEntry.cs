using System.ComponentModel;
using System.Windows.Media;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// ログメッセージエントリ
/// </summary>
public class LogEntry : INotifyPropertyChanged
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    public string LevelDisplay => Level.ToString();
    public Brush TextColor => Level switch
    {
        LogLevel.Error => Brushes.Red,
        LogLevel.Warning => Brushes.Orange,
        LogLevel.Info => Brushes.Blue,
        LogLevel.Success => Brushes.Green,
        _ => Brushes.Black
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

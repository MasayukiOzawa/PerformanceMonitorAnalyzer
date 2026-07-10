using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// 凡例アイテム用のデータクラス
/// </summary>
public class LegendItem : INotifyPropertyChanged
{
    private bool _isVisible = true;
    private bool _isHighlighted = false;
    private bool _isSecondaryAxis;
    private string _currentValue = "";
    private Color _color = Colors.Blue;

    public string CounterName { get; set; } = string.Empty;
    public string CounterPath { get; set; } = string.Empty;

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackgroundBrush));
                OnPropertyChanged(nameof(TextBrush));
            }
        }
    }

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            if (_isHighlighted != value)
            {
                _isHighlighted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackgroundBrush));
                OnPropertyChanged(nameof(CounterFontWeight));
                OnPropertyChanged(nameof(HighlightMark));
                OnPropertyChanged(nameof(HighlightBrush));
            }
        }
    }

    public bool IsSecondaryAxis
    {
        get => _isSecondaryAxis;
        set
        {
            if (_isSecondaryAxis != value)
            {
                _isSecondaryAxis = value;
                OnPropertyChanged();
            }
        }
    }

    public string CurrentValue
    {
        get => _currentValue;
        set
        {
            if (_currentValue != value)
            {
                _currentValue = value;
                OnPropertyChanged();
            }
        }
    }

    public Color Color
    {
        get => _color;
        set
        {
            if (_color != value)
            {
                _color = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ColorBrush));
            }
        }
    }

    public Brush ColorBrush => new SolidColorBrush(_color);
    public Brush BackgroundBrush => _isHighlighted
        ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 255, 242, 204))
        : (_isVisible ? Brushes.Transparent : new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 200, 200, 200)));
    public Brush TextBrush => _isVisible ? Brushes.Black : Brushes.Gray;
    public FontWeight CounterFontWeight => _isHighlighted
        ? FontWeights.Bold
        : FontWeights.Normal;
    public string HighlightMark => _isHighlighted ? "★" : "☆";
    public Brush HighlightBrush => _isHighlighted ? Brushes.Goldenrod : Brushes.Gray;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

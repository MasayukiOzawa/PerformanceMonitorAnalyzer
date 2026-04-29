using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PerformanceMonitorAnalyzer;

public sealed class CounterSelectorItem : INotifyPropertyChanged
{
    private bool _isChecked;

    public string DisplayName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public string InstanceName { get; init; } = string.Empty;
    public string CounterName { get; init; } = string.Empty;
    public bool IsAllInstances { get; init; }

    public string InstanceDisplayName => string.IsNullOrWhiteSpace(InstanceName) ? string.Empty : InstanceName;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }

            _isChecked = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

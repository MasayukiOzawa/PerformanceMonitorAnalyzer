using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// TreeViewで使用する階層構造のノードクラス
/// </summary>
public class CounterTreeNode : INotifyPropertyChanged
{
    private bool? _isChecked = false;
    private CounterTreeNode? _parent;

    public string DisplayName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public ObservableCollection<CounterTreeNode> Children { get; set; } = new();
    public NodeType Type { get; set; } = NodeType.Counter;
    public bool IsWildCard { get; set; } = false;

    public bool IsLeaf => Children.Count == 0;
    public Visibility CheckBoxVisibility => Visibility.Visible;

    /// <summary>
    /// 三状態チェックボックスかどうか（親ノードの場合はtrue）
    /// </summary>
    public bool IsThreeState => !IsLeaf;

    public string FontWeight => Type == NodeType.Object ? "Bold" : "Normal";
    public string TextColor => Type switch
    {
        NodeType.Object => "DarkBlue",
        NodeType.Instance when IsWildCard => "Purple",
        NodeType.Instance => "DarkGreen",
        NodeType.Counter when IsWildCard => "Purple",
        _ => "Black"
    };

    public string CountDisplay => Type == NodeType.Object ? $"({Children.Count} インスタンス)" :
                                 Type == NodeType.Instance ? $"({Children.Count} カウンター)" : "";

    public Visibility CountVisibility => Type != NodeType.Counter ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// 親ノードの設定
    /// </summary>
    public CounterTreeNode? Parent
    {
        get => _parent;
        set => _parent = value;
    }

    /// <summary>
    /// 三状態チェックボックス対応のIsCheckedプロパティ
    /// true: 全選択, false: 全解除, null: 部分選択
    /// </summary>
    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked != value)
            {
                SetIsCheckedInternal(value, true, true);
            }
        }
    }

    /// <summary>
    /// ユーザー操作時は部分選択を経由させず、全選択/全解除を切り替える
    /// </summary>
    public void ToggleFromUserInteraction()
    {
        IsChecked = _isChecked == true ? false : true;
    }

    /// <summary>
    /// IsChecked状態を内部的に設定（イベント通知と親子更新制御）
    /// </summary>
    private void SetIsCheckedInternal(bool? value, bool updateChildren, bool updateParent)
    {
        if (_isChecked == value)
        {
            return;
        }

        _isChecked = value;
        OnPropertyChanged(nameof(IsChecked));

        if (updateChildren && value.HasValue)
        {
            foreach (var child in Children)
            {
                child.SetIsCheckedInternal(value.Value, true, false);
            }
        }

        if (updateParent && _parent != null)
        {
            _parent.UpdateParentStateFromChild();
        }
    }

    /// <summary>
    /// 子ノードの状態変更に基づいて親ノードの状態を更新
    /// </summary>
    public void UpdateParentStateFromChild()
    {
        if (Children.Count == 0)
        {
            return;
        }

        var checkedCount = 0;
        var uncheckedCount = 0;
        var indeterminateCount = 0;

        foreach (var child in Children)
        {
            switch (child.IsChecked)
            {
                case true:
                    checkedCount++;
                    break;
                case false:
                    uncheckedCount++;
                    break;
                case null:
                    indeterminateCount++;
                    break;
            }
        }

        bool? newState;

        if (indeterminateCount > 0)
        {
            newState = null;
        }
        else if (checkedCount == Children.Count)
        {
            newState = true;
        }
        else if (uncheckedCount == Children.Count)
        {
            newState = false;
        }
        else
        {
            newState = null;
        }

        if (_isChecked != newState)
        {
            SetIsCheckedInternal(newState, false, true);
        }
    }

    /// <summary>
    /// 公開用の親ノード状態更新メソッド
    /// </summary>
    public void UpdateParentState()
    {
        UpdateParentStateFromChild();
    }

    /// <summary>
    /// 選択されているリーフ（カウンター）ノードを取得
    /// ワイルドカードカウンターは除外し、実際のカウンターのみを返す
    /// </summary>
    public IEnumerable<CounterTreeNode> GetSelectedCounters()
    {
        if (IsLeaf && IsChecked == true && !IsWildCard)
        {
            yield return this;
        }

        foreach (var child in Children)
        {
            foreach (var selectedCounter in child.GetSelectedCounters())
            {
                yield return selectedCounter;
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

using System.Collections.ObjectModel;

namespace DuoTuXiuXiu.ViewModels;

/// <summary>左栏中按张数分组的版式集合。</summary>
public sealed class LayoutGroupViewModel
{
    public LayoutGroupViewModel(int count, bool isCurrent, int optionCount)
    {
        Count = count;
        IsCurrent = isCurrent;
        Header = count.ToString();
        Subtitle = isCurrent ? $"当前张数 · {optionCount} 种" : $"切换为 {count} 张 · {optionCount} 种";
    }

    public int Count { get; }
    public bool IsCurrent { get; }
    public string Header { get; }
    public string Subtitle { get; }
    public ObservableCollection<LayoutOptionViewModel> Options { get; } = new();
}

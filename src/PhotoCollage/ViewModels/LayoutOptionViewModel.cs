using CommunityToolkit.Mvvm.ComponentModel;
using DuoTuXiuXiu.Models;

namespace DuoTuXiuXiu.ViewModels;

/// <summary>左栏版式缩略图选项。</summary>
public sealed partial class LayoutOptionViewModel : ObservableObject
{
    public LayoutOptionViewModel(LayoutPreset preset, int groupCount, bool isSelected)
    {
        Preset = preset;
        GroupCount = groupCount;
        this.isSelected = isSelected;
    }

    public LayoutPreset Preset { get; }

    /// <summary>该选项所属分组对应的图片张数。</summary>
    public int GroupCount { get; }

    public string Name => Preset.Name;
    public LayoutSpec Spec => Preset.Spec;
    public string Description => Preset.Description;

    [ObservableProperty] private bool isSelected;
}

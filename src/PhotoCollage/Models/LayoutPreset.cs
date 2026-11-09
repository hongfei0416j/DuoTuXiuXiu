using DuoTuXiuXiu.Services;

namespace DuoTuXiuXiu.Models;

/// <summary>
/// 左栏展示的可选版式（缩略图 + 名称）。
/// Count 为该版式适用的图片张数，Category 对应「自定义 / 异形」分组。
/// </summary>
public sealed record LayoutPreset(
    string Id,
    string Name,
    string Description,
    LayoutSpec Spec,
    int Count,
    string Category)
{
    public bool IsAsymmetric => Category == "asymmetric";
}

public static class LayoutPresets
{
    /// <summary>按张数自动匹配的等高网格，不属于任何固定张数分组。</summary>
    public static readonly LayoutPreset UniformAuto = new(
        "uniform-auto",
        "自动等高网格",
        "按张数与画布比例自动计算行列",
        new LayoutSpec(CollageLayoutKind.Uniform),
        0,
        "custom");

    public static LayoutPreset? Find(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (id == UniformAuto.Id) return UniformAuto;
        return LayoutLibrary.Find(id);
    }
}

using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace DuoTuXiuXiu.Models;

/// <summary>
/// 渲染所需的完整场景快照。任何参数变化都会生成新的 Scene 触发重绘。
/// </summary>
public sealed class CollageScene
{
    public required IReadOnlyList<CollageImage> Images { get; init; }
    public required LayoutSpec Spec { get; init; }

    public double Gap { get; init; }
    public double Padding { get; init; }
    public double CornerRadius { get; init; }

    /// <summary>设计基准宽度（即画布像素宽度），用于让预览与导出的间距/圆角比例一致。</summary>
    public double BaseWidth { get; init; } = 1200;

    public double AspectRatio { get; init; } = 1;
    public CellFillMode FillMode { get; init; } = CellFillMode.Crop;

    public Color Background { get; init; } = Color.Parse("#F3F4F8");
    public Bitmap? BackgroundImage { get; init; }

    public int SlotCount => Images.Count;
}

using Avalonia.Media.Imaging;

namespace DuoTuXiuXiu.Models;

/// <summary>
/// 渲染用的图片快照（包含单张微调参数）。Bitmap 为 null 表示空槽位。
/// </summary>
public sealed record CollageImage(Bitmap? Bitmap, double Zoom, double OffsetX, double OffsetY)
{
    public static readonly CollageImage Placeholder = new(null, 1, 0, 0);

    public bool IsPlaceholder => Bitmap is null;
}

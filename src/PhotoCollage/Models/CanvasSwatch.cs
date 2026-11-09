using Avalonia.Media;

namespace DuoTuXiuXiu.Models;

/// <summary>
/// 画布背景预设色。
/// </summary>
public sealed record CanvasSwatch(string Name, Color Color)
{
    public IBrush Brush => new SolidColorBrush(Color);
}

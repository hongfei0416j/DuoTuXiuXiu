namespace DuoTuXiuXiu.Models;

/// <summary>
/// 版式的单元格定义：使用 0~1 的归一化坐标描述位置与大小。
/// 渲染时由 LayoutEngine 换算为像素矩形，再统一叠加间距 / 外边距 / 圆角，
/// 因此同一份定义可以同时用于缩略图、实时预览与任意分辨率导出。
/// </summary>
public readonly record struct LayoutTile(double X, double Y, double W, double H)
{
    public double Right => X + W;
    public double Bottom => Y + H;
    public double Area => W * H;
}

/// <summary>
/// 一个完整版式：固定张数 + 单元格列表。数据驱动，方便持续扩充。
/// </summary>
public sealed record LayoutDefinition(
    string Id,
    string Name,
    string Description,
    string Category,
    int Count,
    IReadOnlyList<LayoutTile> Tiles);

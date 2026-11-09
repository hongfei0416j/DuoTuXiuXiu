namespace DuoTuXiuXiu.Models;

/// <summary>
/// 一次排版请求的参数。与具体渲染无关，便于单独测试与扩展。
/// </summary>
public sealed record LayoutSpec(
    CollageLayoutKind Kind,
    int Columns = 0,
    int Rows = 0,
    double HeroFraction = 0.6,
    int SideColumns = 0,
    bool HeroOnLeft = true,
    bool Manual = false,
    LayoutDefinition? Definition = null);

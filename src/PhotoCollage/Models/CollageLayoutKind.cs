namespace DuoTuXiuXiu.Models;

/// <summary>
/// 版式的底层排布算法类型。渲染与排版逻辑相互独立，可在此扩展新算法。
/// </summary>
public enum CollageLayoutKind
{
    /// <summary>等高网格（行高一致，列数可自动或指定）。</summary>
    Uniform,

    /// <summary>大图 + 小图：一张主图占据一侧，其余图片填充另一侧网格。</summary>
    Hero,

    /// <summary>横幅主图：部分整行主图与多列小图交替。</summary>
    Banner,

    /// <summary>错落拼贴：行高按权重变化，形成横竖混排。</summary>
    Weighted,

    /// <summary>预设版式：按 LayoutDefinition 中的归一化单元格直接排布。</summary>
    Preset
}

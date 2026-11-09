namespace DuoTuXiuXiu.Models;

/// <summary>格子内图片的填充方式。</summary>
public enum CellFillMode
{
    /// <summary>填充裁切：铺满格子，超出部分裁掉。</summary>
    Crop,

    /// <summary>完整适应：保留整张图片，可能出现留白。</summary>
    Fit
}

using Avalonia;

namespace DuoTuXiuXiu.Models;

/// <summary>
/// 排版结果中的一个格子。Index 对应图片顺序。
/// </summary>
public readonly record struct LayoutCell(int Index, Rect Bounds, double CornerRadius);

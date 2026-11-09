using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DuoTuXiuXiu.Models;
using DuoTuXiuXiu.Services;

namespace DuoTuXiuXiu.Controls;

/// <summary>
/// 版式缩略示意图：使用真实的 LayoutEngine 渲染，所见即所得。
/// 面积最大的那格颜色更深，方便一眼看出大图/小图混排结构。
/// </summary>
public class LayoutThumb : Control
{
    public static readonly StyledProperty<LayoutSpec?> SpecProperty =
        AvaloniaProperty.Register<LayoutThumb, LayoutSpec?>(nameof(Spec));

    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<LayoutThumb, int>(nameof(Count), 4);

    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<LayoutThumb, double>(nameof(AspectRatio), 1);

    private static readonly IBrush CellBrush = new SolidColorBrush(Color.Parse("#C9CEDA"));
    private static readonly IBrush HeroBrush = new SolidColorBrush(Color.Parse("#A9B2C3"));

    static LayoutThumb()
    {
        AffectsRender<LayoutThumb>(SpecProperty, CountProperty, AspectRatioProperty);
    }

    public LayoutSpec? Spec
    {
        get => GetValue(SpecProperty);
        set => SetValue(SpecProperty, value);
    }

    public int Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public double AspectRatio
    {
        get => GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var spec = Spec;
        if (spec is null) return;

        int count = Math.Clamp(spec.Definition?.Count ?? Count, 1, 100);
        double availableWidth = Bounds.Width;
        double availableHeight = Bounds.Height;
        if (availableWidth <= 4 || availableHeight <= 4) return;

        double aspect = AspectRatio > 0 ? AspectRatio : 1;
        double width = availableWidth;
        double height = width / aspect;
        if (height > availableHeight)
        {
            height = availableHeight;
            width = height * aspect;
        }

        var area = new Rect((availableWidth - width) / 2, (availableHeight - height) / 2, width, height);

        // 缩略图尺寸小，间距/圆角按比例缩放，形状比例与真实出图保持一致。
        double gap = Math.Max(0.5, area.Width * 0.012);
        double padding = Math.Max(1, Math.Min(area.Width, area.Height) * 0.02);
        double radius = Math.Max(1, Math.Min(area.Width, area.Height) * 0.02);

        var cells = LayoutEngine.Build(spec, count, area.Size, gap, padding, radius);
        int heroIndex = FindLargestTile(spec, cells);

        foreach (var cell in cells)
        {
            var brush = cell.Index == heroIndex ? HeroBrush : CellBrush;
            context.DrawRectangle(brush, null, new RoundedRect(
                cell.Bounds.Translate(area.Position),
                Math.Min(2.5, Math.Min(cell.Bounds.Width, cell.Bounds.Height) / 3)));
        }
    }

    private static int FindLargestTile(LayoutSpec spec, IReadOnlyList<LayoutCell> cells)
    {
        if (cells.Count <= 1) return -1;

        int bestIndex = -1;
        double bestArea = 0;
        double total = 0;
        foreach (var cell in cells)
        {
            double cellArea = cell.Bounds.Width * cell.Bounds.Height;
            total += cellArea;
            if (cellArea > bestArea)
            {
                bestArea = cellArea;
                bestIndex = cell.Index;
            }
        }

        // 只有明显更大的主图才高亮，均匀网格保持同色。
        double average = total / cells.Count;
        return bestArea > average * 1.6 ? bestIndex : -1;
    }
}

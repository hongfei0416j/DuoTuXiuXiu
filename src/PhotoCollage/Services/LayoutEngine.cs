using Avalonia;
using DuoTuXiuXiu.Models;

namespace DuoTuXiuXiu.Services;

/// <summary>
/// 拼图版式计算。输入版式参数与画布尺寸，输出每个格子的矩形，保证铺满画布且无空洞。
/// 与渲染完全解耦，可单独用于缩略图、导出与预览。
/// </summary>
public static class LayoutEngine
{
    public static IReadOnlyList<LayoutCell> Build(CollageScene scene, Size size) =>
        Build(scene.Spec, scene.Images.Count, size, scene.Gap, scene.Padding, scene.CornerRadius);

    public static IReadOnlyList<LayoutCell> Build(LayoutSpec spec, int count, Size size, double gap, double padding, double cornerRadius)
    {
        var cells = new List<LayoutCell>();
        if (count <= 0 || size.Width <= 0 || size.Height <= 0) return cells;

        double minSide = Math.Min(size.Width, size.Height);
        double pad = Math.Clamp(padding, 0, minSide / 2.5);
        double g = Math.Clamp(gap, 0, minSide / 6);
        double radius = Math.Clamp(cornerRadius, 0, minSide / 3);
        var inner = new Rect(pad, pad, Math.Max(1, size.Width - pad * 2), Math.Max(1, size.Height - pad * 2));

        switch (spec.Kind)
        {
            case CollageLayoutKind.Hero:
                BuildHero(spec, count, inner, g, radius, cells);
                break;
            case CollageLayoutKind.Banner:
                BuildBanner(spec, count, inner, g, radius, cells);
                break;
            case CollageLayoutKind.Weighted:
                BuildWeighted(spec, count, inner, g, radius, cells);
                break;
            case CollageLayoutKind.Preset:
                BuildPreset(spec, count, inner, g, radius, cells);
                break;
            default:
                BuildUniform(spec, count, inner, g, radius, cells);
                break;
        }

        return cells;
    }

    /// <summary>根据张数与画布比例推荐行数，使格子尽量接近方形。</summary>
    public static int SuggestRows(int count, double aspect)
    {
        if (count <= 1) return 1;
        aspect = Math.Max(0.05, aspect);
        int bestRows = 1;
        double bestScore = double.MaxValue;
        for (int rows = 1; rows <= count; rows++)
        {
            int cols = (int)Math.Ceiling(count / (double)rows);
            double cellAspect = aspect * rows / cols;
            double score = Math.Abs(Math.Log(cellAspect));
            if (score < bestScore - 1e-9)
            {
                bestScore = score;
                bestRows = rows;
            }
        }
        return bestRows;
    }

    public static (int Rows, int Columns) SuggestGrid(int count, double aspect)
    {
        int rows = SuggestRows(count, aspect);
        int cols = (int)Math.Ceiling(count / (double)rows);
        return (Math.Max(1, rows), Math.Max(1, cols));
    }

    /// <summary>从排版结果反推行数与最大列数，用于界面状态显示。</summary>
    public static (int Rows, int Columns) Describe(IReadOnlyList<LayoutCell> cells)
    {
        if (cells.Count == 0) return (0, 0);
        var counts = cells
            .GroupBy(c => Math.Round(c.Bounds.Y, 1))
            .Select(g => g.Count())
            .ToList();
        return (counts.Count, counts.Count == 0 ? 0 : counts.Max());
    }

    private static void BuildUniform(LayoutSpec spec, int count, Rect area, double gap, double radius, List<LayoutCell> cells)
    {
        int rows;
        int cols;

        if (spec.Manual && spec.Rows > 0 && spec.Columns > 0)
        {
            rows = spec.Rows;
            cols = spec.Columns;
        }
        else if (spec.Columns > 0)
        {
            cols = Math.Min(spec.Columns, count);
            rows = (int)Math.Ceiling(count / (double)cols);
        }
        else if (spec.Rows > 0)
        {
            rows = Math.Min(spec.Rows, count);
            cols = (int)Math.Ceiling(count / (double)rows);
        }
        else
        {
            rows = SuggestRows(count, area.Width / Math.Max(1, area.Height));
            cols = (int)Math.Ceiling(count / (double)rows);
        }

        rows = Math.Max(1, rows);
        cols = Math.Max(1, cols);

        var rowCounts = new int[rows];
        if (spec.Manual && spec.Rows > 0 && spec.Columns > 0)
        {
            for (int r = 0; r < rows; r++) rowCounts[r] = cols;
        }
        else if (spec.Columns > 0)
        {
            int remaining = count;
            for (int r = 0; r < rows; r++)
            {
                rowCounts[r] = Math.Min(cols, remaining);
                remaining -= rowCounts[r];
            }
        }
        else
        {
            int baseCount = count / rows;
            int rem = count % rows;
            for (int r = 0; r < rows; r++) rowCounts[r] = baseCount + (r < rem ? 1 : 0);
        }

        double rowH = (area.Height - gap * (rows - 1)) / rows;
        double y = area.Y;
        int index = 0;

        for (int r = 0; r < rows; r++)
        {
            int c = rowCounts[r];
            if (c <= 0)
            {
                y += rowH + gap;
                continue;
            }

            double cellW = (area.Width - gap * (c - 1)) / c;
            double x = area.X;
            for (int i = 0; i < c && index < count; i++)
            {
                AddCell(cells, index++, new Rect(x, y, cellW, rowH), radius);
                x += cellW + gap;
            }
            y += rowH + gap;
        }
    }

    private static void BuildHero(LayoutSpec spec, int count, Rect area, double gap, double radius, List<LayoutCell> cells)
    {
        if (count <= 1)
        {
            AddCell(cells, 0, area, radius);
            return;
        }

        bool landscape = area.Width >= area.Height;
        double fraction = Math.Clamp(spec.HeroFraction, 0.3, 0.8);
        int rest = count - 1;

        Rect hero;
        Rect side;

        if (landscape)
        {
            double heroW = Math.Max(1, (area.Width - gap) * fraction);
            if (spec.HeroOnLeft)
            {
                hero = new Rect(area.X, area.Y, heroW, area.Height);
                side = new Rect(area.X + heroW + gap, area.Y, Math.Max(1, area.Width - heroW - gap), area.Height);
            }
            else
            {
                double sideW = Math.Max(1, area.Width - heroW - gap);
                side = new Rect(area.X, area.Y, sideW, area.Height);
                hero = new Rect(area.X + sideW + gap, area.Y, heroW, area.Height);
            }
        }
        else
        {
            double heroH = Math.Max(1, (area.Height - gap) * fraction);
            if (spec.HeroOnLeft)
            {
                hero = new Rect(area.X, area.Y, area.Width, heroH);
                side = new Rect(area.X, area.Y + heroH + gap, area.Width, Math.Max(1, area.Height - heroH - gap));
            }
            else
            {
                double sideH = Math.Max(1, area.Height - heroH - gap);
                side = new Rect(area.X, area.Y, area.Width, sideH);
                hero = new Rect(area.X, area.Y + sideH + gap, area.Width, heroH);
            }
        }

        AddCell(cells, 0, hero, radius);

        int sideCols = spec.SideColumns > 0
            ? Math.Min(spec.SideColumns, rest)
            : Math.Max(1, (int)Math.Round(Math.Sqrt(rest * side.Height / Math.Max(1, side.Width))));
        sideCols = Math.Clamp(sideCols, 1, rest);

        int sideRows = (int)Math.Ceiling(rest / (double)sideCols);
        double rowH = (side.Height - gap * (sideRows - 1)) / sideRows;
        double y = side.Y;
        int index = 1;

        for (int r = 0; r < sideRows; r++)
        {
            int c = Math.Min(sideCols, rest - (index - 1));
            if (c <= 0) break;

            double cellW = (side.Width - gap * (c - 1)) / c;
            double x = side.X;
            for (int i = 0; i < c; i++)
            {
                AddCell(cells, index++, new Rect(x, y, cellW, rowH), radius);
                x += cellW + gap;
            }
            y += rowH + gap;
        }
    }

    private static void BuildBanner(LayoutSpec spec, int count, Rect area, double gap, double radius, List<LayoutCell> cells)
    {
        double aspect = area.Width / Math.Max(1, area.Height);
        int rows = SuggestRows(count, aspect);

        if (count < 6 || rows < 3)
        {
            BuildWeighted(spec, count, area, gap, radius, cells);
            return;
        }

        rows = Math.Min(rows, count);
        int baseCols = (int)Math.Ceiling(count / (double)rows);
        var counts = new int[rows];
        for (int i = 0; i < rows; i++) counts[i] = baseCols;

        if (baseCols <= 4)
        {
            var bannerRows = new List<int>();
            for (int i = 0; i < rows; i += 3) bannerRows.Add(i);
            foreach (int i in bannerRows) counts[i] = 1;

            int extras = bannerRows.Sum(i => baseCols - 1);
            int cursor = 0;
            while (extras > 0 && cursor < 1000)
            {
                int i = cursor % rows;
                if (!bannerRows.Contains(i))
                {
                    counts[i]++;
                    extras--;
                }
                cursor++;
            }
        }

        int diff = count - counts.Sum();
        int guard = 0;
        while (diff != 0 && guard++ < 5000)
        {
            int i = guard % rows;
            if (diff > 0)
            {
                counts[i]++;
                diff--;
            }
            else if (counts[i] > 1)
            {
                counts[i]--;
                diff++;
            }
        }
        counts[^1] += count - counts.Sum();

        double rowH = (area.Height - gap * (rows - 1)) / rows;
        double y = area.Y;
        int index = 0;

        for (int r = 0; r < rows; r++)
        {
            int c = Math.Max(1, counts[r]);
            double cellW = (area.Width - gap * (c - 1)) / c;
            double x = area.X;
            for (int i = 0; i < c && index < count; i++)
            {
                AddCell(cells, index++, new Rect(x, y, cellW, rowH), radius);
                x += cellW + gap;
            }
            y += rowH + gap;
        }
    }

    private static void BuildWeighted(LayoutSpec spec, int count, Rect area, double gap, double radius, List<LayoutCell> cells)
    {
        if (count <= 1)
        {
            AddCell(cells, 0, area, radius);
            return;
        }

        double aspect = area.Width / Math.Max(1, area.Height);
        int rows = SuggestRows(count, aspect);
        rows = Math.Clamp(rows, 2, count);

        double[] pattern = { 1.35, 0.8, 1.0, 1.1, 0.9 };
        var weights = new double[rows];
        for (int i = 0; i < rows; i++) weights[i] = pattern[i % pattern.Length];

        double weightSum = weights.Sum();
        double availableH = area.Height - gap * (rows - 1);
        var heights = new double[rows];
        for (int i = 0; i < rows; i++) heights[i] = availableH * weights[i] / weightSum;

        var raw = new double[rows];
        for (int i = 0; i < rows; i++) raw[i] = area.Width / Math.Max(1, heights[i]);
        double rawSum = raw.Sum();

        var counts = new int[rows];
        for (int i = 0; i < rows; i++)
            counts[i] = Math.Max(1, (int)Math.Round(raw[i] * count / Math.Max(1, rawSum)));

        int diff = count - counts.Sum();
        int guard = 0;
        while (diff != 0 && guard++ < 5000)
        {
            int i = guard % rows;
            if (diff > 0)
            {
                counts[i]++;
                diff--;
            }
            else if (counts[i] > 1)
            {
                counts[i]--;
                diff++;
            }
        }
        counts[^1] += count - counts.Sum();

        double y = area.Y;
        int index = 0;

        for (int r = 0; r < rows; r++)
        {
            int c = Math.Max(1, counts[r]);
            double cellW = (area.Width - gap * (c - 1)) / c;
            double x = area.X;
            for (int i = 0; i < c && index < count; i++)
            {
                AddCell(cells, index++, new Rect(x, y, cellW, heights[r]), radius);
                x += cellW + gap;
            }
            y += heights[r] + gap;
        }
    }

    private static void AddCell(List<LayoutCell> cells, int index, Rect rect, double radius)
    {
        double r = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2);
        cells.Add(new LayoutCell(index, rect, Math.Max(0, r)));
    }

    /// <summary>
    /// 按预设的归一化单元格排布。间距只作用在相邻内部边界上（外边界保持画布外边距），
    /// 因此无论版式多复杂都不会出现错位、空洞或双重留白。
    /// </summary>
    private static void BuildPreset(LayoutSpec spec, int count, Rect area, double gap, double radius, List<LayoutCell> cells)
    {
        var definition = spec.Definition;
        if (definition is null || definition.Tiles.Count == 0)
        {
            BuildUniform(spec, count, area, gap, radius, cells);
            return;
        }

        int limit = Math.Min(count, definition.Tiles.Count);
        double half = gap / 2;

        for (int i = 0; i < limit; i++)
        {
            var tile = definition.Tiles[i];

            double left = area.X + tile.X * area.Width;
            double top = area.Y + tile.Y * area.Height;
            double right = area.X + tile.Right * area.Width;
            double bottom = area.Y + tile.Bottom * area.Height;

            if (tile.X > 0.0005) left += half;
            if (tile.Y > 0.0005) top += half;
            if (tile.Right < 0.9995) right -= half;
            if (tile.Bottom < 0.9995) bottom -= half;

            var rect = new Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
            AddCell(cells, i, rect, radius);
        }
    }
}

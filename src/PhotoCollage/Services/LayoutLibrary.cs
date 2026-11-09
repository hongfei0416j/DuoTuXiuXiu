using DuoTuXiuXiu.Models;

namespace DuoTuXiuXiu.Services;

/// <summary>
/// 数据驱动版式库。
/// 每个版式都由一组 0~1 归一化单元格（LayoutTile）描述，渲染时统一交给 LayoutEngine
/// 换算像素并叠加间距 / 外边距 / 圆角，所以新增版式只需在这里追加定义，
/// 缩略图、实时预览与导出会自动同步，不需要改渲染或界面代码。
/// </summary>
public static class LayoutLibrary
{
    public const int MinPresetCount = 2;
    public const int MaxPresetCount = 100;

    private static readonly Dictionary<int, IReadOnlyList<LayoutPreset>> Cache = new();
    private static readonly Dictionary<string, LayoutPreset> ById = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    /// <summary>某个张数下的全部版式（已按几何去重）。</summary>
    public static IReadOnlyList<LayoutPreset> AllForCount(int count)
    {
        count = Math.Clamp(count, MinPresetCount, MaxPresetCount);
        lock (Gate)
        {
            if (Cache.TryGetValue(count, out var cached)) return cached;

            var presets = Build(count);
            Cache[count] = presets;
            foreach (var preset in presets) ById[preset.Id] = preset;
            return presets;
        }
    }

    /// <summary>某个张数、某个分类（custom / asymmetric）下的版式。</summary>
    public static IReadOnlyList<LayoutPreset> PresetsForCount(int count, string category)
    {
        var all = AllForCount(count);
        var filtered = new List<LayoutPreset>(all.Count);
        foreach (var preset in all)
        {
            if (string.Equals(preset.Category, category, StringComparison.Ordinal))
                filtered.Add(preset);
        }

        return filtered.Count > 0 ? filtered : all;
    }

    public static LayoutPreset? Find(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        lock (Gate)
        {
            if (ById.TryGetValue(id, out var cached)) return cached;

            // id 形如 p12-03，前缀即所属张数；按需构建该档位后再查。
            if (id.Length > 2 && id[0] == 'p')
            {
                int dash = id.IndexOf('-');
                if (dash > 1 && int.TryParse(id.AsSpan(1, dash - 1), out int count))
                {
                    AllForCount(count);
                    if (ById.TryGetValue(id, out var preset)) return preset;
                }
            }
        }

        return null;
    }

    // ===================== 生成 =====================

    private static List<LayoutPreset> Build(int count)
    {
        var result = new List<LayoutPreset>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string name, string description, string category, TileBuilder builder)
        {
            var tiles = builder.Tiles;
            if (tiles.Count != count || !IsValid(tiles)) return;

            string signature = Signature(tiles);
            if (!seen.Add(signature)) return;

            string id = $"p{count}-{result.Count:00}";
            var definition = new LayoutDefinition(id, name, description, category, count, tiles.ToArray());
            result.Add(new LayoutPreset(
                id,
                name,
                description,
                new LayoutSpec(CollageLayoutKind.Preset, Definition: definition),
                count,
                category));
        }

        // ---------- 均匀等分 ----------
        Add($"横向 {count} 条", "等高横条自上而下铺满", "custom",
            new TileBuilder().Rows(0, 0, 1, 1, RepeatOne(count)));

        Add($"竖向 {count} 条", "等宽竖条自左向右铺满", "custom",
            new TileBuilder().Cols(0, 0, 1, 1, RepeatOne(count)));

        var rowCandidates = new SortedSet<int>
        {
            2,
            3,
            4,
            count - 1,
            (int)Math.Round(Math.Sqrt(count)),
            (int)Math.Ceiling(Math.Sqrt(count)),
            (int)Math.Ceiling(count / 2.0)
        };

        foreach (int rows in rowCandidates)
        {
            if (rows < 2 || rows >= count) continue;
            Add($"{rows} 行均分", $"{rows} 行等高，每行按张数均分", "custom",
                new TileBuilder().Rows(0, 0, 1, 1, Balanced(count, rows)));
        }

        // ---------- 大图 + 小图 ----------
        double heroFraction = count <= 3 ? 0.66 : 0.62;
        Add("左大图", "左侧整列主图 + 右侧小图网格", "asymmetric", HeroVertical(count, true, heroFraction, 0));
        Add("右大图", "右侧整列主图 + 左侧小图网格", "asymmetric", HeroVertical(count, false, heroFraction, 0));
        Add("上大图", "顶部通栏主图 + 下方小图网格", "asymmetric", HeroHorizontal(count, true, 0.58, 0));
        Add("下大图", "底部通栏主图 + 上方小图网格", "asymmetric", HeroHorizontal(count, false, 0.58, 0));

        if (count >= 4)
        {
            Add("左宽主图", "左侧宽幅主图，右侧竖排小图", "asymmetric", HeroVertical(count, true, 0.4, 0));
            Add("上宽主图", "顶部宽幅主图，下方横排小图", "asymmetric", HeroHorizontal(count, true, 0.4, 0));
        }

        // ---------- 双主图 / 侧栏 ----------
        if (count >= 4)
            Add("左列双大图", "左侧两张竖向大图 + 右侧小图网格", "asymmetric", TwoBigColumn(count, true));

        if (count >= 4)
            Add("右列双大图", "右侧两张竖向大图 + 左侧小图网格", "asymmetric", TwoBigColumn(count, false));

        if (count >= 7)
            Add("侧栏大图", "一侧整列大图 + 另一侧多行网格", "asymmetric", SideColumn(count));

        // ---------- 中心环绕 ----------
        if (count is 5 or 7 or 9)
            Add("中心大图", "中心主图 + 四周环绕小图", "asymmetric", CenterBig(count));

        // ---------- 横幅 / 杂志 / 错位 ----------
        if (count >= 6)
            Add("横幅混排", "整行横幅与多图行交替", "asymmetric", BannerRows(count));

        Add("杂志错落", "行高错落，横竖图混排", "asymmetric", MagazineRows(count));

        if (count >= 6)
            Add("L 形拼贴", "左上大图 + 右下分区，形成 L 形节奏", "asymmetric", LShape(count));

        if (count >= 4)
            Add("错位拼贴", "列宽不均的错落分行", "asymmetric", StaggeredRows(count));

        Add("拍立得", "留白相框风，图片之间露出画布底色", "asymmetric", Polaroid(count));

        return result;
    }

    // ===================== 版式生成器 =====================

    private static IReadOnlyList<int> RepeatOne(int count)
    {
        var counts = new int[count];
        Array.Fill(counts, 1);
        return counts;
    }

    private static int[] Balanced(int count, int rows)
    {
        rows = Math.Clamp(rows, 1, Math.Max(1, count));
        var counts = new int[rows];
        int baseCount = count / rows;
        int remainder = count % rows;
        for (int i = 0; i < rows; i++) counts[i] = baseCount + (i < remainder ? 1 : 0);
        return counts;
    }

    private static TileBuilder HeroVertical(int count, bool heroLeft, double fraction, int sideColumns)
    {
        fraction = Math.Clamp(fraction, 0.2, 0.8);
        int rest = count - 1;
        double sideWidth = 1 - fraction;
        int columns = sideColumns > 0
            ? sideColumns
            : Math.Clamp((int)Math.Round(Math.Sqrt(rest * sideWidth)), 1, rest);

        var builder = new TileBuilder();
        builder.Add(heroLeft ? 0 : 1 - fraction, 0, fraction, 1);
        builder.Grid(heroLeft ? fraction : 0, 0, sideWidth, 1, rest, columns);
        return builder;
    }

    private static TileBuilder HeroHorizontal(int count, bool heroTop, double fraction, int sideColumns)
    {
        fraction = Math.Clamp(fraction, 0.2, 0.8);
        int rest = count - 1;
        double sideHeight = 1 - fraction;
        int columns = sideColumns > 0
            ? sideColumns
            : Math.Clamp((int)Math.Round(Math.Sqrt(rest / Math.Max(0.05, sideHeight))), 1, rest);

        var builder = new TileBuilder();
        builder.Add(0, heroTop ? 0 : 1 - fraction, 1, fraction);
        builder.Grid(0, heroTop ? fraction : 0, 1, sideHeight, rest, columns);
        return builder;
    }

    private static TileBuilder TwoBigColumn(int count, bool onLeft)
    {
        const double fraction = 0.55;
        int rest = count - 2;
        int columns = rest >= 4 ? 2 : 1;
        double sideX = onLeft ? fraction : 0;
        double bigX = onLeft ? 0 : 1 - fraction;

        var builder = new TileBuilder();
        builder.Add(bigX, 0, fraction, 0.5);
        builder.Add(bigX, 0.5, fraction, 0.5);
        builder.Grid(sideX, 0, 1 - fraction, 1, rest, columns);
        return builder;
    }

    private static TileBuilder SideColumn(int count)
    {
        const double fraction = 0.34;
        int rest = count - 1;
        int columns = rest >= 9 ? 3 : rest >= 4 ? 2 : 1;

        var builder = new TileBuilder();
        builder.Add(0, 0, fraction, 1);
        builder.Grid(fraction, 0, 1 - fraction, 1, rest, columns);
        return builder;
    }

    private static TileBuilder CenterBig(int count)
    {
        int band = (count - 3) / 2;
        double size = count switch { 5 => 0.44, 7 => 0.4, _ => 0.36 };
        double left = (1 - size) / 2;
        double top = (1 - size) / 2;

        var builder = new TileBuilder();
        builder.Add(left, top, size, size);

        for (int i = 0; i < band; i++)
        {
            builder.Add(i / (double)band, 0, 1.0 / band, top);
            builder.Add(i / (double)band, top + size, 1.0 / band, 1 - top - size);
        }

        builder.Add(0, top, left, size);
        builder.Add(left + size, top, left, size);
        return builder;
    }

    private static TileBuilder BannerRows(int count)
    {
        int rest = count - 2;
        int half = rest / 2;
        var counts = new[] { 1, half, 1, rest - half };
        var weights = new[] { 1.45, 0.9, 1.45, 0.9 };
        return new TileBuilder().Rows(0, 0, 1, 1, counts, weights);
    }

    private static TileBuilder MagazineRows(int count)
    {
        int rows = Math.Clamp((int)Math.Round(Math.Sqrt(count)), 2, 5);
        double[] pattern = { 1.45, 0.85, 1.2, 0.95, 1.05 };
        var weights = new double[rows];
        for (int i = 0; i < rows; i++) weights[i] = pattern[i % pattern.Length];
        return new TileBuilder().Rows(0, 0, 1, 1, Balanced(count, rows), weights);
    }

    private static TileBuilder LShape(int count)
    {
        int rightCount = Math.Max(1, (count - 1) / 2);
        int bottomLeft = count - 1 - rightCount;

        var builder = new TileBuilder();
        builder.Add(0, 0, 0.58, 0.56);
        builder.Rows(0, 0.56, 0.58, 0.44, new[] { bottomLeft });
        builder.Rows(0.58, 0, 0.42, 1, new[] { rightCount });
        return builder;
    }

    private static TileBuilder StaggeredRows(int count)
    {
        int rows = Math.Clamp((int)Math.Round(count / 2.0), 2, 4);
        var counts = Balanced(count, rows);
        var columnWeights = new double[rows][];
        for (int r = 0; r < rows; r++)
        {
            if (counts[r] == 2)
                columnWeights[r] = r % 2 == 0 ? new[] { 0.64, 0.36 } : new[] { 0.36, 0.64 };
            else if (counts[r] == 3)
                columnWeights[r] = r % 2 == 0 ? new[] { 0.42, 0.33, 0.25 } : new[] { 0.25, 0.33, 0.42 };
            else
                columnWeights[r] = Array.Empty<double>();
        }

        return new TileBuilder().Rows(0, 0, 1, 1, counts, null, columnWeights);
    }

    private static TileBuilder Polaroid(int count)
    {
        int rows = Math.Clamp((int)Math.Round(Math.Sqrt(count)), 2, 6);
        return new TileBuilder()
            .Rows(0, 0, 1, 1, Balanced(count, rows))
            .Inset(0.014);
    }

    // ===================== 工具 =====================

    private static bool IsValid(List<LayoutTile> tiles)
    {
        foreach (var tile in tiles)
        {
            if (tile.W <= 0.001 || tile.H <= 0.001) return false;
            if (tile.X < -0.001 || tile.Y < -0.001) return false;
            if (tile.Right > 1.001 || tile.Bottom > 1.001) return false;
        }
        return true;
    }

    private static string Signature(List<LayoutTile> tiles)
    {
        var parts = new string[tiles.Count];
        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            parts[i] = $"{t.X:F3},{t.Y:F3},{t.W:F3},{t.H:F3}";
        }
        Array.Sort(parts, StringComparer.Ordinal);
        return string.Join(';', parts);
    }

    /// <summary>轻量单元格构建器：只记录归一化矩形，所有间距在渲染阶段统一处理。</summary>
    private sealed class TileBuilder
    {
        public List<LayoutTile> Tiles { get; } = new();

        public TileBuilder Add(double x, double y, double w, double h)
        {
            if (w > 0.0005 && h > 0.0005) Tiles.Add(new LayoutTile(x, y, w, h));
            return this;
        }

        public TileBuilder Rows(
            double x, double y, double w, double h,
            IReadOnlyList<int> counts,
            double[]? weights = null,
            double[][]? columnWeights = null)
        {
            int rows = counts.Count;
            if (rows == 0) return this;

            var rowWeights = weights ?? Enumerable.Repeat(1.0, rows).ToArray();
            double weightSum = Math.Max(1e-6, rowWeights.Sum());
            double cursor = y;

            for (int r = 0; r < rows; r++)
            {
                int columns = Math.Max(1, counts[r]);
                double rowHeight = h * rowWeights[r] / weightSum;

                if (columnWeights is not null && r < columnWeights.Length
                    && columnWeights[r] is { Length: > 0 } cw && cw.Length == columns)
                {
                    double cwSum = Math.Max(1e-6, cw.Sum());
                    double columnCursor = x;
                    for (int i = 0; i < columns; i++)
                    {
                        double cellWidth = w * cw[i] / cwSum;
                        Add(columnCursor, cursor, cellWidth, rowHeight);
                        columnCursor += cellWidth;
                    }
                }
                else
                {
                    for (int i = 0; i < columns; i++)
                        Add(x + w * i / columns, cursor, w / columns, rowHeight);
                }

                cursor += rowHeight;
            }

            return this;
        }

        public TileBuilder Cols(double x, double y, double w, double h, IReadOnlyList<int> counts)
        {
            int columns = counts.Count;
            if (columns == 0) return this;
            double columnWidth = w / columns;

            for (int c = 0; c < columns; c++)
            {
                int cells = Math.Max(1, counts[c]);
                for (int i = 0; i < cells; i++)
                    Add(x + columnWidth * c, y + h * i / cells, columnWidth, h / cells);
            }

            return this;
        }

        public TileBuilder Grid(double x, double y, double w, double h, int count, int columns)
        {
            if (count <= 0) return this;
            columns = Math.Clamp(columns, 1, count);
            int rows = (int)Math.Ceiling(count / (double)columns);
            return Rows(x, y, w, h, Balanced(count, rows));
        }

        public TileBuilder Inset(double amount)
        {
            for (int i = 0; i < Tiles.Count; i++)
            {
                var tile = Tiles[i];
                Tiles[i] = new LayoutTile(
                    tile.X + amount,
                    tile.Y + amount,
                    tile.W - amount * 2,
                    tile.H - amount * 2);
            }

            return this;
        }
    }
}

using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace DuoTuXiuXiu.Services;

/// <summary>
/// 首次启动时生成的占位示例图，避免空白画布。
/// </summary>
public static class SampleImageFactory
{
    private static readonly (string A, string B)[] Palettes =
    {
        ("#7C5CFF", "#22D3EE"),
        ("#FF7A8A", "#FFB86B"),
        ("#22C1A8", "#0EA5E9"),
        ("#F59E0B", "#EF4444"),
        ("#6366F1", "#EC4899"),
        ("#10B981", "#84CC16")
    };

    private static readonly (int Width, int Height)[] Sizes =
    {
        (1200, 900), (900, 1200), (1000, 1000), (1200, 800), (800, 1200), (1000, 760)
    };

    public static IReadOnlyList<Bitmap> CreateSamples()
    {
        var result = new List<Bitmap>(Sizes.Length);
        for (int i = 0; i < Sizes.Length; i++)
            result.Add(Create(i, Sizes[i].Width, Sizes[i].Height));
        return result;
    }

    private static Bitmap Create(int index, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            var palette = Palettes[index % Palettes.Length];
            var gradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
            };
            gradient.GradientStops.Add(new GradientStop(Color.Parse(palette.A), 0));
            gradient.GradientStops.Add(new GradientStop(Color.Parse(palette.B), 1));
            context.FillRectangle(gradient, new Rect(0, 0, width, height));

            double unit = Math.Min(width, height);
            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)), null,
                new Point(width * 0.82, height * 0.18), unit * 0.30, unit * 0.30);
            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), null,
                new Point(width * 0.12, height * 0.88), unit * 0.38, unit * 0.38);
            context.DrawEllipse(
                null,
                new Pen(new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)), Math.Max(2, unit * 0.004)),
                new Point(width * 0.5, height * 0.5), unit * 0.34, unit * 0.34);

            var number = new FormattedText(
                $"{index + 1:00}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold),
                unit * 0.30,
                Brushes.White);
            context.DrawText(number, new Point(width / 2 - number.Width / 2, height / 2 - number.Height / 2 - unit * 0.05));

            var label = new FormattedText(
                "SAMPLE",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
                unit * 0.055,
                new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)));
            context.DrawText(label, new Point(width / 2 - label.Width / 2, height / 2 + unit * 0.14));
        }

        return bitmap;
    }
}

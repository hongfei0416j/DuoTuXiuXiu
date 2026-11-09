using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DuoTuXiuXiu.Models;

namespace DuoTuXiuXiu.Services;

/// <summary>
/// 纯绘制逻辑：把场景画到任意 DrawingContext 上。预览与导出共用同一套渲染。
/// </summary>
public static class CollageRenderer
{
    private static readonly IBrush PlaceholderFill = new SolidColorBrush(Color.Parse("#E7EAF1"));
    private static readonly IBrush PlaceholderStroke = new SolidColorBrush(Color.Parse("#B9BFCD"));

    public static void Render(DrawingContext context, Rect bounds, CollageScene scene)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (scene.BackgroundImage is { } background)
        {
            using (context.PushClip(new RoundedRect(bounds)))
                DrawCover(context, background, bounds);
        }
        else
        {
            context.FillRectangle(new SolidColorBrush(scene.Background), bounds);
        }

        // 间距 / 外边距 / 圆角以画布像素为单位，按目标绘制尺寸等比缩放，
        // 保证预览、1x、2x、3x 导出的视觉比例完全一致。
        double scale = scene.BaseWidth > 0 ? bounds.Width / scene.BaseWidth : 1;
        var cells = LayoutEngine.Build(scene.Spec, scene.Images.Count, bounds.Size,
            scene.Gap * scale, scene.Padding * scale, scene.CornerRadius * scale);

        using (context.PushTransform(Matrix.CreateTranslation(bounds.X, bounds.Y)))
        {
            foreach (var cell in cells)
            {
                if (cell.Index < 0 || cell.Index >= scene.Images.Count) continue;
                var image = scene.Images[cell.Index];

                if (image.Bitmap is null)
                {
                    DrawPlaceholder(context, cell);
                    continue;
                }

                using (context.PushClip(new RoundedRect(cell.Bounds, cell.CornerRadius)))
                    DrawPhoto(context, image, cell.Bounds, scene.FillMode);
            }
        }
    }

    private static void DrawPhoto(DrawingContext context, CollageImage image, Rect cell, CellFillMode fillMode)
    {
        var bitmap = image.Bitmap;
        if (bitmap is null) return;

        double imageWidth = bitmap.PixelSize.Width;
        double imageHeight = bitmap.PixelSize.Height;
        if (imageWidth <= 0 || imageHeight <= 0) return;

        double scale = fillMode == CellFillMode.Crop
            ? Math.Max(cell.Width / imageWidth, cell.Height / imageHeight)
            : Math.Min(cell.Width / imageWidth, cell.Height / imageHeight);

        scale *= Math.Clamp(image.Zoom, 0.2, 6);

        double width = imageWidth * scale;
        double height = imageHeight * scale;

        double cx = cell.Center.X + image.OffsetX * cell.Width;
        double cy = cell.Center.Y + image.OffsetY * cell.Height;

        var destination = new Rect(cx - width / 2, cy - height / 2, width, height);
        context.DrawImage(bitmap, new Rect(0, 0, imageWidth, imageHeight), destination);
    }

    private static void DrawCover(DrawingContext context, Bitmap bitmap, Rect bounds)
    {
        double iw = bitmap.PixelSize.Width;
        double ih = bitmap.PixelSize.Height;
        if (iw <= 0 || ih <= 0) return;

        double scale = Math.Max(bounds.Width / iw, bounds.Height / ih);
        double w = iw * scale;
        double h = ih * scale;
        var dest = new Rect(bounds.Center.X - w / 2, bounds.Center.Y - h / 2, w, h);
        context.DrawImage(bitmap, new Rect(0, 0, iw, ih), dest);
    }

    private static void DrawPlaceholder(DrawingContext context, LayoutCell cell)
    {
        var rect = cell.Bounds;
        context.DrawRectangle(PlaceholderFill, null, new RoundedRect(rect, cell.CornerRadius));
        context.DrawRectangle(null, new Pen(PlaceholderStroke, 1, new DashStyle(new double[] { 5, 4 }, 0)),
            new RoundedRect(rect.Deflate(1), Math.Max(0, cell.CornerRadius - 1)));

        double size = Math.Min(rect.Width, rect.Height) * 0.12;
        if (size < 3) return;

        var pen = new Pen(PlaceholderStroke, Math.Max(1.2, size * 0.14), lineCap: PenLineCap.Round);
        var center = rect.Center;
        context.DrawLine(pen, new Point(center.X - size, center.Y), new Point(center.X + size, center.Y));
        context.DrawLine(pen, new Point(center.X, center.Y - size), new Point(center.X, center.Y + size));
    }
}

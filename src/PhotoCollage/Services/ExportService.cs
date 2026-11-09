using Avalonia;
using Avalonia.Media.Imaging;
using DuoTuXiuXiu.Models;
using SkiaSharp;

namespace DuoTuXiuXiu.Services;

public static class ExportService
{
    public static RenderTargetBitmap Render(CollageScene scene, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
            CollageRenderer.Render(context, new Rect(0, 0, width, height), scene);
        return bitmap;
    }

    public static void SavePng(Bitmap bitmap, string path) => bitmap.Save(path);

    public static void SaveJpeg(Bitmap bitmap, string path, int quality)
    {
        using var png = new MemoryStream();
        bitmap.Save(png);

        using var skBitmap = SKBitmap.Decode(png.ToArray())
            ?? throw new InvalidOperationException("无法解码渲染结果");
        using var image = SKImage.FromBitmap(skBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, Math.Clamp(quality, 30, 100))
            ?? throw new InvalidOperationException("无法编码 JPG");
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace DuoTuXiuXiu.Services;

/// <summary>图片读取失败（格式不支持、文件损坏、无权限等）时抛出，消息可直接展示给用户。</summary>
public sealed class ImageLoadException : Exception
{
    public ImageLoadException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>
/// 图片解码。统一先把文件读成 byte[]，再交给 SkiaSharp 解码，
/// 避免出现「创建 SKCodec 后复用同一个文件流」导致流被释放的问题。
/// 解码结果直接拷贝为 Avalonia 的 WriteableBitmap（不再二次编码 / 二次解码），
/// 是该路径上最省时的一步。
/// </summary>
public static class ImageLoader
{
    /// <summary>解码后最长边上限，避免超大图占用过多内存。</summary>
    private const int MaxSide = 2400;

    private static readonly string[] Extensions =
        { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".gif", ".tif", ".tiff" };

    public static bool IsSupported(string path) =>
        Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static Task<Bitmap> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => Load(path, cancellationToken), cancellationToken);

    public static Bitmap Load(string path, CancellationToken cancellationToken = default)
    {
        var name = Path.GetFileName(path);

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            throw new ImageLoadException($"无法读取文件：{name}", ex);
        }

        if (bytes.Length == 0)
            throw new ImageLoadException($"文件内容为空：{name}");

        try
        {
            return Decode(bytes, cancellationToken);
        }
        catch (ImageLoadException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ImageLoadException($"图片格式不支持或文件已损坏：{name}", ex);
        }
    }

    private static Bitmap Decode(byte[] bytes, CancellationToken cancellationToken)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new ImageLoadException("无法识别的图片格式");

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0)
            throw new ImageLoadException("图片尺寸无效");

        // 按最长边等比缩小后再解码，兼顾清晰度与内存占用。
        float scale = Math.Min(1f, MaxSide / (float)Math.Max(info.Width, info.Height));
        var size = codec.GetScaledDimensions(scale);
        if (size.Width <= 0 || size.Height <= 0)
            size = new SKSizeI(info.Width, info.Height);

        var targetInfo = new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var decoded = new SKBitmap(targetInfo);
        var result = codec.GetPixels(targetInfo, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            throw new ImageLoadException($"图片解码失败（{result}）");

        cancellationToken.ThrowIfCancellationRequested();

        // EXIF 方向为正时省掉整张位图的复制。
        if (codec.EncodedOrigin == SKEncodedOrigin.TopLeft)
            return ToBitmap(decoded, cancellationToken);

        using var oriented = ApplyExifOrientation(decoded, codec.EncodedOrigin);
        return ToBitmap(oriented, cancellationToken);
    }

    /// <summary>
    /// 直接按行拷贝 BGRA 像素到 WriteableBitmap，跳过「编码成 PNG / JPEG → Avalonia 再解码」，
    /// 大图加载可快数倍。
    /// </summary>
    private static unsafe Bitmap ToBitmap(SKBitmap source, CancellationToken cancellationToken)
    {
        var info = source.Info;
        var bitmap = new WriteableBitmap(
            new PixelSize(info.Width, info.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using var frame = bitmap.Lock();
        int rowBytes = Math.Min(source.RowBytes, frame.RowBytes);
        byte* src = (byte*)source.GetPixels();
        byte* dst = (byte*)frame.Address;

        for (int y = 0; y < info.Height; y++)
        {
            if ((y & 0x3F) == 0) cancellationToken.ThrowIfCancellationRequested();
            Buffer.MemoryCopy(
                src + (long)y * source.RowBytes,
                dst + (long)y * frame.RowBytes,
                frame.RowBytes,
                rowBytes);
        }

        return bitmap;
    }

    /// <summary>按 EXIF 方向把像素摆正（手机竖拍照片通常为 6 / 8）。</summary>
    private static SKBitmap ApplyExifOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
            return source.Copy();

        bool swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        int width = swap ? source.Height : source.Width;
        int height = swap ? source.Width : source.Height;

        var output = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(output);
        canvas.Clear(SKColors.Transparent);

        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(width, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(width, height);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, height);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.Translate(0, height);
                canvas.Scale(1, -1);
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(width, 0);
                canvas.Scale(-1, 1);
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, height);
                canvas.RotateDegrees(270);
                break;
        }

        canvas.DrawBitmap(source, 0, 0);
        canvas.Flush();
        return output;
    }
}

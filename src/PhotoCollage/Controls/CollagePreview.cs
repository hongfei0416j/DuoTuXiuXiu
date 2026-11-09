using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DuoTuXiuXiu.Models;
using DuoTuXiuXiu.Services;

namespace DuoTuXiuXiu.Controls;

/// <summary>
/// 拼图实时预览控件（自绘，非布局拼贴）。支持缩放 / 旋转 / 水平翻转。
/// </summary>
public class CollagePreview : Control
{
    public static readonly StyledProperty<CollageScene?> SceneProperty =
        AvaloniaProperty.Register<CollagePreview, CollageScene?>(nameof(Scene));

    public static readonly StyledProperty<double> ViewZoomProperty =
        AvaloniaProperty.Register<CollagePreview, double>(nameof(ViewZoom), 1);

    public static readonly StyledProperty<double> ViewRotationProperty =
        AvaloniaProperty.Register<CollagePreview, double>(nameof(ViewRotation));

    public static readonly StyledProperty<bool> FlipHorizontalProperty =
        AvaloniaProperty.Register<CollagePreview, bool>(nameof(FlipHorizontal));

    public static readonly StyledProperty<bool> ViewFillProperty =
        AvaloniaProperty.Register<CollagePreview, bool>(nameof(ViewFill));

    private static readonly IBrush ShadowBrush = new SolidColorBrush(Colors.Transparent);
    private static readonly IBrush BorderBrush = new SolidColorBrush(Color.Parse("#DADDE5"));
    private static readonly IPen BorderPen = new Pen(BorderBrush, 1);

    static CollagePreview()
    {
        AffectsRender<CollagePreview>(SceneProperty, ViewZoomProperty, ViewRotationProperty, FlipHorizontalProperty, ViewFillProperty);
    }

    public CollagePreview()
    {
        ClipToBounds = true;
    }

    public CollageScene? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public double ViewZoom
    {
        get => GetValue(ViewZoomProperty);
        set => SetValue(ViewZoomProperty, value);
    }

    public double ViewRotation
    {
        get => GetValue(ViewRotationProperty);
        set => SetValue(ViewRotationProperty, value);
    }

    public bool FlipHorizontal
    {
        get => GetValue(FlipHorizontalProperty);
        set => SetValue(FlipHorizontalProperty, value);
    }

    public bool ViewFill
    {
        get => GetValue(ViewFillProperty);
        set => SetValue(ViewFillProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var scene = Scene;
        if (scene is null) return;

        double availableWidth = Bounds.Width;
        double availableHeight = Bounds.Height;
        if (availableWidth <= 20 || availableHeight <= 20) return;

        const double margin = 30;
        double aspect = scene.AspectRatio > 0 ? scene.AspectRatio : 1;
        double width = Math.Max(1, availableWidth - margin * 2);
        double height = width / aspect;
        if (height > availableHeight - margin * 2)
        {
            height = Math.Max(1, availableHeight - margin * 2);
            width = height * aspect;
        }

        if (ViewFill)
        {
            double fillScale = Math.Max(
                (availableWidth - margin * 2) / width,
                (availableHeight - margin * 2) / height);
            width *= fillScale;
            height *= fillScale;
        }

        var canvas = new Rect(
            (availableWidth - width) / 2,
            (availableHeight - height) / 2,
            width,
            height);

        double zoom = Math.Clamp(ViewZoom, 0.2, 4);
        var center = canvas.Center;
        var matrix = Matrix.CreateTranslation(-center.X, -center.Y)
                     * Matrix.CreateScale(FlipHorizontal ? -zoom : zoom, zoom)
                     * Matrix.CreateRotation(ViewRotation * Math.PI / 180)
                     * Matrix.CreateTranslation(center.X, center.Y);

        const double corner = 7;

        using (context.PushTransform(matrix))
        {
            var shadow = new BoxShadows(new BoxShadow
            {
                OffsetY = 10,
                Blur = 34,
                Color = Color.FromArgb(46, 24, 30, 44)
            });
            context.DrawRectangle(ShadowBrush, null, canvas, corner, corner, shadow);

            using (context.PushClip(new RoundedRect(canvas, corner)))
                CollageRenderer.Render(context, canvas, scene);

            context.DrawRectangle(null, BorderPen, canvas, corner, corner);
        }
    }
}

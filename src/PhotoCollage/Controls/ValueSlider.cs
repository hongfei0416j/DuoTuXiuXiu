using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace DuoTuXiuXiu.Controls;

/// <summary>
/// 自绘滑块：统一样式、支持拖动与点击定位，替代默认 Fluent Slider。
/// </summary>
public class ValueSlider : Control
{
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<ValueSlider, double>(nameof(Minimum), 0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<ValueSlider, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ValueSlider, double>(nameof(Value), 0, defaultBindingMode: BindingMode.TwoWay);

    private static readonly IBrush TrackBrush = new SolidColorBrush(Color.Parse("#E4E7EE"));
    private static readonly IBrush ThumbFill = new SolidColorBrush(Colors.White);
    private static readonly IBrush ThumbRing = new SolidColorBrush(Color.Parse("#FF6B81"));
    private static readonly IBrush ThumbHalo = new SolidColorBrush(Color.FromArgb(38, 255, 107, 129));
    private static readonly IBrush FillBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.Parse("#FF9A6C"), 0),
            new GradientStop(Color.Parse("#FF5C8A"), 1)
        }
    };

    private bool _dragging;

    static ValueSlider()
    {
        AffectsRender<ValueSlider>(ValueProperty, MinimumProperty, MaximumProperty);
    }

    public ValueSlider()
    {
        MinHeight = 24;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        _dragging = true;
        e.Pointer.Capture(this);
        UpdateFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        UpdateFromPoint(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 2 || height <= 2) return;

        const double side = 9;
        const double trackHeight = 4;
        double usable = Math.Max(1, width - side * 2);
        double centerY = height / 2;

        double ratio = Maximum > Minimum ? (Value - Minimum) / (Maximum - Minimum) : 0;
        ratio = Math.Clamp(ratio, 0, 1);

        var track = new Rect(side, centerY - trackHeight / 2, usable, trackHeight);
        context.DrawRectangle(TrackBrush, null, track, trackHeight / 2, trackHeight / 2);

        if (ratio > 0.001)
        {
            var fill = new Rect(track.X, track.Y, Math.Max(trackHeight, usable * ratio), trackHeight);
            context.DrawRectangle(FillBrush, null, fill, trackHeight / 2, trackHeight / 2);
        }

        double thumbX = side + usable * ratio;
        context.DrawEllipse(ThumbHalo, null, new Point(thumbX, centerY), 11, 11);
        context.DrawEllipse(ThumbFill, new Pen(ThumbRing, 2.2), new Point(thumbX, centerY), 6.2, 6.2);
    }

    private void UpdateFromPoint(Point point)
    {
        const double side = 9;
        double usable = Math.Max(1, Bounds.Width - side * 2);
        double ratio = Math.Clamp((point.X - side) / usable, 0, 1);
        double value = Minimum + (Maximum - Minimum) * ratio;
        SetCurrentValue(ValueProperty, value);
    }
}

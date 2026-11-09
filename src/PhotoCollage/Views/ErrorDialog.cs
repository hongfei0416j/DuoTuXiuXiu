using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DuoTuXiuXiu.Services;

namespace DuoTuXiuXiu.Views;

/// <summary>
/// 全局未处理异常的兜底提示窗口，避免直接弹出调试器/闪退。
/// </summary>
internal static class ErrorDialog
{
    private static int _opened;

    public static void Show(Exception? exception, string? message = null)
    {
        CrashLog.Write(exception);

        // 连续异常时只保留一个提示窗口
        if (Interlocked.Exchange(ref _opened, 1) == 1) return;

        var detail = exception?.Message ?? "未知错误";

        var window = new Window
        {
            Title = "多图秀秀 · 提示",
            Width = 520,
            Height = 290,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new SolidColorBrush(Color.Parse("#FFFFFF"))
        };

        var copyButton = new Button
        {
            Content = "复制详情",
            Padding = new Thickness(16, 6, 16, 6),
            CornerRadius = new CornerRadius(8)
        };
        copyButton.Click += async (_, _) =>
        {
            var clipboard = window.Clipboard;
            if (clipboard is not null)
                await clipboard.SetTextAsync(exception?.ToString() ?? detail);
        };

        var closeButton = new Button
        {
            Content = "关闭",
            Padding = new Thickness(20, 6, 20, 6),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.Parse("#FF6B81")),
            Foreground = Brushes.White
        };
        closeButton.Click += (_, _) => window.Close();

        var body = new TextBox
        {
            Text = detail,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Height = 96,
            Background = new SolidColorBrush(Color.Parse("#F6F7FA")),
            BorderBrush = new SolidColorBrush(Color.Parse("#E6E8EF")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10)
        };

        window.Content = new StackPanel
        {
            Margin = new Thickness(22, 18, 22, 18),
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = message ?? "处理时出现了一个意外错误",
                    FontSize = 15,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(Color.Parse("#20242E"))
                },
                new TextBlock
                {
                    Text = "已记录到日志：" + CrashLog.FilePath,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.Parse("#8A90A2")),
                    TextWrapping = TextWrapping.Wrap
                },
                body,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { copyButton, closeButton }
                }
            }
        };

        window.Closed += (_, _) => Interlocked.Exchange(ref _opened, 0);
        window.Show();
    }
}

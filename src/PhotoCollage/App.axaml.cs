using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DuoTuXiuXiu.Services;
using DuoTuXiuXiu.ViewModels;
using DuoTuXiuXiu.Views;

namespace DuoTuXiuXiu;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Dispatcher.UIThread.UnhandledException += OnUiThreadUnhandledException;

            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            viewModel.StorageProvider = window.StorageProvider;
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>UI 线程兜底：把异常转成友好提示，而不是让进程直接崩溃。</summary>
    private static void OnUiThreadUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        try
        {
            ErrorDialog.Show(e.Exception);
        }
        catch
        {
            CrashLog.Write(e.Exception, "展示错误提示窗口失败");
        }
    }
}

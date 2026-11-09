using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using DuoTuXiuXiu.ViewModels;

namespace DuoTuXiuXiu.Views;

public partial class MainWindow : Window
{
    private ImageItemViewModel? _dragItem;
    private Point _pressPoint;
    private bool _dragging;

    public MainWindow()
    {
        InitializeComponent();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    // ============ 窗口拖拽 / 按钮 ============

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (e.Source is Visual source &&
            (source.FindAncestorOfType<Button>(true) is not null
             || source.FindAncestorOfType<ToggleButton>(true) is not null
             || source.FindAncestorOfType<ComboBox>(true) is not null))
            return;

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        BeginMoveDrag(e);
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    // ============ 拖入图片 ============

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var files = e.Data.GetFiles();
        if (files is null) return;

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!)
            .ToList();

        if (paths.Count > 0 && DataContext is MainWindowViewModel vm)
            await vm.AddPathsAsync(paths);
    }

    // ============ 缩略图拖动排序 ============

    private void FilmStrip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(FilmStrip).Properties.IsLeftButtonPressed) return;

        var item = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(true);
        if (item?.DataContext is not ImageItemViewModel image) return;

        _dragItem = image;
        _pressPoint = e.GetPosition(FilmStrip);
        _dragging = false;
    }

    private void FilmStrip_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragItem is null) return;

        if (!e.GetCurrentPoint(FilmStrip).Properties.IsLeftButtonPressed)
        {
            ResetFilmDrag();
            return;
        }

        var position = e.GetPosition(FilmStrip);
        if (!_dragging)
        {
            var delta = position - _pressPoint;
            if (Math.Abs(delta.X) + Math.Abs(delta.Y) < 7) return;
            _dragging = true;
        }

        var hit = FilmStrip.InputHitTest(position) as Visual;
        var targetItem = hit?.FindAncestorOfType<ListBoxItem>(true);
        if (targetItem?.DataContext is not ImageItemViewModel target) return;
        if (ReferenceEquals(target, _dragItem)) return;
        if (DataContext is not MainWindowViewModel vm) return;

        int from = vm.Images.IndexOf(_dragItem);
        int to = vm.Images.IndexOf(target);
        if (from < 0 || to < 0) return;

        vm.MoveImage(from, to);
    }

    private void FilmStrip_PointerReleased(object? sender, PointerReleasedEventArgs e) => ResetFilmDrag();

    private void FilmStrip_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => ResetFilmDrag();

    private void ResetFilmDrag()
    {
        _dragItem = null;
        _dragging = false;
    }
}

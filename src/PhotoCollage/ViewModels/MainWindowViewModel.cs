using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DuoTuXiuXiu.Models;
using DuoTuXiuXiu.Services;

namespace DuoTuXiuXiu.ViewModels;

/// <summary>
/// 主窗口状态：图片列表、版式决策（自动 / 手动）、画布参数与导出。
/// 所有参数变化都会重建 Scene，由自绘控件立即重绘。
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    public const int MinCount = 2;
    public const int MaxCount = 100;

    private static readonly Color DefaultBackground = Color.Parse("#F3F4F8");

    private static readonly (string Key, int W, int H)[] RatioPresets =
    {
        ("3:4", 900, 1200),
        ("1:1", 1200, 1200),
        ("4:3", 1200, 900),
        ("2:3", 800, 1200),
        ("4:5", 960, 1200),
        ("3:2", 1200, 800),
        ("9:16", 675, 1200),
        ("16:9", 1200, 675)
    };

    private readonly Random _random = new();
    private readonly Dictionary<ImageItemViewModel, PropertyChangedEventHandler> _handlers = new();

    private LayoutSpec _currentSpec = LayoutPresets.UniformAuto.Spec;
    private string? _selectedPresetId = LayoutPresets.UniformAuto.Id;
    private readonly List<string> _randomBag = new();
    private bool _countUserSet;
    private bool _updatingCount;
    private bool _syncingCanvas;
    private bool _syncingRatio;
    private bool _syncingGrid;
    private bool _syncingSeamless;
    private bool _suspendScene;
    private int _importVersion;
    private double _lockedAspect = 1;
    private double _gapBeforeSeamless = 8;
    private double _paddingBeforeSeamless = 16;

    public MainWindowViewModel()
    {
        CountOptions = Enumerable.Range(MinCount, MaxCount - MinCount + 1).ToList();
        Swatches = new ObservableCollection<CanvasSwatch>
        {
            new("纯白", Color.Parse("#FFFFFF")),
            new("浅灰", Color.Parse("#F3F4F8")),
            new("暖白", Color.Parse("#FAF7F2")),
            new("石墨", Color.Parse("#3A3F4B")),
            new("墨黑", Color.Parse("#20242E")),
            new("珊瑚粉", Color.Parse("#FF6B81")),
            new("樱粉", Color.Parse("#FFEDF1")),
            new("薄荷", Color.Parse("#DFF6EE")),
            new("天蓝", Color.Parse("#E3F0FF")),
            new("奶油", Color.Parse("#FFF4DC")),
            new("淡紫", Color.Parse("#EEE9FF")),
            new("莫兰迪", Color.Parse("#D8D5CC"))
        };

        LoadSampleImages();
    }

    public IStorageProvider? StorageProvider { get; set; }

    public ObservableCollection<ImageItemViewModel> Images { get; } = new();
    public ObservableCollection<LayoutGroupViewModel> LayoutGroups { get; } = new();
    public ObservableCollection<CanvasSwatch> Swatches { get; }
    public IReadOnlyList<int> CountOptions { get; }
    public IReadOnlyList<string> ModeOptions { get; } = new[] { "拼图", "长图", "海报" };

    [ObservableProperty] private CollageScene? scene;
    [ObservableProperty] private ImageItemViewModel? selectedImage;

    [ObservableProperty] private int selectedCount = 6;
    [ObservableProperty] private bool isAutoLayout = true;
    [ObservableProperty] private string layoutCategory = "custom";
    [ObservableProperty] private string layoutStateText = string.Empty;
    [ObservableProperty] private string layoutInfoText = string.Empty;
    [ObservableProperty] private string layoutHintText = string.Empty;
    [ObservableProperty] private int rowsInput = 2;
    [ObservableProperty] private int columnsInput = 3;

    [ObservableProperty] private double gap = 8;
    [ObservableProperty] private double padding = 16;
    [ObservableProperty] private double cornerRadius = 8;
    [ObservableProperty] private bool isSeamless;

    [ObservableProperty] private int canvasWidth = 1200;
    [ObservableProperty] private int canvasHeight = 1200;
    [ObservableProperty] private bool isCanvasRatioLocked = true;
    [ObservableProperty] private string ratioKey = "1:1";

    [ObservableProperty] private Color canvasBackground = DefaultBackground;
    [ObservableProperty] private string backgroundTab = "color";
    [ObservableProperty] private Bitmap? backgroundImage;
    [ObservableProperty] private string? backgroundImagePath;
    [ObservableProperty] private bool isColorPickerOpen;

    [ObservableProperty] private CellFillMode fillMode = CellFillMode.Crop;

    [ObservableProperty] private double previewZoom = 1;
    [ObservableProperty] private double previewRotation;
    [ObservableProperty] private bool flipHorizontal;
    [ObservableProperty] private bool fillCanvas;

    [ObservableProperty] private string exportFormat = "PNG";
    [ObservableProperty] private string exportScaleMode = "1x";
    [ObservableProperty] private double jpegQuality = 92;
    [ObservableProperty] private bool openFolderAfterExport = true;
    [ObservableProperty] private string exportHint = string.Empty;

    [ObservableProperty] private string selectedMode = "拼图";
    [ObservableProperty] private string statusText = string.Empty;
    [ObservableProperty] private bool isBusy;

    public string ImageCountText => $"{Images.Count} 张";
    public bool HasImages => Images.Count > 0;
    public bool HasSelection => SelectedImage is not null;
    public bool HasBackgroundImage => BackgroundImage is not null;
    public bool IsJpeg => ExportFormat == "JPG";
    public double CanvasAspect => CanvasWidth / (double)Math.Max(1, CanvasHeight);
    public string ZoomPercentText => $"{Math.Round(PreviewZoom * 100)}%";
    public string BackgroundImageName => string.IsNullOrEmpty(BackgroundImagePath)
        ? "未选择背景图片"
        : Path.GetFileName(BackgroundImagePath);
    public string CanvasBackgroundText =>
        $"#{CanvasBackground.R:X2}{CanvasBackground.G:X2}{CanvasBackground.B:X2}";
    public string ImageAdjustTitle => SelectedImage is null ? "单张微调" : $"单张微调 · {SelectedImage.DisplayName}";

    // ===================== 初始化 =====================

    private void LoadSampleImages()
    {
        var samples = SampleImageFactory.CreateSamples();
        for (int i = 0; i < samples.Count; i++)
            AddImage(new ImageItemViewModel(samples[i], string.Empty, $"示例图 {i + 1:00}", true));

        SelectedImage = Images.FirstOrDefault();
        SetCountInternal(Math.Clamp(Images.Count, MinCount, MaxCount), userSet: false);
        ApplyAutoLayoutCore();
        RefreshLayoutGroups();
        RefreshScene();
        StatusText = $"已加载 {Images.Count} 张示例图，可直接拖入或导入你的图片";
    }

    // ===================== 图片集合 =====================

    private void AddImage(ImageItemViewModel item)
    {
        Images.Add(item);
        Subscribe(item);
        Renumber();
        OnPropertyChanged(nameof(ImageCountText));
        OnPropertyChanged(nameof(HasImages));
    }

    private void Subscribe(ImageItemViewModel item)
    {
        if (_handlers.ContainsKey(item)) return;
        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (e.PropertyName is nameof(ImageItemViewModel.Zoom)
                or nameof(ImageItemViewModel.OffsetX)
                or nameof(ImageItemViewModel.OffsetY))
                RefreshScene();
        };
        _handlers[item] = handler;
        item.PropertyChanged += handler;
    }

    private void Unsubscribe(ImageItemViewModel item)
    {
        if (!_handlers.TryGetValue(item, out var handler)) return;
        item.PropertyChanged -= handler;
        _handlers.Remove(item);
    }

    private void Renumber()
    {
        for (int i = 0; i < Images.Count; i++) Images[i].Order = i + 1;
        OnPropertyChanged(nameof(ImageCountText));
        OnPropertyChanged(nameof(HasImages));
    }

    public async Task AddPathsAsync(IEnumerable<string> paths)
    {
        var expanded = new List<string>();
        foreach (var p in paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct())
        {
            try
            {
                if (Directory.Exists(p))
                    expanded.AddRange(Directory.EnumerateFiles(p)
                        .Where(ImageLoader.IsSupported)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                else if (File.Exists(p) && ImageLoader.IsSupported(p))
                    expanded.Add(p);
            }
            catch
            {
                // 忽略无法访问的目录
            }
        }

        if (expanded.Count == 0)
        {
            StatusText = "没有找到支持的图片文件";
            return;
        }

        IsBusy = true;
        _suspendScene = true;
        int added = 0;
        int failed = 0;
        int skipped = 0;
        int overflow = 0;
        ImageItemViewModel? firstAdded = null;

        var existing = new HashSet<string>(
            Images.Select(i => i.FilePath).Where(p => !string.IsNullOrEmpty(p)),
            StringComparer.OrdinalIgnoreCase);

        var candidates = new List<string>();
        foreach (var path in expanded)
        {
            if (!existing.Add(path))
            {
                skipped++;
                continue;
            }

            candidates.Add(path);
        }

        int room = Math.Max(0, MaxCount - Images.Count);
        if (candidates.Count > room)
        {
            overflow = candidates.Count - room;
            candidates.RemoveRange(room, overflow);
        }

        var results = new Bitmap?[candidates.Count];

        if (candidates.Count > 0)
        {
            StatusText = $"正在导入 {candidates.Count} 张图片…";

            // 多张图片并行解码（限制并发数，避免内存峰值），解码完成后再按原顺序加入列表。
            int done = 0;
            int importVersion = ++_importVersion;
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 2, 6)
            };

            await Task.Run(() => Parallel.For(0, candidates.Count, options, i =>
            {
                try
                {
                    results[i] = ImageLoader.Load(candidates[i]);
                }
                catch
                {
                    Interlocked.Increment(ref failed);
                }

                int current = Interlocked.Increment(ref done);
                if (current % 2 == 0 || current == candidates.Count)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_importVersion == importVersion)
                            StatusText = $"正在导入 {current}/{candidates.Count} 张…";
                    });
                }
            }));
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            var bitmap = results[i];
            if (bitmap is null) continue;

            var item = new ImageItemViewModel(bitmap, candidates[i], Path.GetFileName(candidates[i]));
            AddImage(item);
            firstAdded ??= item;
            added++;
        }

        _importVersion++;
        _suspendScene = false;
        IsBusy = false;

        if (added > 0)
        {
            SelectedImage = firstAdded ?? SelectedImage;
            if (!_countUserSet || Images.Count > SelectedCount)
                SetCountInternal(Math.Clamp(Images.Count, MinCount, MaxCount), userSet: false);
            else
            {
                RefreshLayoutGroups();
                RefreshScene();
            }
        }
        else
        {
            RefreshScene();
        }

        if (added > 0)
        {
            var notes = new List<string>();
            if (failed > 0) notes.Add($"{failed} 张无法识别或已损坏");
            if (skipped > 0) notes.Add($"{skipped} 张已在列表中");
            if (overflow > 0) notes.Add($"{overflow} 张超出 {MaxCount} 张上限");
            StatusText = $"已导入 {added} 张图片" + (notes.Count > 0 ? $"，{string.Join("，", notes)}" : string.Empty);
        }
        else if (failed > 0)
        {
            StatusText = $"导入失败：{failed} 个文件不是有效图片或已损坏";
        }
        else
        {
            StatusText = "这些图片已经在列表中";
        }
    }

    [RelayCommand]
    private async Task ImportFilesAsync()
    {
        if (StorageProvider is null) return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择图片",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("图片文件")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp", "*.gif", "*.tif", "*.tiff" }
                    }
                }
            });
            var paths = files.Select(f => f.TryGetLocalPath()).Where(p => !string.IsNullOrEmpty(p)).Select(p => p!).ToList();
            if (paths.Count > 0) await AddPathsAsync(paths);
        }
        catch (Exception ex)
        {
            StatusText = "导入失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ImportFolderAsync()
    {
        if (StorageProvider is null) return;
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择包含图片的文件夹",
                AllowMultiple = false
            });
            var path = folders.FirstOrDefault()?.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path)) await AddPathsAsync(new[] { path });
        }
        catch (Exception ex)
        {
            StatusText = "导入文件夹失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedImage is null) return;
        int index = Images.IndexOf(SelectedImage);
        if (index < 0) return;

        Unsubscribe(SelectedImage);
        Images.RemoveAt(index);
        Renumber();
        SelectedImage = Images.Count > 0 ? Images[Math.Min(index, Images.Count - 1)] : null;

        if (!_countUserSet || Images.Count < SelectedCount)
            SetCountInternal(Math.Clamp(Math.Max(Images.Count, MinCount), MinCount, MaxCount), userSet: false);
        else
        {
            RefreshLayoutGroups();
            RefreshScene();
        }

        StatusText = $"已删除 1 张，当前 {Images.Count} 张";
    }

    [RelayCommand]
    private async Task ReplaceSelectedAsync()
    {
        if (StorageProvider is null || SelectedImage is null) return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "替换当前图片",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("图片文件")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp", "*.gif", "*.tif", "*.tiff" }
                    }
                }
            });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            var bitmap = await ImageLoader.LoadAsync(path);
            int index = Images.IndexOf(SelectedImage);
            if (index < 0) return;

            var old = Images[index];
            var item = new ImageItemViewModel(bitmap, path, Path.GetFileName(path))
            {
                Zoom = old.Zoom,
                OffsetX = old.OffsetX,
                OffsetY = old.OffsetY
            };
            Unsubscribe(old);
            Images[index] = item;
            Subscribe(item);
            SelectedImage = item;
            Renumber();
            RefreshScene();
            StatusText = "已替换当前图片";
        }
        catch (Exception ex)
        {
            StatusText = "替换失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        foreach (var item in Images) Unsubscribe(item);
        Images.Clear();
        _handlers.Clear();
        SelectedImage = null;
        OnPropertyChanged(nameof(ImageCountText));
        OnPropertyChanged(nameof(HasImages));
        ApplyAutoLayoutCore();
        RefreshLayoutGroups();
        RefreshScene();
        StatusText = "已清空图片，空位会以 + 显示";
    }

    public void MoveImage(int from, int to)
    {
        if (from < 0 || to < 0 || from >= Images.Count || to >= Images.Count || from == to) return;
        Images.Move(from, to);
        Renumber();
        RefreshScene();
    }

    // ===================== 张数 / 版式 =====================

    partial void OnSelectedCountChanged(int value)
    {
        if (_updatingCount) return;

        int clamped = Math.Clamp(value, MinCount, MaxCount);
        if (clamped != value)
        {
            _updatingCount = true;
            SelectedCount = clamped;
            _updatingCount = false;
        }

        _countUserSet = true;
        EnsureLayoutFitsCount(clamped);
        RefreshLayoutGroups();
        RefreshScene();
    }

    private void SetCountInternal(int count, bool userSet)
    {
        count = Math.Clamp(count, MinCount, MaxCount);
        if (userSet) _countUserSet = true;

        _updatingCount = true;
        if (SelectedCount != count) SelectedCount = count;
        _updatingCount = false;

        if (!userSet) EnsureLayoutFitsCount(count);
        RefreshLayoutGroups();
        RefreshScene();
    }

    /// <summary>
    /// 预设版式与张数一一对应：张数变化后若旧版式已无法容纳，自动回退到自动网格。
    /// （手动行列网格容量足够时保持不动，符合「手动版式不被自动覆盖」的约定。）
    /// </summary>
    private void EnsureLayoutFitsCount(int count)
    {
        if (_currentSpec.Definition is { } definition && definition.Count != count)
        {
            ApplyAutoLayoutCore();
            StatusText = $"张数变为 {count}，已自动匹配新版式";
        }
    }

    partial void OnLayoutCategoryChanged(string value)
    {
        RefreshLayoutGroups();
        StatusText = value == "asymmetric" ? "已切换到异形版式" : "已切换到自定义版式";
    }

    private void ApplyAutoLayoutCore()
    {
        _selectedPresetId = LayoutPresets.UniformAuto.Id;
        _currentSpec = LayoutPresets.UniformAuto.Spec;
        IsAutoLayout = true;
    }

    private void ApplyPreset(LayoutPreset preset)
    {
        _selectedPresetId = preset.Id;
        _currentSpec = preset.Spec;
        IsAutoLayout = false;
        _countUserSet = true;
        RefreshLayoutGroups();
        RefreshScene();
        StatusText = $"已应用版式：{preset.Name}（{preset.Count} 张）";
    }

    [RelayCommand]
    private void SelectLayout(LayoutOptionViewModel? option)
    {
        if (option is null) return;

        if (option.GroupCount != SelectedCount)
        {
            _countUserSet = true;
            _updatingCount = true;
            SelectedCount = Math.Clamp(option.GroupCount, MinCount, MaxCount);
            _updatingCount = false;
        }

        ApplyPreset(option.Preset);
    }

    [RelayCommand]
    private void RestoreAutoLayout()
    {
        ApplyAutoLayoutCore();
        SyncGridInputsFromSpec();
        RefreshLayoutGroups();
        RefreshScene();
        StatusText = "已恢复自动匹配版式";
    }

    [RelayCommand]
    private void RandomizeLayout()
    {
        // 保持当前张数不变。用「洗牌袋」随机：一轮内不重复，保证每次都能切到不同版式。
        var all = LayoutLibrary.AllForCount(SelectedCount);
        if (all.Count == 0)
        {
            ApplyAutoLayoutCore();
            RefreshLayoutGroups();
            RefreshScene();
            StatusText = "已切回自动版式";
            return;
        }

        _randomBag.RemoveAll(id => all.All(p => p.Id != id));

        if (_randomBag.Count == 0)
        {
            foreach (var item in all)
            {
                if (item.Id != _selectedPresetId) _randomBag.Add(item.Id);
            }
            Shuffle(_randomBag);
        }

        if (_randomBag.Count == 0)
        {
            ApplyAutoLayoutCore();
            RefreshLayoutGroups();
            RefreshScene();
            StatusText = "已切回自动版式";
            return;
        }

        var preset = all.First(p => p.Id == _randomBag[0]);
        _randomBag.RemoveAt(0);

        ApplyPreset(preset);
        StatusText = $"已随机切换版式：{preset.Name}（{preset.Count} 张）";
    }

    private void Shuffle(List<string> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    [RelayCommand]
    private void ApplyGrid()
    {
        int rows = Math.Clamp(RowsInput, 1, 20);
        int cols = Math.Clamp(ColumnsInput, 1, 20);

        if (rows * cols < SelectedCount)
        {
            cols = Math.Min(20, (int)Math.Ceiling(SelectedCount / (double)rows));
            StatusText = $"行列不足，已自动扩展为 {rows} 行 × {cols} 列";
        }
        else
        {
            StatusText = $"已应用手动网格：{rows} 行 × {cols} 列";
        }

        _currentSpec = new LayoutSpec(CollageLayoutKind.Uniform, Columns: cols, Rows: rows, Manual: true);
        _selectedPresetId = null;
        IsAutoLayout = false;
        _countUserSet = true;
        RefreshLayoutGroups();
        RefreshScene();
    }

    private void RefreshLayoutGroups()
    {
        LayoutGroups.Clear();

        // 当前张数优先，下面顺延展示后续张数，方便直接浏览 / 切换。
        var counts = new List<int> { SelectedCount };
        for (int offset = 1; offset <= 3; offset++)
        {
            if (SelectedCount + offset <= MaxCount) counts.Add(SelectedCount + offset);
        }
        for (int offset = 1; offset <= 2; offset++)
        {
            if (SelectedCount - offset >= MinCount) counts.Add(SelectedCount - offset);
        }

        foreach (int count in counts)
        {
            var presets = LayoutLibrary.PresetsForCount(count, LayoutCategory);
            if (presets.Count == 0) continue;

            bool isCurrent = count == SelectedCount;
            var group = new LayoutGroupViewModel(count, isCurrent, presets.Count);
            foreach (var preset in presets)
            {
                bool selected = isCurrent && IsPresetSelected(preset);
                group.Options.Add(new LayoutOptionViewModel(preset, count, selected));
            }
            LayoutGroups.Add(group);
        }
    }

    private bool IsPresetSelected(LayoutPreset preset) =>
        !IsAutoLayout && _selectedPresetId == preset.Id;

    private void SyncGridInputsFromSpec()
    {
        if (_currentSpec.Manual && _currentSpec.Rows > 0 && _currentSpec.Columns > 0)
        {
            _syncingGrid = true;
            RowsInput = _currentSpec.Rows;
            ColumnsInput = _currentSpec.Columns;
            _syncingGrid = false;
        }
    }

    // ===================== 场景重建 =====================

    private void RefreshScene()
    {
        if (_suspendScene) return;

        if (_currentSpec.Manual && _currentSpec.Rows > 0 && _currentSpec.Columns > 0
            && _currentSpec.Rows * _currentSpec.Columns < SelectedCount)
        {
            int fitRows = _currentSpec.Rows;
            int fitCols = Math.Min(20, (int)Math.Ceiling(SelectedCount / (double)fitRows));
            _currentSpec = new LayoutSpec(CollageLayoutKind.Uniform, Columns: fitCols, Rows: fitRows, Manual: true);
            _selectedPresetId = null;
            StatusText = $"当前 {SelectedCount} 张已超出网格容量，自动扩展为 {fitRows} 行 × {fitCols} 列";
        }

        int slots = _currentSpec.Definition?.Count
            ?? (_currentSpec.Manual && _currentSpec.Rows > 0 && _currentSpec.Columns > 0
                ? _currentSpec.Rows * _currentSpec.Columns
                : SelectedCount);
        slots = Math.Clamp(slots, 1, 400);

        var list = new List<CollageImage>(slots);
        int limit = Math.Min(SelectedCount, slots);
        for (int i = 0; i < slots; i++)
        {
            if (i < limit && i < Images.Count)
            {
                var item = Images[i];
                list.Add(new CollageImage(item.Bitmap, item.Zoom, item.OffsetX, item.OffsetY));
            }
            else
            {
                list.Add(CollageImage.Placeholder);
            }
        }

        int width = Math.Max(200, CanvasWidth);
        int height = Math.Max(200, CanvasHeight);

        Scene = new CollageScene
        {
            Images = list,
            Spec = _currentSpec,
            Gap = Gap,
            Padding = Padding,
            CornerRadius = CornerRadius,
            BaseWidth = width,
            AspectRatio = width / (double)height,
            FillMode = FillMode,
            Background = CanvasBackground,
            BackgroundImage = BackgroundImage
        };

        var cells = LayoutEngine.Build(_currentSpec, slots, new Size(width, height), Gap, Padding, CornerRadius);
        var (rows, cols) = LayoutEngine.Describe(cells);

        if (!_syncingGrid)
        {
            _syncingGrid = true;
            RowsInput = Math.Max(1, rows);
            ColumnsInput = Math.Max(1, cols);
            _syncingGrid = false;
        }

        string presetName = IsAutoLayout
            ? "自动匹配"
            : LayoutPresets.Find(_selectedPresetId)?.Name ?? "手动网格";
        LayoutStateText = $"{presetName} · {rows} 行 × {cols} 列";
        LayoutInfoText = $"{SelectedCount} 张图片 · {slots} 个格子";

        var hints = new List<string>();
        if (Images.Count < SelectedCount)
            hints.Add($"还需 {SelectedCount - Images.Count} 张，空位显示为 +");
        if (slots > SelectedCount)
            hints.Add($"共 {slots} 格，末尾 {slots - SelectedCount} 格留空");
        if (!IsAutoLayout)
            hints.Add("手动版式不会被自动覆盖");
        LayoutHintText = string.Join("；", hints);

        OnPropertyChanged(nameof(ImageCountText));
        OnPropertyChanged(nameof(HasImages));
        UpdateExportHint();
    }

    // ===================== 画布参数 =====================

    partial void OnGapChanged(double value)
    {
        if (_syncingSeamless) return;
        SyncSeamlessFromValues();
        RefreshScene();
    }

    partial void OnPaddingChanged(double value)
    {
        if (_syncingSeamless) return;
        SyncSeamlessFromValues();
        RefreshScene();
    }

    partial void OnCornerRadiusChanged(double value) => RefreshScene();

    partial void OnFillModeChanged(CellFillMode value) => RefreshScene();

    private void SyncSeamlessFromValues()
    {
        bool seamless = Gap <= 0.01 && Padding <= 0.01;
        if (IsSeamless == seamless) return;

        _syncingSeamless = true;
        IsSeamless = seamless;
        _syncingSeamless = false;
        OnPropertyChanged(nameof(IsSeamless));
    }

    partial void OnIsSeamlessChanged(bool value)
    {
        if (_syncingSeamless) return;

        _syncingSeamless = true;
        if (value)
        {
            _gapBeforeSeamless = Gap > 0.01 ? Gap : 8;
            _paddingBeforeSeamless = Padding > 0.01 ? Padding : 16;
            Gap = 0;
            Padding = 0;
        }
        else
        {
            Gap = _gapBeforeSeamless;
            Padding = _paddingBeforeSeamless;
        }
        _syncingSeamless = false;

        RefreshScene();
        StatusText = value ? "已开启无缝模式：间距与外边距为 0" : "已关闭无缝模式";
    }

    partial void OnCanvasWidthChanged(int value)
    {
        if (!_syncingCanvas)
        {
            if (IsCanvasRatioLocked && _lockedAspect > 0)
            {
                _syncingCanvas = true;
                CanvasHeight = Math.Clamp((int)Math.Round(CanvasWidth / _lockedAspect), 200, 10000);
                _syncingCanvas = false;
            }
            SyncRatioKeyFromSize();
        }

        RefreshScene();
        OnPropertyChanged(nameof(CanvasAspect));
    }

    partial void OnCanvasHeightChanged(int value)
    {
        if (!_syncingCanvas)
        {
            if (IsCanvasRatioLocked && _lockedAspect > 0)
            {
                _syncingCanvas = true;
                CanvasWidth = Math.Clamp((int)Math.Round(CanvasHeight * _lockedAspect), 200, 10000);
                _syncingCanvas = false;
            }
            SyncRatioKeyFromSize();
        }

        RefreshScene();
        OnPropertyChanged(nameof(CanvasAspect));
    }

    partial void OnIsCanvasRatioLockedChanged(bool value)
    {
        if (value && CanvasHeight > 0)
            _lockedAspect = CanvasWidth / (double)CanvasHeight;
        StatusText = value ? "已锁定画布比例" : "已解除画布比例锁定";
    }

    partial void OnRatioKeyChanged(string value)
    {
        if (_syncingRatio) return;

        var preset = RatioPresets.FirstOrDefault(r => r.Key == value);
        if (preset.Key is null) return;

        _lockedAspect = preset.W / (double)preset.H;
        _syncingCanvas = true;
        CanvasWidth = preset.W;
        CanvasHeight = preset.H;
        _syncingCanvas = false;

        RefreshScene();
        StatusText = $"画布比例：{value}（{preset.W} × {preset.H}）";
    }

    private void SyncRatioKeyFromSize()
    {
        if (CanvasHeight <= 0) return;
        double aspect = CanvasWidth / (double)CanvasHeight;
        var match = RatioPresets.FirstOrDefault(r => Math.Abs(r.W / (double)r.H - aspect) < 0.003);

        string key = match.Key ?? string.Empty;
        if (RatioKey == key) return;

        _syncingRatio = true;
        RatioKey = key;
        _syncingRatio = false;
    }

    // ===================== 背景 =====================

    partial void OnCanvasBackgroundChanged(Color value)
    {
        OnPropertyChanged(nameof(CanvasBackgroundText));
        RefreshScene();
    }

    partial void OnBackgroundImageChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasBackgroundImage));
        OnPropertyChanged(nameof(BackgroundImageName));
        RefreshScene();
    }

    partial void OnBackgroundImagePathChanged(string? value) =>
        OnPropertyChanged(nameof(BackgroundImageName));

    [RelayCommand]
    private void SetBackground(CanvasSwatch? swatch)
    {
        if (swatch is null) return;
        BackgroundImage = null;
        BackgroundImagePath = null;
        CanvasBackground = swatch.Color;
        BackgroundTab = "color";
        StatusText = $"画布背景：{swatch.Name}";
    }

    [RelayCommand]
    private void ToggleColorPicker() => IsColorPickerOpen = !IsColorPickerOpen;

    [RelayCommand]
    private async Task PickBackgroundImageAsync()
    {
        if (StorageProvider is null) return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择画布背景图片",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("图片文件")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp" }
                    }
                }
            });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            BackgroundImage = await ImageLoader.LoadAsync(path);
            BackgroundImagePath = path;
            BackgroundTab = "image";
            StatusText = "已设置画布背景图片";
        }
        catch (Exception ex)
        {
            StatusText = "背景图片加载失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private void RestoreBackground()
    {
        BackgroundImage = null;
        BackgroundImagePath = null;
        CanvasBackground = DefaultBackground;
        StatusText = "已恢复默认画布背景";
    }

    // ===================== 预览变换 / 单张微调 =====================

    partial void OnPreviewZoomChanged(double value) => OnPropertyChanged(nameof(ZoomPercentText));

    partial void OnSelectedImageChanged(ImageItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ImageAdjustTitle));
    }

    [RelayCommand]
    private void ZoomIn() => PreviewZoom = Math.Min(3, Math.Round(PreviewZoom + 0.1, 2));

    [RelayCommand]
    private void ZoomOut() => PreviewZoom = Math.Max(0.2, Math.Round(PreviewZoom - 0.1, 2));

    [RelayCommand]
    private void ResetPreview()
    {
        PreviewZoom = 1;
        PreviewRotation = 0;
        FlipHorizontal = false;
        FillCanvas = false;
    }

    [RelayCommand]
    private void Rotate() => PreviewRotation = (PreviewRotation + 90) % 360;

    [RelayCommand]
    private void RotateBack() => PreviewRotation = (PreviewRotation + 270) % 360;

    [RelayCommand]
    private void Flip() => FlipHorizontal = !FlipHorizontal;

    [RelayCommand]
    private void ResetAdjust()
    {
        if (SelectedImage is null) return;
        SelectedImage.Zoom = 1;
        SelectedImage.OffsetX = 0;
        SelectedImage.OffsetY = 0;
        StatusText = "已重置当前图片的微调";
    }

    // ===================== 导出 =====================

    private int GetExportScale() => ExportScaleMode switch
    {
        "2x" => 2,
        "3x" => 3,
        _ => 1
    };

    private void UpdateExportHint()
    {
        int scale = GetExportScale();
        int w = Math.Clamp(CanvasWidth * scale, 16, 12000);
        int h = Math.Clamp(CanvasHeight * scale, 16, 12000);
        ExportHint = $"导出尺寸 {w} × {h} px · {ExportFormat}";
    }

    partial void OnExportFormatChanged(string value)
    {
        OnPropertyChanged(nameof(IsJpeg));
        UpdateExportHint();
    }

    partial void OnExportScaleModeChanged(string value) => UpdateExportHint();

    partial void OnSelectedModeChanged(string value)
    {
        if (value == "拼图") return;
        StatusText = $"「{value}」模式尚未开放，已保持拼图模式";
        selectedMode = "拼图";
        OnPropertyChanged(nameof(SelectedMode));
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (IsBusy) return;
        if (StorageProvider is null || Scene is null)
        {
            StatusText = "暂时无法导出，请稍后再试";
            return;
        }

        if (Images.Count == 0)
        {
            StatusText = "请先导入至少一张图片";
            return;
        }

        try
        {
            IsBusy = true;
            int scale = GetExportScale();
            int width = Math.Clamp(CanvasWidth * scale, 16, 12000);
            int height = Math.Clamp(CanvasHeight * scale, 16, 12000);
            bool jpeg = ExportFormat == "JPG";

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出拼图",
                SuggestedFileName = $"拼图_{DateTime.Now:yyyyMMdd_HHmmss}",
                DefaultExtension = jpeg ? "jpg" : "png",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(jpeg ? "JPG 图片" : "PNG 图片")
                    {
                        Patterns = new[] { jpeg ? "*.jpg" : "*.png" },
                        MimeTypes = new[] { jpeg ? "image/jpeg" : "image/png" }
                    }
                }
            });

            if (file is null)
            {
                StatusText = "已取消导出";
                return;
            }

            var path = file.TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
            {
                StatusText = "无法获取导出路径";
                return;
            }

            var bitmap = ExportService.Render(Scene, width, height);
            if (jpeg) ExportService.SaveJpeg(bitmap, path, (int)Math.Round(JpegQuality));
            else ExportService.SavePng(bitmap, path);

            StatusText = $"已导出 {width} × {height} px → {path}";
            if (OpenFolderAfterExport) OpenInExplorer(path);
        }
        catch (Exception ex)
        {
            StatusText = "导出失败：" + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void OpenInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // 打开资源管理器失败不影响导出结果
        }
    }
}

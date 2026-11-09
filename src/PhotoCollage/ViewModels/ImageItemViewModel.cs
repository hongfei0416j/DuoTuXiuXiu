using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DuoTuXiuXiu.ViewModels;

public partial class ImageItemViewModel : ObservableObject
{
    public ImageItemViewModel(Bitmap bitmap, string filePath, string displayName, bool isSample = false)
    {
        Bitmap = bitmap;
        FilePath = filePath;
        DisplayName = displayName;
        IsSample = isSample;
        SizeText = $"{bitmap.PixelSize.Width} × {bitmap.PixelSize.Height}";
    }

    public Bitmap Bitmap { get; }
    public string FilePath { get; }
    public string DisplayName { get; }
    public string SizeText { get; }
    public bool IsSample { get; }

    [ObservableProperty] private double zoom = 1;
    [ObservableProperty] private double offsetX;
    [ObservableProperty] private double offsetY;
    [ObservableProperty] private int order;

    public string OrderText => Order.ToString("00");

    partial void OnOrderChanged(int value) => OnPropertyChanged(nameof(OrderText));
}

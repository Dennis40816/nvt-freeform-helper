using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

public enum DxfLayerImageFormat
{
    Png,
    Bmp,
    Jpg,
}

public readonly record struct DxfLayerImageExportRequest(
    string LayerName,
    int WidthPixels,
    int HeightPixels,
    decimal LineWidthPixels,
    int PaddingXPixels,
    int PaddingYPixels,
    DxfLayerImageFormat Format,
    bool UseDarkTheme)
{
    public string FileExtension => Format switch
    {
        DxfLayerImageFormat.Png => "png",
        DxfLayerImageFormat.Bmp => "bmp",
        DxfLayerImageFormat.Jpg => "jpg",
        _ => "png",
    };
}

public sealed partial class DxfLayerImageExportViewModel : ObservableObject
{
    public DxfLayerImageExportViewModel(
        IReadOnlyList<string> layerNames,
        DxfLayerImageExportRequest? initialRequest = null)
    {
        ArgumentNullException.ThrowIfNull(layerNames);
        LayerOptions = layerNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new LayerOption(name))
            .ToList();
        if (LayerOptions.Count == 0)
        {
            LayerOptions = new List<LayerOption> { new LayerOption("0") };
        }

        FormatOptions =
        [
            new ImageFormatOption(DxfLayerImageFormat.Png, "PNG"),
            new ImageFormatOption(DxfLayerImageFormat.Bmp, "BMP"),
            new ImageFormatOption(DxfLayerImageFormat.Jpg, "JPG"),
        ];

        var preferredLayerName = initialRequest?.LayerName;
        _selectedLayer = LayerOptions.FirstOrDefault(option =>
            string.Equals(option.Name, preferredLayerName, StringComparison.Ordinal))
            ?? LayerOptions[0];
        _selectedFormat = initialRequest is { } initial
            ? FormatOptions.FirstOrDefault(option => option.Value == initial.Format) ?? FormatOptions[0]
            : FormatOptions[0];

        if (initialRequest is { } request)
        {
            _widthPixels = request.WidthPixels;
            _heightPixels = request.HeightPixels;
            _lineWidthPixels = request.LineWidthPixels;
            _paddingXPixels = request.PaddingXPixels;
            _paddingYPixels = request.PaddingYPixels;
            _useDarkTheme = request.UseDarkTheme;
        }
    }

    public IReadOnlyList<LayerOption> LayerOptions { get; private set; }

    public IReadOnlyList<ImageFormatOption> FormatOptions { get; }

    [ObservableProperty] private LayerOption _selectedLayer;

    [ObservableProperty] private decimal _widthPixels = 1024m;

    [ObservableProperty] private decimal _heightPixels = 1024m;

    [ObservableProperty] private decimal _lineWidthPixels = 1m;

    [ObservableProperty] private decimal _paddingXPixels = 200m;

    [ObservableProperty] private decimal _paddingYPixels = 200m;

    [ObservableProperty] private ImageFormatOption _selectedFormat;

    [ObservableProperty] private bool _useDarkTheme;

    public int OutputWidthPixels => ClampPixels(WidthPixels) + (ClampPadding(PaddingXPixels) * 2);
    public int OutputHeightPixels => ClampPixels(HeightPixels) + (ClampPadding(PaddingYPixels) * 2);
    public string OutputResolutionText =>
        $"Final output: {OutputWidthPixels} x {OutputHeightPixels} px (layer {ClampPixels(WidthPixels)} x {ClampPixels(HeightPixels)} + padding).";

    public DxfLayerImageExportRequest BuildRequest()
    {
        return new DxfLayerImageExportRequest(
            SelectedLayer.Name,
            ClampPixels(WidthPixels),
            ClampPixels(HeightPixels),
            ClampLineWidth(LineWidthPixels),
            ClampPadding(PaddingXPixels),
            ClampPadding(PaddingYPixels),
            SelectedFormat.Value,
            UseDarkTheme);
    }

    partial void OnWidthPixelsChanged(decimal value)
    {
        OnPropertyChanged(nameof(OutputWidthPixels));
        OnPropertyChanged(nameof(OutputResolutionText));
    }

    partial void OnHeightPixelsChanged(decimal value)
    {
        OnPropertyChanged(nameof(OutputHeightPixels));
        OnPropertyChanged(nameof(OutputResolutionText));
    }

    partial void OnPaddingXPixelsChanged(decimal value)
    {
        OnPropertyChanged(nameof(OutputWidthPixels));
        OnPropertyChanged(nameof(OutputResolutionText));
    }

    partial void OnPaddingYPixelsChanged(decimal value)
    {
        OnPropertyChanged(nameof(OutputHeightPixels));
        OnPropertyChanged(nameof(OutputResolutionText));
    }

    private static int ClampPixels(decimal value)
    {
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded < 2)
        {
            return 2;
        }

        return rounded > 100_000 ? 100_000 : rounded;
    }

    private static decimal ClampLineWidth(decimal value)
    {
        if (value < 0.1m)
        {
            return 0.1m;
        }

        return value > 100m ? 100m : value;
    }

    private static int ClampPadding(decimal value)
    {
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded < 0)
        {
            return 0;
        }

        return rounded > 100_000 ? 100_000 : rounded;
    }

    public sealed record LayerOption(string Name)
    {
        public string Display => Name;
    }

    public sealed record ImageFormatOption(DxfLayerImageFormat Value, string Display);
}

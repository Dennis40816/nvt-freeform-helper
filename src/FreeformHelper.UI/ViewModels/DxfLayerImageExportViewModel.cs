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

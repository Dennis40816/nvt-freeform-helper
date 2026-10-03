using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private const int DxfLayerImageExportMinPixels = 2;
    private const int DxfLayerImageExportMaxPixels = 100_000;
    private const int DxfLayerImageExportMinPadding = 0;
    private const int DxfLayerImageExportMaxPadding = 100_000;
    private const decimal DxfLayerImageExportMinLineWidth = 0.1m;
    private const decimal DxfLayerImageExportMaxLineWidth = 100m;

    private string _dxfLayerImageExportPreferredLayerName = string.Empty;
    private int _dxfLayerImageExportWidthPixels = 1024;
    private int _dxfLayerImageExportHeightPixels = 1024;
    private decimal _dxfLayerImageExportLineWidthPixels = 1m;
    private int _dxfLayerImageExportPaddingXPixels = 200;
    private int _dxfLayerImageExportPaddingYPixels = 200;
    private DxfLayerImageFormat _dxfLayerImageExportFormat = DxfLayerImageFormat.Png;
    private bool _dxfLayerImageExportUseDarkTheme;

    internal DxfLayerImageExportRequest BuildDxfLayerImageExportInitialRequest(IReadOnlyList<string> layerNames)
    {
        ArgumentNullException.ThrowIfNull(layerNames);
        var layerName = layerNames.FirstOrDefault(name =>
            string.Equals(name, _dxfLayerImageExportPreferredLayerName, StringComparison.Ordinal))
            ?? (layerNames.Count > 0 ? layerNames[0] : null)
            ?? "0";

        return new DxfLayerImageExportRequest(
            LayerName: layerName,
            WidthPixels: ClampDxfLayerImagePixels(_dxfLayerImageExportWidthPixels),
            HeightPixels: ClampDxfLayerImagePixels(_dxfLayerImageExportHeightPixels),
            LineWidthPixels: ClampDxfLayerImageLineWidth(_dxfLayerImageExportLineWidthPixels),
            PaddingXPixels: ClampDxfLayerImagePadding(_dxfLayerImageExportPaddingXPixels),
            PaddingYPixels: ClampDxfLayerImagePadding(_dxfLayerImageExportPaddingYPixels),
            Format: _dxfLayerImageExportFormat,
            UseDarkTheme: _dxfLayerImageExportUseDarkTheme);
    }

    internal void RememberDxfLayerImageExportRequest(DxfLayerImageExportRequest request)
    {
        var normalizedLayer = request.LayerName ?? string.Empty;
        var normalizedWidth = ClampDxfLayerImagePixels(request.WidthPixels);
        var normalizedHeight = ClampDxfLayerImagePixels(request.HeightPixels);
        var normalizedLineWidth = ClampDxfLayerImageLineWidth(request.LineWidthPixels);
        var normalizedPaddingX = ClampDxfLayerImagePadding(request.PaddingXPixels);
        var normalizedPaddingY = ClampDxfLayerImagePadding(request.PaddingYPixels);

        var changed =
            !string.Equals(_dxfLayerImageExportPreferredLayerName, normalizedLayer, StringComparison.Ordinal) ||
            _dxfLayerImageExportWidthPixels != normalizedWidth ||
            _dxfLayerImageExportHeightPixels != normalizedHeight ||
            _dxfLayerImageExportLineWidthPixels != normalizedLineWidth ||
            _dxfLayerImageExportPaddingXPixels != normalizedPaddingX ||
            _dxfLayerImageExportPaddingYPixels != normalizedPaddingY ||
            _dxfLayerImageExportFormat != request.Format ||
            _dxfLayerImageExportUseDarkTheme != request.UseDarkTheme;

        _dxfLayerImageExportPreferredLayerName = normalizedLayer;
        _dxfLayerImageExportWidthPixels = normalizedWidth;
        _dxfLayerImageExportHeightPixels = normalizedHeight;
        _dxfLayerImageExportLineWidthPixels = normalizedLineWidth;
        _dxfLayerImageExportPaddingXPixels = normalizedPaddingX;
        _dxfLayerImageExportPaddingYPixels = normalizedPaddingY;
        _dxfLayerImageExportFormat = request.Format;
        _dxfLayerImageExportUseDarkTheme = request.UseDarkTheme;

        if (!changed)
        {
            return;
        }

        MarkUnsaved();
        SchedulePersistAppGeneralSettings();
    }

    private void ApplyDxfLayerImageExportSnapshot(UiViewSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        _dxfLayerImageExportPreferredLayerName = snapshot.DxfLayerImagePreferredLayer ?? string.Empty;
        _dxfLayerImageExportWidthPixels = ClampDxfLayerImagePixels((int)Math.Round(snapshot.DxfLayerImageWidthPixels));
        _dxfLayerImageExportHeightPixels = ClampDxfLayerImagePixels((int)Math.Round(snapshot.DxfLayerImageHeightPixels));
        _dxfLayerImageExportLineWidthPixels = ClampDxfLayerImageLineWidth((decimal)snapshot.DxfLayerImageLineWidthPixels);
        _dxfLayerImageExportPaddingXPixels = ClampDxfLayerImagePadding((int)Math.Round(snapshot.DxfLayerImagePaddingXPixels));
        _dxfLayerImageExportPaddingYPixels = ClampDxfLayerImagePadding((int)Math.Round(snapshot.DxfLayerImagePaddingYPixels));
        _dxfLayerImageExportUseDarkTheme = snapshot.DxfLayerImageUseDarkTheme;
        _dxfLayerImageExportFormat = Enum.TryParse<DxfLayerImageFormat>(
            snapshot.DxfLayerImageFormat,
            ignoreCase: true,
            out var parsedFormat)
            ? parsedFormat
            : DxfLayerImageFormat.Png;
    }

    private static int ClampDxfLayerImagePixels(int value)
    {
        return Math.Clamp(value, DxfLayerImageExportMinPixels, DxfLayerImageExportMaxPixels);
    }

    private static int ClampDxfLayerImagePadding(int value)
    {
        return Math.Clamp(value, DxfLayerImageExportMinPadding, DxfLayerImageExportMaxPadding);
    }

    private static decimal ClampDxfLayerImageLineWidth(decimal value)
    {
        if (value < DxfLayerImageExportMinLineWidth)
        {
            return DxfLayerImageExportMinLineWidth;
        }

        return value > DxfLayerImageExportMaxLineWidth ? DxfLayerImageExportMaxLineWidth : value;
    }
}

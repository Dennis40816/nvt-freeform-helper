// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.ViewModels.Project;

internal sealed class DxfLayerImagePreferenceState(Action markUnsaved, Action schedulePersistence)
{
    private readonly Action _markUnsaved = markUnsaved;
    private readonly Action _schedulePersistence = schedulePersistence;

    private const int DxfLayerImageExportMinPixels = 2;
    private const int DxfLayerImageExportMaxPixels = 100_000;
    private const int DxfLayerImageExportMinPadding = 0;
    private const int DxfLayerImageExportMaxPadding = 100_000;
    private const decimal DxfLayerImageExportMinLineWidth = 0.1m;
    private const decimal DxfLayerImageExportMaxLineWidth = 100m;

    private static readonly DxfLayerImageExportRequest _defaultDxfLayerImageExportPreferences = new(
        LayerName: string.Empty,
        WidthPixels: 1024,
        HeightPixels: 1024,
        LineWidthPixels: 1m,
        PaddingXPixels: 200,
        PaddingYPixels: 200,
        Format: DxfLayerImageFormat.Png,
        UseDarkTheme: false);

    // UI thread only. The remembered export request. It always holds clamped values and is replaced as a whole.
    // The layer name is the preferred layer. It may name a layer that the current DXF does not have.
    private DxfLayerImageExportRequest _dxfLayerImageExportPreferences = _defaultDxfLayerImageExportPreferences;

    public DxfLayerImageExportRequest Value => _dxfLayerImageExportPreferences;

    public void Remember(DxfLayerImageExportRequest request)
    {
        var normalized = NormalizeDxfLayerImageExportRequest(request);
        if (normalized == _dxfLayerImageExportPreferences)
        {
            return;
        }

        _dxfLayerImageExportPreferences = normalized;
        _markUnsaved();
        _schedulePersistence();
    }

    public void Apply(UiViewSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        var format = Enum.TryParse<DxfLayerImageFormat>(
            snapshot.DxfLayerImageFormat,
            ignoreCase: true,
            out var parsedFormat)
            ? parsedFormat
            : DxfLayerImageFormat.Png;
        _dxfLayerImageExportPreferences = NormalizeDxfLayerImageExportRequest(new DxfLayerImageExportRequest(
            LayerName: snapshot.DxfLayerImagePreferredLayer ?? string.Empty,
            WidthPixels: (int)Math.Round(snapshot.DxfLayerImageWidthPixels),
            HeightPixels: (int)Math.Round(snapshot.DxfLayerImageHeightPixels),
            LineWidthPixels: (decimal)snapshot.DxfLayerImageLineWidthPixels,
            PaddingXPixels: (int)Math.Round(snapshot.DxfLayerImagePaddingXPixels),
            PaddingYPixels: (int)Math.Round(snapshot.DxfLayerImagePaddingYPixels),
            Format: format,
            UseDarkTheme: snapshot.DxfLayerImageUseDarkTheme));
    }

    private static DxfLayerImageExportRequest NormalizeDxfLayerImageExportRequest(DxfLayerImageExportRequest request)
    {
        return request with
        {
            LayerName = request.LayerName ?? string.Empty,
            WidthPixels = ClampDxfLayerImagePixels(request.WidthPixels),
            HeightPixels = ClampDxfLayerImagePixels(request.HeightPixels),
            LineWidthPixels = ClampDxfLayerImageLineWidth(request.LineWidthPixels),
            PaddingXPixels = ClampDxfLayerImagePadding(request.PaddingXPixels),
            PaddingYPixels = ClampDxfLayerImagePadding(request.PaddingYPixels),
        };
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

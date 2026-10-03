using System.Globalization;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private async Task ExportVisibleDxfAsync()
    {
        await ExportDxfAsync(visibleOnly: true);
    }

    private async Task ExportAllDxfAsync()
    {
        await ExportDxfAsync(visibleOnly: false);
    }

    private async Task ExportDxfLayerImageAsync()
    {
        if (_cad is null)
        {
            SetStatus("Export DXF image: import DXF first.");
            if (ShowWarningAsync is not null)
            {
                await ShowWarningAsync(
                    "DXF not loaded",
                    "Export DXF image requires an imported DXF.\nPlease load a DXF file first.");
            }

            return;
        }

        if (OpenDxfLayerImageExportAsync is null)
        {
            SetStatus("Export DXF image: dialog handler not wired.");
            return;
        }

        if (PickSaveExportPathAsync is null)
        {
            SetStatus("Export DXF image: dialog handler not wired.");
            return;
        }

        var activeCad = BuildActiveCadPadSet();
        var pads = activeCad?.Pads ?? [];
        if (pads.Count == 0)
        {
            SetStatus("Export DXF image: no CAD pads available.");
            return;
        }

        var layerNames = pads
            .Select(p => p.Layer)
            .Where(layer => !string.IsNullOrWhiteSpace(layer))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(layer => layer, StringComparer.Ordinal)
            .ToList();
        if (layerNames.Count == 0)
        {
            SetStatus("Export DXF image: no layer found.");
            return;
        }

        try
        {
            var request = await OpenDxfLayerImageExportAsync(layerNames);
            if (request is null)
            {
                return;
            }

            RememberDxfLayerImageExportRequest(request.Value);

            var layerPads = pads
                .Where(p => string.Equals(p.Layer, request.Value.LayerName, StringComparison.Ordinal))
                .ToList();
            if (layerPads.Count == 0)
            {
                SetStatus("Export DXF image: selected layer has no visible pad.");
                return;
            }

            var suggestedName = $"dxf_{SanitizeFileNameToken(request.Value.LayerName)}";
            var path = await PickSaveExportPathAsync(suggestedName, request.Value.FileExtension);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            Services.DxfLayerImageExportService.ExportLayerImage(
                layerPads,
                request.Value.WidthPixels,
                request.Value.HeightPixels,
                request.Value.LineWidthPixels,
                request.Value.PaddingXPixels,
                request.Value.PaddingYPixels,
                request.Value.Format,
                request.Value.UseDarkTheme,
                path);

            SetStatus($"DXF image exported ({request.Value.Format}, layer={request.Value.LayerName}, pads={layerPads.Count}).");
            Logger.Info(CultureInfo.InvariantCulture, "DXF layer image exported to {0}. layer={1}, pads={2}, size={3}x{4}, lineWidth={5}, paddingX={6}, paddingY={7}, format={8}, dark={9}.",
                path,
                request.Value.LayerName,
                layerPads.Count,
                request.Value.WidthPixels,
                request.Value.HeightPixels,
                request.Value.LineWidthPixels,
                request.Value.PaddingXPixels,
                request.Value.PaddingYPixels,
                request.Value.Format,
                request.Value.UseDarkTheme);
        }
        catch (Exception ex)
        {
            SetStatusError("Export DXF image failed", ex);
            Logger.Error(ex, "Export DXF image failed.");
        }
    }

    private async Task ExportDxfAsync(bool visibleOnly)
    {
        if (!TryGetLoadedCad(() => _cad, "Export DXF: import DXF first.", out var cad))
        {
            return;
        }

        if (PickSaveExportPathAsync is null)
        {
            SetStatus("Export DXF: dialog handler not wired.");
            return;
        }

        var pads = visibleOnly
            ? BuildVisibleDxfExportPads(cad)
            : cad.Pads.Where(p => !IsCadPadEffectivelyHidden(p.Id)).ToList();

        if (pads.Count == 0)
        {
            SetStatus(visibleOnly
                ? "Export DXF: no CAD output pads."
                : "Export DXF: no CAD pads available.");
            return;
        }

        try
        {
            var path = await PickSaveExportPathAsync(
                visibleOnly ? "cad_visible_layers" : "cad_all_layers",
                "dxf");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var text = Services.DxfExportService.ExportAscii(pads);
            File.WriteAllText(path, text);
            SetStatus($"DXF exported ({(visibleOnly ? "visible" : "all")}). Pads={pads.Count}.");
            Logger.Info(CultureInfo.InvariantCulture, "DXF exported to {0} (mode={1}, pads={2}).", path, visibleOnly ? "visible" : "all", pads.Count);
        }
        catch (Exception ex)
        {
            SetStatusError("Export DXF failed", ex);
            Logger.Error(ex, "Export DXF failed.");
        }
    }

    private List<CadPad> BuildVisibleDxfExportPads(CadPadSet cad)
    {
        var pads = CadPads.ToList();
        if (pads.Count == 0)
        {
            return pads;
        }

        if (RegularSourceMode != FreeformHelper.Application.Settings.RegularSourceMode.FromDxfLayer)
        {
            return pads;
        }

        var regularLayerName = SelectedRegularSourceLayerOption?.Name;
        if (string.IsNullOrWhiteSpace(regularLayerName))
        {
            return pads;
        }

        var visibleIds = pads.Select(pad => pad.Id).ToHashSet();
        var appended = 0;
        foreach (var pad in cad.Pads)
        {
            if (IsCadPadEffectivelyHidden(pad.Id) ||
                !string.Equals(pad.Layer, regularLayerName, StringComparison.OrdinalIgnoreCase) ||
                !visibleIds.Add(pad.Id))
            {
                continue;
            }

            pads.Add(pad);
            appended++;
        }

        if (appended > 0)
        {
            Logger.Info(CultureInfo.InvariantCulture, "DXF visible export appended regular source layer pads: layer={0}, appended={1}, total={2}.",
                regularLayerName,
                appended,
                pads.Count);
        }

        return pads;
    }
}

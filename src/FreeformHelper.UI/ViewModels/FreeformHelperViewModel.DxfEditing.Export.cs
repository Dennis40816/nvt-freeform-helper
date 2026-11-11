using System.Globalization;
using FreeformHelper.Application.CadEditing;
using FreeformHelper.Application.Services;
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
            : cad.Pads.Where(p => !CadDocument.IsEffectivelyHidden(p.Id)).ToList();

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
            await Services.TextExportWriter.WriteAsync(path, text, includeBom: false, CancellationToken.None);
            SetStatus($"DXF exported ({(visibleOnly ? "visible" : "all")}). Pads={pads.Count}.");
            _logger.Info(CultureInfo.InvariantCulture, "DXF exported to {0} (mode={1}, pads={2}).", path, visibleOnly ? "visible" : "all", pads.Count);
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            SetStatusError("Export DXF failed", ex);
            _logger.Error(ex, "Export DXF failed.");
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

        var toAppend = DxfVisibilityRules.SelectRegularSourceLayerPadsToAppend(pads, cad, regularLayerName, CadDocument.IsEffectivelyHidden);
        pads.AddRange(toAppend);
        var appended = toAppend.Count;

        if (appended > 0)
        {
            _logger.Info(CultureInfo.InvariantCulture, "DXF visible export appended regular source layer pads: layer={0}, appended={1}, total={2}.",
                regularLayerName,
                appended,
                pads.Count);
        }

        return pads;
    }

    private async Task ExportLayerImageAsync()
    {
        if (_cad is null)
        {
            SetStatus("Export DXF image: import DXF first.");
            if (ShowWarningAsync is not null)
            {
                await ShowWarningAsync("DXF not loaded",
                    "Export DXF image requires an imported DXF.\nPlease load a DXF file first.");
            }

            return;
        }

        var openDialogAsync = OpenDxfLayerImageExportAsync;
        var pickSavePathAsync = PickSaveExportPathAsync;
        if (openDialogAsync is null || pickSavePathAsync is null)
        {
            SetStatus("Export DXF image: dialog handler not wired.");
            return;
        }

        var pads = CadDocument.BuildActiveCad()?.Pads;
        if (pads is null || pads.Count == 0)
        {
            SetStatus("Export DXF image: no CAD pads available.");
            return;
        }

        try
        {
            var context = BuildLayerImageExportContext(pads, pickSavePathAsync);
            if (context.Entries.Count == 0)
            {
                SetStatus("Export DXF image: no layer found.");
                return;
            }

            var request = await openDialogAsync(context);
            if (request is null || string.IsNullOrWhiteSpace(request.SavePath) || request.Layers.Count == 0)
            {
                return;
            }

            await CompleteLayerImageExportAsync(context, request);
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            SetStatusError("Export DXF image failed", ex);
            _logger.Error(ex, "Export DXF image failed.");
        }
    }

    internal LayerImageExportContext BuildLayerImageExportContext(
        IReadOnlyList<CadPad> pads, Func<string, string, Task<string?>> pickSavePathAsync)
    {
        ArgumentNullException.ThrowIfNull(pickSavePathAsync);
        var pixels = _projectFile.Settings.ActiveAreaPixels;
        var source = new LayerImageExportSource(pads, pixels.X, pixels.Y);
        var allNames = Array.AsReadOnly(source.Entries.Select(entry => entry.Name).ToArray());
        var preferences = _dxfLayerImageExportPreferences;
        IReadOnlyList<string> checkedNames = allNames.Contains(preferences.LayerName, StringComparer.Ordinal)
            ? new List<string> { preferences.LayerName }.AsReadOnly()
            : allNames;
        return new LayerImageExportContext(source.Entries, checkedNames, source.CreateDefaultSize(checkedNames),
            preferences.LineWidthPixels, preferences.UseDarkTheme, preferences.Format,
            source.PadsOfLayer, source.CreateDefaultSize,
            (name, extension) => pickSavePathAsync(SanitizeFileNameToken(name), extension),
            static (_, _, _) => Task.FromResult<byte[]?>(null));
    }

    private async Task CompleteLayerImageExportAsync(LayerImageExportContext context, LayerImageExportRequest request)
    {
        var writeAsync = context.WriteLayerImagesAsync;
        if (writeAsync is null)
        {
            SetStatus("Export layer image: writer not wired.");
            return;
        }

        var mode = request.OneFilePerLayer ? LayerImageOutputMode.OnePerLayer : LayerImageOutputMode.OneImage;
        var jobs = LayerImageOutputPlanner.Plan(request.Layers, mode, Path.GetFileNameWithoutExtension(request.SavePath));
        var remembered = request.ToRememberedRequest();
        var writeRequest = new LayerImageWriteRequest(jobs, context.PadsOfLayer, request.Size,
            request.LineWidthPixels, request.UseDarkTheme, request.Format, request.SavePath);
        var paths = await writeAsync(writeRequest);
        RememberDxfLayerImageExportRequest(remembered);
        string format = request.Format.ToString().ToUpperInvariant();
        SetStatus(string.Create(CultureInfo.InvariantCulture,
            $"Layer image exported ({paths.Count} {(paths.Count == 1 ? "file" : "files")}, {format})."));
        _logger.Info(CultureInfo.InvariantCulture,
            "Layer image exported to {0}. files={1}, layers={2}, size={3}x{4}, lineWidth={5}, paddingX={6}, paddingY={7}, format={8}, dark={9}.",
            request.SavePath, paths.Count, request.Layers.Count, request.Size.PixelsX, request.Size.PixelsY,
            request.LineWidthPixels, request.Size.PaddingX, request.Size.PaddingY, request.Format, request.UseDarkTheme);
    }
}

// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.CadEditing;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.CadEditing;
using FreeformHelper.UI.CadExport;
using NLog;

namespace FreeformHelper.UI.Services;

// UI-thread orchestration. Document and preferences stay with their existing owners.
internal sealed class CadExportWorkflow(
    CadDocumentSession document, CadExportDialogs dialogs, CadExportInputs inputs, CadExportEffects effects, Logger logger)
    : ICadExportWorkflow
{
    public async Task ExportDxfAsync(bool visibleOnly)
    {
        var cad = document.Cad;
        if (cad is null)
        {
            effects.SetStatus("Export DXF: import DXF first.");
            return;
        }

        if (dialogs.PickSaveExportPathAsync is null)
        {
            effects.SetStatus("Export DXF: dialog handler not wired.");
            return;
        }

        var pads = visibleOnly
            ? BuildVisibleDxfExportPads(cad)
            : DxfVisibilityRules.SelectAllExportPads(cad, document.IsEffectivelyHidden);
        if (pads.Count == 0)
        {
            effects.SetStatus(visibleOnly ? "Export DXF: no CAD output pads." : "Export DXF: no CAD pads available.");
            return;
        }

        await WriteDxfAsync(pads, visibleOnly);
    }

    private async Task WriteDxfAsync(List<CadPad> pads, bool visibleOnly)
    {
        try
        {
            var path = await dialogs.PickSaveExportPathAsync!(
                visibleOnly ? "cad_visible_layers" : "cad_all_layers", "dxf");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var text = DxfExportService.ExportAscii(pads);
            await TextExportWriter.WriteAsync(path, text, includeBom: false, CancellationToken.None);
            effects.SetStatus($"DXF exported ({(visibleOnly ? "visible" : "all")}). Pads={pads.Count}.");
            logger.Info(CultureInfo.InvariantCulture, "DXF exported to {0} (mode={1}, pads={2}).",
                path, visibleOnly ? "visible" : "all", pads.Count);
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            effects.SetStatusError("Export DXF failed", ex);
            logger.Error(ex, "Export DXF failed.");
        }
    }

    private List<CadPad> BuildVisibleDxfExportPads(CadPadSet cad)
    {
        var pads = inputs.VisiblePads().ToList();
        if (pads.Count == 0 || inputs.RegularSourceMode() != RegularSourceMode.FromDxfLayer)
        {
            return pads;
        }

        var regularLayerName = inputs.RegularSourceLayerName();
        if (string.IsNullOrWhiteSpace(regularLayerName))
        {
            return pads;
        }

        var toAppend = DxfVisibilityRules.SelectRegularSourceLayerPadsToAppend(
            pads, cad, regularLayerName, document.IsEffectivelyHidden);
        pads.AddRange(toAppend);
        if (toAppend.Count > 0)
        {
            logger.Info(CultureInfo.InvariantCulture,
                "DXF visible export appended regular source layer pads: layer={0}, appended={1}, total={2}.",
                regularLayerName, toAppend.Count, pads.Count);
        }

        return pads;
    }

    public async Task ExportLayerImageAsync()
    {
        if (document.Cad is null)
        {
            await ReportMissingImageCadAsync();
            return;
        }

        var openDialogAsync = dialogs.OpenDxfLayerImageExportAsync;
        var pickSavePathAsync = dialogs.PickSaveExportPathAsync;
        if (openDialogAsync is null || pickSavePathAsync is null)
        {
            effects.SetStatus("Export DXF image: dialog handler not wired.");
            return;
        }

        var pads = document.BuildActiveCad()?.Pads;
        if (pads is null || pads.Count == 0)
        {
            effects.SetStatus("Export DXF image: no CAD pads available.");
            return;
        }

        await OpenLayerImageExportAsync(pads, pickSavePathAsync, openDialogAsync);
    }

    private async Task ReportMissingImageCadAsync()
    {
        effects.SetStatus("Export DXF image: import DXF first.");
        if (dialogs.ShowWarningAsync is not null)
        {
            await dialogs.ShowWarningAsync("DXF not loaded",
                "Export DXF image requires an imported DXF.\nPlease load a DXF file first.");
        }
    }

    private async Task OpenLayerImageExportAsync(
        IReadOnlyList<CadPad> pads,
        Func<string, string, Task<string?>> pickSavePathAsync,
        Func<LayerImageExportContext, Task<LayerImageExportRequest?>> openDialogAsync)
    {
        try
        {
            var context = BuildLayerImageExportContext(pads, pickSavePathAsync);
            if (context.Entries.Count == 0)
            {
                effects.SetStatus("Export DXF image: no layer found.");
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
            effects.SetStatusError("Export DXF image failed", ex);
            logger.Error(ex, "Export DXF image failed.");
        }
    }

    public LayerImageExportContext BuildLayerImageExportContext(
        IReadOnlyList<CadPad> pads, Func<string, string, Task<string?>> pickSavePathAsync)
    {
        ArgumentNullException.ThrowIfNull(pickSavePathAsync);
        var pixels = inputs.ActiveAreaPixels();
        var source = new LayerImageExportSource(pads, pixels.X, pixels.Y);
        var allNames = Array.AsReadOnly(source.Entries.Select(entry => entry.Name).ToArray());
        var preferences = inputs.ImagePreferences();
        IReadOnlyList<string> checkedNames = allNames.Contains(preferences.LayerName, StringComparer.Ordinal)
            ? new List<string> { preferences.LayerName }.AsReadOnly()
            : allNames;
        return new LayerImageExportContext(source.Entries, checkedNames, source.CreateDefaultSize(checkedNames),
            preferences.LineWidthPixels, preferences.UseDarkTheme, preferences.Format,
            source.PadsOfLayer, source.CreateDefaultSize,
            (name, extension) => pickSavePathAsync(LayerImageFileName.SanitizeToken(name), extension),
            static (_, _, _) => Task.FromResult<byte[]?>(null));
    }

    private async Task CompleteLayerImageExportAsync(LayerImageExportContext context, LayerImageExportRequest request)
    {
        var writeAsync = context.WriteLayerImagesAsync;
        if (writeAsync is null)
        {
            effects.SetStatus("Export layer image: writer not wired.");
            return;
        }

        var mode = request.OneFilePerLayer ? LayerImageOutputMode.OnePerLayer : LayerImageOutputMode.OneImage;
        var jobs = LayerImageOutputPlanner.Plan(request.Layers, mode, Path.GetFileNameWithoutExtension(request.SavePath));
        var remembered = request.ToRememberedRequest();
        var writeRequest = new LayerImageWriteRequest(jobs, context.PadsOfLayer, request.Size,
            request.LineWidthPixels, request.UseDarkTheme, request.Format, request.SavePath);
        var paths = await writeAsync(writeRequest);
        // Preserve writeback to the current preference owner even if the project changed during either await.
        effects.RememberImagePreferences(remembered);
        string format = request.Format.ToString().ToUpperInvariant();
        effects.SetStatus(string.Create(CultureInfo.InvariantCulture,
            $"Layer image exported ({paths.Count} {(paths.Count == 1 ? "file" : "files")}, {format})."));
        logger.Info(CultureInfo.InvariantCulture,
            "Layer image exported to {0}. files={1}, layers={2}, size={3}x{4}, lineWidth={5}, paddingX={6}, paddingY={7}, format={8}, dark={9}.",
            request.SavePath, paths.Count, request.Layers.Count, request.Size.PixelsX, request.Size.PixelsY,
            request.LineWidthPixels, request.Size.PaddingX, request.Size.PaddingY, request.Format, request.UseDarkTheme);
    }
}

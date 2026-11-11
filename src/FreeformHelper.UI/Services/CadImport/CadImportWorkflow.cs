using System.Globalization;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.CadEditing;
using FreeformHelper.UI.CadImport;
using FreeformHelper.UI.Services.CadEditing;
using NLog;

namespace FreeformHelper.UI.Services.CadImport;

/// <summary>Owns ordered CAD acceptance and import effects; the document session remains the only CAD store.</summary>
internal sealed class CadImportWorkflow : ICadImportWorkflow
{
    private readonly CadDocumentSession _document;
    private readonly CadEditWorkflow _editing;
    private readonly ICadLayerWorkflow _layers;
    private readonly CadLoadWorkflowService _loader;
    private readonly LayerCatalogStateService _catalog;
    private readonly DxfImportService _importer;
    private readonly Func<DxfImportOptions> _buildOptions;
    private readonly CadImportEffects _effects;
    private readonly Logger _logger;

    internal CadImportWorkflow(CadDocumentSession document, CadEditWorkflow editing, ICadLayerWorkflow layers,
        CadLoadWorkflowService loader, LayerCatalogStateService catalog, DxfImportService importer,
        Func<DxfImportOptions> buildOptions, CadImportEffects effects, Logger logger)
    {
        _document = document;
        _editing = editing;
        _layers = layers;
        _loader = loader;
        _catalog = catalog;
        _importer = importer;
        _buildOptions = buildOptions;
        _effects = effects;
        _logger = logger;
    }

    public Task ApplyOutcomeAsync(CadLoadOutcome outcome, UiViewSnapshot? snapshot) =>
        ApplyOutcomeCoreAsync(outcome, snapshot, _effects.CaptureNotifications());

    private async Task ApplyOutcomeCoreAsync(CadLoadOutcome outcome, UiViewSnapshot? snapshot, CadEditNotificationSnapshot? previous)
    {
        previous ??= _effects.CaptureNotifications();
        _document.Cad = outcome.Cad;
        _effects.ResetMask(!_effects.IsLoading());
        _document.ClearAutoHiddenDuplicates();
        _editing.ClearRuntime(resetBaseline: false, previous);
        _editing.SetAutoHiddenDuplicates(outcome.DuplicateSanitization);
        _editing.ResetBaseline(outcome.Cad, previous);
        _effects.SyncHidden(previous);
        _effects.SyncCombined(previous);
        _effects.SyncDuplicates(previous);
        _document.InvalidateFilteredCad();
        await _layers.LoadLayerOptionsAsync(outcome.Cad, snapshot?.LayerSelections);
    }

    public async Task OpenAsync(Func<Task<string?>>? pickPath)
    {
        if (pickPath is null)
        {
            _effects.SetStatus("Open DXF: dialog handler not wired.");
            return;
        }
        if (!_effects.CanProceed()) return;
        var path = await pickPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        await _loader.OpenDxfAsync(CreateRequest(path));
    }

    private OpenDxfWorkflowRequest CreateRequest(string path)
    {
        CadEditNotificationSnapshot? previous = null;
        return new OpenDxfWorkflowRequest
        {
            Path = path,
            BuildDxfOptions = _buildOptions,
            SetStatus = _effects.SetStatus,
            LoadLayerCatalogFromPathAsync = selectedPath =>
            {
                _catalog.SetFromPath(selectedPath, _importer.ImportedCatalog);
                return Task.CompletedTask;
            },
            ResetHiddenCadPads = () =>
            {
                previous = _effects.CaptureNotifications();
                _editing.ResetHiddenPads(previous);
            },
            ApplyCadLoadOutcomeAsync = outcome => ApplyOutcomeCoreAsync(outcome, null, previous),
            SetProjectLastDxfPath = _effects.SetProjectPath,
            ClearLastSavedPath = _effects.ClearSavedPath,
            MarkUnsaved = _effects.MarkUnsaved,
            BuildStatus = outcome => BuildCadLoadStatus(outcome, true, null),
            LogCadLoadOutcome = outcome => LogCadLoadOutcome(outcome, true),
            TriggerGridRebuildAsync = _effects.Rebuild,
            RunWithCadLoadCanvasOverlayAsync = _effects.WithOverlay,
            GetCadPadCountForLog = () => _document.Cad?.Pads.Count ?? 0,
            RunUiOperationAsync = _effects.RunOperation,
            OnError = ReportImportError,
        };
    }

    private void ReportImportError(Exception ex)
    {
        _effects.SetError("DXF import failed", ex);
        _logger.Error(ex, "DXF import failed.");
    }

    public void ClearLayerCatalog() => _catalog.Clear();
    public Task LoadCatalogFromPathAsync(string path) => Task.Run(() => _catalog.TryLoadFromPath(path));
    public Task LoadCatalogFromEmbeddedAsync(byte[]? data) => Task.Run(() => _catalog.TryLoadFromEmbedded(data));

    internal static string BuildCadLoadStatus(CadLoadOutcome outcome, bool isOpenDxf, string? projectPath)
    {
        var sourceLabel = outcome.Source == CadLoadSource.Path ? "path" : "embedded";
        var padCount = outcome.Cad.Pads.Count;
        var activePadCount = padCount - outcome.DuplicateSanitization.ExcludedPadIds.Count;
        var duplicateNote = outcome.DuplicateSanitization.HasExcludedDuplicates
            ? $" Auto-hidden same-layer duplicates: {outcome.DuplicateSanitization.ExcludedPadIds.Count}."
            : string.Empty;
        if (isOpenDxf)
        {
            return $"DXF imported ({sourceLabel}). CAD pads: {activePadCount}/{padCount}.{duplicateNote}";
        }

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return $"Project loaded. DXF ({sourceLabel}), CAD pads: {activePadCount}/{padCount}.{duplicateNote}";
        }

        return $"Project loaded: {projectPath}. DXF ({sourceLabel}), CAD pads: {activePadCount}/{padCount}.{duplicateNote}";
    }

    internal void LogCadLoadOutcome(CadLoadOutcome outcome, bool isOpenDxf)
    {
        if (outcome.DuplicateSanitization.HasExcludedDuplicates)
        {
            _logger.Warn(
                CultureInfo.InvariantCulture,
                "CAD load auto-hidden same-layer exact duplicate pads. groups={0}, duplicateIds={1}.",
                outcome.DuplicateSanitization.DuplicateSameLayerGroupCount,
                string.Join(", ", outcome.DuplicateSanitization.ExcludedPadIds));
        }

        if (outcome.Source == CadLoadSource.Path)
        {
            var label = isOpenDxf ? "DXF imported" : "DXF reloaded";
            _logger.Info(CultureInfo.InvariantCulture, "{0}: {1} (pads={2})", label, outcome.SourcePath, outcome.Cad.Pads.Count);
            return;
        }

        _logger.Info(CultureInfo.InvariantCulture, "DXF loaded from embedded data (pads={0})", outcome.Cad.Pads.Count);
    }

}

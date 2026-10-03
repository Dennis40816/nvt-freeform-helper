using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private const string RegularVisibilityMaskNotLoadedSummary =
        "Regular visibility mask: not loaded. Import Regular Visibility Mask (SeeRegular.csv) in Step 1 to constrain Step 4 geometry seed and Simulation.";

    private IReadOnlySet<int>? _loadedRegularVisibilityMaskPadIds;
    private RegularVisibilityMaskResult _regularVisibilityMaskResult = RegularVisibilityMaskResult.NotFound();
    private string? _regularVisibilityMaskSourcePath;
    private byte[]? _embeddedRegularVisibilityMask;
    private string? _embeddedRegularVisibilityMaskName;
    private bool _suppressRegularVisibilityMaskStateChange;

    private async Task ImportRegularVisibilityMaskAsync()
    {
        if (PickOpenRegularVisibilityMaskPathAsync is null)
        {
            SetStatus("Regular visibility mask import: dialog handler not wired.");
            return;
        }

        if (_grid is null)
        {
            SetStatus("Regular visibility mask import: build regular grid first.");
            return;
        }

        var sourcePath = await PickOpenRegularVisibilityMaskPathAsync();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        _regularVisibilityMaskSourcePath = sourcePath;
        _embeddedRegularVisibilityMask = null;
        _embeddedRegularVisibilityMaskName = null;
        _projectFile.EmbeddedRegularVisibilityMask = null;
        _projectFile.EmbeddedRegularVisibilityMaskName = null;
        RefreshRegularVisibilityMask(_grid);

        if (_regularVisibilityMaskResult.HasLoadedMask)
        {
            SetRegularVisibilityMaskEnabled(value: true, refreshAssignment: true);
            SetStatus(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Regular visibility mask imported: {0}. Active={1}, inactive={2}, frames={3}.",
                    Path.GetFileName(sourcePath),
                    _regularVisibilityMaskResult.ActiveRegularCount,
                    _regularVisibilityMaskResult.InactiveRegularCount,
                    _regularVisibilityMaskResult.FrameCount));
            return;
        }

        SetRegularVisibilityMaskEnabled(value: false, refreshAssignment: true);
        SetStatus(RegularVisibilityMaskSummary);
    }

    private void RefreshRegularVisibilityMask(RegularGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var result = _embeddedRegularVisibilityMask is not null
            ? RegularVisibilityMaskService.LoadEmbedded(
                _embeddedRegularVisibilityMask,
                ResolveRegularVisibilityMaskSourceLabel(),
                grid,
                DiffFrameGridRowOrigin.TopRowIsRegularLastRow)
            : RegularVisibilityMaskService.Load(
                _regularVisibilityMaskSourcePath,
                grid,
                DiffFrameGridRowOrigin.TopRowIsRegularLastRow);

        ApplyRegularVisibilityMaskResult(result, grid);
    }

    private void ApplyRegularVisibilityMaskSnapshot(
        string? sourcePath,
        bool isEnabled,
        bool allowRuntimeRefresh,
        byte[]? embeddedMask = null,
        string? embeddedMaskName = null)
    {
        _regularVisibilityMaskSourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? null
            : sourcePath.Trim();
        _embeddedRegularVisibilityMask = embeddedMask?.ToArray();
        _embeddedRegularVisibilityMaskName = string.IsNullOrWhiteSpace(embeddedMaskName)
            ? null
            : embeddedMaskName.Trim();

        if (allowRuntimeRefresh && _grid is not null)
        {
            RefreshRegularVisibilityMask(_grid);
        }
        else
        {
            var result = string.IsNullOrWhiteSpace(_regularVisibilityMaskSourcePath)
                ? RegularVisibilityMaskResult.NotFound()
                : RegularVisibilityMaskResult.NotFound(_regularVisibilityMaskSourcePath);
            ApplyRegularVisibilityMaskResult(result, _grid);
        }

        var shouldRefreshAssignment = allowRuntimeRefresh && _grid is not null && _cad is not null;
        SetRegularVisibilityMaskEnabled(isEnabled, shouldRefreshAssignment);
    }

    private void ClearRegularVisibilityMask()
    {
        var hadMask = !string.IsNullOrWhiteSpace(_regularVisibilityMaskSourcePath) ||
                      _loadedRegularVisibilityMaskPadIds is not null ||
                      IsRegularVisibilityMaskEnabled;

        _regularVisibilityMaskSourcePath = null;
        _embeddedRegularVisibilityMask = null;
        _embeddedRegularVisibilityMaskName = null;
        _projectFile.EmbeddedRegularVisibilityMask = null;
        _projectFile.EmbeddedRegularVisibilityMaskName = null;
        ApplyRegularVisibilityMaskResult(RegularVisibilityMaskResult.NotFound(), _grid);
        SetRegularVisibilityMaskEnabled(value: false, refreshAssignment: false);

        if (hadMask)
        {
            RefreshCadOutputFwDiffIndexingForRegularVisibilityMaskChange();
            SetStatus("Regular visibility mask cleared. Step 4 uses pure geometry seed.");
        }
    }

    private void ResetRegularVisibilityMaskSelection(bool clearProjectEmbeddedMask)
    {
        _regularVisibilityMaskSourcePath = null;
        _embeddedRegularVisibilityMask = null;
        _embeddedRegularVisibilityMaskName = null;
        if (clearProjectEmbeddedMask)
        {
            _projectFile.EmbeddedRegularVisibilityMask = null;
            _projectFile.EmbeddedRegularVisibilityMaskName = null;
        }

        ApplyRegularVisibilityMaskResult(RegularVisibilityMaskResult.NotFound(), _grid);
        SetRegularVisibilityMaskEnabled(value: false, refreshAssignment: false);
    }

    private void ApplyRegularVisibilityMaskResult(RegularVisibilityMaskResult result, RegularGrid? grid)
    {
        _regularVisibilityMaskResult = result;
        _loadedRegularVisibilityMaskPadIds = result.HasLoadedMask
            ? result.ActiveRegularPadIds
            : null;
        InvalidateWorkflowDataSnapshot();
        RegularVisibilityMaskSummary = BuildRegularVisibilityMaskSummary(result, grid, IsRegularVisibilityMaskEnabled);
        RefreshStep4DuplicateDiffGroups();

        if (result.HasLoadedMask)
        {
            Logger.Info(
                CultureInfo.InvariantCulture,
                "Regular visibility mask loaded: source={0}, active={1}, inactive={2}, frames={3}, enabled={4}.",
                result.SourcePath,
                result.ActiveRegularCount,
                result.InactiveRegularCount,
                result.FrameCount,
                IsRegularVisibilityMaskEnabled ? "Y" : "N");
        }
        else if (result.Status == RegularVisibilityMaskStatus.Incompatible)
        {
            Logger.Warn(
                CultureInfo.InvariantCulture,
                "Regular visibility mask ignored: source={0}, diagnostic={1}.",
                result.SourcePath,
                result.Diagnostic ?? "-");
        }
        else if (result.Status == RegularVisibilityMaskStatus.Failed)
        {
            Logger.Warn(
                CultureInfo.InvariantCulture,
                "Regular visibility mask failed: source={0}, diagnostic={1}.",
                result.SourcePath,
                result.Diagnostic ?? "-");
        }
    }

    private IReadOnlySet<int>? GetActiveRegularVisibilityMaskPadIds()
    {
        return IsRegularVisibilityMaskEnabled ? _loadedRegularVisibilityMaskPadIds : null;
    }

    private void RefreshCadOutputFwDiffIndexingForRegularVisibilityMaskChange()
    {
        if (_grid is null || _cad is null)
        {
            NotifySimulationWorkspaceSourceChanged();
            return;
        }

        var visiblePads = CadPads.Count > 0
            ? CadPads.ToList()
            : BuildFilteredCadPadSet()?.Pads.ToList();
        if (visiblePads is not null)
        {
            UpdateCadOutputFwDiffIndexing(visiblePads);
            CanvasHost?.Invalidate();
        }

        InvalidateDownstreamFromStep4(showStatus: false);
        NotifySimulationWorkspaceSourceChanged();
    }

    private void SetRegularVisibilityMaskEnabled(bool value, bool refreshAssignment)
    {
        if (IsRegularVisibilityMaskEnabled == value)
        {
            return;
        }

        _suppressRegularVisibilityMaskStateChange = true;
        IsRegularVisibilityMaskEnabled = value;
        _suppressRegularVisibilityMaskStateChange = false;
        InvalidateWorkflowDataSnapshot();

        RegularVisibilityMaskSummary = BuildRegularVisibilityMaskSummary(_regularVisibilityMaskResult, _grid, IsRegularVisibilityMaskEnabled);
        RefreshStep4DuplicateDiffGroups();
        if (refreshAssignment)
        {
            RefreshCadOutputFwDiffIndexingForRegularVisibilityMaskChange();
        }
    }

    partial void OnIsRegularVisibilityMaskEnabledChanged(bool value)
    {
        RegularVisibilityMaskSummary = BuildRegularVisibilityMaskSummary(_regularVisibilityMaskResult, _grid, value);
        RefreshStep4DuplicateDiffGroups();
        if (_suppressRegularVisibilityMaskStateChange)
        {
            return;
        }

        RefreshCadOutputFwDiffIndexingForRegularVisibilityMaskChange();
        SetStatus(value
            ? "Regular visibility mask enabled."
            : "Regular visibility mask disabled. Step 4 uses pure geometry seed.");
    }

    private static string BuildRegularVisibilityMaskSummary(RegularVisibilityMaskResult result, RegularGrid? grid, bool isEnabled)
    {
        return result.Status switch
        {
            RegularVisibilityMaskStatus.Loaded when isEnabled =>
                $"Regular visibility mask enabled: {Path.GetFileName(result.SourcePath)} loaded, active={result.ActiveRegularCount}, inactive={result.InactiveRegularCount}, frames={result.FrameCount}. Step 4 geometry seed only uses active regulars.",
            RegularVisibilityMaskStatus.Loaded =>
                $"Regular visibility mask loaded but disabled: {Path.GetFileName(result.SourcePath)}. Step 4 uses pure geometry seed.",
            RegularVisibilityMaskStatus.Incompatible =>
                $"Regular visibility mask: {Path.GetFileName(result.SourcePath)} incompatible with current grid {grid?.Rows ?? 0}x{grid?.Cols ?? 0}. Ignored. {result.Diagnostic}",
            RegularVisibilityMaskStatus.Failed =>
                $"Regular visibility mask: failed to load {Path.GetFileName(result.SourcePath)}. Ignored. {result.Diagnostic}",
            _ => RegularVisibilityMaskNotLoadedSummary,
        };
    }

    private string? ResolveRegularVisibilityMaskSourceLabel()
    {
        if (!string.IsNullOrWhiteSpace(_embeddedRegularVisibilityMaskName))
        {
            return _embeddedRegularVisibilityMaskName;
        }

        return string.IsNullOrWhiteSpace(_regularVisibilityMaskSourcePath)
            ? "SeeRegular.csv"
            : Path.GetFileName(_regularVisibilityMaskSourcePath);
    }
}

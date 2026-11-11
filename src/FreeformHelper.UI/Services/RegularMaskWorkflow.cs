// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Contracts;
using NLog;

namespace FreeformHelper.UI.Services;

/// <summary>Owns mask acceptance and ordered mapping/simulation effects. UI thread only.</summary>
internal sealed class RegularMaskWorkflow(
    IRegularMaskPresentation presentation,
    RegularMaskEffects effects,
    Logger logger) : IRegularMaskWorkflow
{
    private readonly IRegularMaskPresentation _presentation = presentation;
    private readonly RegularMaskEffects _effects = effects;
    private readonly Logger _logger = logger;

    // Accepted evidence and provenance survive a failed import; project reset and snapshot replay replace them.
    public RegularVisibilityMaskResult Mask { get; set; } = RegularVisibilityMaskResult.NotFound();
    public string? SourcePath { get; private set; }
    public byte[]? EmbeddedMask { get; private set; }
    public string? EmbeddedName { get; private set; }

    public IReadOnlySet<int>? ActiveRegularPadIds =>
        _presentation.IsEnabled && Mask.HasLoadedMask ? Mask.ActiveRegularPadIds : null;

    public void ResetResult(RegularGrid? grid)
    {
        var result = string.IsNullOrWhiteSpace(SourcePath)
            ? RegularVisibilityMaskResult.NotFound()
            : RegularVisibilityMaskResult.NotFound(SourcePath);
        ApplyResult(result, grid);
    }

    public async Task ImportAsync(Func<Task<string?>>? pickPathAsync)
    {
        if (pickPathAsync is null)
        {
            _effects.SetStatus("Regular visibility mask import: dialog handler not wired.");
            return;
        }

        if (_effects.GetGrid() is null)
        {
            _effects.SetStatus("Regular visibility mask import: build regular grid first.");
            return;
        }

        var sourcePath = await pickPathAsync();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        // Preserve the characterized acceptance against the current project/grid after the picker completes.
        var result = Load(sourcePath, _effects.GetGrid());
        if (!TryApplyLoad(result, _effects.GetGrid(), importSourcePath: sourcePath))
        {
            return;
        }

        SetEnabled(value: true, refreshAssignment: true);
        _effects.SetStatus(
            string.Format(
                CultureInfo.InvariantCulture,
                "Regular visibility mask imported: {0}. Active={1}, inactive={2}, frames={3}.",
                Path.GetFileName(sourcePath),
                Mask.ActiveRegularCount,
                Mask.InactiveRegularCount,
                Mask.FrameCount));
    }

    public void Refresh(RegularGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var result = EmbeddedMask is not null
            ? RegularVisibilityMaskService.LoadEmbedded(
                EmbeddedMask,
                ResolveSourceLabel(),
                grid,
                DiffFrameGridRowOrigin.TopRowIsRegularLastRow)
            : Load(
                SourcePath,
                grid);

        TryApplyLoad(result, grid);
    }

    private bool TryApplyLoad(
        RegularVisibilityMaskResult result,
        RegularGrid? grid,
        string? importSourcePath = null)
    {
        if (importSourcePath is not null)
        {
            if (!result.HasLoadedMask)
            {
                var failure = result.Status == RegularVisibilityMaskStatus.NotFound
                    ? RegularVisibilityMaskResult.Failed(importSourcePath, "File not found or unreadable.")
                    : result;
                _presentation.UpdateSummary(failure, grid, _presentation.IsEnabled);
                _effects.SetStatus(_presentation.Summary);
                return false;
            }

            SourcePath = importSourcePath;
            EmbeddedMask = null;
            EmbeddedName = null;
            _effects.ClearProjectEmbeddedMask();
        }

        ApplyResult(result, grid);
        return true;
    }

    private static RegularVisibilityMaskResult Load(string? sourcePath, RegularGrid? grid)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return RegularVisibilityMaskResult.NotFound(sourcePath);
        }

        ArgumentNullException.ThrowIfNull(grid);
        return RegularVisibilityMaskService.LoadText(
            sourcePath,
            () => new StreamReader(sourcePath),
            grid,
            DiffFrameGridRowOrigin.TopRowIsRegularLastRow);
    }

    public void ApplySnapshot(
        string? sourcePath,
        bool isEnabled,
        bool allowRuntimeRefresh,
        byte[]? embeddedMask = null,
        string? embeddedMaskName = null)
    {
        SourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? null
            : sourcePath.Trim();
        EmbeddedMask = embeddedMask?.ToArray();
        EmbeddedName = string.IsNullOrWhiteSpace(embeddedMaskName)
            ? null
            : embeddedMaskName.Trim();

        if (allowRuntimeRefresh && _effects.GetGrid() is { } grid)
        {
            Refresh(grid);
        }
        else
        {
            ResetResult(_effects.GetGrid());
        }

        var shouldRefreshAssignment = allowRuntimeRefresh && _effects.GetGrid() is not null && _effects.HasCad();
        SetEnabled(isEnabled, shouldRefreshAssignment);
    }

    public void Clear()
    {
        var hadMask = !string.IsNullOrWhiteSpace(SourcePath) ||
                      Mask.HasLoadedMask ||
                      _presentation.IsEnabled;

        SourcePath = null;
        EmbeddedMask = null;
        EmbeddedName = null;
        _effects.ClearProjectEmbeddedMask();
        ApplyResult(RegularVisibilityMaskResult.NotFound(), _effects.GetGrid());
        SetEnabled(value: false, refreshAssignment: false);

        if (hadMask)
        {
            RefreshMappingAndSimulation();
            _effects.SetStatus("Regular visibility mask cleared. Step 4 uses pure geometry seed.");
        }
    }

    public void ResetSelection(bool clearProjectEmbeddedMask)
    {
        SourcePath = null;
        EmbeddedMask = null;
        EmbeddedName = null;
        if (clearProjectEmbeddedMask)
        {
            _effects.ClearProjectEmbeddedMask();
        }

        ApplyResult(RegularVisibilityMaskResult.NotFound(), _effects.GetGrid());
        SetEnabled(value: false, refreshAssignment: false);
    }

    private void ApplyResult(RegularVisibilityMaskResult result, RegularGrid? grid)
    {
        Mask = result;
        _effects.InvalidateSnapshot();
        _presentation.UpdateSummary(result, grid, _presentation.IsEnabled);
        _effects.RefreshDuplicateGroups();

        if (result.HasLoadedMask)
        {
            _logger.Info(
                CultureInfo.InvariantCulture,
                "Regular visibility mask loaded: source={0}, active={1}, inactive={2}, frames={3}, enabled={4}.",
                result.SourcePath,
                result.ActiveRegularCount,
                result.InactiveRegularCount,
                result.FrameCount,
                _presentation.IsEnabled ? "Y" : "N");
        }
        else if (result.Status == RegularVisibilityMaskStatus.Incompatible)
        {
            _logger.Warn(
                CultureInfo.InvariantCulture,
                "Regular visibility mask ignored: source={0}, diagnostic={1}.",
                result.SourcePath,
                result.Diagnostic ?? "-");
        }
        else if (result.Status == RegularVisibilityMaskStatus.Failed)
        {
            _logger.Warn(
                CultureInfo.InvariantCulture,
                "Regular visibility mask failed: source={0}, diagnostic={1}.",
                result.SourcePath,
                result.Diagnostic ?? "-");
        }
    }

    private void RefreshMappingAndSimulation()
    {
        if (_effects.GetGrid() is null || !_effects.HasCad())
        {
            _effects.PublishSimulationChange();
            return;
        }

        var visiblePads = _effects.GetVisiblePads();
        if (visiblePads is not null)
        {
            _effects.UpdateMapping(visiblePads);
            _effects.InvalidateCanvas();
        }

        _effects.InvalidateDownstream();
        _effects.PublishSimulationChange();
    }

    public void SetEnabled(bool value, bool refreshAssignment)
    {
        // Reimporting while enabled keeps the existing same-value no-op, including its stale mapping behavior.
        if (_presentation.IsEnabled == value)
        {
            return;
        }

        using (_effects.EnterStateChange())
        {
            _presentation.IsEnabled = value;
        }
        _effects.InvalidateSnapshot();

        _presentation.UpdateSummary(Mask, _effects.GetGrid(), _presentation.IsEnabled);
        _effects.RefreshDuplicateGroups();
        if (refreshAssignment)
        {
            RefreshMappingAndSimulation();
        }
    }

    public void OnEnabledChanged(bool value)
    {
        _presentation.UpdateSummary(Mask, _effects.GetGrid(), value);
        _effects.RefreshDuplicateGroups();
        if (_effects.IsStateChangeActive())
        {
            return;
        }

        RefreshMappingAndSimulation();
        _effects.SetStatus(value
            ? "Regular visibility mask enabled."
            : "Regular visibility mask disabled. Step 4 uses pure geometry seed.");
    }

    private string? ResolveSourceLabel()
    {
        if (!string.IsNullOrWhiteSpace(EmbeddedName))
        {
            return EmbeddedName;
        }

        return string.IsNullOrWhiteSpace(SourcePath)
            ? "SeeRegular.csv"
            : Path.GetFileName(SourcePath);
    }
}

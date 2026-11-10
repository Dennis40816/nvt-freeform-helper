using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public bool CanRotateSelectedCadPads =>
        _cad is not null &&
        GetRotationTargetCadIds(_cad).Count > 0 &&
        Math.Abs(DxfEditRules.NormalizeRotationDegrees((double)DxfEditRotationDegrees)) > 1e-9;

    public string RotatedDxfEditBadgeTooltip =>
        FormattableString.Invariant($"Rotated pads: {RotatedCadPadCount}. Click to open geometry-rotation details.");

    private async Task OpenRotatedDxfEditChangeListAsync()
        => await ShowDxfEditChangeListAsync(DxfEditChangeKind.Rotated);

    private void RotateSelectedCadPads()
    {
        if (!TryGetLoadedCad(() => _cad, "DXF rotate: import DXF first.", out var cad))
        {
            _logger.Debug(CultureInfo.InvariantCulture, "DXF rotate skipped: no DXF loaded.");
            return;
        }

        var rotationDegrees = DxfEditRules.NormalizeRotationDegrees((double)DxfEditRotationDegrees);
        if (Math.Abs(rotationDegrees) < 1e-9)
        {
            SetStatus("DXF rotate: input a non-zero angle first.");
            return;
        }

        var selectedIds = GetRotationTargetCadIds(cad);
        if (selectedIds.Count == 0)
        {
            SetStatus(BuildRotateUnavailableStatus());
            _logger.Debug(CultureInfo.InvariantCulture, "DXF rotate skipped: no CAD pad resolved for scope {0}.", SelectedDxfEditRotationScopeOption.Value);
            return;
        }

        var undoSnapshot = CaptureCadEditUndoSnapshot();
        var result = CadPadRotationService.Rotate(cad, selectedIds, rotationDegrees);
        if (result.ChangedCount == 0)
        {
            SetStatus("DXF rotate: no CAD pad rotated.");
            return;
        }

        _cad = result.Cad;
        _cachedFilteredCadForBuild = null;
        SyncRelayeredCadState();
        SyncRotatedCadState();
        InteractionState.ClosePadInfo();
        _selectionCoordinator.ClearSelection(CanvasHost);
        FilterCadPadsByLayer();
        if (!RecalcBoundsOnLayerFilter)
        {
            _ = TriggerGridRebuildAsync(requestFit: false);
        }

        SetStatus($"DXF rotate: rotated {result.ChangedCount} CAD pad(s) by {result.AppliedDegrees:0.###}°.");
        _logger.Info(
            CultureInfo.InvariantCulture,
            "DXF rotate applied. changedPads={0}, degrees={1:0.###}.",
            result.ChangedCount,
            result.AppliedDegrees);
        MarkUnsaved();
        PushUndo(() => RestoreCadEditUndoSnapshot(undoSnapshot), "DXF rotate CAD pads");
    }

    private HashSet<int> GetRotationTargetCadIds(CadPadSet? cad)
    {
        if (cad is null)
        {
            return new HashSet<int>();
        }

        var baselineCadIds = _cadEdit.BaselineCad?.Pads.Select(static pad => pad.Id).ToHashSet() ?? new HashSet<int>();
        if (baselineCadIds.Count == 0)
        {
            return new HashSet<int>();
        }

        return SelectedDxfEditRotationScopeOption.Value switch
        {
            DxfEditRotationScope.TargetLayer =>
                DxfEditRules.ResolveRotationTargetLayerCadIds(cad, baselineCadIds, DxfEditRotationLayerName, IsCadPadEffectivelyHidden),
            _ =>
                DxfEditRules.ResolveRotationSelectedCadIds(_selectedCadIds, baselineCadIds, IsCadPadEffectivelyHidden),
        };
    }

    private string BuildRotateUnavailableStatus()
    {
        return SelectedDxfEditRotationScopeOption.Value switch
        {
            DxfEditRotationScope.TargetLayer when string.IsNullOrWhiteSpace(DxfEditRules.NormalizeLayerName(DxfEditRotationLayerName))
                => "DXF rotate: select rotation layer first.",
            DxfEditRotationScope.TargetLayer
                => "DXF rotate: selected layer has no visible baseline CAD pads.",
            _ => "DXF rotate: select baseline CAD pads first.",
        };
    }

    private void RestoreRotatedCadPadGeometry(int cadPadId)
    {
        if (_cad is null || _cadEdit.BaselineCad is null)
        {
            return;
        }

        var baselinePad = _cadEdit.BaselineCad.Pads.FirstOrDefault(pad => pad.Id == cadPadId);
        if (baselinePad is null)
        {
            return;
        }

        var changedCount = 0;
        var updatedPads = new List<CadPad>(_cad.Pads.Count);
        foreach (var pad in _cad.Pads)
        {
            if (pad.Id != cadPadId)
            {
                updatedPads.Add(pad);
                continue;
            }

            if (DxfEditRules.ArePolygonVerticesEquivalent(pad.Polygon, baselinePad.Polygon))
            {
                updatedPads.Add(pad);
                continue;
            }

            updatedPads.Add(new CadPad(pad.Id, pad.Name, pad.Layer, baselinePad.Polygon));
            changedCount++;
        }

        if (changedCount == 0)
        {
            return;
        }

        var undoSnapshot = CaptureCadEditUndoSnapshot();
        _cad = new CadPadSet(updatedPads);
        _cachedFilteredCadForBuild = null;
        SyncRelayeredCadState();
        SyncRotatedCadState();
        InteractionState.ClosePadInfo();
        _selectionCoordinator.ClearSelection(CanvasHost);
        FilterCadPadsByLayer();
        if (!RecalcBoundsOnLayerFilter)
        {
            _ = TriggerGridRebuildAsync(requestFit: false);
        }

        SetStatus($"DXF edit: restored CAD {cadPadId} geometry.");
        _logger.Info(
            CultureInfo.InvariantCulture,
            "DXF geometry restore applied. cadId={0}.",
            cadPadId);
        MarkUnsaved();
        PushUndo(() => RestoreCadEditUndoSnapshot(undoSnapshot), "DXF restore CAD geometry");
    }

    private void ApplyPersistedCadGeometryOverrides(Dictionary<int, ProjectCadGeometrySnapshot> geometryOverrides)
    {
        if (_cad is null || geometryOverrides.Count == 0)
        {
            return;
        }

        var updatedPads = new List<CadPad>(_cad.Pads.Count);
        foreach (var pad in _cad.Pads)
        {
            if (!geometryOverrides.TryGetValue(pad.Id, out var geometryOverride) ||
                geometryOverride.Vertices.Count < 3)
            {
                updatedPads.Add(pad);
                continue;
            }

            updatedPads.Add(new CadPad(pad.Id, pad.Name, pad.Layer, geometryOverride.ToPolygon()));
        }

        _cad = new CadPadSet(updatedPads);
    }

    partial void OnDxfEditRotationDegreesChanged(decimal value)
    {
        NotifyDxfEditPanelActionStateChanged();
    }
}

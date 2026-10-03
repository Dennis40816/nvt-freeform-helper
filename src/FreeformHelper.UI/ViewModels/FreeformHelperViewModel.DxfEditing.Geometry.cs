using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public bool CanRotateSelectedCadPads =>
        _cad is not null &&
        GetRotationTargetCadIds(_cad).Count > 0 &&
        Math.Abs(NormalizeDxfRotationDegrees((double)DxfEditRotationDegrees)) > 1e-9;

    public string RotatedDxfEditBadgeTooltip =>
        FormattableString.Invariant($"Rotated pads: {RotatedCadPadCount}. Click to open geometry-rotation details.");

    private async Task OpenRotatedDxfEditChangeListAsync()
        => await ShowDxfEditChangeListAsync(DxfEditChangeKind.Rotated);

    private void RotateSelectedCadPads()
    {
        if (!TryGetLoadedCad(() => _cad, "DXF rotate: import DXF first.", out var cad))
        {
            Logger.Debug(CultureInfo.InvariantCulture, "DXF rotate skipped: no DXF loaded.");
            return;
        }

        var rotationDegrees = NormalizeDxfRotationDegrees((double)DxfEditRotationDegrees);
        if (Math.Abs(rotationDegrees) < 1e-9)
        {
            SetStatus("DXF rotate: input a non-zero angle first.");
            return;
        }

        var selectedIds = GetRotationTargetCadIds(cad);
        if (selectedIds.Count == 0)
        {
            SetStatus(BuildRotateUnavailableStatus());
            Logger.Debug(CultureInfo.InvariantCulture, "DXF rotate skipped: no CAD pad resolved for scope {0}.", SelectedDxfEditRotationScopeOption.Value);
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
        Logger.Info(
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

        var baselineCadIds = _dxfEditBaselineCad?.Pads.Select(static pad => pad.Id).ToHashSet() ?? new HashSet<int>();
        if (baselineCadIds.Count == 0)
        {
            return new HashSet<int>();
        }

        return SelectedDxfEditRotationScopeOption.Value switch
        {
            DxfEditRotationScope.TargetLayer => ResolveRotationTargetLayerCadIds(cad, baselineCadIds),
            _ => ResolveRotationSelectedCadIds(baselineCadIds),
        };
    }

    private HashSet<int> ResolveRotationSelectedCadIds(HashSet<int> baselineCadIds)
    {
        return _selectedCadIds
            .Where(id => !IsCadPadEffectivelyHidden(id))
            .Where(baselineCadIds.Contains)
            .Distinct()
            .ToHashSet();
    }

    private HashSet<int> ResolveRotationTargetLayerCadIds(CadPadSet cad, HashSet<int> baselineCadIds)
    {
        var targetLayerName = NormalizeDxfLayerName(DxfEditRotationLayerName);
        if (string.IsNullOrWhiteSpace(targetLayerName))
        {
            return new HashSet<int>();
        }

        return cad.Pads
            .Where(pad => baselineCadIds.Contains(pad.Id))
            .Where(pad => !IsCadPadEffectivelyHidden(pad.Id))
            .Where(pad => string.Equals(pad.Layer, targetLayerName, StringComparison.OrdinalIgnoreCase))
            .Select(static pad => pad.Id)
            .ToHashSet();
    }

    private string BuildRotateUnavailableStatus()
    {
        return SelectedDxfEditRotationScopeOption.Value switch
        {
            DxfEditRotationScope.TargetLayer when string.IsNullOrWhiteSpace(NormalizeDxfLayerName(DxfEditRotationLayerName))
                => "DXF rotate: select rotation layer first.",
            DxfEditRotationScope.TargetLayer
                => "DXF rotate: selected layer has no visible baseline CAD pads.",
            _ => "DXF rotate: select baseline CAD pads first.",
        };
    }

    private void RestoreRotatedCadPadGeometry(int cadPadId)
    {
        if (_cad is null || _dxfEditBaselineCad is null)
        {
            return;
        }

        var baselinePad = _dxfEditBaselineCad.Pads.FirstOrDefault(pad => pad.Id == cadPadId);
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

            if (ArePolygonVerticesEquivalent(pad.Polygon, baselinePad.Polygon))
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
        Logger.Info(
            CultureInfo.InvariantCulture,
            "DXF geometry restore applied. cadId={0}.",
            cadPadId);
        MarkUnsaved();
        PushUndo(() => RestoreCadEditUndoSnapshot(undoSnapshot), "DXF restore CAD geometry");
    }

    private Dictionary<int, ProjectCadGeometrySnapshot> CapturePersistedCadGeometryOverrides()
    {
        if (_cad is null || _dxfEditBaselineCad is null)
        {
            return new Dictionary<int, ProjectCadGeometrySnapshot>();
        }

        var baselinePadsByCadId = _dxfEditBaselineCad.Pads
            .GroupBy(static pad => pad.Id)
            .ToDictionary(static group => group.Key, static group => group.First());

        return _cad.Pads
            .Where(pad =>
                baselinePadsByCadId.TryGetValue(pad.Id, out var baselinePad) &&
                !ArePolygonVerticesEquivalent(baselinePad.Polygon, pad.Polygon))
            .ToDictionary(static pad => pad.Id, ProjectCadGeometrySnapshot.FromCadPad);
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

    private static double NormalizeDxfRotationDegrees(double rotationDegrees)
    {
        var normalized = rotationDegrees % 360.0;
        if (normalized <= -180.0)
        {
            normalized += 360.0;
        }
        else if (normalized > 180.0)
        {
            normalized -= 360.0;
        }

        return Math.Abs(normalized) < 1e-9 ? 0.0 : normalized;
    }

    private static bool ArePolygonVerticesEquivalent(Polygon2 left, Polygon2 right, double epsilon = 1e-6)
    {
        if (left.Vertices.Length != right.Vertices.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Vertices.Length; index++)
        {
            var leftVertex = left.Vertices[index];
            var rightVertex = right.Vertices[index];
            if (Math.Abs(leftVertex.X - rightVertex.X) > epsilon ||
                Math.Abs(leftVertex.Y - rightVertex.Y) > epsilon)
            {
                return false;
            }
        }

        return true;
    }

    partial void OnDxfEditRotationDegreesChanged(decimal value)
    {
        NotifyDxfEditPanelActionStateChanged();
    }
}

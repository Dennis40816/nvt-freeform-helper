using System.Globalization;
using FreeformHelper.Application.CadEditing;
using FreeformHelper.Domain.Pads;

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

        _cadEditWorkflow.Rotate(cad, selectedIds, rotationDegrees);
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
                DxfEditRules.ResolveRotationTargetLayerCadIds(cad, baselineCadIds, DxfEditRotationLayerName, CadDocument.IsEffectivelyHidden),
            _ =>
                DxfEditRules.ResolveRotationSelectedCadIds(_selectedCadIds, baselineCadIds, CadDocument.IsEffectivelyHidden),
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

    private void RestoreRotatedCadPadGeometry(int cadPadId) => _cadEditWorkflow.RestoreGeometry(cadPadId);

    partial void OnDxfEditRotationDegreesChanged(decimal value)
    {
        NotifyDxfEditPanelActionStateChanged();
    }
}

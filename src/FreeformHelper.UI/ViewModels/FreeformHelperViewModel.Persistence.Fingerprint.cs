using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void BumpNotchExportCadFingerprint()
    {
        _notchExportFingerprintRevisions.BumpCad();

        InvalidatePadInspectorDerivedCache();
    }

    private void BumpNotchExportGridFingerprint()
    {
        _notchExportFingerprintRevisions.BumpGrid();

        InvalidatePadInspectorDerivedCache();
    }

    private void BumpNotchExportIndexFingerprint()
    {
        _notchExportFingerprintRevisions.BumpIndex();

        InvalidatePadInspectorDerivedCache();
    }

    private ulong BuildNotchExportCadFingerprint(CadPadSet cad)
    {
        ArgumentNullException.ThrowIfNull(cad);

        return NotchExportFingerprints.BuildCadFingerprint(
            _notchExportFingerprintRevisions.Cad, cad.Pads.Count,
            _cadEdit.HiddenCadPadIds.Count + _cadEdit.AutoHiddenDuplicateCadPadIds.Count);
    }

    private ulong BuildNotchExportGridFingerprint(RegularGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        return NotchExportFingerprints.BuildExportGridFingerprint(
            _notchExportFingerprintRevisions.Grid, grid.Pads.Count, _notchExportFingerprintRevisions.Index);
    }

    private ulong BuildNotchComputationGridFingerprint(RegularGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        return NotchExportFingerprints.BuildComputationGridFingerprint(
            _notchExportFingerprintRevisions.Grid, grid.Pads.Count);
    }
}

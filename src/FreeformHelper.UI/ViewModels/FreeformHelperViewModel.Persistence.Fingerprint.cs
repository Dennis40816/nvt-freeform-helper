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

        unchecked
        {
            var hash = 1469598103934665603UL;
            hash = (hash * 1099511628211UL) ^ (uint)_notchExportFingerprintRevisions.Cad;
            hash = (hash * 1099511628211UL) ^ (uint)cad.Pads.Count;
            hash = (hash * 1099511628211UL) ^ (uint)(_cadEdit.HiddenCadPadIds.Count + _cadEdit.AutoHiddenDuplicateCadPadIds.Count);
            return hash;
        }
    }

    private ulong BuildNotchExportGridFingerprint(RegularGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        unchecked
        {
            var hash = 1469598103934665603UL;
            hash = (hash * 1099511628211UL) ^ (uint)_notchExportFingerprintRevisions.Grid;
            hash = (hash * 1099511628211UL) ^ (uint)grid.Pads.Count;
            hash = (hash * 1099511628211UL) ^ (uint)_notchExportFingerprintRevisions.Index;
            return hash;
        }
    }

    private ulong BuildNotchComputationGridFingerprint(RegularGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        unchecked
        {
            var hash = 1469598103934665603UL;
            hash = (hash * 1099511628211UL) ^ (uint)_notchExportFingerprintRevisions.Grid;
            hash = (hash * 1099511628211UL) ^ (uint)grid.Pads.Count;
            return hash;
        }
    }
}

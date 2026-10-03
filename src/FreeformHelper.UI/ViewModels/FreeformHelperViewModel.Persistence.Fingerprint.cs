using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private int _notchExportCadFingerprintRevision = 1;
    private int _notchExportGridFingerprintRevision = 1;
    private int _notchExportIndexFingerprintRevision = 1;

    private void BumpNotchExportCadFingerprint()
    {
        unchecked
        {
            _notchExportCadFingerprintRevision++;
        }

        InvalidatePadInspectorDerivedCache();
    }

    private void BumpNotchExportGridFingerprint()
    {
        unchecked
        {
            _notchExportGridFingerprintRevision++;
        }

        InvalidatePadInspectorDerivedCache();
    }

    private void BumpNotchExportIndexFingerprint()
    {
        unchecked
        {
            _notchExportIndexFingerprintRevision++;
        }

        InvalidatePadInspectorDerivedCache();
    }

    private ulong BuildNotchExportCadFingerprint(CadPadSet cad)
    {
        ArgumentNullException.ThrowIfNull(cad);

        unchecked
        {
            var hash = 1469598103934665603UL;
            hash = (hash * 1099511628211UL) ^ (uint)_notchExportCadFingerprintRevision;
            hash = (hash * 1099511628211UL) ^ (uint)cad.Pads.Count;
            hash = (hash * 1099511628211UL) ^ (uint)(_hiddenCadPadIds.Count + _autoHiddenDuplicateCadPadIds.Count);
            return hash;
        }
    }

    private ulong BuildNotchExportGridFingerprint(RegularGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        unchecked
        {
            var hash = 1469598103934665603UL;
            hash = (hash * 1099511628211UL) ^ (uint)_notchExportGridFingerprintRevision;
            hash = (hash * 1099511628211UL) ^ (uint)grid.Pads.Count;
            hash = (hash * 1099511628211UL) ^ (uint)_notchExportIndexFingerprintRevision;
            return hash;
        }
    }

    private ulong BuildNotchComputationGridFingerprint(RegularGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        unchecked
        {
            var hash = 1469598103934665603UL;
            hash = (hash * 1099511628211UL) ^ (uint)_notchExportGridFingerprintRevision;
            hash = (hash * 1099511628211UL) ^ (uint)grid.Pads.Count;
            return hash;
        }
    }
}

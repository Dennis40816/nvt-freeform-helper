using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void InvalidateNotchCompensationCache()
    {
        _notchComputationSession.Invalidate();
        NotifySimulationWorkspaceSourceChanged();
    }

    private double GetStrictOverlapRatioForNotchCompensation()
    {
        return NotchV22CompensationService.NormalizeStrictOverlapRatio(GetNotchMultiOwnerStrictOverlapPercent() / 100.0);
    }
}

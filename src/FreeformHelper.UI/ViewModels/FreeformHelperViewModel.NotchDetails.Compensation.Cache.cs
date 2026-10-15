using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void InvalidateNotchCompensationCache()
    {
        _notchCompensationCacheCounters.RecordInvalidation(_notchCache.ResolvedResultByCadId.Count);
        _notchCompensationCacheRevision++;
        _notchCache.ResolvedResultByCadId.Clear();
        NotifySimulationWorkspaceSourceChanged();
    }

    private double GetStrictOverlapRatioForNotchCompensation()
    {
        return NotchV22CompensationService.NormalizeStrictOverlapRatio(GetNotchMultiOwnerStrictOverlapPercent() / 100.0);
    }

    private static int ComputeActiveRegularHash(IReadOnlySet<int>? activeRegularPadIds)
    {
        if (activeRegularPadIds is null || activeRegularPadIds.Count == 0)
        {
            return -1;
        }

        unchecked
        {
            var hash = 17;
            foreach (var id in activeRegularPadIds.OrderBy(static id => id))
            {
                hash = (hash * 31) + id;
            }

            return hash;
        }
    }
}

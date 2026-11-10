using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private IReadOnlyList<CadPad> ResolveCadPadsForComputationSource()
    {
        return CadPads.Count > 0 ? CadPads : (_cad?.Pads ?? CadPads);
    }

    private List<CadPad> GetCadPadsForNotchComputation(int? targetCadPadId = null)
    {
        return NotchComputationInputs.SelectCadPadsForComputation(
            ResolveCadPadsForComputationSource(), IsCadPadEffectivelyHidden, _grid, targetCadPadId, TryGetCadIcIndex);
    }

    private HashSet<int>? GetActiveRegularPadIdsForNotchComputation()
    {
        if (_workflowData.LatestPadMatch.RegularToCad.Count == 0)
        {
            return null;
        }

        return _workflowData.LatestPadMatch.RegularToCad.Keys.ToHashSet();
    }

    private bool TryResolveNotchComputationInputs(
        int cadPadId,
        out CadPad cad,
        out IReadOnlyList<CadPad> allCadPads,
        out IReadOnlySet<int>? activeRegularPadIds)
    {
        cad = null!;
        allCadPads = Array.Empty<CadPad>();
        activeRegularPadIds = null;

        if (_grid is null)
        {
            return false;
        }

        allCadPads = GetCadPadsForNotchComputation(cadPadId);
        var sourceCadPads = ResolveCadPadsForComputationSource();
        var targetCad = allCadPads.FirstOrDefault(p => p.Id == cadPadId)
            ?? sourceCadPads.FirstOrDefault(p => p.Id == cadPadId);
        if (targetCad is null)
        {
            return false;
        }

        cad = targetCad;
        activeRegularPadIds = GetActiveRegularPadIdsForNotchComputation();
        return true;
    }
}

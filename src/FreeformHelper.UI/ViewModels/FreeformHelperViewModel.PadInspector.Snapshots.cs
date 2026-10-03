using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private List<PadInspectorMatchedRegularSnapshot> BuildMatchedRegularDetails(
        List<int> matchedRegularIds,
        IReadOnlyList<PadMatchLink> matchedLinks)
    {
        var linkByRegularId = new Dictionary<int, PadMatchLink>();
        foreach (var link in matchedLinks)
        {
            if (!linkByRegularId.ContainsKey(link.RegularPadId))
            {
                linkByRegularId[link.RegularPadId] = link;
            }
        }

        var details = new List<PadInspectorMatchedRegularSnapshot>(matchedRegularIds.Count);
        foreach (var regularPadId in matchedRegularIds)
        {
            var icDiff = GetRegularPadIcDiff(regularPadId);
            linkByRegularId.TryGetValue(regularPadId, out var link);
            details.Add(new PadInspectorMatchedRegularSnapshot(
                RegularPadId: regularPadId,
                IcIndex: icDiff?.IcIndex,
                DiffIndex: icDiff?.DiffIndex,
                CadCoverage: link?.CadCoverage ?? 0.0,
                RegularCoverage: link?.RegularCoverage ?? 0.0));
        }

        return details;
    }

    private List<PadInspectorMatchedCadSnapshot> BuildMatchedCadDetails(
        List<int> matchedCadIds,
        IReadOnlyList<PadMatchLink> matchedLinks)
    {
        var linkByCadId = new Dictionary<int, PadMatchLink>();
        foreach (var link in matchedLinks)
        {
            if (!linkByCadId.ContainsKey(link.CadPadId))
            {
                linkByCadId[link.CadPadId] = link;
            }
        }

        var details = new List<PadInspectorMatchedCadSnapshot>(matchedCadIds.Count);
        foreach (var cadPadId in matchedCadIds)
        {
            var hasDxfIndex = TryGetCadOutputFwDiffIndex(cadPadId, out var dxfIndexValue);
            linkByCadId.TryGetValue(cadPadId, out var link);
            details.Add(new PadInspectorMatchedCadSnapshot(
                CadPadId: cadPadId,
                DxfIndex: hasDxfIndex ? dxfIndexValue : null,
                CadCoverage: link?.CadCoverage ?? 0.0,
                RegularCoverage: link?.RegularCoverage ?? 0.0));
        }

        return details;
    }

    private NotchCadRowEligibility BuildCadNotchRowEligibility(CadPad cadPad)
    {
        if (_grid is null)
        {
            return new NotchCadRowEligibility(
                CadPadId: cadPad.Id,
                EligibleVersions: Array.Empty<NotchAlgorithmVersion>(),
                EstimatedRowCount: 0,
                AnchorRegularPadId: null,
                Reason: "Grid not built.");
        }

        var settingsSnapshot = CreateExportSettingsSnapshot(_projectFile.Settings);
        ApplyUiToSettings(settingsSnapshot, markUnsaved: false);
        return _notchExportService.EvaluateCadRowEligibility(cadPad, _grid, settingsSnapshot);
    }

    private static NotchCadRowEligibility BuildDeferredNotchRowEligibilityPlaceholder(int cadPadId)
    {
        return new NotchCadRowEligibility(
            CadPadId: cadPadId,
            EligibleVersions: Array.Empty<NotchAlgorithmVersion>(),
            EstimatedRowCount: 0,
            AnchorRegularPadId: null,
            Reason: "Computing in background.");
    }
}

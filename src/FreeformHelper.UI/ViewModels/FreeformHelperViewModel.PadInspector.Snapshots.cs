using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels.Inspector;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private InspectorCadWorkflow CaptureInspectorCadWorkflow()
    {
        var snapshot = BuildWorkflowDataSnapshot();
        return new InspectorCadWorkflow(
            id => snapshot.TryGetCadOutputFwDiffIndex(id, out var index) ? index : (int?)null,
            id => snapshot.TryGetCadIcIndex(id, out var index) ? index : (int?)null,
            (id, expensive, compensation, diagnostics) =>
                BuildCadInspectorNotch(id, expensive, snapshot, compensation, diagnostics));
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
}

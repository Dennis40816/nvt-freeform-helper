using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
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

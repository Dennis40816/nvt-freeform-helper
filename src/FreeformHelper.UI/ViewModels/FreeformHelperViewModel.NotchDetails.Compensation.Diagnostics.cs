using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private static string? BuildToFullDiagnosticsText(NotchV22CompensationResult compensation)
        => NotchToFullDiagnostics.BuildToFullDiagnosticsText(compensation);
}

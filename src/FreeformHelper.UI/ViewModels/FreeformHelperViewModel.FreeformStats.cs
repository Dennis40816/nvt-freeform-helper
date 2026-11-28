namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void ResetFreeformAutoDetectStats(string summary = FreeformAutoDetectResult.NotRunSummary) =>
        GridMatching.ResetFreeformAutoDetectStats(summary);

    partial void OnFreeformAutoDetectResultChanged(FreeformAutoDetectResult? oldValue, FreeformAutoDetectResult newValue) =>
        GridMatching.OnFreeformAutoDetectResultChanged(oldValue, newValue);
}

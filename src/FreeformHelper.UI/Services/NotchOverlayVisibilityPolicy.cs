namespace FreeformHelper.UI.Services;

/// <summary>
/// Centralizes visibility rules for Step 3 notch overlays.
/// </summary>
public sealed class NotchOverlayVisibilityPolicy
{
    public static NotchOverlayVisibilityState Resolve(in NotchOverlayVisibilityInput input)
    {
        if (!input.HasPreviewData)
        {
            return NotchOverlayVisibilityState.Hidden;
        }

        var stage = Math.Clamp(input.PreviewStage, 1, 3);
        var showToFull = input.EnableToFullComputation && input.ShowToFullOverlay;

        return new NotchOverlayVisibilityState(
            ShowToRegularLabels: input.ShowToRegularLabels,
            ShowToFullSeed: showToFull && stage == 1,
            ShowToFullCandidate: showToFull && stage == 2,
            ShowToFullFinal: showToFull && stage == 3);
    }
}

public readonly record struct NotchOverlayVisibilityInput(
    bool HasPreviewData,
    bool EnableToFullComputation,
    bool ShowToFullOverlay,
    bool ShowToRegularLabels,
    int PreviewStage);

public readonly record struct NotchOverlayVisibilityState(
    bool ShowToRegularLabels,
    bool ShowToFullSeed,
    bool ShowToFullCandidate,
    bool ShowToFullFinal)
{
    public static NotchOverlayVisibilityState Hidden { get; } = new(
        ShowToRegularLabels: false,
        ShowToFullSeed: false,
        ShowToFullCandidate: false,
        ShowToFullFinal: false);

    public bool ShowAnyToFull => ShowToFullSeed || ShowToFullCandidate || ShowToFullFinal;
}

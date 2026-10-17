namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private Step4IndexState? _step4Index; // Created on first read so that the shift-delta change callback can point at this instance.

    /// <summary>
    /// Gets the Step 4 diff-index values that the Step 4 view binds to.
    /// </summary>
    public Step4IndexState Step4Index => _step4Index ??= new Step4IndexState(OnStep4ShiftDeltaChanged);
}

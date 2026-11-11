namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Gets the Step 4 diff-index values that the Step 4 view binds to.
    /// </summary>
    public Step4IndexState Step4Index => IndexMapping.Assignment.Step4Index;
}

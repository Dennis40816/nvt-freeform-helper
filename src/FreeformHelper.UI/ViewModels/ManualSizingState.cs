namespace FreeformHelper.UI.ViewModels;

/// <summary>Stores the manual sizing input and range error for one view model.</summary>
internal sealed class ManualSizingState
{
    public ManualSizingInput Input { get; set; } = ManualSizingInput.Initial;

    internal string RangeError = string.Empty;
}

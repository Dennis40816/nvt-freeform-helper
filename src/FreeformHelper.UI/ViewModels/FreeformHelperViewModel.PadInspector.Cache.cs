namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    // Selection keeps its existing entry point into the owner's shared request stamps.
    private PadInspectorRequestState _padInspectorRequests => Inspector.Requests;

    private void InvalidatePadInspectorDerivedCache() => Inspector.InvalidateDerivedCache();
}

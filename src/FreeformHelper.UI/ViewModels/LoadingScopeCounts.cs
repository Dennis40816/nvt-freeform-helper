namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds how many scopes are open for the CAD load canvas overlay and for the modal loading spinner, for the main view model.
/// </summary>
internal sealed class LoadingScopeCounts
{
    /// <summary>Gets or sets how many scopes keep the CAD load canvas overlay visible.</summary>
    internal int CadLoadCanvasOverlay { get; set; }

    /// <summary>Gets or sets how many scopes keep the modal loading spinner visible.</summary>
    internal int ModalLoadingSpinner { get; set; }
}

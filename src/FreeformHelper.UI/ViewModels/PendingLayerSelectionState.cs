namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the layer selection data that waits for the next CAD load or layer toggle: the undo snapshot and the layer names of the bounds and the regular source.
/// </summary>
internal sealed class PendingLayerSelectionState
{
    /// <summary>Gets or sets the layer visibility before the first toggle of a batch, kept for one undo entry.</summary>
    internal Dictionary<string, bool>? UndoSnapshot { get; set; }

    /// <summary>Gets or sets the bounds layer name that the next CAD load selects.</summary>
    internal string? BoundLayerName { get; set; }

    /// <summary>Gets or sets the regular source layer name that the next CAD load selects.</summary>
    internal string? RegularSourceLayerName { get; set; }
}

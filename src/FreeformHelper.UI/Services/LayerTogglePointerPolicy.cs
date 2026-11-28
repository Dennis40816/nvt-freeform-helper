using Avalonia.Input;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Decides what a press on a layer toggle button means.
/// </summary>
internal static class LayerTogglePointerPolicy
{
    /// <summary>
    /// True when the press asks to show only this layer: the left button with Control held.
    /// </summary>
    public static bool IsSoloRequest(bool isLeftButtonPressed, KeyModifiers modifiers)
        => isLeftButtonPressed && modifiers.HasFlag(KeyModifiers.Control);
}

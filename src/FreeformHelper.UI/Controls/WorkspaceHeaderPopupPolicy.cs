using Avalonia;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// The open/close and size rules of the view menu popup of the workspace header.
/// </summary>
internal static class WorkspaceHeaderPopupPolicy
{
    private const double MinPopupWidth = 260;
    private const double MinPopupHeight = 200;
    private const double WindowMargin = 30;

    /// <summary>
    /// The popup stays open while it is pinned or while the pointer is over the button or over the popup.
    /// </summary>
    public static bool ShouldBeOpen(bool pinned, bool buttonHovered, bool popupHovered)
    {
        return pinned || buttonHovered || popupHovered;
    }

    /// <summary>
    /// The largest size the popup may take. It leaves a margin to the window and never goes below a usable minimum.
    /// </summary>
    public static Size MaxSize(Size windowSize)
    {
        return new Size(
            Math.Max(MinPopupWidth, windowSize.Width - WindowMargin),
            Math.Max(MinPopupHeight, windowSize.Height - WindowMargin));
    }
}

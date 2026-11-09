using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using FreeformHelper.UI.Controls;

namespace FreeformHelper.UI.Views;

/// <summary>
/// Classifies the source of an input event in the main window.
/// </summary>
internal static class MainWindowInputSources
{
    /// <summary>
    /// True when the source is a text entry or list control that handles its own keys.
    /// The console list handles its own keys. Delete, F and Ctrl+Z must not reach the canvas from a log row.
    /// </summary>
    public static bool IsTextEntry(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        return visual is TextBox or ComboBox or NumberScrubber or ConsolePanel
            || visual.FindAncestorOfType<TextBox>() is not null
            || visual.FindAncestorOfType<ComboBox>() is not null
            || visual.FindAncestorOfType<NumberScrubber>() is not null
            || visual.FindAncestorOfType<ConsolePanel>() is not null;
    }

    /// <summary>
    /// True when the source is the pad canvas or one of its children.
    /// </summary>
    public static bool IsCanvas(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        return visual is PadCanvas || visual.FindAncestorOfType<PadCanvas>() is not null;
    }

    /// <summary>
    /// Determines if the focus should be cleared from input controls and transferred to the canvas.
    /// This is true if the clicked element is not an input control and not in the console panel.
    /// </summary>
    public static bool ShouldClearFocus(object? source)
    {
        if (source is not Visual visual)
        {
            return true;
        }

        if (visual is ConsolePanel || visual.FindAncestorOfType<ConsolePanel>() is not null)
        {
            return false;
        }

        if (visual.FindAncestorOfType<TextBox>() is not null) return false;
        if (visual.FindAncestorOfType<ComboBox>() is not null) return false;
        if (visual.FindAncestorOfType<NumberScrubber>() is not null) return false;

        return true;
    }
}

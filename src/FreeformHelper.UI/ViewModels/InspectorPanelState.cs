namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the inspector panel state of the main view model: the quick-focus query,
/// the active right panel tab flag and the raw rule trace expander flag.
/// The view model replaces the whole value when one of them changes and forwards one property per member
/// so the bindings keep their names.
/// </summary>
/// <param name="QuickFocusQueryText">The unified quick-focus query used by the Inspector panel. Examples: CAD 4809, REG 4616, DIFF 72, IC1 DIFF72.</param>
/// <param name="IsRightPanelSettingsTab">Whether right panel is currently on Settings tab.</param>
/// <param name="IsPadInspectorRawTraceExpanded">Whether Debug raw section in inspector rule trace is expanded.</param>
public sealed record InspectorPanelState(
    string QuickFocusQueryText,
    bool IsRightPanelSettingsTab,
    bool IsPadInspectorRawTraceExpanded)
{
    /// <summary>
    /// Gets the value of a new view model before any project settings are loaded.
    /// </summary>
    public static InspectorPanelState Initial { get; } = new(
        string.Empty,
        true,
        false);
}

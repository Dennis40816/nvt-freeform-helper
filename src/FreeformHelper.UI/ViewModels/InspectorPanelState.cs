namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the inspector panel state of the main view model: the quick-locate inputs, the quick-focus query,
/// the active right panel tab flag and the three rule trace expander flags.
/// The view model replaces the whole value when one of them changes and forwards one property per member
/// so the bindings keep their names.
/// </summary>
/// <param name="QuickLocateCadPadId">Quick-locate CAD pad id input used by Inspector panel.</param>
/// <param name="QuickLocateRegularPadIndex">Quick-locate regular pad index input used by Inspector panel.</param>
/// <param name="QuickFocusQueryText">The unified quick-focus query used by the Inspector panel. Examples: CAD 4809, REG 4616, DIFF 72, IC1 DIFF72.</param>
/// <param name="IsRightPanelSettingsTab">Whether right panel is currently on Settings tab.</param>
/// <param name="IsPadInspectorTraceExpanded">Whether pad inspector trace is expanded in top strip.</param>
/// <param name="IsPadInspectorTracePopupOpen">Whether top-strip rule trace popup is open.</param>
/// <param name="IsPadInspectorRawTraceExpanded">Whether Debug raw section in inspector rule trace is expanded.</param>
public sealed record InspectorPanelState(
    decimal QuickLocateCadPadId,
    decimal QuickLocateRegularPadIndex,
    string QuickFocusQueryText,
    bool IsRightPanelSettingsTab,
    bool IsPadInspectorTraceExpanded,
    bool IsPadInspectorTracePopupOpen,
    bool IsPadInspectorRawTraceExpanded)
{
    /// <summary>
    /// Gets the value of a new view model before any project settings are loaded.
    /// </summary>
    public static InspectorPanelState Initial { get; } = new(
        0m,
        0m,
        string.Empty,
        true,
        false,
        false,
        false);
}

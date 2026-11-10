// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Xunit;

namespace FreeformHelper.Tests.UI.Bindings;

/// <summary>
/// Binding problems that exist in the product today. Every entry names the problem and says why it is still allowed.
/// The lists only go down: fix the binding, then remove its entry. A problem that is not listed fails the test.
/// </summary>
internal static class BindingKnownIssues
{
    /// <summary>Paths that the type walker proves to be wrong. The key is the site location from the walker report.</summary>
    public static readonly IReadOnlyDictionary<string, string> Failures = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Views/SimulationHostView.axaml:87 SimulationWorkspaceView.IsVisible"] =
            "IsVisible binds HasUsableWorkspace, but the same element sets DataContext to CurrentWorkspace, so the path is read on " +
            "SimulationWorkspaceViewModel and the property is on SimulationHostViewModel. The fix changes behavior: owner decision, separate PR.",
    };

    /// <summary>
    /// Warnings that Avalonia logs while the main window runs. The key is Target|Expression|Message.
    /// Not yet sorted into "transient while the control is not attached" and "real defect": owner decision, then fix or keep with a reason.
    /// The reasons below only state what the test run saw; none of them is a verified cause.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Warnings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Foreground|$visualParent[Button].Foreground|Ancestor not found."] =
            "Seen 28 times in the canvas flyout phase: text there has no Button ancestor for the style setter.",
        ["Foreground|$visualParent[Window].Foreground|Ancestor not found."] =
            "Seen in the Help and Dev phase: the text has no Window ancestor at that moment.",
        ["DataContext|$visualParent[Window].DataContext|Ancestor not found."] =
            "Seen in the Help and Dev phase: the ConsolePanel has no Window ancestor at that moment.",
        ["IsVisible|#Root.Controller.Projection.IsEmpty|Value is null."] =
            "Seen in the Help and Dev phase: Controller is null when ConsoleEmptyState reads it.",
        ["Content|PadInspectorUi.Snapshot.Details|Value is null."] =
            "Seen at construction: Snapshot is null when the pad inspector reads it.",
        ["IsChecked|$visualParent[UserControl].DataContext.IsPadInspectorRawTraceExpanded|Ancestor not found."] =
            "Seen in the inspector and canvas flyout phases: the toggle has no UserControl ancestor at that moment.",
        ["IsVisible|$visualParent[UserControl].DataContext.IsPadInspectorRawTraceExpanded|Ancestor not found."] =
            "Seen in the inspector and canvas flyout phases: the list has no UserControl ancestor at that moment.",
    };

    public static void AssertOnlyKnownWarnings(AvaloniaBindingLogCapture capture)
    {
        var unknown = capture.Errors.Where(static entry => !Warnings.ContainsKey(entry.Key))
            .Select(static entry => $"{entry.Phase}: {entry.ControlType}.{entry.Target} {entry.Key}").Distinct(StringComparer.Ordinal).ToArray();
        Assert.True(unknown.Length == 0, "Binding warnings that are not in the known list:" + Environment.NewLine +
            string.Join(Environment.NewLine, unknown));
    }
}

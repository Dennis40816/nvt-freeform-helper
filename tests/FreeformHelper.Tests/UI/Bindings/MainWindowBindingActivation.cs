// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.SettingsSections;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests.UI.Bindings;

internal sealed class MainWindowBindingActivation(MainWindow window, AvaloniaBindingLogCapture capture)
{
    // UI thread only. A phase is covered only after its positive realization assertions pass.
    private readonly List<string> _realized = [];
    private readonly List<string> _uncovered =
    [
        "Conditional duplicate/statistic/validation and virtualized row branches",
        "CAD/Regular pad-info relation cards and close-confirmation overlay",
        "DXF, overlap, mapping, Notch and export secondary windows",
        "Simulation/Coordinate workspaces, help popups and the Simulation C# console binding",
        "Dynamic-resource foreground in both themes and converter-specific value propagation",
        "Live Settings IsEnabled transition while a project load is held at an asynchronous boundary",
    ];

    public string Format()
    {
        string[] planned = ["Workflow panels and populated layer template", "Inspector snapshot details",
            "Display popup and WorkspaceHeaderItem template", "Canvas help flyout", "Root menus and tooltip",
            "Cascade item template and two-way draft isolation", "Shell console source and two-way expansion",
            "Help page template", "Dev page template"];
        var uncovered = _uncovered.Concat(planned.Concat(Enum.GetValues<SettingsWindowSection>().Select(static section => "Settings " + section))
            .Except(_realized, StringComparer.Ordinal)).ToArray();
        return $"Realized activation phases: {_realized.Count}; uncovered activation groups: {uncovered.Length}." + Environment.NewLine +
            string.Join(Environment.NewLine, _realized.Select(static phase => "Realized: " + phase)) + Environment.NewLine +
            string.Join(Environment.NewLine, uncovered.Select(static phase => "Uncovered: " + phase));
    }

    public async Task RunAsync()
    {
        var shell = Assert.IsType<ShellViewModel>(window.DataContext);
        var view = Assert.Single(window.GetVisualDescendants().OfType<FreeformHelperView>());
        Assert.Same(shell.FreeformHelper, view.DataContext);
        await ExpandWorkspaceAsync(view, shell.FreeformHelper);
        await OpenDisplayPopupAsync(view, shell.FreeformHelper);
        await OpenCanvasHelpAsync(view);
        await OpenMenusAndTooltipAsync(view);
        await OpenSettingsAsync(view, shell);
        await ExerciseConsoleAsync(view, shell);
        await VisitShellPagesAsync(shell);
    }

    private async Task ExpandWorkspaceAsync(FreeformHelperView view, FreeformHelperViewModel helper)
    {
        capture.Phase = "workflow panels";
        helper.IsRightPanelSettingsTab = true;
        var activated = await ExpandPanelsAsync(view);
        Assert.Contains(activated.OfType<CollapsiblePanel>(), static panel => panel.Title.StartsWith("Step ", StringComparison.Ordinal));
        Assert.Contains(view.GetVisualDescendants().OfType<Control>(), static control =>
            control.DataContext is FreeformHelperViewModel.LayerToggle);
        RequireNoValidationErrors(view);
        _realized.Add("Workflow panels and populated layer template");

        capture.Phase = "inspector";
        Assert.NotEmpty(helper.CadPads);
        helper.ApplyCanvasSelection([helper.CadPads[0].Id], []);
        await HeadlessUiPump.WaitUntilAsync(() => helper.PadInspectorUi.Snapshot?.Cad?.CadPadId == helper.CadPads[0].Id,
            "Selecting a loaded CAD pad did not publish its inspector snapshot.");
        helper.IsRightPanelInspectorTab = true;
        helper.PadInspectorUi.IsDebugDetailsExpanded = true;
        helper.IsPadInspectorRawTraceExpanded = true;
        await ExpandPanelsAsync(view);
        Assert.Contains(view.GetVisualDescendants().OfType<Control>(), static control =>
            control.IsEffectivelyVisible && control.DataContext is PadInspectorSnapshotDetails);
        RequireNoValidationErrors(view);
        _realized.Add("Inspector snapshot details");
        helper.IsRightPanelSettingsTab = true;
    }

    private async Task OpenDisplayPopupAsync(FreeformHelperView view, FreeformHelperViewModel helper)
    {
        capture.Phase = "Display popup";
        var header = Assert.Single(view.GetVisualDescendants().OfType<WorkspaceHeader>());
        var popup = Assert.IsType<Popup>(header.FindControl<Popup>("ViewMenuPopup"));
        try
        {
            popup.IsOpen = true;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(popup.IsOpen);
            var child = Assert.IsAssignableFrom<Control>(popup.Child);
            Assert.NotNull(TopLevel.GetTopLevel(child));
            Assert.Same(helper, popup.DataContext);
            var toggle = child.GetVisualDescendants().OfType<ToggleSwitch>().FirstOrDefault(static control =>
                control.DataContext is WorkspaceHeaderItem);
            Assert.NotNull(toggle);
            Assert.NotNull(toggle.DataContext);
            RequireNoValidationErrors(child);
            _realized.Add("Display popup and WorkspaceHeaderItem template");
        }
        finally
        {
            popup.IsOpen = false;
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    private async Task OpenCanvasHelpAsync(FreeformHelperView view)
    {
        capture.Phase = "canvas flyout";
        var overlay = Assert.Single(view.GetVisualDescendants().OfType<CanvasOverlayControls>());
        var button = Assert.Single(overlay.GetVisualDescendants().OfType<Button>(), static button => button.Flyout is Flyout);
        var flyout = Assert.IsType<Flyout>(button.Flyout);
        try
        {
            flyout.ShowAt(button);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(flyout.IsOpen);
            var content = Assert.IsAssignableFrom<Control>(flyout.Content);
            Assert.NotNull(TopLevel.GetTopLevel(content));
            Assert.Contains(content.GetVisualDescendants().OfType<Button>(), static control => control.Command is not null);
            RequireNoValidationErrors(content);
            _realized.Add("Canvas help flyout");
        }
        finally
        {
            flyout.Hide();
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    private async Task OpenMenusAndTooltipAsync(FreeformHelperView view)
    {
        capture.Phase = "menus and tooltip";
        var menu = Assert.Single(view.GetVisualDescendants().OfType<Menu>());
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            try
            {
                item.IsSubMenuOpen = true;
                await HeadlessUiPump.FlushQueueAsync();
                Assert.True(item.IsSubMenuOpen);
                Assert.NotEmpty(item.Items);
                Assert.Contains(item.Items.OfType<Control>(), static child => TopLevel.GetTopLevel(child) is not null);
            }
            finally
            {
                item.IsSubMenuOpen = false;
            }
        }
        var target = view.GetVisualDescendants().OfType<Button>().First(static button =>
            button.IsEffectivelyVisible && ToolTip.GetTip(button) is TextBlock);
        try
        {
            ToolTip.SetIsOpen(target, true);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(ToolTip.GetIsOpen(target));
            var tip = Assert.IsType<TextBlock>(ToolTip.GetTip(target));
            Assert.NotNull(TopLevel.GetTopLevel(tip));
            _realized.Add("Root menus and tooltip");
        }
        finally
        {
            ToolTip.SetIsOpen(target, false);
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    private async Task OpenSettingsAsync(FreeformHelperView view, ShellViewModel shell)
    {
        capture.Phase = "Settings entry";
        var menu = Assert.Single(view.GetVisualDescendants().OfType<Menu>());
        var entry = Assert.Single(menu.Items.OfType<MenuItem>().SelectMany(static item => item.Items.OfType<MenuItem>()),
            static item => Equals(item.Header, "Open Settings"));
        entry.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await HeadlessUiPump.FlushQueueAsync();
        var settings = Assert.Single(window.OwnedWindows.OfType<SettingsWindow>());
        try
        {
            var draft = Assert.IsType<SettingsWindowViewModel>(settings.DataContext);
            Assert.NotSame(shell.FreeformHelper, settings.DataContext);
            Assert.Equal(shell.FreeformHelper.IsProjectEditingEnabled, settings.IsEnabled);
            MainWindowBindingCatalog.RequireOwningChildren(shell, draft);
            foreach (var section in Enum.GetValues<SettingsWindowSection>())
            {
                capture.Phase = "Settings " + section;
                settings.NavigateToSection(section);
                await HeadlessUiPump.FlushQueueAsync();
                Assert.Equal(section, draft.SelectedSection);
                Assert.Contains(settings.GetVisualDescendants().OfType<UserControl>(), control =>
                    control.GetType() == SectionType(section) && control.IsEffectivelyVisible && ReferenceEquals(control.DataContext, draft));
                RequireNoValidationErrors(settings);
                _realized.Add("Settings " + section);
            }
            settings.NavigateToSection(SettingsWindowSection.General);
            await HeadlessUiPump.FlushQueueAsync();
            await OpenCascadeAsync(settings, draft, shell.FreeformHelper);
        }
        finally
        {
            CloseOwnedWindows(settings);
            settings.Close();
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    private async Task OpenCascadeAsync(SettingsWindow settings, SettingsWindowViewModel draft, FreeformHelperViewModel live)
    {
        capture.Phase = "Cascade draft";
        var general = Assert.Single(settings.GetVisualDescendants().OfType<SettingsGeneralSectionView>());
        var button = Assert.Single(general.GetVisualDescendants().OfType<Button>(), static control =>
            ToolTip.GetTip(control) is TextBlock { Text: "Edit per-IC X/Y channels." });
        UiEventProbe.Click(button);
        await HeadlessUiPump.FlushQueueAsync();
        var cascade = Assert.Single(settings.OwnedWindows.OfType<CascadeIcSettingsWindow>());
        try
        {
            Assert.True(cascade.IsVisible);
            Assert.Same(draft, cascade.DataContext);
            Assert.NotEmpty(draft.CascadeIcSettings);
            var row = draft.CascadeIcSettings[0];
            var original = live.CascadeIcSettings[0].XChannels;
            var control = cascade.GetVisualDescendants().OfType<NumberScrubber>().First(scrubber =>
                ReferenceEquals(scrubber.DataContext, row) && scrubber.Value == row.XChannels);
            row.XChannels = original + 1;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(row.XChannels, control.Value);
            control.Value = original + 2;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(original + 2, row.XChannels);
            Assert.Equal(original, live.CascadeIcSettings[0].XChannels);
            RequireNoValidationErrors(cascade);
            _realized.Add("Cascade item template and two-way draft isolation");
        }
        finally
        {
            cascade.Close();
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    private async Task ExerciseConsoleAsync(FreeformHelperView view, ShellViewModel shell)
    {
        capture.Phase = "Shell console";
        var console = Assert.Single(view.GetVisualDescendants().OfType<ConsolePanel>());
        Assert.Same(shell, console.DataContext);
        shell.IsConsoleExpanded = true;
        await HeadlessUiPump.FlushQueueAsync();
        var toggle = Assert.IsType<ToggleButton>(console.FindControl<ToggleButton>("ConsoleToggleButton"));
        Assert.True(toggle.IsChecked);
        toggle.IsChecked = false;
        await HeadlessUiPump.FlushQueueAsync();
        Assert.False(shell.IsConsoleExpanded);
        RequireNoValidationErrors(console);
        _realized.Add("Shell console source and two-way expansion");
    }

    private async Task VisitShellPagesAsync(ShellViewModel shell)
    {
        capture.Phase = "Help and Dev";
        shell.ShowHowToUseCommand.Execute(null);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Single(window.GetVisualDescendants().OfType<HowToUseView>());
        _realized.Add("Help page template");
        if (shell.IsDevPageAvailable)
        {
            shell.ShowDevCommand.Execute(null);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Single(window.GetVisualDescendants().OfType<DevView>());
            _realized.Add("Dev page template");
        }
        RequireNoValidationErrors(window);
        shell.ShowWorkspaceCommand.Execute(null);
        await HeadlessUiPump.FlushQueueAsync();
    }

    private static async Task<IReadOnlyList<Control>> ExpandPanelsAsync(Control root)
    {
        // Workflow steps are mutually exclusive. Visit each branch before opening its sibling, not all at once.
        var activated = new HashSet<Control>();
        for (var pass = 0; pass < 200; pass++)
        {
            var next = root.GetVisualDescendants().OfType<Control>().FirstOrDefault(control =>
                control is CollapsiblePanel or HidePanelBlock && !activated.Contains(control));
            if (next is null)
            {
                return activated.ToArray();
            }
            activated.Add(next);
            if (next is CollapsiblePanel panel)
            {
                panel.IsExpanded = true;
                await HeadlessUiPump.FlushQueueAsync();
                Assert.True(panel.IsExpanded);
            }
            else
            {
                var block = Assert.IsType<HidePanelBlock>(next);
                block.IsExpanded = true;
                await HeadlessUiPump.FlushQueueAsync();
                Assert.True(block.IsExpanded);
            }
            RequireNoValidationErrors(next);
        }
        throw new InvalidOperationException("Panel realization did not converge after visiting 200 distinct panel instances.");
    }

    private static void RequireNoValidationErrors(Control root) => Assert.DoesNotContain(
        root.GetVisualDescendants().OfType<Control>().Prepend(root), static control => DataValidationErrors.GetHasErrors(control));

    private static Type SectionType(SettingsWindowSection section) => section switch
    {
        SettingsWindowSection.General => typeof(SettingsGeneralSectionView),
        SettingsWindowSection.Step1GeometryMatch => typeof(SettingsStep1SectionView),
        SettingsWindowSection.Step2FreeformTagging => typeof(SettingsStep2SectionView),
        SettingsWindowSection.Step3NotchCompensation => typeof(SettingsStep3SectionView),
        SettingsWindowSection.Step4IndexDiagnostics => typeof(SettingsStep4SectionView),
        SettingsWindowSection.Step5NotchExport => typeof(SettingsStep5SectionView),
        _ => throw new ArgumentOutOfRangeException(nameof(section)),
    };

    internal static void CloseOwnedWindows(Window owner)
    {
        foreach (var child in owner.OwnedWindows.ToArray())
        {
            CloseOwnedWindows(child);
            child.Close();
        }
    }
}

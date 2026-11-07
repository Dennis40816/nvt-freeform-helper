// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ConfirmWarningDialogsIntegratedLayoutTests
{
    private static readonly ThemeVariant[] _themes = [ThemeVariant.Dark, ThemeVariant.Light];
    private static readonly Key[] _warningDismissKeys = [Key.Enter, Key.Escape];
    private static readonly string?[] _nextStartMessages =
        [null, "", "  ", "This also overwrites app-level general settings for next startup."];
    private static readonly (string Title, string Confirm, string Cancel, string ConfirmTip, string CancelTip)[] _actions =
    [
        ("Reset DXF edits", "Reset", "Cancel", "Reset all DXF edits in this project.", "Keep the current DXF edits."),
        ("Reset all settings", "Reset", "Cancel", "Reset all settings to defaults.", "Keep the current settings."),
        ("Embed DXF", "Embed", "Skip", "Embed DXF into the project file.", "Cancel and keep external DXF reference."),
    ];
    private static readonly (string? Button, bool? Expected)[] _unsavedChoices =
        [("CancelButton", false), ("DismissButton", null), ("ConfirmButton", true), (null, null)];

    [AvaloniaFact]
    public async Task ConfirmFooterPlacesCancelLeftOfConfirmAndUsesCallerTooltipsInBothThemesAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        foreach (var action in _actions)
        {
            var dialog = new ConfirmDialog(action.Title, "Continue with this action?", action.Confirm, action.Cancel,
                confirmTip: action.ConfirmTip, cancelTip: action.CancelTip);
            await WithDialogAsync(dialog, async () =>
            {
                foreach (var theme in _themes)
                {
                    dialog.RequestedThemeVariant = theme;
                    await HeadlessUiPump.FlushQueueAsync();
                    var cancel = Find<Button>(dialog, "CancelButton");
                    var confirm = Find<Button>(dialog, "ConfirmButton");
                    var footer = Find<Grid>(dialog, "FooterActions");
                    Assert.Equal(HorizontalAlignment.Right, cancel.HorizontalAlignment);
                    Assert.True(BoundsIn(dialog, cancel).Right <= BoundsIn(dialog, confirm).Left);
                    Assert.Equal(BoundsIn(dialog, footer).Right, BoundsIn(dialog, confirm).Right, precision: 1);
                    Assert.Equal(action.Cancel, Find<SelectableTextBlock>(dialog, "CancelButtonText").Text);
                    Assert.Equal(action.Confirm, Find<SelectableTextBlock>(dialog, "ConfirmButtonText").Text);
                    Assert.Equal(action.CancelTip, TipText(cancel));
                    Assert.Equal(action.ConfirmTip, TipText(confirm));
                    Assert.Contains("actionNeutral", cancel.Classes);
                    Assert.Same(confirm, OnlyPrimary(dialog));
                    ToolTip.SetIsOpen(confirm, true);
                    await HeadlessUiPump.FlushQueueAsync();
                    Assert.True(ToolTip.GetIsOpen(confirm));
                    ToolTip.SetIsOpen(confirm, false);
                }
            });
        }
    }

    [AvaloniaFact]
    public async Task NextStartupParagraphAppearsOnlyForCallerSuppliedTextAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        foreach (var message in _nextStartMessages)
        {
            var dialog = new ConfirmDialog("Reset all settings", "Reset all settings to defaults?", "Reset", "Cancel",
                nextStartMessage: message);
            await WithDialogAsync(dialog, () =>
            {
                var section = Find<Border>(dialog, "NextStartSection");
                Assert.Equal(!string.IsNullOrWhiteSpace(message), section.IsEffectivelyVisible);
                Assert.Contains("workspaceSectionCard", section.Classes);
                Assert.Equal(message, Find<SelectableTextBlock>(dialog, "NextStartText").Text);
                Assert.Equal("Reset all settings to defaults?", Find<SelectableTextBlock>(dialog, "MessageText").Text);
                return Task.CompletedTask;
            });
        }
    }

    [AvaloniaFact]
    public async Task ConfirmLongConsequencesScrollWhileTitleAndFooterStayVisibleAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var consequences = LongMessage();
        var dialog = new ConfirmDialog("Reset DXF edits", "Reset all DXF edits?", "Reset", "Cancel",
            consequenceText: consequences);
        await WithDialogAsync(dialog, async () =>
        {
            var scroll = Find<ScrollViewer>(dialog, "MessageScroll");
            var body = Find<SelectableTextBlock>(dialog, "ConsequencesText");
            Assert.Equal(consequences, body.Text);
            Assert.Equal(TextWrapping.Wrap, body.TextWrapping);
            AssertScrollable(scroll);
            var title = BoundsIn(dialog, Find<SelectableTextBlock>(dialog, "TitleText"));
            var footer = BoundsIn(dialog, Find<Grid>(dialog, "FooterActions"));
            Assert.True(title.Bottom <= BoundsIn(dialog, scroll).Top);
            Assert.True(BoundsIn(dialog, scroll).Bottom <= footer.Top);
            scroll.Offset = new Vector(0, scroll.Extent.Height);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(scroll.Offset.Y > 0);
            Assert.Equal(title, BoundsIn(dialog, Find<SelectableTextBlock>(dialog, "TitleText")));
            Assert.Equal(footer, BoundsIn(dialog, Find<Grid>(dialog, "FooterActions")));
        });
    }

    [AvaloniaFact]
    public async Task UnsavedProjectCallerSeparatesDangerActionAndKeepsSaveAsOnlyPrimaryAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var helper = new FreeformHelperViewModel();
        using var shell = new ShellViewModel(helper) { IsDevPageAvailable = true };
        shell.ShowHowToUseCommand.Execute(null);
        var owner = new MainWindow();
        owner.SetShellViewModel(shell);
        try
        {
            owner.Show();
            helper.HasUnsavedChanges = true;
            owner.Close();
            await HeadlessUiPump.WaitUntilAsync(() => owner.OwnedWindows.OfType<ConfirmDialog>().Any(),
                "Closing the unsaved project did not open its confirmation dialog.");
            var dialog = Assert.Single(owner.OwnedWindows.OfType<ConfirmDialog>());
            await HeadlessUiPump.FlushQueueAsync();
            var discard = Find<Button>(dialog, "CancelButton");
            var cancel = Find<Button>(dialog, "DismissButton");
            var save = Find<Button>(dialog, "ConfirmButton");
            Assert.Contains("actionDanger", discard.Classes);
            Assert.DoesNotContain("actionNeutral", discard.Classes);
            Assert.Equal(HorizontalAlignment.Left, discard.HorizontalAlignment);
            Assert.True(BoundsIn(dialog, discard).Right < BoundsIn(dialog, cancel).Left);
            Assert.True(BoundsIn(dialog, cancel).Right <= BoundsIn(dialog, save).Left);
            Assert.Equal("Exit without saving", Find<SelectableTextBlock>(dialog, "CancelButtonText").Text);
            Assert.Equal("Cancel", cancel.Content);
            Assert.Equal("Save and exit", Find<SelectableTextBlock>(dialog, "ConfirmButtonText").Text);
            Assert.Equal("Exit without saving the project.", TipText(discard));
            Assert.Equal("Keep the project open.", TipText(cancel));
            Assert.Equal("Save the project and exit.", TipText(save));
            Assert.Same(save, OnlyPrimary(dialog));
            Assert.False(Find<Border>(dialog, "NextStartSection").IsEffectivelyVisible);
            UiEventProbe.Click(cancel);
            await HeadlessUiPump.WaitUntilAsync(() => !dialog.IsVisible && owner.IsEnabled,
                "Cancel did not return control to the unsaved project.");
            Assert.True(owner.IsVisible);
            Assert.True(helper.HasUnsavedChanges);
        }
        finally
        {
            owner.DataContext = null;
            owner.Close();
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(helper);
        }
    }

    [AvaloniaFact]
    public async Task UnsavedProjectPreservesTrueFalseAndNullResultsAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        foreach (var choice in _unsavedChoices)
        {
            var dialog = new ConfirmDialog("Unsaved project", "Save before exiting?", "Save and exit", "Exit without saving",
                emphasizeCancel: true, dismissText: "Cancel");
            Assert.Equal(choice.Expected, await ChooseConfirmationAsync(dialog, choice.Button));
        }
    }

    [AvaloniaFact]
    public async Task WarningLongBodyScrollsBelowFixedHeaderAndEnterOrEscapeClosesAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        foreach (var key in _warningDismissKeys)
        {
            var message = LongMessage();
            var dialog = new WarningDialog("Warning", message);
            await WithDialogAsync(dialog, async () =>
            {
                var scroll = Find<ScrollViewer>(dialog, "MessageScroll");
                var body = Find<SelectableTextBlock>(dialog, "MessageText");
                var ok = Find<Button>(dialog, "OkButton");
                Assert.Equal(message, body.Text);
                Assert.Equal(TextWrapping.Wrap, body.TextWrapping);
                AssertScrollable(scroll);
                Assert.Equal("OK", ok.Content);
                Assert.True(ok.IsDefault);
                Assert.True(ok.IsCancel);
                Assert.Same(ok, OnlyPrimary(dialog));
                var title = BoundsIn(dialog, Find<SelectableTextBlock>(dialog, "TitleText"));
                var action = BoundsIn(dialog, ok);
                Assert.True(title.Bottom <= BoundsIn(dialog, scroll).Top);
                Assert.True(BoundsIn(dialog, scroll).Bottom <= action.Top);
                scroll.Offset = new Vector(0, scroll.Extent.Height);
                await HeadlessUiPump.FlushQueueAsync();
                Assert.True(scroll.Offset.Y > 0);
                Assert.Equal(title, BoundsIn(dialog, Find<SelectableTextBlock>(dialog, "TitleText")));
                Assert.Equal(action, BoundsIn(dialog, ok));
                dialog.KeyPress(key, RawInputModifiers.None, key == Key.Enter ? PhysicalKey.Enter : PhysicalKey.Escape, null);
                await HeadlessUiPump.FlushQueueAsync();
                Assert.False(dialog.IsVisible);
            });
        }
    }

    [AvaloniaFact]
    public async Task LegacyCallersWorkWithoutAnyNewParameterAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var confirm = new ConfirmDialog("Confirm action", "Continue with this action?", "Continue", "Cancel");
        await WithDialogAsync(confirm, () =>
        {
            Assert.Null(TipText(Find<Button>(confirm, "ConfirmButton")));
            Assert.Null(TipText(Find<Button>(confirm, "CancelButton")));
            Assert.False(Find<Button>(confirm, "DismissButton").IsEffectivelyVisible);
            Assert.False(Find<Border>(confirm, "NextStartSection").IsEffectivelyVisible);
            return Task.CompletedTask;
        });
        Assert.True(await ChooseConfirmationAsync(
            new ConfirmDialog("Confirm action", "Continue with this action?", "Continue", "Cancel"), "ConfirmButton"));
        Assert.False(await ChooseConfirmationAsync(
            new ConfirmDialog("Confirm action", "Continue with this action?", "Continue", "Cancel"), "CancelButton"));
        Assert.Null(await ChooseConfirmationAsync(new ConfirmDialog(), null));
        var warning = new WarningDialog();
        await WithDialogAsync(warning, () =>
        {
            UiEventProbe.Click(Find<Button>(warning, "OkButton"));
            Assert.False(warning.IsVisible);
            return Task.CompletedTask;
        });
    }

    private static async Task WithDialogAsync(Window dialog, Func<Task> inspectAsync)
    {
        var owner = new Window();
        try
        {
            owner.Show();
            var completion = dialog.ShowDialog(owner);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(completion.IsCompleted);
            await inspectAsync();
            dialog.Close();
            await completion.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        finally
        {
            dialog.Close();
            owner.Close();
        }
    }

    private static async Task<bool?> ChooseConfirmationAsync(ConfirmDialog dialog, string? buttonName)
    {
        var owner = new Window();
        try
        {
            owner.Show();
            var completion = dialog.ShowDialog<bool?>(owner);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(completion.IsCompleted);
            if (buttonName is null)
            {
                dialog.Close();
            }
            else
            {
                UiEventProbe.Click(Find<Button>(dialog, buttonName));
            }

            return await completion.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        finally
        {
            dialog.Close();
            owner.Close();
        }
    }

    private static T Find<T>(Control root, string name) where T : Control => Assert.IsType<T>(root.FindControl<T>(name));

    private static Button OnlyPrimary(Window dialog) => Assert.Single(dialog.GetVisualDescendants().OfType<Button>(),
        static button => button.IsEffectivelyVisible && button.Classes.Contains("actionPrimary"));

    private static Rect BoundsIn(Window dialog, Control control) =>
        new(Assert.IsType<Point>(control.TranslatePoint(default, dialog)), control.Bounds.Size);

    private static string? TipText(Control control) => ToolTip.GetTip(control) switch
    {
        string text => text,
        TextBlock block => block.Text,
        _ => null,
    };

    private static string LongMessage() => string.Join(Environment.NewLine,
        Enumerable.Repeat("Review the DXF layers and pad edits in this project before continuing.", 60));

    private static void AssertScrollable(ScrollViewer scroll)
    {
        Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
        Assert.True(scroll.Viewport.Height > 0);
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
    }
}

// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

internal sealed class NotchExportSelectionInteractionFixture : IAsyncDisposable
{
    public FreeformHelperViewModel Helper { get; }
    public Window Owner { get; }
    public NotchTable Table { get; } = new(
    [
        CreateRow(0, 30), CreateRow(0, 10), CreateRow(0, 20),
        CreateRow(1, 60), CreateRow(1, 40), CreateRow(1, 50),
    ]);
    public NotchExportSelectionWindow Window => Assert.Single(Owner.OwnedWindows.OfType<NotchExportSelectionWindow>());
    public NotchExportSelectionViewModel Selection => Assert.IsType<NotchExportSelectionViewModel>(Window.DataContext);

    public NotchExportSelectionInteractionFixture(FreeformHelperViewModel? helper = null)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        Helper = helper ?? new FreeformHelperViewModel();
        Owner = new Window { Content = new FreeformHelperView { DataContext = Helper } };
        Owner.Show();
        HeadlessSessionGuardAttribute.CloseAtTestEnd(Owner);
    }

    public async Task<NotchTable?> OpenAsync()
    {
        await HeadlessUiPump.FlushQueueAsync();
        var openAsync = Helper.OpenNotchExportSelectionAsync;
        Assert.NotNull(openAsync);
        return await openAsync(new NotchExportSelectionViewModel(Table));
    }

    public async Task ClearSelectionAsync()
    {
        foreach (var group in Selection.IcGroups.ToArray())
        {
            await ClickAsync(FindGroupButton(group.IcIndex, "None shown"));
        }

        Assert.All(Selection.Rows, row => Assert.False(row.IsSelected));
    }

    public Button FindGroupButton(int icIndex, string content) =>
        Assert.Single(Window.GetVisualDescendants().OfType<Button>(), button =>
            button.Content as string == content &&
            button.DataContext is NotchExportIcGroupViewModel group && group.IcIndex == icIndex);

    public CheckBox FindRowCheckBox(int diffIndex) =>
        Assert.Single(Window.GetVisualDescendants().OfType<CheckBox>(), checkBox =>
            checkBox.DataContext is NotchExportRowItemViewModel row && row.Row.DiffIndex == diffIndex);

    public static Button FindButton(Control root, string content) =>
        Assert.Single(root.GetVisualDescendants().OfType<Button>(), button => button.Content as string == content);

    public static Button FindTooltipButton(Control root, string tooltip) =>
        Assert.Single(root.GetVisualDescendants().OfType<Button>(), button => ToolTip.GetTip(button) switch
        {
            string text => text == tooltip,
            TextBlock text => text.Text == tooltip,
            _ => false,
        });

    public static async Task ClickAsync(Button button)
    {
        Assert.True(button.IsEffectivelyEnabled);
        if (button.Command is null && button is not ToggleButton)
        {
            UiEventProbe.Click(button);
        }
        else
        {
            var window = Assert.IsAssignableFrom<Window>(TopLevel.GetTopLevel(button));
            var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window);
            Assert.NotNull(point);
            window.MouseDown(point.Value, MouseButton.Left);
            window.MouseUp(point.Value, MouseButton.Left);
        }

        await HeadlessUiPump.FlushQueueAsync();
    }

    public async ValueTask DisposeAsync()
    {
        CloseWindows(Owner);
        await Helper.WaitForGridRebuildIdleAsync().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
    }

    private static NotchTableRow CreateRow(int icIndex, int diffIndex) =>
        new(icIndex, diffIndex, 100 + diffIndex, 200 + diffIndex,
            new NotchV22Node(diffIndex, 95, diffIndex + 1, 5, 65535, 0, 0), "synthetic transfer");

    private static void CloseWindows(Window window)
    {
        foreach (var child in window.OwnedWindows.ToArray())
        {
            CloseWindows(child);
        }

        window.Close();
    }
}

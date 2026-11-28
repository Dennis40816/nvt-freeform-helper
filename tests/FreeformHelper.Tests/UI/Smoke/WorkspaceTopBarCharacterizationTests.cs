// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class WorkspaceTopBarCharacterizationTests
{
    [AvaloniaFact]
    public async Task RootMenusRetainTheirCommandsAndEditingGateAsync()
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var menu = Assert.Single(fixture.Window.GetVisualDescendants().OfType<Menu>());
        Assert.Equal(["File", "Export", "Settings"], menu.Items.OfType<MenuItem>().Select(item => item.Header));
        var file = Assert.IsType<MenuItem>(menu.Items[0]);
        Assert.Same(fixture.Helper.OpenDxfCommand, Assert.IsType<MenuItem>(file.Items[0]).Command);
        Assert.Same(fixture.Helper.SaveProjectCommand, Assert.IsType<MenuItem>(file.Items[1]).Command);
        Assert.Same(fixture.Helper.LoadProjectCommand, Assert.IsType<MenuItem>(file.Items[2]).Command);
        Assert.Equal(fixture.Helper.IsProjectEditingEnabled, menu.IsEffectivelyEnabled);
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Helper.PickLoadProjectPathAsync = () => release.Task.WaitAsync(TestContext.Current.CancellationToken);
        fixture.Helper.HasUnsavedChanges = false;
        var load = fixture.Helper.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            HeadlessUiPump.Drain(fixture.Window);
            Assert.False(fixture.Helper.IsProjectEditingEnabled);
            Assert.False(menu.IsEffectivelyEnabled);
        }
        finally
        {
            release.TrySetResult(null);
            await load.WaitAsync(TestContext.Current.CancellationToken);
        }
        HeadlessUiPump.Drain(fixture.Window);
        Assert.True(menu.IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public async Task DisplayPinKeepsTheExistingTwoWayControlsAsync()
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var header = Assert.Single(fixture.Window.GetVisualDescendants().OfType<WorkspaceHeader>());
        var button = header.FindControl<ToggleButton>("ViewMenuButton")!;
        var popup = header.FindControl<Popup>("ViewMenuPopup")!;
        Assert.NotEqual(true, button.IsChecked);
        button.IsChecked = true;
        HeadlessUiPump.Drain(fixture.Window);
        Assert.True(popup.IsOpen);
        Assert.Same(fixture.Helper, popup.DataContext);
        Assert.NotEmpty(popup.Child!.GetVisualDescendants().OfType<ToggleSwitch>());
        button.IsChecked = false;
        HeadlessUiPump.Drain(fixture.Window);
        Assert.False(popup.IsOpen);
    }
}

// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfEditChangeListWindowEntryTests
{
    [AvaloniaFact]
    public async Task CloseCommand_ClosesChangeListWindowAndKeepsWorkspaceOpenAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var fixture = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var view = new FreeformHelperView { DataContext = vm };
        var owner = new Window { Content = view };
        DxfEditChangeListWindow? window = null;
        try
        {
            owner.Show();
            await vm.OpenHiddenDxfEditChangeListCommand.ExecuteAsync(null);
            await HeadlessUiPump.FlushQueueAsync();
            window = Assert.Single(owner.OwnedWindows.OfType<DxfEditChangeListWindow>());
            Assert.True(window.IsVisible);
            var changes = Assert.IsType<DxfEditChangeListViewModel>(window.DataContext);
            var closeButton = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                button => ReferenceEquals(button.Command, changes.CloseCommand));
            Assert.True(closeButton.IsEffectivelyEnabled);
            var command = closeButton.Command;
            Assert.NotNull(command);
            Assert.True(command.CanExecute(closeButton.CommandParameter));
            var closedCount = 0;
            window.Closed += (_, _) => closedCount++;

            command.Execute(closeButton.CommandParameter);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.False(window.IsVisible);
            Assert.Equal(1, closedCount);
            Assert.DoesNotContain(window, owner.OwnedWindows);
            Assert.True(owner.IsVisible);
        }
        finally
        {
            window?.Close();
            foreach (var ownedWindow in owner.OwnedWindows.ToArray())
            {
                ownedWindow.Close();
            }

            owner.Close();
            await vm.WaitForGridRebuildIdleAsync();
        }
    }
}

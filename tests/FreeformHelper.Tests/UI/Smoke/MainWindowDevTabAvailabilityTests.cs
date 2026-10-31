// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class MainWindowDevTabAvailabilityTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void DevTab_FollowsDevPageAvailability(bool devPageAvailable)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var shell = new ShellViewModel { IsConsoleExpanded = false, IsDevPageAvailable = devPageAvailable };
        var window = new MainWindow { Width = 1280, Height = 900, WindowState = WindowState.Normal };
        try
        {
            window.SetShellViewModel(shell);
            window.Show();
            HeadlessUiPump.Drain(window);

            var tabs = window.GetVisualDescendants().OfType<ToggleButton>()
                .Where(static button => button.Classes.Contains("shellTab")).ToArray();
            var devTab = tabs.Single(static tab => Equals(tab.Content, "Dev"));

            Assert.Equal(devPageAvailable, devTab.IsVisible);
            Assert.All(tabs.Where(static tab => !Equals(tab.Content, "Dev")), static tab => Assert.True(tab.IsVisible));
        }
        finally
        {
            shell.FreeformHelper.HasUnsavedChanges = false;
            window.Close();
            window.Dispatcher.RunJobs();
        }
    }
}

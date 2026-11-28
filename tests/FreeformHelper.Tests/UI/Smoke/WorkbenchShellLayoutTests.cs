// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class WorkbenchShellLayoutTests
{
    private static readonly string[] _states = ["", ":pointerover", ":focus-visible", ":disabled"];

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task BothWorkspacesKeepSplitterLayoutAndPreviewAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var variant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        await AssertWorkspaceAsync(new SimulationWorkspaceView(), variant);
        await AssertWorkspaceAsync(new CoordinatePlannerWorkspaceView(), variant);
    }

    private static async Task AssertWorkspaceAsync(UserControl view, ThemeVariant theme)
    {
        var window = new Window { Content = view, RequestedThemeVariant = theme, Width = 1400, Height = 900 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var splitters = view.GetVisualDescendants().OfType<GridSplitter>().ToArray();
            var columns = splitters.Where(static splitter => Grid.GetColumn(splitter) is 1 or 3).ToArray();
            Assert.Equal(2, columns.Length);
            foreach (var splitter in columns)
            {
                await AssertColumnStatesAsync(splitter);
            }

            if (view is SimulationWorkspaceView)
            {
                var row = Assert.Single(splitters, static splitter => splitter.ResizeDirection == GridResizeDirection.Rows);
                Assert.True(double.IsNaN(row.Width));
                Assert.True(row.ShowsPreview);
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task AssertColumnStatesAsync(GridSplitter splitter)
    {
        foreach (var state in _states)
        {
            splitter.IsEnabled = state != ":disabled";
            var pseudoClasses = (IPseudoClasses)splitter.Classes;
            pseudoClasses.Set(":pointerover", state == ":pointerover");
            pseudoClasses.Set(":focus-visible", state == ":focus-visible");
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(splitter.TryFindResource("SplitterThickness", out var width));
            Assert.Equal(Assert.IsType<double>(width), splitter.Width);
            Assert.Equal(HorizontalAlignment.Stretch, splitter.HorizontalAlignment);
            Assert.Equal(GridResizeDirection.Auto, splitter.ResizeDirection);
            Assert.True(splitter.ShowsPreview);
            Assert.True(splitter.TryFindResource("NfcBorderMutedBrush", splitter.ActualThemeVariant, out var brush));
            Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color,
                Assert.IsAssignableFrom<ISolidColorBrush>(splitter.Background).Color);
        }
    }
}

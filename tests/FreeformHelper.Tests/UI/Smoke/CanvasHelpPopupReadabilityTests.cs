// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using Rect = Avalonia.Rect;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CanvasHelpPopupReadabilityTests
{
    [AvaloniaTheory]
    [InlineData("Workspace", true)]
    [InlineData("Workspace", false)]
    [InlineData("Coordinate", true)]
    [InlineData("Simulation", true)]
    public async Task HelpOpenedByMouseKeepsEveryLineReadableAsync(string page, bool dark)
    {
        await using var workspace = await MainWindowGlobalInputFixture.CreateAsync();
        var window = workspace.Window;
        window.Width = 1920;
        window.Height = 1080;
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var shell = Assert.IsType<ShellViewModel>(window.DataContext);
        await SelectPageAsync(shell, page);
        await HeadlessUiPump.FlushQueueAsync();
        var button = Assert.Single(window.GetVisualDescendants().OfType<Button>(), static button =>
            button.IsEffectivelyVisible && (button.Content as string == "Canvas help" ||
                button.Name is "CanvasHelpButton" or "CanvasHelpToggle"));
        var position = Avalonia.VisualExtensions.TranslatePoint(button, new Rect(button.Bounds.Size).Center, window)!.Value;
        window.MouseMove(position);
        window.MouseDown(position, MouseButton.Left);
        window.MouseUp(position, MouseButton.Left);
        await HeadlessUiPump.FlushQueueAsync();
        var popup = button.Flyout is Flyout flyout
            ? flyout.Popup
            : Assert.Single(window.GetLogicalDescendants().OfType<Popup>(), static popup => popup.IsOpen);
        Assert.NotNull(popup);
        Assert.True(popup.IsOpen);
        var content = Assert.IsAssignableFrom<Control>(popup.Child);
        var lines = content.GetVisualDescendants().OfType<Control>().Where(static control =>
            control is TextBlock or SelectableTextBlock && control.IsEffectivelyVisible &&
                !control.GetVisualAncestors().OfType<Button>().Any()).ToArray();
        Assert.NotEmpty(lines);
        Assert.All(lines, AssertReadable);
        if (page == "Workspace") await AssertClearSelectionAsync(workspace, content);
    }

    private static Task SelectPageAsync(ShellViewModel shell, string page)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 3, cellWidth: 10, cellHeight: 10);
        foreach (var pad in grid.Pads) pad.DiffIndex = pad.RegularPadId;
        return page switch
        {
            "Coordinate" => shell.ShowCoordinateAsync(new CoordinatePlannerWorkspaceViewModel(
                new FreeformHelper.UI.Services.CoordinatePlannerWorkspaceUseCase(),
                new FreeformHelper.UI.Services.CoordinatePlannerWorkspaceSession(grid, [], [], 0, 30, 10, 300, 100))),
            "Simulation" => shell.ShowSimulationAsync(new SimulationWorkspaceViewModel(
                new FreeformHelper.UI.Services.SimulationWorkspaceUseCase(new FreeformHelper.UI.Services.NotchApplySimulationReviewUseCase()),
                new FreeformHelper.UI.Services.SimulationWorkspaceSession(grid, new NotchTable([]), 255, 0,
                    grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet()))),
            _ => Task.CompletedTask,
        };
    }

    private static void AssertReadable(Control control)
    {
        var (text, foreground, trimming, layout) = control switch
        {
            SelectableTextBlock block => (block.Text, block.Foreground, block.TextTrimming, block.TextLayout),
            TextBlock block => (block.Text, block.Foreground, block.TextTrimming, block.TextLayout),
            _ => throw new InvalidOperationException("Expected a help line."),
        };
        var color = Assert.IsAssignableFrom<ISolidColorBrush>(foreground).Color;
        var background = control.GetVisualAncestors().OfType<Control>().Select(static ancestor => ancestor switch
        {
            Border border => border.Background,
            TemplatedControl templated => templated.Background,
            _ => null,
        }).OfType<ISolidColorBrush>().First(static brush => brush.Color.A == byte.MaxValue).Color;
        var luminance = Luminance(color);
        var surface = Luminance(background);
        var contrast = (Math.Max(luminance, surface) + 0.05) / (Math.Min(luminance, surface) + 0.05);
        Assert.True(contrast >= 4.5, $"{text}: {color} on {background}, contrast {contrast:F2}:1");
        Assert.Equal(TextTrimming.None, trimming);
        Assert.All(layout.TextLines, static line => Assert.False(line.HasCollapsed));
        Assert.True(layout.Height <= control.Bounds.Height + 1, $"Clipped help line: {text}");
        if (control is SelectableTextBlock selectable)
        {
            Assert.True(selectable.IsHitTestVisible);
            Assert.True(selectable.Focusable);
            selectable.SelectionStart = 0;
            selectable.SelectionEnd = selectable.Text?.Length ?? 0;
            Assert.Equal(selectable.Text, selectable.SelectedText);
        }
    }

    private static double Luminance(Color color) =>
        (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));

    private static double Linear(byte component)
    {
        var value = component / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static async Task AssertClearSelectionAsync(MainWindowGlobalInputFixture workspace, Control content)
    {
        workspace.Helper.ApplyCanvasSelection([workspace.Helper.CadPads[0].Id], []);
        var clear = Assert.Single(content.GetVisualDescendants().OfType<Button>(),
            static button => button.Content as string == "Clear selection");
        Assert.Same(workspace.Helper.ClearSelectionCommand, clear.Command);
        var root = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(clear));
        var position = Avalonia.VisualExtensions.TranslatePoint(clear, new Rect(clear.Bounds.Size).Center, root)!.Value;
        root.MouseDown(position, MouseButton.Left);
        root.MouseUp(position, MouseButton.Left);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Empty(workspace.Helper.SelectedCadPadIds);
        Assert.Empty(workspace.Helper.SelectedRegularPadIndices);
    }
}

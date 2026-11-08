// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the canvas shortcut keys and the placement of the inline value editor of the simulation workspace view.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class SimulationWorkspaceViewInteractionTests
{
    private const double Gap = 20.0;
    private const double Tolerance = 0.5;
    private static readonly int[] _diffIndices = [10, 11];

    [AvaloniaTheory]
    [InlineData(Key.B, NotchApplySimulationCanvasViewMode.Before)]
    [InlineData(Key.A, NotchApplySimulationCanvasViewMode.After)]
    [InlineData(Key.D, NotchApplySimulationCanvasViewMode.Delta)]
    [InlineData(Key.C, NotchApplySimulationCanvasViewMode.ChangedOnly)]
    public async Task CanvasLetterKeysSelectTheMatchingViewModeAsync(Key key, NotchApplySimulationCanvasViewMode expected)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateWorkspace();
        var view = new SimulationWorkspaceView { DataContext = viewModel };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            // Start from a mode other than the expected one so that the key press has to change it.
            viewModel.SetCanvasViewMode(expected == NotchApplySimulationCanvasViewMode.Before
                ? NotchApplySimulationCanvasViewMode.After
                : NotchApplySimulationCanvasViewMode.Before);

            var handled = PressKey(view, key);

            Assert.True(handled);
            Assert.Equal(expected, viewModel.SelectedCanvasViewMode);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task CanvasVKeyTogglesBetweenBeforeAndAfterAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateWorkspace();
        var view = new SimulationWorkspaceView { DataContext = viewModel };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            viewModel.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.Before);

            Assert.True(PressKey(view, Key.V));
            Assert.Equal(NotchApplySimulationCanvasViewMode.After, viewModel.SelectedCanvasViewMode);
            Assert.True(PressKey(view, Key.V));
            Assert.Equal(NotchApplySimulationCanvasViewMode.Before, viewModel.SelectedCanvasViewMode);

            viewModel.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.Delta);
            Assert.True(PressKey(view, Key.V));
            Assert.Equal(NotchApplySimulationCanvasViewMode.Before, viewModel.SelectedCanvasViewMode);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task OtherKeysLeaveTheViewModeAndTheEventUnhandledAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateWorkspace();
        var view = new SimulationWorkspaceView { DataContext = viewModel };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            viewModel.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.Delta);

            Assert.False(PressKey(view, Key.X));
            Assert.Equal(NotchApplySimulationCanvasViewMode.Delta, viewModel.SelectedCanvasViewMode);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task InlineEditActionAndCardStayInsideTheCanvasWithAGapAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateWorkspace();
        var view = new SimulationWorkspaceView { DataContext = viewModel };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            viewModel.SelectRegularPad(0);
            await HeadlessUiPump.FlushQueueAsync();

            var editAction = Assert.IsType<Button>(view.FindControl<Button>("SimulationInlineEditAction"));
            Assert.True(editAction.IsVisible);
            AssertInsideCanvasWithGap(view, editAction);

            editAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await HeadlessUiPump.FlushQueueAsync();

            var card = Assert.IsType<Border>(view.FindControl<Border>("SimulationInlineEditorCard"));
            Assert.True(card.IsVisible);
            Assert.False(editAction.IsVisible);
            AssertInsideCanvasWithGap(view, card);
        }
        finally
        {
            window.Close();
        }
    }

    private static bool PressKey(SimulationWorkspaceView view, Key key)
    {
        var canvas = Assert.IsType<PadCanvas>(view.FindControl<PadCanvas>("SimulationPadCanvas"));
        var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key };
        canvas.RaiseEvent(args);
        return args.Handled;
    }

    private static void AssertInsideCanvasWithGap(SimulationWorkspaceView view, Control editor)
    {
        var overlay = Assert.IsType<Canvas>(view.FindControl<Canvas>("SimulationInlineEditorOverlay"));
        var canvas = Assert.IsType<PadCanvas>(view.FindControl<PadCanvas>("SimulationPadCanvas"));
        var origin = canvas.TranslatePoint(new Point(0, 0), overlay);
        Assert.NotNull(origin);
        var canvasRect = new Rect(origin.Value, canvas.Bounds.Size);
        var left = Canvas.GetLeft(editor);
        var top = Canvas.GetTop(editor);
        var size = editor.DesiredSize;

        Assert.True(left >= canvasRect.Left + Gap - Tolerance, $"left {left} is closer than the gap to {canvasRect.Left}.");
        Assert.True(top >= canvasRect.Top + Gap - Tolerance, $"top {top} is closer than the gap to {canvasRect.Top}.");
        Assert.True(left + size.Width <= canvasRect.Right - Gap + Tolerance,
            $"right edge {left + size.Width} is closer than the gap to {canvasRect.Right}.");
        Assert.True(top + size.Height <= canvasRect.Bottom - Gap + Tolerance,
            $"bottom edge {top + size.Height} is closer than the gap to {canvasRect.Bottom}.");
    }

    private static Window CreateWindow(Control content) => new() { Content = content, Width = 1400, Height = 1000 };

    private static SimulationWorkspaceViewModel CreateWorkspace()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(_diffIndices);
        var table = new NotchTable(new[]
        {
            new NotchTableRow(icIndex: 0, diffIndex: 10, regularPadIndex: 0, cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, 65535, 0, 0), comment: "sample"),
        });
        return new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            new SimulationWorkspaceSession(grid, table, 65535, SourceRevision: 0,
                ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
                CadPadsForCopperProjection: new[]
                {
                    TestGeometryFactory.CreateCadPad(100, "SENSOR", 0, 0, 1, 1),
                    TestGeometryFactory.CreateCadPad(101, "SENSOR", 1, 0, 2, 1),
                },
                CadOutputFwDiffIndexByCadId: new Dictionary<int, int> { [100] = 10, [101] = 11 },
                CadIcIndexByCadId: new Dictionary<int, int> { [100] = 0, [101] = 0 }));
    }
}

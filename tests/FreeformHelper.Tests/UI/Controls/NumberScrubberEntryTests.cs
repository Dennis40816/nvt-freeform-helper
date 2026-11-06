// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NumberScrubberEntryTests
{
    [AvaloniaFact]
    public async Task EnterCommitsTypedNumbersWithStepNormalizationAndBoundsAsync()
    {
        using var session = new ScrubberSession();
        await session.ShowAsync();

        await AssertEnterCommitsAsync(session, "3.26", 3.5m, "3.50");
        await AssertEnterCommitsAsync(session, "-11", -10m, "-10.00");
        await AssertEnterCommitsAsync(session, "11", 10m, "10.00");

        session.Scrubber.SnapToStep = false;
        await AssertEnterCommitsAsync(session, "3.26", 3.26m, "3.26");
    }

    [AvaloniaFact]
    public async Task EscapeRestoresFormattedValueWithoutUndoingLiveEditsAsync()
    {
        using var session = new ScrubberSession();
        await session.ShowAsync();
        Assert.True(session.Input.Focus());

        session.Input.Text = "3.26";
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(3.5m, session.Scrubber.Value);
        Assert.Equal("3.26", session.Input.Text);

        Assert.True(session.PressKey(Key.Escape).Handled);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(3.5m, session.Scrubber.Value);
        Assert.Equal("3.50", session.Input.Text);
    }

    [AvaloniaFact]
    public async Task LostFocusCommitsValidTextAndRevertsInvalidTextAsync()
    {
        using var session = new ScrubberSession();
        await session.ShowAsync();
        Assert.True(session.Input.Focus());

        session.Input.Text = "3.26";
        Assert.True(session.FocusTarget.Focus());
        await HeadlessUiPump.FlushQueueAsync();

        Assert.False(session.Input.IsFocused);
        Assert.Equal(3.5m, session.Scrubber.Value);
        Assert.Equal("3.50", session.Input.Text);

        Assert.True(session.Input.Focus());
        session.Input.Text = "not-a-number";
        Assert.True(session.FocusTarget.Focus());
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(3.5m, session.Scrubber.Value);
        Assert.Equal("3.50", session.Input.Text);
    }

    [AvaloniaFact]
    public async Task EnterRejectsInvalidTextAndRestoresValueAsync()
    {
        using var session = new ScrubberSession();
        await session.ShowAsync();
        Assert.True(session.Input.Focus());

        session.Input.Text = "not-a-number";
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(2m, session.Scrubber.Value);
        Assert.Equal("not-a-number", session.Input.Text);

        Assert.True(session.PressKey(Key.Enter).Handled);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(2m, session.Scrubber.Value);
        Assert.Equal("2.00", session.Input.Text);
    }

    [AvaloniaFact]
    public async Task WheelUsesSmallChangeAndHonorsAltRequirementAsync()
    {
        using var session = new ScrubberSession();
        await session.ShowAsync();
        using var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);

        Assert.True(session.Scrubber.RequireAltForWheel);
        Assert.False(session.Wheel(pointer, 1, KeyModifiers.None).Handled);
        Assert.Equal(2m, session.Scrubber.Value);

        Assert.True(session.Wheel(pointer, 1, KeyModifiers.Alt).Handled);
        Assert.Equal(2.5m, session.Scrubber.Value);
        Assert.Equal("2.50", session.Input.Text);

        Assert.True(session.Wheel(pointer, -1, KeyModifiers.Alt).Handled);
        Assert.Equal(2m, session.Scrubber.Value);

        session.Scrubber.RequireAltForWheel = false;
        Assert.True(session.Wheel(pointer, 1, KeyModifiers.None).Handled);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(2.5m, session.Scrubber.Value);
        Assert.Equal("2.50", session.Input.Text);
    }

    [AvaloniaFact]
    public async Task ReadOnlyBlocksTypingAndWheelUntilEnabledAsync()
    {
        using var session = new ScrubberSession();
        await session.ShowAsync();
        using var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);
        session.Scrubber.IsReadOnly = true;
        Assert.True(session.Input.IsReadOnly);
        Assert.False(session.ScrubArea.IsHitTestVisible);
        Assert.True(session.Input.Focus());

        session.Input.SelectAll();
        session.Window.KeyTextInput("6");
        session.PressKey(Key.Enter);
        Assert.False(session.Wheel(pointer, 1, KeyModifiers.Alt).Handled);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(2m, session.Scrubber.Value);
        Assert.Equal("2.00", session.Input.Text);

        session.Scrubber.IsReadOnly = false;
        Assert.False(session.Input.IsReadOnly);
        Assert.True(session.ScrubArea.IsHitTestVisible);
        session.Input.SelectAll();
        session.Window.KeyTextInput("6");
        session.PressKey(Key.Enter);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal(6m, session.Scrubber.Value);

        Assert.True(session.Wheel(pointer, 1, KeyModifiers.Alt).Handled);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(6.5m, session.Scrubber.Value);
        Assert.Equal("6.50", session.Input.Text);
    }

    [AvaloniaFact]
    public async Task DragUsesVerticalStepDistanceAndClampsBothBoundsAsync()
    {
        using var session = new ScrubberSession();
        await session.ShowAsync();
        using var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);
        var origin = session.ScrubOrigin;

        session.Press(pointer, origin);
        // Ten pixels at four pixels per step rounds 2.5 steps away from zero.
        session.Move(pointer, origin - new Vector(0, 10));
        Assert.Equal(3.5m, session.Scrubber.Value);
        Assert.Equal("3.50", session.Input.Text);

        session.Move(pointer, origin - new Vector(0, 100));
        Assert.Equal(10m, session.Scrubber.Value);

        var end = origin + new Vector(0, 100);
        session.Move(pointer, end);
        Assert.Equal(-10m, session.Scrubber.Value);
        session.Release(pointer, end);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(-10m, session.Scrubber.Value);
        Assert.Equal("-10.00", session.Input.Text);
    }

    [AvaloniaFact]
    public async Task ReleaseStopsFurtherDragUpdatesAsync()
    {
        using var session = new ScrubberSession();
        await session.ShowAsync();
        using var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);
        var origin = session.ScrubOrigin;
        var end = origin - new Vector(0, 8);

        session.Press(pointer, origin);
        session.Move(pointer, end);
        Assert.Equal(3m, session.Scrubber.Value);

        session.Release(pointer, end);
        Assert.Null(pointer.Captured);
        session.Move(pointer, origin - new Vector(0, 16), RawInputModifiers.None);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(3m, session.Scrubber.Value);
        Assert.Equal("3.00", session.Input.Text);
    }

    [AvaloniaFact]
    public async Task CaptureLostStopsFurtherDragUpdatesAsync()
    {
        using var session = new ScrubberSession();
        await session.ShowAsync();
        using var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);
        var origin = session.ScrubOrigin;

        session.Press(pointer, origin);
        session.Move(pointer, origin - new Vector(0, 8));
        Assert.Equal(3m, session.Scrubber.Value);

        pointer.Capture(null);
        Assert.Null(pointer.Captured);
        session.Move(pointer, origin - new Vector(0, 16));
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(3m, session.Scrubber.Value);
        Assert.Equal("3.00", session.Input.Text);
    }

    private static async Task AssertEnterCommitsAsync(ScrubberSession session, string text, decimal value, string display)
    {
        Assert.True(session.Input.Focus());
        session.Input.Text = text;

        Assert.True(session.PressKey(Key.Enter).Handled);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(value, session.Scrubber.Value);
        Assert.Equal(display, session.Input.Text);
    }

    private sealed class ScrubberSession : IDisposable
    {
        public ScrubberSession()
        {
            HeadlessAppBootstrap.EnsureInitialized();
            Scrubber = new NumberScrubber
            {
                Minimum = -10m,
                Maximum = 10m,
                SmallChange = 0.5m,
                FormatString = "0.00",
                ScrubPixelsPerStep = 4,
                Value = 2m,
                Width = 200,
            };
            Input = Assert.IsType<TextBox>(Scrubber.FindControl<TextBox>("InputBox"));
            ScrubArea = Assert.IsType<Border>(Scrubber.FindControl<Border>("ScrubArea"));
            FocusTarget = new Button { Content = "Move focus" };
            Window = new Window
            {
                Width = 320,
                Height = 180,
                Content = new StackPanel { Children = { Scrubber, FocusTarget } },
            };
        }

        public NumberScrubber Scrubber { get; }
        public TextBox Input { get; }
        public Border ScrubArea { get; }
        public Button FocusTarget { get; }
        public Window Window { get; }

        public Point ScrubOrigin =>
            ScrubArea.TranslatePoint(new Point(ScrubArea.Bounds.Width / 2, ScrubArea.Bounds.Height / 2), Window)
            ?? throw new InvalidOperationException("The scrub area is not attached to the test window.");

        public async Task ShowAsync()
        {
            Window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(FocusTarget.Focus());
            await HeadlessUiPump.FlushQueueAsync();
        }

        public KeyEventArgs PressKey(Key key)
        {
            var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key };
            Input.RaiseEvent(args);
            return args;
        }

        public PointerWheelEventArgs Wheel(Pointer pointer, double delta, KeyModifiers modifiers)
        {
            var args = new PointerWheelEventArgs(ScrubArea, pointer, Window, ScrubOrigin, 0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), modifiers, new Vector(0, delta));
            ScrubArea.RaiseEvent(args);
            return args;
        }

        public void Press(Pointer pointer, Point position)
        {
            ScrubArea.RaiseEvent(new PointerPressedEventArgs(ScrubArea, pointer, Window, position, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None, clickCount: 1));
            Assert.Same(ScrubArea, pointer.Captured);
        }

        public void Move(Pointer pointer, Point position, RawInputModifiers modifiers = RawInputModifiers.LeftMouseButton)
        {
            ScrubArea.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, ScrubArea, pointer, Window, position, 0,
                new PointerPointProperties(modifiers, PointerUpdateKind.Other), KeyModifiers.None));
        }

        public void Release(Pointer pointer, Point position)
        {
            ScrubArea.RaiseEvent(new PointerReleasedEventArgs(ScrubArea, pointer, Window, position, 0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
                KeyModifiers.None, MouseButton.Left));
        }

        public void Dispose() => Window.Close();
    }
}

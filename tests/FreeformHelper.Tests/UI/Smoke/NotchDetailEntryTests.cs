// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Interaction;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchDetailEntryTests
{
    [AvaloniaFact]
    public async Task HeaderActionOpensNotchDetailAndPreservesPadInfoAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var fixture = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        vm.ShowRegular = false;
        var view = new FreeformHelperView { DataContext = vm };
        var owner = new Window { Content = view, Width = 1200, Height = 800 };
        try
        {
            owner.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var cad = vm.CadPads[0];
            vm.ApplyCanvasSelection([cad.Id], []);
            await HeadlessUiPump.FlushQueueAsync();
            OpenCadPadInfo(view, cad);
            await HeadlessUiPump.FlushQueueAsync();
            var context = Assert.IsType<PadInfoContext>(vm.InteractionState.ActivePadInfo);
            var info = Assert.IsType<CadPadInfoViewModel>(context.ViewModel);
            var popover = Assert.IsType<PadInfoPopover>(view.FindControl<PadInfoPopover>("PadInfoPopover"));
            var overlay = Assert.IsType<Canvas>(view.FindControl<Canvas>("PadInfoOverlay"));
            Assert.Same(info, popover.DataContext);
            Assert.True(overlay.IsVisible);
            Assert.False(info.ShowLowFrequencyDetailSection);
            var button = Assert.Single(popover.GetVisualDescendants().OfType<Button>(),
                static candidate => AutomationProperties.GetName(candidate) == "Notch detail");
            Assert.True(button.IsEffectivelyVisible);
            Assert.True(button.IsEffectivelyEnabled);
            var command = button.Command;
            Assert.NotNull(command);
            Assert.Same(vm.ShowNotchDetailCommand, command);
            Assert.True(command.CanExecute(button.CommandParameter));

            command.Execute(button.CommandParameter);
            await HeadlessUiPump.FlushQueueAsync();

            var detail = Assert.Single(owner.OwnedWindows.OfType<NotchDetailWindow>());
            Assert.True(detail.IsVisible);
            Assert.Same(cad, Assert.IsType<NotchDetailViewModel>(detail.DataContext).CadPad);
            detail.Close();
            await HeadlessUiPump.FlushQueueAsync();

            Assert.False(detail.IsVisible);
            Assert.Empty(owner.OwnedWindows.OfType<NotchDetailWindow>());
            Assert.True(owner.IsVisible);
            Assert.True(owner.IsEnabled);
            Assert.Same(view, owner.Content);
            Assert.Same(vm, view.DataContext);
            Assert.Equal(cad.Id, Assert.Single(vm.SelectedCadPadIds));
            Assert.Empty(vm.SelectedRegularPadIndices);
            Assert.Equal(cad.Id, Assert.Single(vm.InteractionState.SelectedCadIds));
            Assert.Same(context, vm.InteractionState.ActivePadInfo);
            Assert.Same(info, popover.DataContext);
            Assert.True(popover.IsEffectivelyVisible);
            Assert.True(overlay.IsVisible);
            Assert.False(info.ShowLowFrequencyDetailSection);
            Assert.False(popover.IsCloseConfirmVisible);
        }
        finally
        {
            CloseWindows(owner);
            vm.ClearSelectionCommand.Execute(null);
        }
    }

    private static void OpenCadPadInfo(FreeformHelperView view, CadPad cad)
    {
        var canvas = Assert.IsType<PadCanvas>(view.FindControl<PadCanvas>("PadCanvas"));
        using var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);
        var root = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(canvas));
        var position = Assert.IsType<Point>(canvas.TranslatePoint(canvas.WorldToScreen(cad.Centroid), root));
        canvas.RaiseEvent(new PointerPressedEventArgs(canvas, pointer, root, position, 0,
            new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed),
            KeyModifiers.None, clickCount: 1));
    }

    private static void CloseWindows(Window owner)
    {
        foreach (var window in owner.OwnedWindows.ToArray())
        {
            window.Close();
        }

        owner.Close();
    }
}

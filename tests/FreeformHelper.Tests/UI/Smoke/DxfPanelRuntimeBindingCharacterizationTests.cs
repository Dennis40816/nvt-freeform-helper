// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Views;
using Nvt.Core.Avalonia.Panels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfPanelRuntimeBindingCharacterizationTests
{
    [AvaloniaFact]
    public async Task TargetLayerBinding_ShowsVmChangesAndWritesUserSelectionBackAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        vm.IsDxfEditAdvancedPanelExpanded = true;
        var view = new LeftDxfPanel { DataContext = vm };
        var window = new Window { Width = 600, Height = 1000, Content = view };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var target = Assert.Single(view.GetVisualDescendants().OfType<ComboBox>(),
                static box => ToolTip.GetTip(box) is TextBlock { Text: "Target layer for selected CAD pads." });

            vm.DxfEditTargetLayerName = "beta";
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal("beta", target.SelectedItem);
            Assert.Contains(target.GetVisualDescendants().OfType<TextBlock>(), static text => text.Text == "beta");
            target.SelectedItem = "alpha";
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal("alpha", vm.DxfEditTargetLayerName);
            Assert.Contains(target.GetVisualDescendants().OfType<TextBlock>(), static text => text.Text == "alpha");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditButtonBinding_RunsTheSelectedCommandAndUpdatesItsStateAsync(bool rotate)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        vm.IsDxfEditAdvancedPanelExpanded = true;
        var view = new LeftDxfPanel { DataContext = vm };
        var window = new Window { Width = 600, Height = 1600, Content = view };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Single(view.GetVisualDescendants().OfType<CollapsiblePanel>(),
                static panel => panel.Title == "DXF edits").IsExpanded = true;
            await HeadlessUiPump.FlushQueueAsync();
            var button = FindEditButton(view, rotate);
            Assert.Same(rotate ? vm.RotateSelectedCadPadsCommand : vm.DeleteSelectedCadPadsCommand, button.Command);
            Assert.Equal(!rotate, button.IsEffectivelyEnabled);
            var selectedId = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha").Id;
            await SelectCadPadsAsync(vm, [selectedId]);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(button.IsEffectivelyEnabled);

            Assert.True(button.Command!.CanExecute(button.CommandParameter));
            button.Command.Execute(button.CommandParameter);
            await WaitForGridRebuildAsync(vm);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal(rotate ? 0 : 1, vm.DeletedCadPadCount);
            Assert.Equal(rotate ? 1 : 0, vm.RotatedCadPadCount);
            Assert.Empty(vm.SelectedCadPadIds);
            Assert.Equal(!rotate, button.IsEffectivelyEnabled);
            Assert.True(vm.CanUndo);
        }
        finally
        {
            window.Close();
        }
    }

    private static Button FindEditButton(LeftDxfPanel view, bool rotate) =>
        Assert.Single(view.GetVisualDescendants().OfType<Button>(), button => rotate
            ? Equals(button.Content, "Rotate")
            : button.GetVisualDescendants().OfType<SelectableTextBlock>().Any(static text => text.Text == "Hide selected"));
}

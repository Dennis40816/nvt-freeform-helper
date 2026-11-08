// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.SettingsSections;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class UiEventFailureTests
{
    private static readonly (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] _resetDxfPads =
    [
        ("signal", 0, 0, 10, 10),
        ("signal", 20, 0, 30, 10),
    ];

    [AvaloniaFact]
    public async Task ResetAllDxfEditsCancelPreservesEditsAndConfirmRestoresBaselineAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(_resetDxfPads);
        var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
        var panel = new LeftDxfPanel { DataContext = vm };
        var owner = new Window { Content = panel };
        try
        {
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
            var baseline = CaptureCadState(vm);
            Assert.Equal(2, baseline.Length);
            await HideAndMovePadsAsync(vm, baseline[0].Id, baseline[1].Id);
            var edited = CaptureCadState(vm);
            Assert.Equal(baseline[1].Id, Assert.Single(edited).Id);
            Assert.Equal("moved", Assert.Single(edited).Layer);
            Assert.Equal(1, vm.DeletedCadPadCount);
            Assert.Equal(1, vm.RelayeredCadPadCount);
            owner.Show();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Single(panel.GetVisualDescendants().OfType<Nvt.Core.Avalonia.Panels.CollapsiblePanel>(),
                static block => block.Title == "DXF edits").IsExpanded = true;
            await HeadlessUiPump.FlushQueueAsync();
            var reset = Assert.Single(panel.GetVisualDescendants().OfType<Button>(),
                static button => Equals(button.Content, "Reset all DXF edits"));

            var cancelled = await ClickResetAndWaitForConfirmAsync(owner, reset);
            UiEventProbe.Click(Assert.IsType<Button>(cancelled.FindControl<Button>("CancelButton")));
            await HeadlessUiPump.FlushQueueAsync();

            Assert.False(cancelled.IsVisible);
            Assert.True(vm.HasAnyCadEdits);
            Assert.Equal(1, vm.DeletedCadPadCount);
            Assert.Equal(1, vm.RelayeredCadPadCount);
            Assert.Equal(edited, CaptureCadState(vm));

            var confirmed = await ClickResetAndWaitForConfirmAsync(owner, reset);
            UiEventProbe.Click(Assert.IsType<Button>(confirmed.FindControl<Button>("ConfirmButton")));
            await HeadlessUiPump.WaitUntilAsync(() => !vm.HasAnyCadEdits, "Confirming did not reset DXF edits.");
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

            Assert.False(confirmed.IsVisible);
            Assert.False(vm.HasAnyCadEdits);
            Assert.Equal(0, vm.DeletedCadPadCount);
            Assert.Equal(0, vm.RelayeredCadPadCount);
            Assert.Equal(baseline, CaptureCadState(vm));
        }
        finally
        {
            owner.Close();
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public async Task ResetAllSettingsCancelPreservesSettingsAndConfirmRestoresDefaultsAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = new FreeformHelperViewModel
        {
            MatchThreshold = 0.19m,
            TargetCoverageCapPercent = 155m,
            ImportOnlyClosedPolylines = false,
        };
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        vm.HasUnsavedChanges = false;
        var draft = vm.CreateSettingsWindowViewModel();
        draft.MatchThreshold = 0.27m;
        var window = new SettingsWindow { DataContext = draft };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var reset = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                static button => button.Classes.Contains("dangerTextButton"));

            var cancelled = await ClickResetAndWaitForConfirmAsync(window, reset);
            UiEventProbe.Click(Assert.IsType<Button>(cancelled.FindControl<Button>("CancelButton")));
            await HeadlessUiPump.FlushQueueAsync();

            Assert.False(cancelled.IsVisible);
            Assert.True(window.IsVisible);
            Assert.Equal(0.19m, vm.MatchThreshold);
            Assert.Equal(155m, vm.TargetCoverageCapPercent);
            Assert.False(vm.ImportOnlyClosedPolylines);
            Assert.False(vm.HasUnsavedChanges);
            Assert.Equal(0.27m, draft.MatchThreshold);
            Assert.Equal(155m, draft.TargetCoverageCapPercent);
            Assert.False(draft.ImportOnlyClosedPolylines);

            var confirmed = await ClickResetAndWaitForConfirmAsync(window, reset);
            UiEventProbe.Click(Assert.IsType<Button>(confirmed.FindControl<Button>("ConfirmButton")));
            await HeadlessUiPump.WaitUntilAsync(() => !window.IsVisible, "Confirming did not finish resetting settings.");
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

            var defaults = new ProjectSettings();
            var defaultUi = new ProjectUiSnapshot();
            Assert.False(confirmed.IsVisible);
            Assert.Equal((decimal)defaults.Matching.MatchThreshold, vm.MatchThreshold);
            Assert.Equal((decimal)defaults.Notch.TargetCoverageCapPercent, vm.TargetCoverageCapPercent);
            Assert.Equal(defaultUi.Import.OnlyClosedPolylines, vm.ImportOnlyClosedPolylines);
            Assert.True(vm.HasUnsavedChanges);
        }
        finally
        {
            window.Close();
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        }
    }

    private static async Task HideAndMovePadsAsync(FreeformHelperViewModel vm, int hiddenId, int movedId)
    {
        vm.ApplyCanvasSelection([hiddenId], []);
        Assert.True(vm.DeleteSelectedCadPadsCommand.CanExecute(null));
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        vm.ApplyCanvasSelection([movedId], []);
        // The panel combo box only lists existing layers and clears a typed target, so create the layer by name.
        vm.DxfEditNewLayerName = "moved";
        Assert.True(vm.CreateLayerAndMoveSelectedCadPadsCommand.CanExecute(null));
        vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
    }

    private static (int Id, string Layer, Rect2 Bounds)[] CaptureCadState(FreeformHelperViewModel vm)
        => vm.CadPads.OrderBy(static pad => pad.Id).Select(static pad => (pad.Id, pad.Layer, pad.Bounds)).ToArray();

    private static async Task<ConfirmDialog> ClickResetAndWaitForConfirmAsync(Window owner, Button reset)
    {
        UiEventProbe.Click(reset);
        await HeadlessUiPump.WaitUntilAsync(
            () => owner.OwnedWindows.OfType<ConfirmDialog>().Any(static dialog => dialog.IsVisible),
            "Clicking Reset did not open a ConfirmDialog.");
        return Assert.Single(owner.OwnedWindows.OfType<ConfirmDialog>(), static dialog => dialog.IsVisible);
    }

    [AvaloniaFact]
    public void CascadeDetails_DialogFails_ReportsOnceAndKeepsOwnerUsable()
    {
        var probe = new UiEventProbe();
        var failure = new InvalidOperationException("dialog unavailable");
        var view = new SettingsGeneralSectionView(new SettingsGeneralSectionSeams(
            probe.Runner, (_, _) => Task.FromException(failure)));
        var owner = new Window { Content = view };
        HeadlessSessionGuardAttribute.CloseAtTestEnd(owner);
        owner.Show();

        SettingsGeneralSectionViewPrivateAccess.EditCascadeDetailsButton_Click(view, view, new RoutedEventArgs());

        var report = Assert.Single(probe.Reports);
        Assert.Equal("Settings.CascadeDetails", report.Operation);
        Assert.Same(failure, report.Exception);
        Assert.True(owner.IsVisible);
        Assert.True(view.IsEnabled);
    }

    [AvaloniaFact]
    public void CopyOverlapReport_ClipboardFails_ReportsOnceAndPreservesReport()
    {
        var probe = new UiEventProbe();
        var failure = new InvalidOperationException("clipboard unavailable");
        var vm = new DxfOverlapReportViewModel("overlap", ["synthetic issue"]);
        var window = new DxfOverlapReportWindow(new DxfOverlapReportSeams(
            probe.Runner, _ => Task.FromException(failure)))
        { DataContext = vm };
        HeadlessSessionGuardAttribute.CloseAtTestEnd(window);
        window.Show();

        DxfOverlapReportWindowPrivateAccess.CopyAll_Click(window, window, new RoutedEventArgs());

        var report = Assert.Single(probe.Reports);
        Assert.Equal("DxfOverlap.CopyAll", report.Operation);
        Assert.Same(failure, report.Exception);
        Assert.Equal("synthetic issue", vm.ReportText);
        Assert.True(window.IsVisible);
        Assert.True(window.IsEnabled);
    }

    [AvaloniaFact]
    public void CopyOverlapReport_ProductionReporter_UsesExistingStatusRoute()
    {
        var helper = new FreeformHelperViewModel();
        var failure = new InvalidOperationException("clipboard unavailable");
        var window = new DxfOverlapReportWindow(new DxfOverlapReportSeams(
            helper.UiEvents, _ => Task.FromException(failure)))
        {
            DataContext = new DxfOverlapReportViewModel("overlap", ["synthetic issue"]),
        };
        HeadlessSessionGuardAttribute.CloseAtTestEnd(window);

        DxfOverlapReportWindowPrivateAccess.CopyAll_Click(window, window, new RoutedEventArgs());

        Assert.Equal("DxfOverlap.CopyAll failed: clipboard unavailable", helper.StatusText);
    }
}

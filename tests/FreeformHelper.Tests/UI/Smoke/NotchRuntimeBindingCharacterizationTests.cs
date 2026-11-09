// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.WorkflowSteps;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchRuntimeBindingCharacterizationTests
{
    [AvaloniaFact]
    public async Task PreviewReplacementAndBoundStageButtons_UpdateCanvasAndVisibleStageSummaryAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = new FreeformHelperViewModel
        {
            NotchPreviewAutoPlayEnabled = false,
            EnableToFull = true,
            IsStep3Expanded = true,
        };
        var view = new FreeformHelperView { DataContext = vm };
        var window = new Window { Content = view, Width = 1200, Height = 900 };
        try
        {
            window.Show();
            await HeadlessUiPump.WaitUntilAsync(() => vm.RegularPads.Count > 0, "The initial grid was not displayed.");
            await vm.WaitForGridRebuildIdleAsync();
            vm.CadPads = new ObservableCollection<CadPad>
            {
                TestGeometryFactory.CreateCadPad(101, "signal", 0, 3, 3, 7),
            };
            vm.IsStep3Expanded = true;
            await HeadlessUiPump.FlushQueueAsync();
            var step3 = Assert.Single(view.GetVisualDescendants().OfType<RightWorkflowStep3View>());
            var canvas = Assert.IsType<PadCanvas>(view.FindControl<PadCanvas>("PadCanvas"));
            var previous = FindStageButton(step3, "Previous stage");
            var next = FindStageButton(step3, "Next stage");
            Assert.Same(vm.PreviousNotchPreviewStageCommand, previous.Command);
            Assert.Same(vm.NextNotchPreviewStageCommand, next.Command);
            Assert.False(previous.IsEffectivelyEnabled);
            Assert.False(next.IsEffectivelyEnabled);

            vm.NotchCanvasPreviewItems = CreatePreviewItems();
            vm.NotchPreviewVisualizationStep = 1m;
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Same(vm.NotchCanvasPreviewItems, canvas.NotchCanvasPreviewItems);
            Assert.Equal(101, Assert.Single(canvas.NotchCanvasPreviewItems!).CadPadId);
            Assert.True(previous.IsEffectivelyEnabled);
            Assert.True(next.IsEffectivelyEnabled);
            AssertStage(step3, canvas, 1, "Current layer: Stage 1 (Seed)");
            next.Command!.Execute(next.CommandParameter);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(2m, vm.NotchPreviewVisualizationStep);
            AssertStage(step3, canvas, 2, "Current layer: Stage 2 (Candidate)");
            previous.Command!.Execute(previous.CommandParameter);
            previous.Command!.Execute(previous.CommandParameter);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(3m, vm.NotchPreviewVisualizationStep);
            AssertStage(step3, canvas, 3, "Current layer: Stage 3 (Final)");

            vm.NotchCanvasPreviewItems = [];
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Same(vm.NotchCanvasPreviewItems, canvas.NotchCanvasPreviewItems);
            Assert.Empty(canvas.NotchCanvasPreviewItems!);
            Assert.False(canvas.ShowNotchCanvasPreview);
            Assert.False(previous.IsEffectivelyEnabled);
            Assert.False(next.IsEffectivelyEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Step5SummaryAndProgress_TrackBusyValuesAndHideAtCompletionAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = new FreeformHelperViewModel { NotchPreviewAutoPlayEnabled = false, IsStep5Expanded = true };
        var view = new RightWorkflowStep5View { DataContext = vm };
        var window = new Window { Content = view, Width = 480, Height = 900 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var summary = Assert.Single(view.GetVisualDescendants().OfType<SelectableTextBlock>(),
                static text => text.Text == "Notch export: not run.");
            var progress = Assert.Single(view.GetVisualDescendants().OfType<ProgressBar>());
            Assert.False(progress.IsVisible);

            vm.NotchExportSummary = "Characterization export in progress.";
            vm.NotchExportProgressText = "Generating Notch rows.";
            vm.NotchExportProgress = 0.25;
            vm.IsNotchExporting = true;
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal("Characterization export in progress.", summary.Text);
            Assert.True(summary.IsEffectivelyVisible);
            Assert.Equal(0.25, progress.Value);
            Assert.True(progress.IsEffectivelyVisible);
            Assert.Single(view.GetVisualDescendants().OfType<SelectableTextBlock>(),
                static text => text.IsEffectivelyVisible && text.Text == "Generating Notch rows.");

            vm.IsNotchExporting = false;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(progress.IsVisible);
            vm.IsNotchExporting = true;
            vm.NotchExportProgress = 0.5;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(progress.IsEffectivelyVisible);
            Assert.Equal(0.5, progress.Value);

            vm.NotchExportSummary = "Characterization export completed.";
            vm.NotchExportProgress = 1.0;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal("Characterization export completed.", summary.Text);
            Assert.Equal(1.0, progress.Value);
            Assert.False(progress.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task MainMenuExportCommand_ExecutesAndDisablesWhileSavePathIsPendingAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new NotchDataEditExportTestScope();
        var vm = await files.CreateAsync();
        var view = new FreeformHelperView { DataContext = vm };
        var window = new Window { Content = view, Width = 1200, Height = 900 };
        using var dialog = new SavePathGate();
        Task export = Task.CompletedTask;
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var menu = Assert.IsType<Menu>(view.FindControl<Menu>("MainMenu"));
            var exportMenu = Assert.Single(menu.Items.OfType<MenuItem>(), static candidate => candidate.Header as string == "Export");
            exportMenu.IsSubMenuOpen = true;
            await HeadlessUiPump.FlushQueueAsync();
            var item = Assert.Single(exportMenu.Items.OfType<MenuItem>(), static entry => entry.Header as string == "Export Notch Rows…");
            var command = Assert.IsAssignableFrom<IAsyncRelayCommand>(item.Command);
            Assert.Same(vm.ExportNotchCommand, command);
            Assert.True(item.IsEffectivelyEnabled);
            vm.OpenNotchExportSelectionAsync = null;
            vm.PickSaveNotchPathAsync = dialog.PickAsync;

            export = await dialog.StartAndWaitForEntryAsync(() => command.ExecuteAsync(item.CommandParameter));
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(vm.IsNotchExporting);
            Assert.False(item.IsEffectivelyEnabled);
            Assert.False(command.CanExecute(item.CommandParameter));
            dialog.Release(files.ExportPath);
            await export.WaitAsync(TestContext.Current.CancellationToken);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(item.IsEffectivelyEnabled);
            Assert.True(command.CanExecute(item.CommandParameter));
            Assert.False(vm.IsNotchExporting);
            Assert.Equal("CSV review exported: rows=2.", vm.NotchExportSummary);
            var csv = await TestFiles.ReadContentAsync(files.ExportPath, TestContext.Current.CancellationToken);
            Assert.Equal(2, NotchDataEditExportTestScope.Rows(csv).Length);
        }
        finally
        {
            dialog.Release();
            await export.WaitAsync(TestContext.Current.CancellationToken);
            window.Close();
        }
    }

    private static Button FindStageButton(RightWorkflowStep3View view, string tip) =>
        Assert.Single(view.GetVisualDescendants().OfType<Button>(), button => ToolTip.GetTip(button) is TextBlock text && text.Text == tip);

    private static void AssertStage(RightWorkflowStep3View view, PadCanvas canvas, int stage, string summary)
    {
        Assert.Equal((double)stage, canvas.NotchPreviewStage);
        Assert.True(canvas.ShowNotchCanvasPreview);
        Assert.Equal(stage == 1, canvas.ShowNotchToFullSeedOverlay);
        Assert.Equal(stage == 2, canvas.ShowNotchToFullCandidateOverlay);
        Assert.Equal(stage == 3, canvas.ShowNotchToFullFinalOverlay);
        Assert.Single(view.GetVisualDescendants().OfType<SelectableTextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == summary);
    }

    private static ObservableCollection<NotchCanvasPreviewItem> CreatePreviewItems()
    {
        var seed = TestGeometryFactory.CreateRect(0, 3, 3, 7);
        var final = TestGeometryFactory.CreateRect(0, 0, 10, 10);
        return new ObservableCollection<NotchCanvasPreviewItem>
        {
            new(101, 0.12, 100.0 / 12, true, [seed], [final], [final], [final]),
        };
    }
}

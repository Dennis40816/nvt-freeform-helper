// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Views.WorkflowSteps;
using Nvt.Core.Avalonia.Panels;
using Xunit;
using static FreeformHelper.Tests.CompactWorkflowCardsTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CompactWorkflowCardCommandCharacterizationTests
{
    [AvaloniaFact]
    public async Task BoundClearButtons_ClearMatchingAndExportResultsAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateWorkspaceAsync(files);
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.MatchLinksForCanvas);
        vm.IsStep1Expanded = true;
        vm.IsStep5Expanded = true;
        var step1 = new RightWorkflowStep1View { DataContext = vm };
        var step5 = new RightWorkflowStep5View { DataContext = vm };
        var content = new StackPanel { Children = { step1, step5 } };
        var window = new Window { Content = content, Width = 320, Height = 900 };
        try
        {
            window.Show();
            await ExpandCardAsync(step1, "Step 1 · Geometry match");
            vm.NotchExportSummary = "CSV review exported: rows=1.";
            ActivateButton(window, FindCommandButton(step1, vm.ClearStep1ResultCommand));
            Assert.Empty(vm.MatchLinksForCanvas);
            Assert.Equal("Notch export: not run.", vm.NotchExportSummary);

            vm.IsStep5Expanded = true;
            vm.NotchExportSummary = "CSV review exported: rows=1.";
            vm.NotchExportProgress = 1;
            vm.NotchValidationSummaryText = "Synthetic validation result.";
            await HeadlessUiPump.FlushQueueAsync();
            ActivateButton(window, FindCommandButton(step5, vm.ClearStep5ResultCommand));

            Assert.Equal("Notch export: not run.", vm.NotchExportSummary);
            Assert.Equal(0, vm.NotchExportProgress);
            Assert.Equal("Idle.", vm.NotchExportProgressText);
            Assert.False(vm.HasNotchValidationItems);
            Assert.NotEqual("Synthetic validation result.", vm.NotchValidationSummaryText);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task BoundMaskControls_KeepImportClearAndTogglePathsAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateWorkspaceAsync(files);
        vm.IsStep1Expanded = true;
        var view = new RightWorkflowStep1View { DataContext = vm };
        var window = new Window { Content = view, Width = 320, Height = 900 };
        var maskPath = Path.ChangeExtension(files.ExportPath, ".csv");
        TestFiles.WriteText(maskPath, "Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,0,10\n");
        var pickerCalls = 0;
        vm.PickOpenRegularVisibilityMaskPathAsync = () =>
        {
            pickerCalls++;
            return Task.FromResult<string?>(maskPath);
        };
        try
        {
            window.Show();
            await ExpandCardAsync(view, "Step 1 · Geometry match");
            var mask = Assert.Single(view.GetVisualDescendants().OfType<CollapsiblePanel>(),
                static panel => panel.Title == "Regular Visibility Mask");
            mask.IsExpanded = true;
            await HeadlessUiPump.FlushQueueAsync();
            var import = FindCommandButton(view, vm.RegularMask.ImportRegularVisibilityMaskCommand);
            await Assert.IsAssignableFrom<IAsyncRelayCommand>(import.Command).ExecuteAsync(import.CommandParameter);
            Assert.Equal(1, pickerCalls);
            Assert.True(vm.RegularMask.Workflow.Mask.HasLoadedMask);
            var toggle = Assert.Single(view.GetVisualDescendants().OfType<ToggleSwitch>());
            toggle.IsChecked = true;
            Assert.True(vm.RegularMask.IsEnabled);
            toggle.IsChecked = false;
            Assert.False(vm.RegularMask.IsEnabled);
            toggle.IsChecked = true;
            ActivateButton(window, FindCommandButton(view, vm.RegularMask.ClearRegularVisibilityMaskCommand));
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(vm.RegularMask.IsEnabled);
            Assert.False(toggle.IsChecked);
            Assert.Contains("not loaded", vm.RegularMask.Summary, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task BoundCardCommands_DisableDuringProjectLoadAndRecoverAfterCancellationAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateWorkspaceAsync(files);
        vm.IsStep1Expanded = true;
        vm.IsStep5Expanded = true;
        var step1 = new RightWorkflowStep1View { DataContext = vm };
        var step5 = new RightWorkflowStep5View { DataContext = vm };
        var window = new Window { Content = new StackPanel { Children = { step1, step5 } }, Width = 320, Height = 900 };
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PickLoadProjectPathAsync = () => release.Task.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            window.Show();
            await ExpandCardAsync(step1, "Step 1 · Geometry match");
            FindPanel(step1, "Regular Visibility Mask").IsExpanded = true;
            await HeadlessUiPump.FlushQueueAsync();
            Button[] maskButtons = [FindCommandButton(step1, vm.ClearStep1ResultCommand),
                FindCommandButton(step1, vm.RegularMask.ImportRegularVisibilityMaskCommand),
                FindCommandButton(step1, vm.RegularMask.ClearRegularVisibilityMaskCommand)];
            await ExpandCardAsync(step5, "Step 5 · Notch export");
            Button[] buttons = [.. maskButtons, FindCommandButton(step5, vm.ClearStep5ResultCommand)];
            Assert.All(buttons, static button => Assert.True(button.IsEffectivelyEnabled));
            var load = vm.LoadProjectCommand.ExecuteAsync(null);
            try
            {
                await HeadlessUiPump.FlushQueueAsync();
                Assert.All(buttons, static button => Assert.False(button.IsEffectivelyEnabled));
            }
            finally
            {
                release.TrySetResult(null);
                await load.WaitAsync(TestContext.Current.CancellationToken);
            }

            await HeadlessUiPump.FlushQueueAsync();
            Assert.All(buttons, static button => Assert.True(button.IsEffectivelyEnabled));
        }
        finally
        {
            window.Close();
        }
    }
}

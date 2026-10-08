using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class ProjectLoadingSecondaryWindowTests
{
    [AvaloniaFact]
    public async Task LoadPickerAwait_BlocksOpenMappingReportOverridesAndRestoresCommands()
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new FreeformHelperViewModel();
        var project = GetField<ProjectFile>(vm, "_projectFile");
        project.DxfRegularMappingOverrides[10] = 200;
        var issue = new DxfRegularMappingIssue(DxfRegularMappingIssueKind.LowConfidence, "synthetic",
            CadPadId: 10, RegularPadIndex: 200, DiffIndex: 8);
        var report = new DxfRegularMappingReport(1, 1, 1, 0, 0, 1, 0, true, "synthetic", 1, false, [issue], [issue]);
        var clear = typeof(FreeformHelperViewModel).GetMethod("ClearMappingOverrideFromReport", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(clear);
        var clearOverride = clear.CreateDelegate<Func<int, bool>>(vm);
        var callbacks = 0;
        var reportVm = new IndexMappingReportViewModel("synthetic", report,
            locateTarget: (_, _) => { callbacks++; return true; },
            applyOverride: (_, _) => { callbacks++; return true; },
            applyDiffOverride: (_, _) => { callbacks++; return true; },
            clearOverride: clearOverride,
            getOverrideRegularIndex: _ => 200);
        var view = new FreeformHelperView { DataContext = vm };
        var owner = new Window { Content = view };
        IndexMappingReportWindow? window = null;
        Task? load = null;
        try
        {
            owner.Show();
            await ShowSecondaryWindowAsync(view, "ShowIndexMappingReportWindowAsync", reportVm);
            window = GetField<IndexMappingReportWindow>(view, "_indexMappingReportWindow");
            reportVm.SelectDecisionCommand.Execute(Assert.Single(reportVm.DecisionRows));
            Assert.True(reportVm.CanClearSelectedIssueOverride);
            callbacks = 0;
            IRelayCommand[] commands = [reportVm.ApplyOverrideCommand, reportVm.ApplyDiffOverrideCommand,
                reportVm.ApplySegmentDiffOverridesCommand, reportVm.ApplyCadOutputFwDiffOverridesCommand,
                reportVm.ClearOverrideCommand, reportVm.LocateSelectedIssueCommand];
            var notifications = 0;
            foreach (var command in commands)
            {
                Assert.True(command.CanExecute(null));
                command.CanExecuteChanged += (_, _) => notifications++;
            }

            vm.PickLoadProjectPathAsync = () => release.Task;
            load = vm.LoadProjectCommand.ExecuteAsync(null);
            Assert.False(window.IsEnabled);
            foreach (var command in commands)
            {
                Assert.False(command.CanExecute(null));
                command.Execute(null);
            }
            Assert.Equal(0, callbacks);
            Assert.Equal(200, project.DxfRegularMappingOverrides[10]);
            Assert.False(vm.CanUndo);
            Assert.False(vm.HasUnsavedChanges);
            Assert.True(notifications >= commands.Length);

            release.SetResult(null);
            await load;
            Assert.True(window.IsEnabled);
            Assert.All(commands, command => Assert.True(command.CanExecute(null)));
            Assert.True(notifications >= commands.Length * 2);
            reportVm.ClearOverrideCommand.Execute(null);
            Assert.Empty(project.DxfRegularMappingOverrides);
            Assert.True(vm.HasUnsavedChanges);
        }
        finally
        {
            release.TrySetResult(null);
            if (load is not null) await load;
            window?.Close();
            owner.Close();
        }
    }

    [AvaloniaFact]
    public async Task LoadPickerAwait_BlocksOpenNotchExportSelectionPreviewAndConfirmation()
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new FreeformHelperViewModel();
        var table = new NotchTable(new[]
        {
            new NotchTableRow(0, 9, 100, 10, new NotchV22Node(9, 100, 10, 25, 65535, 0, 0), "synthetic 1"),
            new NotchTableRow(0, 11, 101, 11, new NotchV22Node(11, 100, 12, 25, 65535, 0, 0), "synthetic 2")
        });
        var previews = 0;
        var export = new NotchExportSelectionViewModel(table, _ => previews++);
        var view = new FreeformHelperView { DataContext = vm };
        var owner = new Window { Content = view };
        NotchExportSelectionWindow? window = null;
        Task? load = null;
        Task? selection = null;
        try
        {
            owner.Show();
            selection = ShowSecondaryWindowAsync(view, "ShowNotchExportSelectionWindowAsync", export);
            window = GetField<NotchExportSelectionWindow>(view, "_notchExportSelectionWindow");
            var row = export.Rows.Last();
            var selected = export.SelectedCount;
            var notifications = 0;
            export.SelectNoneCommand.CanExecuteChanged += (_, _) => notifications++;
            previews = 0;
            vm.PickLoadProjectPathAsync = () => release.Task;
            load = vm.LoadProjectCommand.ExecuteAsync(null);
            Assert.False(window.IsEnabled);
            Assert.False(export.SelectNoneCommand.CanExecute(null));
            Assert.False(export.SelectPreviewRowCommand.CanExecute(row));
            export.SelectNoneCommand.Execute(null);
            export.SelectPreviewRowCommand.Execute(row);
            export.SelectedRow = row;
            var confirm = typeof(NotchExportSelectionWindow).GetMethod("RequestClose", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(confirm);
            confirm.Invoke(window, [true]);
            Assert.False(selection.IsCompleted);
            Assert.Equal(selected, export.SelectedCount);
            Assert.Equal(0, previews);
            Assert.False(vm.CanUndo);
            Assert.False(vm.HasUnsavedChanges);

            release.SetResult(null);
            await load;
            Assert.True(window.IsEnabled);
            Assert.True(export.SelectNoneCommand.CanExecute(null));
            Assert.True(export.SelectPreviewRowCommand.CanExecute(row));
            Assert.True(notifications >= 2);
            export.SelectedRow = null;
            export.SelectPreviewRowCommand.Execute(row);
            Assert.Equal(1, previews);
            confirm.Invoke(window, [true]);
            await selection;
        }
        finally
        {
            release.TrySetResult(null);
            if (load is not null) await load;
            window?.Close();
            if (selection is not null) await selection;
            owner.Close();
        }
    }

    private static Task ShowSecondaryWindowAsync(FreeformHelperView view, string name, object viewModel)
    {
        var method = typeof(FreeformHelperView).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method.Invoke(view, [viewModel]));
    }
}

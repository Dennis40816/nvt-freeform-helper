// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchExportPreviewBehaviorTests
{
    [AvaloniaFact]
    public async Task ExportNotchCommand_PreviewRow_ReplacesWorkspaceSelectionWithRowTargets()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        NotchTableRow? previewedRow = null;
        var previewStatus = string.Empty;
        vm.PickSaveNotchPathAsync = (_, _) => Task.FromResult<string?>(files.ExportPath);
        vm.OpenNotchExportSelectionAsync = selection =>
        {
            selection.SetViewFilterModeCommand.Execute(NotchExportRowDisplayMode.AllRows);
            var row = selection.Rows[0];
            var otherRegular = Assert.Single(vm.RegularPads, pad => pad.RegularPadId != row.Row.RegularPadIndex);
            vm.ApplyCanvasSelection(Array.Empty<int>(), new[] { otherRegular.RegularPadId });

            selection.SelectPreviewRowCommand.Execute(row);

            previewedRow = row.Row;
            previewStatus = vm.StatusText;
            return Task.FromResult<NotchTable?>(null);
        };

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.NotNull(previewedRow);
        Assert.Equal(Assert.Single(vm.CadPads).Id, previewedRow.CadPadId);
        Assert.Equal(previewedRow.CadPadId, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(previewedRow.RegularPadIndex, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal("Notch row focused: CAD=1, REG=1.", previewStatus);
        Assert.Equal("C v2.2 export canceled at stage 3 (row selection).", vm.NotchExportSummary);
    }

    [AvaloniaFact]
    public async Task ExportNotchCommand_CrossIcOwner_ReportsCadAndRegularCountsBeforeSelection()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateLoadedAsync();
        vm.CascadeNum = 2;
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        foreach (var cascade in vm.CascadeIcSettings)
        {
            cascade.XChannels = 1;
            cascade.YChannels = 1;
        }

        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        await vm.MatchCommand.ExecuteAsync(null);
        await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);
        var cad = Assert.Single(vm.CadPads);
        Assert.Equal(2, vm.RegularPads.Count);
        Assert.All(vm.RegularPads, pad => Assert.Equal(cad.Id, pad.MatchedCadPadId));
        vm.EnableV21 = false;
        vm.EnableV22 = true;
        vm.SelectedNotchExportFileTypeOption = Assert.Single(vm.NotchExportFileTypeOptions,
            option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv22);
        var summaries = new ConcurrentQueue<string>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FreeformHelperViewModel.NotchExportSummary))
            {
                summaries.Enqueue(vm.NotchExportSummary);
            }
        };
        NotchTable? reviewedTable = null;
        vm.PickSaveNotchPathAsync = (_, _) => Task.FromResult<string?>(files.ExportPath);
        vm.OpenNotchExportSelectionAsync = selection =>
        {
            reviewedTable = selection.BuildSelectedTable();
            return Task.FromResult<NotchTable?>(null);
        };

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.NotNull(reviewedTable);
        Assert.NotEmpty(reviewedTable.Rows);
        Assert.Contains(summaries, text => text.Contains(" | crossIC=1 cad/2 reg", StringComparison.Ordinal));
        Assert.Equal("C v2.2 export canceled at stage 3 (row selection).", vm.NotchExportSummary);
        Assert.False(File.Exists(files.ExportPath));
    }
}

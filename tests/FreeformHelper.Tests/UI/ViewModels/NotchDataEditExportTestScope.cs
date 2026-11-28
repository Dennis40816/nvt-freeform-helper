// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

internal sealed class NotchDataEditExportTestScope : IDisposable
{
    private readonly DxfEditingBehaviorTestScope _dxf = new();
    private readonly OperationProgressLifecycleTestScope _files = new();
    private int _savePickerCalls;

    public string ExportPath => Path.ChangeExtension(_files.ExportPath, ".csv");
    public int SavePickerCalls => _savePickerCalls;

    public async Task<FreeformHelperViewModel> CreateAsync()
    {
        var vm = await _files.CreateLoadedAsync();
        vm.RecalcBoundsOnLayerFilter = false;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        Assert.Single(vm.CascadeIcSettings).XChannels = 4;
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        await _dxf.OpenAsync(vm, ("signal", 0, 0, 10, 10), ("signal", 10, 0, 20, 10));
        vm.EnableV21 = false;
        vm.EnableV22 = true;
        // Isolate manual None from the separate To Full path that can emit untagged CAD rows.
        vm.EnableToFull = false;
        vm.SelectedNotchExportFileTypeOption = Assert.Single(vm.NotchExportFileTypeOptions,
            option => option.Value == FreeformHelperViewModel.NotchExportFileType.Csv);
        vm.OpenNotchExportSelectionAsync = null;
        vm.PickSaveNotchPathAsync = (_, _) =>
        {
            _savePickerCalls++;
            return Task.FromResult<string?>(ExportPath);
        };
        await RunStepsAsync(vm);
        Assert.Equal(2, vm.CadPads.Count);
        Assert.Equal(4, vm.RegularPads.Count);
        Assert.All(vm.RegularPads, pad => Assert.Equal(FreeformType.XWay, pad.Freeform));
        return vm;
    }

    public static async Task RunStepsAsync(FreeformHelperViewModel vm)
    {
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.True(vm.GetWorkflowStateSnapshot().HasStep1Result, vm.StatusText);
        await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);
        await vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);
        Assert.True(vm.GetWorkflowStateSnapshot().HasStep4Result, vm.StatusText);
    }

    public async Task<string> ExportAsync(FreeformHelperViewModel vm)
    {
        var pickerCalls = SavePickerCalls;
        await vm.ExportNotchCommand.ExecuteAsync(null);
        Assert.Equal(pickerCalls + 1, SavePickerCalls);
        Assert.Equal(1.0, vm.NotchExportProgress);
        Assert.StartsWith("CSV review exported: rows=", vm.NotchExportSummary, StringComparison.Ordinal);
        var text = await TestFiles.ReadContentAsync(ExportPath, TestContext.Current.CancellationToken);
        Assert.NotEmpty(Rows(text));
        return text;
    }

    public static string[][] Rows(string text)
    {
        // The identity and payload columns precede the quoted CSV comment.
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1)
            .Select(line => line.Split(','))
            .ToArray();
    }

    public static int FirstCadId(FreeformHelperViewModel vm)
        => vm.CadPads.OrderBy(pad => pad.Bounds.MinX).First().Id;

    public static void Tag(FreeformHelperViewModel vm, int cadId, FreeformType type)
    {
        var ids = vm.GetMatchedRegularPadIds(cadId);
        Assert.NotEmpty(ids);
        vm.ApplyCanvasSelection([], ids);
        var command = type switch
        {
            FreeformType.None => vm.SetFreeformNoneCommand,
            FreeformType.XWay => vm.SetFreeformXCommand,
            FreeformType.XYWay => vm.SetFreeformXYCommand,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
        command.Execute(null);
        Assert.All(vm.RegularPads.Where(pad => ids.Contains(pad.RegularPadId)),
            pad => Assert.Equal(type, pad.Freeform));
        Assert.Equal($"Freeform set to {type} for {ids.Count} regular pads.", vm.StatusText);
    }

    public static void AssertDownstreamCleared(FreeformHelperViewModel vm, bool hasMatch, bool hasFreeform)
    {
        var state = vm.GetWorkflowStateSnapshot();
        Assert.Equal(hasMatch, state.HasStep1Result);
        Assert.Equal(hasFreeform, state.HasStep2Result);
        Assert.False(state.HasStep3Result);
        Assert.False(state.HasStep4Result);
        Assert.Empty(vm.NotchCanvasPreviewItems);
        Assert.Equal(MappingDiagnosticsResult.NotRunSummary, vm.DxfRegularMappingSummary);
        Assert.Equal("Notch export: not run.", vm.NotchExportSummary);
        Assert.Equal("Idle.", vm.NotchExportProgressText);
        Assert.Equal(0.0, vm.NotchExportProgress);
    }

    public void Dispose()
    {
        _dxf.Dispose();
        _files.Dispose();
    }
}

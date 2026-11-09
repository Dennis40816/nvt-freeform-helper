// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text.Json;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class Step3ClearWorkflowBehaviorTests
{
    [AvaloniaFact]
    public async Task ClearStep3ResultCommand_ClearsPreviewAndDownstreamResultsButPreservesUpstreamAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var project = new SyntheticWorkflowProjectScope();
        var vm = await project.LoadAsync();
        await RunThroughStep4AndGenerateExportAsync(vm, project);
        var matches = vm.MatchLinksForCanvas.ToArray();
        var freeforms = vm.RegularPads.Select(static pad => (pad.RegularPadId, pad.MatchedCadPadId, pad.Freeform)).ToArray();
        var stats = vm.FreeformAutoDetectSummary;

        vm.ClearStep3ResultCommand.Execute(null);

        AssertClearedResults(vm);
        Assert.Equal(matches, vm.MatchLinksForCanvas);
        Assert.Equal(freeforms, vm.RegularPads.Select(static pad => (pad.RegularPadId, pad.MatchedCadPadId, pad.Freeform)));
        Assert.Equal(stats, vm.FreeformAutoDetectSummary);
        Assert.Equal("Step 3 result cleared.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task ClearStepQuery_Step3_ClearsTheSameResultsAndReportsPreservedUpstreamFlagsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var project = new SyntheticWorkflowProjectScope();
        var vm = await project.LoadAsync();
        await RunThroughStep4AndGenerateExportAsync(vm, project);
        var matches = vm.MatchLinksForCanvas.ToArray();
        var freeforms = vm.RegularPads.Select(static pad => (pad.RegularPadId, pad.MatchedCadPadId, pad.Freeform)).ToArray();
        using var shell = new ShellViewModel(vm) { IsDevPageAvailable = true };
        var work = new RecordingBackgroundWorkRunner();
        shell.BackgroundWork.Runner = work;
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            RuntimeQueryProtocol.Version, "clear-step", new Dictionary<string, string> { ["step"] = "3" }));

        Assert.True(response.Ok, response.Error?.Message);
        await work.LastStartedAsync(ShellViewModel.WorkspaceSourceChangeDrainWorkName);
        var payload = JsonSerializer.SerializeToElement(response.Data, RuntimeQueryProtocol.CompactJsonOptions);
        Assert.Equal(3, payload.GetProperty("step").GetInt32());
        var workflow = payload.GetProperty("workflow");
        Assert.True(workflow.GetProperty("hasCad").GetBoolean());
        Assert.True(workflow.GetProperty("hasGrid").GetBoolean());
        Assert.True(workflow.GetProperty("hasStep1Result").GetBoolean());
        Assert.True(workflow.GetProperty("hasStep2Result").GetBoolean());
        Assert.False(workflow.GetProperty("hasStep3Result").GetBoolean());
        Assert.False(workflow.GetProperty("hasStep4Result").GetBoolean());
        AssertClearedResults(vm);
        Assert.Equal(matches, vm.MatchLinksForCanvas);
        Assert.Equal(freeforms, vm.RegularPads.Select(static pad => (pad.RegularPadId, pad.MatchedCadPadId, pad.Freeform)));
    }

    private static async Task RunThroughStep4AndGenerateExportAsync(
        FreeformHelperViewModel vm,
        SyntheticWorkflowProjectScope project)
    {
        await vm.MatchCommand.ExecuteAsync(null);
        await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);
        var cad = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
        var regular = Assert.Single(vm.RegularPads, pad => pad.MatchedCadPadId == cad.Id);
        vm.ApplyCanvasSelection([], [regular.Index]);
        vm.SetFreeformXCommand.Execute(null);
        vm.EnableToFull = true;
        vm.ShowNotchCanvasPreview = true;
        vm.ShowNotchToRegularLabels = true;
        vm.ApplyCanvasSelection([cad.Id], []);
        var preview = await vm.RunWorkflowStepAsync(WorkflowStepId.Step3NotchPreview);
        Assert.True(preview.IsReady, preview.StatusText);
        Assert.Equal(cad.Id, Assert.Single(vm.NotchCanvasPreviewItems).CadPadId);
        Assert.True(vm.CanControlNotchPreviewStage);
        var diagnostics = await vm.RunWorkflowStepAsync(WorkflowStepId.Step4IndexDiagnostics);
        Assert.True(diagnostics.IsReady, diagnostics.StatusText);
        vm.PickSaveNotchPathAsync = (_, _) => Task.FromResult<string?>(project.PathFor("notch.csv"));
        await vm.ExportNotchCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.RegularPads.SelectMany(pad => vm.GetLastNotchRowsForRegularPad(pad.RegularPadId)));
        Assert.NotEqual("Notch export: not run.", vm.NotchExportSummary);
        var workflow = vm.GetWorkflowStateSnapshot();
        Assert.True(workflow.HasStep1Result);
        Assert.True(workflow.HasStep2Result);
        Assert.True(workflow.HasStep3Result);
        Assert.True(workflow.HasStep4Result);
    }

    private static void AssertClearedResults(FreeformHelperViewModel vm)
    {
        var workflow = vm.GetWorkflowStateSnapshot();
        Assert.True(workflow.HasCad);
        Assert.True(workflow.HasGrid);
        Assert.True(workflow.HasStep1Result);
        Assert.True(workflow.HasStep2Result);
        Assert.False(workflow.HasStep3Result);
        Assert.False(workflow.HasStep4Result);
        Assert.Empty(vm.NotchCanvasPreviewItems);
        Assert.False(vm.CanControlNotchPreviewStage);
        Assert.False(vm.IsNotchToRegularPreviewVisible);
        Assert.False(vm.IsNotchToFullSeedPreviewVisible);
        Assert.False(vm.IsNotchToFullCandidatePreviewVisible);
        Assert.False(vm.IsNotchToFullFinalPreviewVisible);
        Assert.Equal(MappingDiagnosticsResult.NotRunSummary, vm.DxfRegularMappingSummary);
        Assert.Empty(vm.DxfRegularMappingIssues);
        Assert.False(vm.HasDxfRegularMappingIssues);
        Assert.Equal(0d, vm.IndexMappingProgress);
        Assert.Empty(vm.RegularPads.SelectMany(pad => vm.GetLastNotchRowsForRegularPad(pad.RegularPadId)));
        Assert.Equal("Notch export: not run.", vm.NotchExportSummary);
        Assert.Equal(0d, vm.NotchExportProgress);
        Assert.Equal("Idle.", vm.NotchExportProgressText);
    }
}

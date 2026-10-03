using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateSimulationWorkspaceSessionAsync_FinalProjectionChangesAtCompletion_RetainsBatchButRejectsProjection(
        bool applyThroughSettingsWindow)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 20, 10));
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            vm.EnableTargetCoverageGuard = applyThroughSettingsWindow;
            vm.TargetCoverageCapPercent = 50m;
            var step3Revision = vm.GetNotchStep3Revision();
            var sourceRevision = vm.SimulationWorkspaceSourceRevision;

            var staleSession = await RunSimulationRequestAtFinalPhaseAsync(
                vm,
                currentVm =>
                {
                    if (applyThroughSettingsWindow)
                    {
                        var settings = currentVm.CreateSettingsWindowViewModel();
                        settings.TargetCoverageCapPercent = 255m;
                        settings.SaveCommand.Execute(null);
                    }
                    else
                    {
                        currentVm.EnableTargetCoverageGuard = true;
                    }
                });

            Assert.Null(staleSession);
            Assert.Equal(step3Revision, vm.GetNotchStep3Revision());
            Assert.True(vm.SimulationWorkspaceSourceRevision > sourceRevision);
            Assert.Equal("Notch export: not run.", vm.NotchExportSummary);
            Assert.DoesNotContain(
                "Simulation ready",
                vm.LastSimulationWorkspaceAttemptMessage,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "generated",
                vm.NotchExportProgressText,
                StringComparison.OrdinalIgnoreCase);

            var regularPadId = vm.RegularPads[0].RegularPadId;
            Assert.Equal(0, vm.GetLastNotchRowCountForRegularPad(regularPadId));
            Assert.False(vm.TryGetNotchValidationReportForRegularPad(
                regularPadId,
                out _,
                out _,
                out var validationFailureCode,
                out _));
            Assert.Equal("NOT_READY", validationFailureCode);

            var staleMetrics = vm.GetNotchExportGenerationCacheMetrics();
            Assert.True(staleMetrics.HasEntry);
            Assert.Equal(0, staleMetrics.EntryRowCount);
            Assert.Equal(1, staleMetrics.MissCount);
            Assert.Equal(0, staleMetrics.HitCount);
            Assert.Equal(1, staleMetrics.StoreCount);
            Assert.Equal(0, staleMetrics.ClearCount);

            var currentSession = await vm.CreateSimulationWorkspaceSessionAsync();
            Assert.NotNull(currentSession);
            Assert.Equal(vm.SimulationWorkspaceSourceRevision, currentSession!.SourceRevision);

            var currentMetrics = vm.GetNotchExportGenerationCacheMetrics();
            Assert.True(currentMetrics.EntryRowCount > 0);
            Assert.Equal(1, currentMetrics.MissCount);
            Assert.Equal(1, currentMetrics.HitCount);
            Assert.Equal(1, currentMetrics.StoreCount);

            var freshVm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            freshVm.EnableTargetCoverageGuard = true;
            freshVm.TargetCoverageCapPercent = applyThroughSettingsWindow ? 255m : 50m;
            var freshSession = await freshVm.CreateSimulationWorkspaceSessionAsync();

            Assert.NotNull(freshSession);
            Assert.Equal(
                NotchTableExporter.ExportAsCsv(freshSession!.Table),
                NotchTableExporter.ExportAsCsv(currentSession.Table));
            Assert.Equivalent(
                freshSession.Table.ToFullCoverageAudit,
                currentSession.Table.ToFullCoverageAudit,
                strict: true);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task CreateSimulationWorkspaceSessionAsync_DoesNotExposePreAcceptanceGeneratedCallback()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 20, 10));
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            var progressMessages = new List<string>();

            var session = await vm.CreateSimulationWorkspaceSessionAsync(
                silent: false,
                progressTextReporter: progressMessages.Add);

            Assert.NotNull(session);
            Assert.DoesNotContain(
                progressMessages,
                message => message.StartsWith("Simulation workspace: generated ", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task CreateSimulationWorkspaceSessionAsync_WarmFinalOnlyInvalidation_ResetsRowCountAndClearsPriorSuccessOnReject()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 20, 10));
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            vm.EnableTargetCoverageGuard = false;
            vm.TargetCoverageCapPercent = 50m;

            var acceptedSession = await vm.CreateSimulationWorkspaceSessionAsync();

            Assert.NotNull(acceptedSession);
            Assert.Contains(
                "Simulation ready",
                vm.LastSimulationWorkspaceAttemptMessage,
                StringComparison.OrdinalIgnoreCase);
            Assert.True(vm.GetNotchExportGenerationCacheMetrics().EntryRowCount > 0);

            vm.EnableTargetCoverageGuard = true;

            var invalidatedMetrics = vm.GetNotchExportGenerationCacheMetrics();
            Assert.True(invalidatedMetrics.HasEntry);
            Assert.Equal(0, invalidatedMetrics.EntryRowCount);
            Assert.Equal(1, invalidatedMetrics.StoreCount);
            Assert.Equal(0, invalidatedMetrics.ClearCount);

            var rejectedSession = await RunSimulationRequestAtFinalPhaseAsync(
                vm,
                currentVm => currentVm.TargetCoverageCapPercent = 255m);

            Assert.Null(rejectedSession);
            Assert.DoesNotContain(
                "Simulation ready",
                vm.LastSimulationWorkspaceAttemptMessage,
                StringComparison.OrdinalIgnoreCase);

            var rejectedMetrics = vm.GetNotchExportGenerationCacheMetrics();
            Assert.True(rejectedMetrics.HasEntry);
            Assert.Equal(0, rejectedMetrics.EntryRowCount);
            Assert.Equal(1, rejectedMetrics.MissCount);
            Assert.Equal(1, rejectedMetrics.HitCount);
            Assert.Equal(1, rejectedMetrics.StoreCount);
            Assert.Equal(0, rejectedMetrics.ClearCount);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task CreateSimulationWorkspaceSessionAsync_ComputationChangesAtCompletion_DiscardsStaleBatch()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 20, 10));
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            var selectedCad = vm.CadPads.First();
            vm.ShowNotchCanvasPreview = false;
            vm.ApplyCanvasSelection([selectedCad.Id], Array.Empty<int>());
            Assert.Equal(0, vm.GetNotchCompensationCacheMetrics().EntryCount);
            var step3Revision = vm.GetNotchStep3Revision();

            var staleSession = await RunSimulationRequestAtFinalPhaseAsync(
                vm,
                currentVm =>
                {
                    currentVm.ToFullStrictOverlapPercent += 0.1m;
                });

            Assert.Null(staleSession);
            Assert.True(vm.GetNotchStep3Revision() > step3Revision);
            Assert.Equal("Notch export: not run.", vm.NotchExportSummary);
            Assert.DoesNotContain(
                "generated",
                vm.NotchExportProgressText,
                StringComparison.OrdinalIgnoreCase);

            var staleMetrics = vm.GetNotchExportGenerationCacheMetrics();
            Assert.False(staleMetrics.HasEntry);
            Assert.Equal(0, staleMetrics.EntryRowCount);
            Assert.Equal(1, staleMetrics.MissCount);
            Assert.Equal(0, staleMetrics.HitCount);
            Assert.Equal(1, staleMetrics.StoreCount);
            Assert.Equal(1, staleMetrics.ClearCount);
            Assert.Equal(0, vm.GetNotchCompensationCacheMetrics().EntryCount);
            var currentResolved = Assert.IsType<NotchV22ResolvedResult>(
                vm.GetCadV22ResolvedResult(selectedCad.Id));
            Assert.Equal(
                vm.GetNotchMultiOwnerStrictOverlapPercent() / 100.0,
                currentResolved.Identity!.StrictOverlapRatio,
                12);

            var currentSession = await vm.CreateSimulationWorkspaceSessionAsync();
            Assert.NotNull(currentSession);
            var currentMetrics = vm.GetNotchExportGenerationCacheMetrics();
            Assert.True(currentMetrics.EntryRowCount > 0);
            Assert.Equal(2, currentMetrics.MissCount);
            Assert.Equal(0, currentMetrics.HitCount);
            Assert.Equal(2, currentMetrics.StoreCount);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task CreateSimulationWorkspaceSessionAsync_Step5ClearsAtCompletion_DoesNotReviveClearedState()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 20, 10));
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(dxfPath);

            var staleSession = await RunSimulationRequestAtFinalPhaseAsync(
                vm,
                static currentVm => currentVm.ClearStep5ResultCommand.Execute(null));

            Assert.Null(staleSession);
            Assert.Equal("Step 5 result cleared.", vm.StatusText);
            Assert.Equal("Notch export: not run.", vm.NotchExportSummary);
            Assert.Equal("Idle.", vm.NotchExportProgressText);

            var staleMetrics = vm.GetNotchExportGenerationCacheMetrics();
            Assert.False(staleMetrics.HasEntry);
            Assert.Equal(0, staleMetrics.EntryRowCount);
            Assert.Equal(1, staleMetrics.MissCount);
            Assert.Equal(0, staleMetrics.HitCount);
            Assert.Equal(1, staleMetrics.StoreCount);
            Assert.Equal(1, staleMetrics.ClearCount);

            var currentSession = await vm.CreateSimulationWorkspaceSessionAsync();
            Assert.NotNull(currentSession);
            var currentMetrics = vm.GetNotchExportGenerationCacheMetrics();
            Assert.True(currentMetrics.EntryRowCount > 0);
            Assert.Equal(2, currentMetrics.MissCount);
            Assert.Equal(2, currentMetrics.StoreCount);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task CreateSimulationWorkspaceSessionAsync_SourceChangesAfterAcceptance_UsesCapturedSourceRevision()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 20, 10));
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            vm.EnableTargetCoverageGuard = false;
            var acceptedSourceRevision = vm.SimulationWorkspaceSourceRevision;
            var mutationCount = 0;

            var session = await vm.CreateSimulationWorkspaceSessionAsync(
                silent: false,
                progressTextReporter: message =>
                {
                    if (!message.Contains("Finalizing simulation workspace", StringComparison.Ordinal) ||
                        Interlocked.CompareExchange(ref mutationCount, 1, 0) != 0)
                    {
                        return;
                    }

                    vm.EnableTargetCoverageGuard = true;
                });

            Assert.Equal(1, mutationCount);
            Assert.NotNull(session);
            Assert.Equal(acceptedSourceRevision, session!.SourceRevision);
            Assert.True(session.SourceRevision < vm.SimulationWorkspaceSourceRevision);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task ExportAndSimulation_Overlap_CoalesceColdBatchAndKeepBusyUntilBothOperationsComplete()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateConcurrentNotchDxfFile();
        var selectionEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSelection = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task exportRequest = Task.CompletedTask;
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            var selectedCad = vm.CadPads.First();
            vm.ApplyCanvasSelection([selectedCad.Id], Array.Empty<int>());
            var selectedResolved = Assert.IsType<NotchV22ResolvedResult>(
                vm.GetCadV22ResolvedResult(selectedCad.Id));
            vm.PickSaveNotchPathAsync = (_, _) => Task.FromResult<string?>(null);
            vm.OpenNotchExportSelectionAsync = async _ =>
            {
                selectionEntered.TrySetResult();
                await releaseSelection.Task;
                return null;
            };

            var exportStarted = 0;
            var session = await vm.CreateSimulationWorkspaceSessionAsync(
                silent: false,
                progressTextReporter: message =>
                {
                    if (!message.Contains("Build allocation profiles", StringComparison.Ordinal) ||
                        Interlocked.CompareExchange(ref exportStarted, 1, 0) != 0)
                    {
                        return;
                    }

                    exportRequest = vm.ExportNotchCommand.ExecuteAsync(null);
                });
            await selectionEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(1, exportStarted);
            Assert.NotNull(session);
            Assert.False(exportRequest.IsCompleted);
            Assert.True(vm.IsNotchExporting);

            releaseSelection.TrySetResult();
            await exportRequest;
            Assert.False(vm.IsNotchExporting);

            var metrics = vm.GetNotchExportGenerationCacheMetrics();
            Assert.Equal(1, metrics.MissCount);
            Assert.Equal(1, metrics.StoreCount);
            Assert.Equal(0, metrics.ClearCount);
            Assert.Same(selectedResolved, vm.GetCadV22ResolvedResult(selectedCad.Id));
        }
        finally
        {
            releaseSelection.TrySetResult();
            await exportRequest;

            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task SimulationOverlap_FinalOnlyChange_SharesColdBatchButProjectsCurrentRequest()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateConcurrentNotchDxfFile();
        Task<SimulationWorkspaceSession?> currentRequest = Task.FromResult<SimulationWorkspaceSession?>(null);
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            vm.EnableTargetCoverageGuard = false;
            vm.TargetCoverageCapPercent = 50m;
            var currentStarted = 0;

            var staleRequest = vm.CreateSimulationWorkspaceSessionAsync(
                silent: false,
                progressTextReporter: message =>
                {
                    if (!message.Contains("Build allocation profiles", StringComparison.Ordinal) ||
                        Interlocked.CompareExchange(ref currentStarted, 1, 0) != 0)
                    {
                        return;
                    }

                    vm.EnableTargetCoverageGuard = true;
                    currentRequest = vm.CreateSimulationWorkspaceSessionAsync();
                });

            var staleSession = await staleRequest;
            var currentSession = await currentRequest;

            Assert.Equal(1, currentStarted);
            Assert.Null(staleSession);
            Assert.NotNull(currentSession);
            Assert.Equal(vm.SimulationWorkspaceSourceRevision, currentSession!.SourceRevision);

            var metrics = vm.GetNotchExportGenerationCacheMetrics();
            Assert.True(metrics.EntryRowCount > 0);
            Assert.Equal(1, metrics.MissCount);
            Assert.Equal(1, metrics.StoreCount);
            Assert.Equal(0, metrics.ClearCount);

            var freshVm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            freshVm.EnableTargetCoverageGuard = true;
            freshVm.TargetCoverageCapPercent = 50m;
            var freshSession = await freshVm.CreateSimulationWorkspaceSessionAsync();

            Assert.NotNull(freshSession);
            Assert.Equal(
                NotchTableExporter.ExportAsCsv(freshSession!.Table),
                NotchTableExporter.ExportAsCsv(currentSession.Table));
            Assert.Equivalent(
                freshSession.Table.ToFullCoverageAudit,
                currentSession.Table.ToFullCoverageAudit,
                strict: true);
        }
        finally
        {
            await currentRequest;
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task ExportNotchCommand_Step5ClearsAtCompletion_DoesNotOpenDialogsOrWriteFile()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 20, 10));
        var exportPath = Path.Combine(Path.GetTempPath(), $"stale-notch-export-{Guid.NewGuid():N}.csv");
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            var selectionCount = 0;
            var savePickerCount = 0;
            var mutationCount = 0;
            vm.OpenNotchExportSelectionAsync = _ =>
            {
                selectionCount++;
                return Task.FromResult<FreeformHelper.Domain.Notch.NotchTable?>(null);
            };
            vm.PickSaveNotchPathAsync = (_, _) =>
            {
                savePickerCount++;
                return Task.FromResult<string?>(exportPath);
            };
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(FreeformHelperViewModel.NotchExportProgressText) ||
                    !vm.NotchExportProgressText.Contains("Finalize canonical exports 1/1", StringComparison.Ordinal) ||
                    Interlocked.CompareExchange(ref mutationCount, 1, 0) != 0)
                {
                    return;
                }

                vm.ClearStep5ResultCommand.Execute(null);
            };

            await vm.ExportNotchCommand.ExecuteAsync(null);

            Assert.Equal(1, mutationCount);
            Assert.Equal(0, selectionCount);
            Assert.Equal(0, savePickerCount);
            Assert.False(File.Exists(exportPath));
            Assert.Equal("Notch export: not run.", vm.NotchExportSummary);
            Assert.Equal("Step 5 result cleared.", vm.StatusText);
            Assert.Equal("Idle.", vm.NotchExportProgressText);

            var metrics = vm.GetNotchExportGenerationCacheMetrics();
            Assert.False(metrics.HasEntry);
            Assert.Equal(0, metrics.EntryRowCount);
            Assert.Equal(1, metrics.MissCount);
            Assert.Equal(1, metrics.StoreCount);
            Assert.Equal(1, metrics.ClearCount);
        }
        finally
        {
            File.Delete(dxfPath);
            File.Delete(exportPath);
        }
    }

    private static async Task<SimulationWorkspaceSession?> RunSimulationRequestAtFinalPhaseAsync(
        FreeformHelperViewModel vm,
        Action<FreeformHelperViewModel> mutate)
    {
        var finalPhaseReached = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var mutationCount = 0;
        var supersededSuccessCount = 0;
        var request = vm.CreateSimulationWorkspaceSessionAsync(
            silent: false,
            progressTextReporter: message =>
            {
                if (Volatile.Read(ref mutationCount) > 0 &&
                    message.Contains(": generated ", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref supersededSuccessCount);
                }

                if (!message.Contains("Finalize canonical exports 1/1", StringComparison.Ordinal) ||
                    Interlocked.CompareExchange(ref mutationCount, 1, 0) != 0)
                {
                    return;
                }

                mutate(vm);
                finalPhaseReached.TrySetResult();
            });

        await finalPhaseReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var session = await request;
        Assert.Equal(1, mutationCount);
        Assert.Equal(0, supersededSuccessCount);
        return session;
    }

    private static string CreateConcurrentNotchDxfFile()
    {
        return CreateTempDxfFile(Enumerable.Range(0, 64)
            .Select(static index => (
                Layer: "signal",
                MinX: index * 0.01,
                MinY: 0d,
                MaxX: 20d + (index * 0.01),
                MaxY: 10d))
            .ToArray());
    }
}

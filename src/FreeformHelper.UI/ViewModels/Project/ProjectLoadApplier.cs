// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Diagnostics;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.UseCases.ProjectLoad;

namespace FreeformHelper.UI.ViewModels.Project;

/// <summary>Applies an accepted document in the existing observable order, including partial failure state.</summary>
internal sealed class ProjectLoadApplier(
    ProjectApplyPorts apply,
    ProjectCadPorts cad,
    ProjectReplayPorts replay,
    ProjectOperationPorts operations)
{
    private readonly ProjectApplyPorts _apply = apply;
    private readonly ProjectCadPorts _cad = cad;
    private readonly ProjectReplayPorts _replay = replay;
    private readonly ProjectOperationPorts _operations = operations;

    public async Task<ProjectLoadApplyTiming> ApplyAsync(ProjectFile file, string path, long persistenceElapsedMs)
    {
        var status = _operations.CreateStatus("LoadProject");
        var totalSw = Stopwatch.StartNew();
        _operations.Info("Loading project: {0}", path);
        var originalSuppressAppGeneralPersistence = _apply.GetSuppressPersistence();
        _apply.SetSuppressPersistence(true);
        _apply.InstallDocument(file);
        _apply.InvalidateWorkflowSnapshot();
        _apply.SetPath(path);
        try
        {
            using var sourceChangeBatch = _apply.BeginSourceChangeBatch();
            var applyStateSw = Stopwatch.StartNew();
            ApplyLoadedProjectSettings(file, path, status);
            applyStateSw.Stop();

            var cadReload = await ReloadCadAfterProjectLoadAsync(file, path, status);

            cadReload.CombinedGroupReplay?.ReportWarnings(status.ReportWarning, message => _operations.WarnFormat("{0}", message));
            totalSw.Stop();
            LogProjectLoadTiming(path, persistenceElapsedMs, totalSw.ElapsedMilliseconds, applyStateSw.ElapsedMilliseconds, cadReload);

            return new ProjectLoadApplyTiming(
                TotalElapsedMs: totalSw.ElapsedMilliseconds,
                ApplyStateElapsedMs: applyStateSw.ElapsedMilliseconds,
                DxfElapsedMs: cadReload.DxfElapsedMs,
                RebuildElapsedMs: cadReload.RebuildElapsedMs,
                Step1ReplayElapsedMs: cadReload.ReplayTiming.Step1ReplayElapsedMs,
                Step2ReplayElapsedMs: cadReload.ReplayTiming.Step2ReplayElapsedMs,
                Step1Replayed: cadReload.ReplayTiming.Step1Replayed,
                Step2Replayed: cadReload.ReplayTiming.Step2Replayed);
        }
        finally
        {
            _apply.SetSuppressPersistence(originalSuppressAppGeneralPersistence);
        }
    }

    private void ApplyLoadedProjectSettings(ProjectFile file, string path, ProjectOperationStatus status)
    {
        _apply.ReplaceCustomPadValues(file);
        _apply.LoadSettings(file.Settings);
        _apply.ApplyView(file.UiSnapshot.View);
        _apply.ApplyImport(file.UiSnapshot.Import, file);
        _apply.ApplyAppVisualPreferences();
        _apply.ClearStep5();
        status.ReportSuccess($"Project loaded: {path}");
        _operations.Info("Project loaded: {0}", path);
        _apply.SetDirty(false);
        _apply.DeferAppPersistence();
    }

    private async Task<ProjectLoadCadReload> ReloadCadAfterProjectLoadAsync(ProjectFile file, string path, ProjectOperationStatus status)
    {
        var dxfElapsedMs = 0L;
        var rebuildElapsedMs = 0L;
        var replayTiming = ProjectLoadReplayTiming.Empty;
        DxfCombinedGroupReplayResult? combinedGroupReplay = null;

        using (_apply.EnterSettingsLoading())
        {
            _apply.ResetCad();
            _apply.ResetCadEditBaseline();
            _apply.ClearCadBuildCache();
            _apply.ClearCadCatalog();
            _apply.PublishEmptyCadPads();
            _apply.DetachLayerToggles();

            await _apply.RunCadOverlayAsync(async () =>
            {
                (dxfElapsedMs, combinedGroupReplay) = await _cad.LoadAsync(file, path, status);

                var rebuildSw = Stopwatch.StartNew();
                await _apply.RebuildGridAsync();
                rebuildSw.Stop();
                rebuildElapsedMs = rebuildSw.ElapsedMilliseconds;

                replayTiming = await ReplayWorkflowStepsAfterProjectLoadAsync();
            });
        }

        return new ProjectLoadCadReload(dxfElapsedMs, rebuildElapsedMs, replayTiming, combinedGroupReplay);
    }

    private void LogProjectLoadTiming(string path, long persistenceElapsedMs, long totalElapsedMs, long applyStateElapsedMs, ProjectLoadCadReload cadReload)
    {
        var replayTiming = cadReload.ReplayTiming;
        _operations.Info("Load project timing: total={0} ms, persistence={1} ms, apply-state={2} ms, dxf={3} ms, rebuild={4} ms.",
            totalElapsedMs,
            persistenceElapsedMs,
            applyStateElapsedMs,
            cadReload.DxfElapsedMs,
            cadReload.RebuildElapsedMs);
        _operations.Info("Load project timing detail: step1-auto-replay={0} ms (replayed={1}).", replayTiming.Step1ReplayElapsedMs, replayTiming.Step1Replayed);
        _operations.Info("Load project timing detail: step2-auto-replay={0} ms (replayed={1}).", replayTiming.Step2ReplayElapsedMs, replayTiming.Step2Replayed);
        _operations.Debug("PERF LOAD_PROJECT SUMMARY: total={0}ms persistence={1}ms applyState={2}ms dxf={3}ms rebuild={4}ms step1={5}ms step2={6}ms step1Replayed={7} step2Replayed={8} path={9}",
            totalElapsedMs,
            persistenceElapsedMs,
            applyStateElapsedMs,
            cadReload.DxfElapsedMs,
            cadReload.RebuildElapsedMs,
            replayTiming.Step1ReplayElapsedMs,
            replayTiming.Step2ReplayElapsedMs,
            replayTiming.Step1Replayed ? "Y" : "N",
            replayTiming.Step2Replayed ? "Y" : "N",
            path);
    }

    private async Task<ProjectLoadReplayTiming> ReplayWorkflowStepsAfterProjectLoadAsync()
    {
        if (!_replay.IsStep2Enabled())
        {
            _operations.InfoMessage(_replay.GetDisabledMessage());
        }

        var step1ElapsedMs = 0L;
        var step2ElapsedMs = 0L;
        var step1Replayed = false;
        var step2Replayed = false;
        var replayPlan = _replay.BuildPlan();

        foreach (var action in replayPlan)
        {
            _operations.InfoMessage(action.GetStartedMessage());
            var stepSw = Stopwatch.StartNew();
            var replayed = await action.ExecuteAsync();
            stepSw.Stop();

            if (action.Step == ProjectReplayStep.Step1Match)
            {
                step1ElapsedMs = stepSw.ElapsedMilliseconds;
                step1Replayed = replayed;
            }
            else if (action.Step == ProjectReplayStep.Step2Freeform)
            {
                step2ElapsedMs = stepSw.ElapsedMilliseconds;
                step2Replayed = replayed;
            }

            if (replayed)
            {
                _operations.InfoMessage(action.GetCompletedMessage());
            }
            else
            {
                _operations.InfoMessage(action.GetSkippedMessage());
            }
        }

        return new ProjectLoadReplayTiming(
            Step1ReplayElapsedMs: step1ElapsedMs,
            Step2ReplayElapsedMs: step2ElapsedMs,
            Step1Replayed: step1Replayed,
            Step2Replayed: step2Replayed);
    }
}

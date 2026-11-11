// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.UseCases.ProjectLoad;

namespace FreeformHelper.UI.ViewModels.Project;

/// <summary>Individual application operations; no workspace values are captured when these ports are composed.</summary>
internal sealed class ProjectApplyPorts(
    Func<bool> getSuppressPersistence,
    Action<bool> setSuppressPersistence,
    Action<ProjectFile> installDocument,
    Action invalidateWorkflowSnapshot,
    Action<string> setPath,
    Func<IDisposable> beginSourceChangeBatch,
    Action<ProjectFile> replaceCustomPadValues,
    Action<ProjectSettings> loadSettings,
    Action<UiViewSnapshot> applyView,
    Action<UiImportSnapshot, ProjectFile> applyImport,
    Action applyAppVisualPreferences,
    Action clearStep5,
    Action<bool> setDirty,
    Action deferAppPersistence,
    Func<IDisposable> enterSettingsLoading,
    Action resetCad,
    Action resetCadEditBaseline,
    Action clearCadBuildCache,
    Action clearCadCatalog,
    Action publishEmptyCadPads,
    Action detachLayerToggles,
    Func<Func<Task>, Task> runCadOverlayAsync,
    Func<Task> rebuildGridAsync)
{
    public Func<bool> GetSuppressPersistence { get; } = getSuppressPersistence;
    public Action<bool> SetSuppressPersistence { get; } = setSuppressPersistence;
    public Action<ProjectFile> InstallDocument { get; } = installDocument;
    public Action InvalidateWorkflowSnapshot { get; } = invalidateWorkflowSnapshot;
    public Action<string> SetPath { get; } = setPath;
    public Func<IDisposable> BeginSourceChangeBatch { get; } = beginSourceChangeBatch;
    public Action<ProjectFile> ReplaceCustomPadValues { get; } = replaceCustomPadValues;
    public Action<ProjectSettings> LoadSettings { get; } = loadSettings;
    public Action<UiViewSnapshot> ApplyView { get; } = applyView;
    public Action<UiImportSnapshot, ProjectFile> ApplyImport { get; } = applyImport;
    public Action ApplyAppVisualPreferences { get; } = applyAppVisualPreferences;
    public Action ClearStep5 { get; } = clearStep5;
    public Action<bool> SetDirty { get; } = setDirty;
    public Action DeferAppPersistence { get; } = deferAppPersistence;
    public Func<IDisposable> EnterSettingsLoading { get; } = enterSettingsLoading;
    public Action ResetCad { get; } = resetCad;
    public Action ResetCadEditBaseline { get; } = resetCadEditBaseline;
    public Action ClearCadBuildCache { get; } = clearCadBuildCache;
    public Action ClearCadCatalog { get; } = clearCadCatalog;
    public Action PublishEmptyCadPads { get; } = publishEmptyCadPads;
    public Action DetachLayerToggles { get; } = detachLayerToggles;
    public Func<Func<Task>, Task> RunCadOverlayAsync { get; } = runCadOverlayAsync;
    public Func<Task> RebuildGridAsync { get; } = rebuildGridAsync;
}

internal sealed class ProjectCadPorts(
    Func<ProjectFile, string, ProjectOperationStatus, Task<(long DxfElapsedMs, DxfCombinedGroupReplayResult? CombinedGroupReplay)>> loadAsync)
{
    public Func<ProjectFile, string, ProjectOperationStatus, Task<(long DxfElapsedMs, DxfCombinedGroupReplayResult? CombinedGroupReplay)>>
        LoadAsync
    { get; } = loadAsync;
}

internal sealed class ProjectReplayPorts(
    Func<bool> isStep2Enabled,
    Func<string> getDisabledMessage,
    Func<IReadOnlyList<ProjectReplayAction>> buildPlan)
{
    public Func<bool> IsStep2Enabled { get; } = isStep2Enabled;
    public Func<string> GetDisabledMessage { get; } = getDisabledMessage;
    public Func<IReadOnlyList<ProjectReplayAction>> BuildPlan { get; } = buildPlan;
}

internal enum ProjectReplayStep
{
    Other,
    Step1Match,
    Step2Freeform
}

internal sealed class ProjectReplayAction(
    ProjectReplayStep step,
    Func<string> getStartedMessage,
    Func<string> getCompletedMessage,
    Func<string> getSkippedMessage,
    Func<Task<bool>> executeAsync)
{
    public ProjectReplayStep Step { get; } = step;
    public Func<string> GetStartedMessage { get; } = getStartedMessage;
    public Func<string> GetCompletedMessage { get; } = getCompletedMessage;
    public Func<string> GetSkippedMessage { get; } = getSkippedMessage;
    public Func<Task<bool>> ExecuteAsync { get; } = executeAsync;
}

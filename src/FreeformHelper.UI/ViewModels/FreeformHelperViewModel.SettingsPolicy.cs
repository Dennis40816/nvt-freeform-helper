// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Contracts.Settings;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels.Settings;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel : ISettingsEffectHost, ISettingsDraftApplyHost, ISettingsResetHost, ISettingsStepCallbackHost
{
    private readonly SettingsEffectCoordinator _settingsEffects;
    private readonly SettingsSnapshotMapper _settingsSnapshotMapper;
    private readonly SettingsStepCallbackAdapter _settingsStepCallbacks;

    private void ApplySettingsOrchestration(string changedPropertyName) =>
        _settingsEffects.ApplySettingsOrchestration(changedPropertyName);

    private void ApplySettingsWindowOrchestration(IReadOnlyCollection<string> changedPropertyNames) =>
        _settingsEffects.ApplySettingsWindowOrchestration(changedPropertyNames);

    private void ApplySettingsChangeEffects(SettingsChangeEffects effects, bool showStatus = false,
        bool invalidateNotchFinalProjection = false) =>
        _settingsEffects.ApplySettingsChangeEffects(effects, showStatus, invalidateNotchFinalProjection);

    private bool TryCommitCadOutputFwDiffIndexAnchorCadIdFromCurrentValue() => _settingsEffects.TryCommitAnchorFromCurrentValue();

    void ISettingsEffectHost.NotifyPropertyChanged(string propertyName) => OnPropertyChanged(propertyName);

    void ISettingsEffectHost.ApplyRegularSourceLayerAutoHidePolicy() => ApplyRegularSourceLayerAutoHidePolicy();

    void ISettingsEffectHost.RebuildCascadeSettings() => RebuildCascadeSettings();

    void ISettingsEffectHost.RefreshDxfRegularSourceHint() => RefreshDxfRegularSourceHint();

    void ISettingsEffectHost.UpdateCadOutputFwDiffIndexing() => UpdateCadOutputFwDiffIndexing(CadPads.ToList());

    void ISettingsEffectHost.NotifySimulationWorkspaceSourceChanged() => NotifySimulationWorkspaceSourceChanged();

    void ISettingsEffectHost.InvalidateNotchCompensationCache() => InvalidateNotchCompensationCache();

    void ISettingsEffectHost.ClearStep5ProjectedResultCore() => ClearStep5ProjectedResultCore();

    void ISettingsEffectHost.InvalidateDownstreamFromStep2(bool showStatus) => InvalidateDownstreamFromStep2(showStatus);

    void ISettingsEffectHost.InvalidateDownstreamFromStep3(bool showStatus) => InvalidateDownstreamFromStep3(showStatus);

    void ISettingsEffectHost.InvalidateDownstreamFromStep4(bool showStatus) => InvalidateDownstreamFromStep4(showStatus);

    void ISettingsEffectHost.NotifyNotchOverlayVisibilityChanged() => NotifyNotchOverlayVisibilityChanged();

    void ISettingsEffectHost.RefreshNotchCanvasPreviewAfterStep3SettingsChanged() => RefreshNotchCanvasPreviewAfterStep3SettingsChanged();

    void ISettingsEffectHost.MarkUnsaved() => MarkUnsaved();

    void ISettingsEffectHost.PersistAppGeneralSettingsNow() => PersistAppGeneralSettingsNow();

    Task ISettingsEffectHost.TriggerGridRebuildAsync(bool requestFit) => TriggerGridRebuildAsync(requestFit);

    bool ISettingsEffectHost.TryGetCadIcIndex(int cadId, out int icIndex) => _cadIcIndexByCadId.TryGetValue(cadId, out icIndex);

    IDisposable ISettingsEffectHost.EnterDxfIndexAnchorChange() => _settingsSuppression.EnterDxfIndexAnchorChange();

    IDisposable ISettingsEffectHost.EnterUndoSuppression() => _undoSuppression.Enter();

    int ISettingsEffectHost.ClampToInt(decimal value, int min, int max) => ClampToInt(value, min, max);

    void ISettingsEffectHost.SetStatus(string message) => SetStatus(message);

    void ISettingsEffectHost.ClearFwDiffAnchors()
    {
        _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc.Clear();
        _projectFile.CadOutputFwDiffIndexAnchorCadPadId = null;
    }

    void ISettingsEffectHost.SaveFwDiffAnchor(int icIndex, int cadId)
    {
        _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc[icIndex] = cadId;
        _projectFile.CadOutputFwDiffIndexAnchorCadPadId = null;
    }

    private void LoadSettingsToUi(ProjectSettings settings) => _settingsSnapshotMapper.LoadSettingsToUi(settings);

    private void ApplyUiToSettings(ProjectSettings settings) => ApplyUiToSettings(settings, markUnsaved: true);

    private void ApplyUiToSettings(ProjectSettings settings, bool markUnsaved) =>
        _settingsSnapshotMapper.ApplyUiToSettings(settings, markUnsaved);

    internal bool TryAcceptSettingsWindowDraft(object? draftProject) =>
        SettingsDraftApplyController.TryAcceptDraft(draftProject, _projectFile, this);

    internal void ApplySettingsWindowDraft(SettingsWindowViewModel draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!IsProjectEditingEnabled) return;
        SettingsDraftApplyController.Apply(new SettingsWindowDraftAdapter(this, draft), this);
    }

    internal void ResetAllSettingsToDefaults() => SettingsDraftApplyController.ResetAllSettingsToDefaults(this);

    IEnumerable<decimal> ISettingsDraftApplyHost.PerIcXChannels => CascadeIcSettings.Select(static row => row.XChannels);
    IEnumerable<decimal> ISettingsDraftApplyHost.PerIcYChannels => CascadeIcSettings.Select(static row => row.YChannels);
    IDisposable ISettingsDraftApplyHost.EnterSettingsLoading() => _settingsSuppression.EnterSettingsLoading();
    bool ISettingsDraftApplyHost.TryCommitAnchorFromCurrentValue() => TryCommitCadOutputFwDiffIndexAnchorCadIdFromCurrentValue();
    void ISettingsDraftApplyHost.ApplySettingsWindowOrchestration(IReadOnlyCollection<string> changedPropertyNames) =>
        ApplySettingsWindowOrchestration(changedPropertyNames);
    void ISettingsDraftApplyHost.SetStatus(string message) => SetStatus(message);

    bool ISettingsResetHost.SuppressAppPersistence { set => _appGeneralPersistence.SuppressPersistence = value; }
    void ISettingsResetHost.ReplaceSettingsAndSnapshot(ProjectSettings settings, ProjectUiSnapshot snapshot)
    {
        _projectFile.Settings = settings;
        _projectFile.UiSnapshot = snapshot;
        _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc.Clear();
        _projectFile.CadOutputFwDiffIndexAnchorCadPadId = null;
    }
    void ISettingsResetHost.LoadSettingsToUi(ProjectSettings settings) => LoadSettingsToUi(settings);
    void ISettingsResetHost.ApplyViewSnapshot(UiViewSnapshot snapshot) => ApplyViewSnapshot(snapshot);
    void ISettingsResetHost.ApplyImportSnapshot(UiImportSnapshot snapshot) => ApplyImportSnapshot(snapshot);
    void ISettingsResetHost.RebuildCascadeSettings() => RebuildCascadeSettings();
    void ISettingsResetHost.InvalidateNotchCompensationCache() => InvalidateNotchCompensationCache();
    void ISettingsResetHost.InvalidateNotchValidationCache() => InvalidateNotchValidationCache();
    void ISettingsResetHost.SetStatus(string message) => SetStatus(message);
    Task ISettingsResetHost.TriggerGridRebuildAsync(bool requestFit) => TriggerGridRebuildAsync(requestFit);
    void ISettingsResetHost.PersistAppGeneralSettingsNow() => PersistAppGeneralSettingsNow();

    bool ISettingsStepCallbackHost.IsLoadingSettings => _isLoadingSettings;
    IDisposable ISettingsStepCallbackHost.EnterUndoSuppression() => _undoSuppression.Enter();
    void ISettingsStepCallbackHost.NotifyPropertyChanged(string propertyName) => OnPropertyChanged(propertyName);
    void ISettingsStepCallbackHost.NotifyNotchModelSummaryChanged() => NotifyNotchModelSummaryChanged();
    void ISettingsStepCallbackHost.ApplySettingsOrchestration(string propertyName) => ApplySettingsOrchestration(propertyName);
    void ISettingsStepCallbackHost.SetNotchExportProfile(NotchExportProfile profile) => _projectFile.Settings.Notch.ExportProfile = profile;

    partial void OnFreeformAxisThresholdChanged(decimal value) => _settingsStepCallbacks.OnDetectionChanged(nameof(FreeformAxisThreshold));
    partial void OnEnableAutoDetectXyChanged(bool value) => _settingsStepCallbacks.OnDetectionChanged(nameof(EnableAutoDetectXy));
    partial void OnEnableFreeformEdgeSpecializationChanged(bool value) =>
        _settingsStepCallbacks.OnDetectionChanged(nameof(EnableFreeformEdgeSpecialization));
    partial void OnEnableToFullChanged(bool value) => _settingsStepCallbacks.OnEnableToFullChanged();
    partial void OnEnableBoundaryVirtualAreaCapChanged(bool value) => _settingsStepCallbacks.OnEnableBoundaryVirtualAreaCapChanged();
    partial void OnBoundaryVirtualAreaCapPercentChanged(decimal value) => _settingsStepCallbacks.OnBoundaryVirtualAreaCapPercentChanged(value);
    partial void OnEnableTargetCoverageGuardChanged(bool value) => _settingsStepCallbacks.OnEnableTargetCoverageGuardChanged();
    partial void OnTargetCoverageCapPercentChanged(decimal value) => _settingsStepCallbacks.OnTargetCoverageCapPercentChanged(value);
    partial void OnSelectedNotchExportFileTypeOptionChanged(NotchExportFileTypeOption value) =>
        _settingsStepCallbacks.OnSelectedNotchExportFileTypeOptionChanged();
    partial void OnSelectedNotchExportProfileOptionChanged(NotchExportProfileOption value) =>
        _settingsStepCallbacks.OnSelectedNotchExportProfileOptionChanged(value.Value);
}

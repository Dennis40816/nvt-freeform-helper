// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Contracts.Settings;

/// <summary>Existing workspace effects, executed synchronously in settings policy order.</summary>
internal interface ISettingsEffectHost
{
    bool IsPanelAlignment { get; }
    bool IsDxfLayerRegularSource { get; }
    decimal CadOutputFwDiffIndexAnchorCadId { get; set; }
    void NotifyPropertyChanged(string propertyName);
    void ApplyRegularSourceLayerAutoHidePolicy();
    void RebuildCascadeSettings();
    void RefreshDxfRegularSourceHint();
    void UpdateCadOutputFwDiffIndexing();
    void NotifySimulationWorkspaceSourceChanged();
    void InvalidateNotchCompensationCache();
    void ClearStep5ProjectedResultCore();
    void InvalidateDownstreamFromStep2(bool showStatus);
    void InvalidateDownstreamFromStep3(bool showStatus);
    void InvalidateDownstreamFromStep4(bool showStatus);
    void NotifyNotchOverlayVisibilityChanged();
    void RefreshNotchCanvasPreviewAfterStep3SettingsChanged();
    void MarkUnsaved();
    void PersistAppGeneralSettingsNow();
    Task TriggerGridRebuildAsync(bool requestFit);
    void ClearFwDiffAnchors();
    void SaveFwDiffAnchor(int icIndex, int cadId);
    bool TryGetCadIcIndex(int cadId, out int icIndex);
    IDisposable EnterDxfIndexAnchorChange();
    IDisposable EnterUndoSuppression();
    int ClampToInt(decimal value, int min, int max);
    void SetStatus(string message);
}

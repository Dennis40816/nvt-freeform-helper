// Copyright (c) 2026 Dennis Liu. All rights reserved.
namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Owns independent, nesting-safe suppression domains. UI-thread-only access.
/// Settings loading is a nesting counter held across the project load await;
/// the other domains suppress synchronous regions.
/// </summary>
internal sealed class SettingsSuppressionScopes
{
    private readonly UndoSuppression _cascadeRebuild = new();
    private readonly UndoSuppression _layerToggleChange = new();
    private readonly UndoSuppression _boundLayerSelectionChange = new();
    private readonly UndoSuppression _regularSourceLayerSelectionChange = new();
    private readonly UndoSuppression _scanOrderSelectionSync = new();
    private readonly UndoSuppression _gridAlignmentSelectionSync = new();
    private readonly UndoSuppression _regularSourceModeSelectionSync = new();
    private readonly UndoSuppression _cadOutputFwDiffAutoModeSelectionSync = new();
    private readonly UndoSuppression _dxfIndexAnchorChange = new();
    private readonly UndoSuppression _manualSizingApply = new();
    private readonly UndoSuppression _regularVisibilityMaskStateChange = new();
    private readonly UndoSuppression _scopeSync = new();
    private readonly UndoSuppression _notchThresholdSync = new();
    private readonly UndoSuppression _settingsLoading = new();

    public bool IsSettingsLoadingActive => _settingsLoading.IsActive;
    public UndoSuppression.Scope EnterSettingsLoading() => _settingsLoading.Enter();

    public bool IsCascadeRebuildActive => _cascadeRebuild.IsActive;
    public UndoSuppression.Scope EnterCascadeRebuild() => _cascadeRebuild.Enter();

    public bool IsLayerToggleChangeActive => _layerToggleChange.IsActive;
    public UndoSuppression.Scope EnterLayerToggleChange() => _layerToggleChange.Enter();

    public bool IsBoundLayerSelectionChangeActive => _boundLayerSelectionChange.IsActive;
    public UndoSuppression.Scope EnterBoundLayerSelectionChange() => _boundLayerSelectionChange.Enter();

    public bool IsRegularSourceLayerSelectionChangeActive => _regularSourceLayerSelectionChange.IsActive;
    public UndoSuppression.Scope EnterRegularSourceLayerSelectionChange() => _regularSourceLayerSelectionChange.Enter();

    public bool IsScanOrderSelectionSyncActive => _scanOrderSelectionSync.IsActive;
    public UndoSuppression.Scope EnterScanOrderSelectionSync() => _scanOrderSelectionSync.Enter();

    public bool IsGridAlignmentSelectionSyncActive => _gridAlignmentSelectionSync.IsActive;
    public UndoSuppression.Scope EnterGridAlignmentSelectionSync() => _gridAlignmentSelectionSync.Enter();

    public bool IsRegularSourceModeSelectionSyncActive => _regularSourceModeSelectionSync.IsActive;
    public UndoSuppression.Scope EnterRegularSourceModeSelectionSync() => _regularSourceModeSelectionSync.Enter();

    public bool IsCadOutputFwDiffAutoModeSelectionSyncActive => _cadOutputFwDiffAutoModeSelectionSync.IsActive;
    public UndoSuppression.Scope EnterCadOutputFwDiffAutoModeSelectionSync() => _cadOutputFwDiffAutoModeSelectionSync.Enter();

    public bool IsDxfIndexAnchorChangeActive => _dxfIndexAnchorChange.IsActive;
    public UndoSuppression.Scope EnterDxfIndexAnchorChange() => _dxfIndexAnchorChange.Enter();

    public bool IsManualSizingApplyActive => _manualSizingApply.IsActive;
    public UndoSuppression.Scope EnterManualSizingApply() => _manualSizingApply.Enter();

    public bool IsRegularVisibilityMaskStateChangeActive => _regularVisibilityMaskStateChange.IsActive;
    public UndoSuppression.Scope EnterRegularVisibilityMaskStateChange() => _regularVisibilityMaskStateChange.Enter();

    public bool IsScopeSyncActive => _scopeSync.IsActive;
    public UndoSuppression.Scope EnterScopeSync() => _scopeSync.Enter();

    public bool IsNotchThresholdSyncActive => _notchThresholdSync.IsActive;
    public UndoSuppression.Scope EnterNotchThresholdSync() => _notchThresholdSync.Enter();
}

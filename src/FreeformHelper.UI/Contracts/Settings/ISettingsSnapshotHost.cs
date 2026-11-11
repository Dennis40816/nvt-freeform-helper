// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.Contracts.Settings;

/// <summary>Live settings projections and existing load scopes; no snapshot or option storage.</summary>
internal interface ISettingsSnapshotHost : ISettingsGridValues, ISettingsMappingValues,
    ISettingsNotchValues, ISettingsGeneralValues, ISettingsAppearanceValues
{
    decimal PendingColumnWidth { get; set; }
    decimal PendingRowHeight { get; set; }
    bool HasUnsavedChanges { get; set; }
    string ManualRowsRange { get; set; }
    string ManualColsRange { get; set; }
    string ManualRangeStatus { get; set; }
    string ManualRangeError { get; set; }

    bool IsLoadingSettings { get; }
    int? LegacyFwDiffAnchor { get; }
    IEnumerable<int> FwDiffAnchorValues { get; }
    string? PendingBoundLayerName { set; }
    string? PendingRegularSourceLayerName { set; }
    string? SelectedBoundLayerName { get; }
    string? SelectedRegularSourceLayerName { get; }
    IEnumerable<decimal> PerIcXChannels { get; }
    IEnumerable<decimal> PerIcYChannels { get; }
    NotchCompensationModel SelectedCompensationModel { get; }
    NotchExportProfile SelectedExportProfile { get; }
    IDisposable EnterSettingsLoading();
    IDisposable EnterScopeSync();
    IDisposable EnterNotchThresholdSync();
    IDisposable EnterUndoSuppression();
    void NotifyPropertyChanged(string propertyName);
    void RebuildCascadeSettings();
    void MarkUnsaved();
    int ClampToInt(decimal value, int min, int max);
    decimal NormalizeLineOpacity(double value);
    void EnsureGridAlignmentOption();
    void EnsureRegularSourceModeOption();
    void SelectNotchCompensationModel(NotchCompensationModel value);
    void SelectNotchExportProfile(NotchExportProfile value);
    void SelectNotchExportFileType(string value);
    void SelectLogLevel(string value);
    void ApplyNotchCompensationModeToLegacySwitches(NotchCompensationModel mode, bool? currentGainEnableToRegular, bool? currentGainEnableToFull);
    void ApplyDxfLayerImageExportSnapshot(UiViewSnapshot snapshot);
    void ApplyRegularVisibilityMaskSnapshot(string? sourcePath, bool isEnabled, bool allowRuntimeRefresh, byte[]? embeddedMask, string? embeddedMaskName);
}

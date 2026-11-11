// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel : ISettingsHost, IFwAssignmentSettingsHost
{
    // The child is created once in the shell constructor and never replaced. Its constructor does not call back into the shell.
    private readonly SettingsStateViewModel _settingsState;

    private readonly FwAssignmentSettingsViewModel _fwAssignmentSettings;

    private SettingsStateViewModel SettingsState => _settingsState;

    private static decimal NormalizeLineOpacity(double value) => SettingsStateViewModel.NormalizeLineOpacity(value);

    void ISettingsHost.NotifyPropertyChanging(string propertyName) => OnPropertyChanging(propertyName);

    void ISettingsHost.NotifyPropertyChanged(string propertyName) => OnPropertyChanged(propertyName);

    void ISettingsHost.OnCoordinatePreferredAaOutlineLayerNameChanged(string value) => OnCoordinatePreferredAaOutlineLayerNameChanged(value);

    void ISettingsHost.OnMaxAreaBucketsChanging(decimal value) => OnMaxAreaBucketsChanging(value);

    void ISettingsHost.OnAreaBucketToleranceChanging(decimal value) => OnAreaBucketToleranceChanging(value);

    void ISettingsHost.OnIsHeightColumnLocalChanged(bool value) => OnIsHeightColumnLocalChanged(value);

    void ISettingsHost.OnIsWidthRowLocalChanged(bool value) => OnIsWidthRowLocalChanged(value);

    void ISettingsHost.OnUseLocalSizingChanged(bool value) => OnUseLocalSizingChanged(value);

    void ISettingsHost.OnRegularSourceModeChanged(RegularSourceMode value) => OnRegularSourceModeChanged(value);

    void ISettingsHost.OnGridAlignmentModeChanging(GridAlignmentMode value) => OnGridAlignmentModeChanging(value);

    void ISettingsHost.OnGridAlignmentModeChanged(GridAlignmentMode value) => OnGridAlignmentModeChanged(value);

    void ISettingsHost.OnSelectedScanOrderChanging(ScanOrder value) => OnSelectedScanOrderChanging(value);

    void ISettingsHost.OnSelectedScanOrderChanged(ScanOrder value) => OnSelectedScanOrderChanged(value);

    void ISettingsHost.OnSelectedRegularSourceLayerOptionChanged() =>
        OnSelectedRegularSourceLayerOptionChanged(SelectedRegularSourceLayerOption);

    void ISettingsHost.OnSelectedBoundLayerOptionChanged() => OnSelectedBoundLayerOptionChanged(SelectedBoundLayerOption);

    bool IFwAssignmentSettingsHost.IsLoadingSettings => _isLoadingSettings;

    SettingsSuppressionScopes IFwAssignmentSettingsHost.Suppression => _settingsSuppression;

    UndoSuppression IFwAssignmentSettingsHost.UndoSuppression => _undoSuppression;

    void IFwAssignmentSettingsHost.NotifyPropertyChanging(string propertyName) => OnPropertyChanging(propertyName);

    void IFwAssignmentSettingsHost.NotifyPropertyChanged(string propertyName) => OnPropertyChanged(propertyName);

    void IFwAssignmentSettingsHost.OnShowDiffIndexOverlayChanging(bool value) => OnShowDiffIndexOverlayChanging(value);

    void IFwAssignmentSettingsHost.OnCadOutputFwDiffIndexStartChanging(decimal value) => OnCadOutputFwDiffIndexStartChanging(value);

    void IFwAssignmentSettingsHost.OnCadOutputFwDiffAutoModeChanging(CadOutputFwDiffAutoMode value) => OnCadOutputFwDiffAutoModeChanging(value);

    void IFwAssignmentSettingsHost.OnCadOutputFwDiffIndexAnchorCadIdChanging(decimal value) => OnCadOutputFwDiffIndexAnchorCadIdChanging(value);

    void IFwAssignmentSettingsHost.NotifySelectedAutoModeChanged() => OnPropertyChanged(nameof(SelectedCadOutputFwDiffAutoModeOption));

    bool IFwAssignmentSettingsHost.TryCommitAnchorFromCurrentValue() => TryCommitCadOutputFwDiffIndexAnchorCadIdFromCurrentValue();

    void IFwAssignmentSettingsHost.ApplySettingsOrchestration(string changedPropertyName) => ApplySettingsOrchestration(changedPropertyName);

    /// <summary>
    /// Gets or sets a value indicating whether to display CAD Output FW Diff overlay hints on canvas.
    /// </summary>
    public bool ShowDiffIndexOverlay
    {
        get => _fwAssignmentSettings.ShowDiffIndexOverlay;
        set => _fwAssignmentSettings.ShowDiffIndexOverlay = value;
    }

    partial void OnShowDiffIndexOverlayChanging(bool value);

    /// <summary>
    /// Gets or sets the summary text for current CAD Output FW Diff assignment policy.
    /// </summary>
    public string CadOutputFwDiffAssignmentSummary
    {
        get => _fwAssignmentSettings.CadOutputFwDiffAssignmentSummary;
        set => _fwAssignmentSettings.CadOutputFwDiffAssignmentSummary = value;
    }

    /// <summary>
    /// Gets or sets the CAD id used as diff idx start anchor.
    /// Use -1 to indicate "first CAD in scan order".
    /// </summary>
    public decimal CadOutputFwDiffIndexAnchorCadId
    {
        get => _fwAssignmentSettings.CadOutputFwDiffIndexAnchorCadId;
        set => _fwAssignmentSettings.CadOutputFwDiffIndexAnchorCadId = value;
    }

    partial void OnCadOutputFwDiffIndexAnchorCadIdChanging(decimal value);

    /// <summary>
    /// Gets or sets the CAD auto CAD Output FW Diff assignment mode.
    /// </summary>
    public CadOutputFwDiffAutoMode CadOutputFwDiffAutoMode
    {
        get => _fwAssignmentSettings.CadOutputFwDiffAutoMode;
        set => _fwAssignmentSettings.CadOutputFwDiffAutoMode = value;
    }

    partial void OnCadOutputFwDiffAutoModeChanging(CadOutputFwDiffAutoMode value);

    /// <summary>
    /// Gets or sets the starting expected diff idx value for CAD scan-order indexing.
    /// </summary>
    public decimal CadOutputFwDiffIndexStart
    {
        get => _fwAssignmentSettings.CadOutputFwDiffIndexStart;
        set => _fwAssignmentSettings.CadOutputFwDiffIndexStart = value;
    }

    partial void OnCadOutputFwDiffIndexStartChanging(decimal value);
}

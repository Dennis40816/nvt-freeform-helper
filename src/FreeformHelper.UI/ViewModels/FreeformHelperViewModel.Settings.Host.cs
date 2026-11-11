// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel : ISettingsHost
{
    // The child is created once in the shell constructor and never replaced. Its constructor does not call back into the shell.
    private readonly SettingsStateViewModel _settingsState;

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

    void ISettingsHost.OnSelectedRegularSourceLayerOptionChanged(RegularSourceLayerOption value) =>
        OnSelectedRegularSourceLayerOptionChanged(value);

    void ISettingsHost.OnSelectedBoundLayerOptionChanged(BoundLayerOption value) => OnSelectedBoundLayerOptionChanged(value);
}

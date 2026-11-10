// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using BoundLayerOption = FreeformHelper.UI.ViewModels.FreeformHelperViewModel.BoundLayerOption;
using RegularSourceLayerOption = FreeformHelper.UI.ViewModels.FreeformHelperViewModel.RegularSourceLayerOption;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Runs shell notifications and existing setting hooks synchronously at the caller's transition point.</summary>
internal interface ISettingsHost
{
    void NotifyPropertyChanging(string propertyName);

    void NotifyPropertyChanged(string propertyName);

    // Compatibility boundary: these hooks retain the shell's Undo, suppression and workflow policy.
    void OnCoordinatePreferredAaOutlineLayerNameChanged(string value);

    void OnMaxAreaBucketsChanging(decimal value);

    void OnAreaBucketToleranceChanging(decimal value);

    void OnIsHeightColumnLocalChanged(bool value);

    void OnIsWidthRowLocalChanged(bool value);

    void OnUseLocalSizingChanged(bool value);

    void OnRegularSourceModeChanged(RegularSourceMode value);

    void OnGridAlignmentModeChanging(GridAlignmentMode value);

    void OnGridAlignmentModeChanged(GridAlignmentMode value);

    void OnSelectedScanOrderChanging(ScanOrder value);

    void OnSelectedScanOrderChanged(ScanOrder value);

    // Compatibility leak: the existing layer option type is nested in the shell.
    void OnSelectedRegularSourceLayerOptionChanged(RegularSourceLayerOption value);

    // Compatibility leak: the existing layer option type is nested in the shell.
    void OnSelectedBoundLayerOptionChanged(BoundLayerOption value);
}

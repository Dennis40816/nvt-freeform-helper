// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.Contracts.Settings;
using static FreeformHelper.UI.ViewModels.FreeformHelperViewModel;

namespace FreeformHelper.UI.ViewModels.Settings;

/// <summary>Live values and current presentation catalogs read by one settings window Apply.</summary>
internal interface ISettingsWindowDraftHost : ISettingsGridValues, ISettingsMappingValues,
    ISettingsNotchValues, ISettingsGeneralValues
{
    ScanOrderOption SelectedScanOrderOption { get; set; }
    GridAlignmentOption SelectedGridAlignmentOption { get; set; }
    RegularSourceModeOption SelectedRegularSourceModeOption { get; set; }
    RegularSourceLayerOption SelectedRegularSourceLayerOption { get; set; }
    LogLevelOption SelectedLogLevelOption { get; set; }
    CadOutputFwDiffAutoModeOption SelectedCadOutputFwDiffAutoModeOption { get; set; }
    NotchCompensationModelOption SelectedNotchCompensationModelOption { get; set; }
    NotchExportFileTypeOption SelectedNotchExportFileTypeOption { get; set; }
    NotchExportProfileOption SelectedNotchExportProfileOption { get; set; }
    bool ApplyAppVisualPreferencesOnProjectLoad { get; set; }

    IReadOnlyList<ScanOrderOption> ScanOrderOptions { get; }
    IReadOnlyList<GridAlignmentOption> GridAlignmentOptions { get; }
    IReadOnlyList<RegularSourceModeOption> RegularSourceModeOptions { get; }
    IReadOnlyList<RegularSourceLayerOption> RegularSourceLayerOptions { get; }
    IReadOnlyList<LogLevelOption> LogLevelOptions { get; }
    IReadOnlyList<CadOutputFwDiffAutoModeOption> CadOutputFwDiffAutoModeOptions { get; }
    IReadOnlyList<NotchExportFileTypeOption> NotchExportFileTypeOptions { get; }
    IDisposable EnterSettingsLoading();
    void ReplacePerIcChannels(IReadOnlyList<int> xChannels, IReadOnlyList<int> yChannels);
}

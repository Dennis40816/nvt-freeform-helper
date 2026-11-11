// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.Services;

/// <summary>Provides the existing app settings I/O and workspace snapshot entry points.</summary>
internal interface IAppGeneralPersistenceHost
{
    bool IsLoadingSettings { get; }

    bool ApplyAppVisualPreferencesOnProjectLoad { get; set; }

    IDisposable EnterSettingsLoading();

    AppGeneralSettingsDocument? LoadAppGeneralSettings();

    void SaveAppGeneralSettings(AppGeneralSettingsDocument document);

    ProjectUiSnapshot BuildUiSnapshot();

    void ReplaceProjectUiSnapshot(ProjectUiSnapshot snapshot);

    void ApplyViewSnapshot(UiViewSnapshot snapshot);

    void ReportAppGeneralSettingsLoaded();

    void ReportAppGeneralSettingsPersisted();

    void ReportAppGeneralPersistenceFailure(Exception exception);

    void ReportAppVisualPreferencesApplied();
}

// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.Contracts.Settings;

/// <summary>Reset writes to the current project and reuses its existing load and persistence paths.</summary>
internal interface ISettingsResetHost
{
    bool SuppressAppPersistence { set; }
    bool ApplyAppVisualPreferencesOnProjectLoad { get; set; }
    bool HasUnsavedChanges { get; set; }
    void ReplaceSettingsAndSnapshot(ProjectSettings settings, ProjectUiSnapshot snapshot);
    void LoadSettingsToUi(ProjectSettings settings);
    void ApplyViewSnapshot(UiViewSnapshot snapshot);
    void ApplyImportSnapshot(UiImportSnapshot snapshot);
    void RebuildCascadeSettings();
    void InvalidateNotchCompensationCache();
    void InvalidateNotchValidationCache();
    void SetStatus(string message);
    Task TriggerGridRebuildAsync(bool requestFit);
    void PersistAppGeneralSettingsNow();
}

// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Contracts.Settings;

namespace FreeformHelper.UI.Services;

/// <summary>Applies one edited-field set under the existing scope, then dispatches one effect plan.</summary>
internal static class SettingsDraftApplyController
{
    internal static bool TryAcceptDraft(object? draftProject, object currentProject, ISettingsDraftApplyHost host)
    {
        if (draftProject is null || ReferenceEquals(draftProject, currentProject)) return true;

        host.SetStatus("Settings not saved: another project was loaded after this window opened. Reopen Settings to edit the current project.");
        return false;
    }

    internal static void Apply(ISettingsDraftApplyOperation draft, ISettingsDraftApplyHost host)
    {
        var hasCascadeDetailChanges = !CascadeIcLayoutService.HasSameChannels(
            host.PerIcXChannels.Select(static value => Math.Max(1, (int)Math.Round(value))),
            host.PerIcYChannels.Select(static value => Math.Max(1, (int)Math.Round(value))),
            draft.PerIcXChannels, draft.PerIcYChannels);
        var changedProperties = draft.CollectChanges(hasCascadeDetailChanges);
        if (changedProperties.Count == 0) return;

        using (host.EnterSettingsLoading())
        {
            draft.ApplyValues(changedProperties);
            draft.ApplyNotchValues(changedProperties);
        }
        if (changedProperties.Contains("SelectedLogLevelOption")) draft.ApplyLogLevel();
        if (changedProperties.Contains("CadOutputFwDiffIndexAnchorCadId") && !host.TryCommitAnchorFromCurrentValue())
        {
            changedProperties.Remove("CadOutputFwDiffIndexAnchorCadId");
        }

        host.ApplySettingsWindowOrchestration(changedProperties);
    }

    internal static void ResetAllSettingsToDefaults(ISettingsResetHost host)
    {
        var defaultsSettings = new ProjectSettings();
        var defaultsUi = new ProjectUiSnapshot();

        host.ReplaceSettingsAndSnapshot(defaultsSettings, defaultsUi);

        host.SuppressAppPersistence = true;
        try
        {
            host.ApplyAppVisualPreferencesOnProjectLoad = true;
            host.LoadSettingsToUi(defaultsSettings);
            host.ApplyViewSnapshot(defaultsUi.View);
            host.ApplyImportSnapshot(defaultsUi.Import);
            host.RebuildCascadeSettings();
        }
        finally
        {
            host.SuppressAppPersistence = false;
        }

        host.InvalidateNotchCompensationCache();
        host.InvalidateNotchValidationCache();
        host.HasUnsavedChanges = true;
        host.SetStatus("Settings reset to defaults.");
        _ = host.TriggerGridRebuildAsync(requestFit: true);

        host.PersistAppGeneralSettingsNow();
    }
}

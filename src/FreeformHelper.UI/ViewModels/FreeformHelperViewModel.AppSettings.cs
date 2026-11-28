using System.Globalization;
using Avalonia.Threading;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel : IAppGeneralPersistenceHost
{
    private readonly AppGeneralSettingsStore _appGeneralSettingsStore;
    // UI thread only after construction. Project loading and the coordinator share this same holder.
    private readonly AppGeneralPersistenceState _appGeneralPersistence = new();
    private readonly AppGeneralPersistenceCoordinator _appGeneralPersistenceCoordinator;

    /// <summary>Gets the app general settings persistence holder for tests.</summary>
    internal AppGeneralPersistenceState AppGeneralPersistenceForTests => _appGeneralPersistence;

    private void InitializeAppGeneralSettingsPersistence() => _appGeneralPersistenceCoordinator.InitializeAppGeneralSettingsPersistence();

    private void SchedulePersistAppGeneralSettings() => _appGeneralPersistenceCoordinator.SchedulePersistAppGeneralSettings();

    internal void PersistAppGeneralSettingsNow(bool forceWhenDeferred = false) =>
        _appGeneralPersistenceCoordinator.PersistAppGeneralSettingsNow(forceWhenDeferred);

    private void MarkProjectLoadedForAppGeneralPersistence() => _appGeneralPersistenceCoordinator.MarkProjectLoadedForAppGeneralPersistence();

    private void FlushDeferredAppGeneralSettingsIfNeeded() => _appGeneralPersistenceCoordinator.FlushDeferredAppGeneralSettingsIfNeeded();

    private void TryApplyAppGeneralSettingsLayer() => _appGeneralPersistenceCoordinator.TryApplyAppGeneralSettingsLayer();

    private void TryApplyAppGeneralVisualPreferencesAfterProjectLoad() =>
        _appGeneralPersistenceCoordinator.TryApplyAppGeneralVisualPreferencesAfterProjectLoad();

    bool IAppGeneralPersistenceHost.IsLoadingSettings => _isLoadingSettings;

    bool IAppGeneralPersistenceHost.ApplyAppVisualPreferencesOnProjectLoad
    {
        get => ApplyAppVisualPreferencesOnProjectLoad;
        set => ApplyAppVisualPreferencesOnProjectLoad = value;
    }

    IDisposable IAppGeneralPersistenceHost.EnterSettingsLoading() => _settingsSuppression.EnterSettingsLoading();

    AppGeneralSettingsDocument? IAppGeneralPersistenceHost.LoadAppGeneralSettings() => _appGeneralSettingsStore.TryLoad();

    void IAppGeneralPersistenceHost.SaveAppGeneralSettings(AppGeneralSettingsDocument document) => _appGeneralSettingsStore.Save(document);

    ProjectUiSnapshot IAppGeneralPersistenceHost.BuildUiSnapshot() => BuildUiSnapshot();

    void IAppGeneralPersistenceHost.ReplaceProjectUiSnapshot(ProjectUiSnapshot snapshot) => _projectFile.UiSnapshot = snapshot;

    void IAppGeneralPersistenceHost.ApplyViewSnapshot(UiViewSnapshot snapshot) => ApplyViewSnapshot(snapshot);

    void IAppGeneralPersistenceHost.ReportAppGeneralSettingsLoaded() =>
        _logger.Info(CultureInfo.InvariantCulture, "App general settings loaded: {0}", _appGeneralSettingsStore.SettingsPath);

    void IAppGeneralPersistenceHost.ReportAppGeneralSettingsPersisted() =>
        _logger.Debug(CultureInfo.InvariantCulture, "App general settings persisted: {0}", _appGeneralSettingsStore.SettingsPath);

    void IAppGeneralPersistenceHost.ReportAppGeneralPersistenceFailure(Exception exception) =>
        _logger.Warn(exception, "Persist app general settings failed.");

    void IAppGeneralPersistenceHost.ReportAppVisualPreferencesApplied() =>
        _logger.Info(CultureInfo.InvariantCulture, "Load project: app visual preferences layer applied.");

    /// <summary>
    /// Holds the debounce timer, the suppression and deferral flags and the last loaded or saved snapshots of the
    /// app general settings persistence, for the main view model.
    /// </summary>
    internal sealed class AppGeneralPersistenceState : IAppGeneralPersistenceState
    {
        /// <summary>Gets or sets the debounce timer that writes the app general settings. It is created on first use on the UI thread.</summary>
        internal DispatcherTimer? PersistTimer { get; set; }

        /// <summary>Gets or sets a value indicating whether writes wait for the next successful project save after a project load.</summary>
        internal bool DeferAfterProjectLoad { get; set; }

        /// <summary>Gets or sets a value indicating whether changes are not persisted, for example while settings are applied in bulk.</summary>
        internal bool SuppressPersistence { get; set; }

        /// <summary>Gets or sets a value indicating whether a change waited for the next successful project save.</summary>
        internal bool HasDeferredChanges { get; set; }

        /// <summary>Gets or sets the view snapshot of the last loaded or saved app general settings.</summary>
        internal UiViewSnapshot ViewSnapshot { get; set; } = new();

        /// <summary>Gets or sets the import snapshot of the last loaded or saved app general settings.</summary>
        internal UiImportSnapshot ImportSnapshot { get; set; } = new();

        DispatcherTimer? IAppGeneralPersistenceState.PersistTimer
        {
            get => PersistTimer;
            set => PersistTimer = value;
        }

        bool IAppGeneralPersistenceState.DeferAfterProjectLoad
        {
            get => DeferAfterProjectLoad;
            set => DeferAfterProjectLoad = value;
        }

        bool IAppGeneralPersistenceState.SuppressPersistence
        {
            get => SuppressPersistence;
            set => SuppressPersistence = value;
        }

        bool IAppGeneralPersistenceState.HasDeferredChanges
        {
            get => HasDeferredChanges;
            set => HasDeferredChanges = value;
        }

        UiViewSnapshot IAppGeneralPersistenceState.ViewSnapshot
        {
            get => ViewSnapshot;
            set => ViewSnapshot = value;
        }

        UiImportSnapshot IAppGeneralPersistenceState.ImportSnapshot
        {
            get => ImportSnapshot;
            set => ImportSnapshot = value;
        }
    }
}

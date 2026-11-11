using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.CadEditing;

namespace FreeformHelper.UI.CadImport;

internal interface ICadImportWorkflow
{
    Task OpenAsync(Func<Task<string?>>? pickPath);
    Task ApplyOutcomeAsync(CadLoadOutcome outcome, UiViewSnapshot? snapshot);
    void ClearLayerCatalog();
    Task LoadCatalogFromPathAsync(string path);
    Task LoadCatalogFromEmbeddedAsync(byte[]? data);
}

internal sealed record CadImportEffects(
    Func<bool> CanProceed, Func<bool> IsLoading, Action<bool> ResetMask,
    Func<CadEditNotificationSnapshot> CaptureNotifications,
    Action<CadEditNotificationSnapshot> SyncHidden, Action<CadEditNotificationSnapshot> SyncCombined,
    Action<CadEditNotificationSnapshot> SyncDuplicates,
    Action<string> SetProjectPath, Action ClearSavedPath, Action MarkUnsaved,
    Action<string> SetStatus, Action<string, Exception> SetError,
    Func<Task> Rebuild, Func<Func<Task>, Task> WithOverlay,
    Func<string, Func<Task>, Action<Exception>, Task> RunOperation);

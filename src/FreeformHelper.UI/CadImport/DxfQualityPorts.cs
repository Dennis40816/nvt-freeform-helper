using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.CadImport;

internal interface IDxfQualityWorkflow
{
    Task CheckAsync();
    void SelectOverlapPads();
    void ClearHighlights();
    Task ShowDetailsAsync();
}

internal interface IDxfQualityPresentation
{
    DxfOverlapResult DxfOverlapResult { get; set; }
    bool IsCheckingDxfOverlap { get; set; }
    double DxfOverlapProgress { get; set; }
    bool HasDetailsHandler { get; }
    Task OpenDetailsAsync(string summary, IReadOnlyList<string> issues);
}

internal delegate Task RunCadQualityOperation(string operationName, Action<bool> setBusy, Action<double> setProgress,
    Func<Action<double>, Task> operationAsync, Action<Exception> onError, Action onStart);

internal sealed record DxfQualityEffects(
    Func<IReadOnlyList<CadPad>> VisiblePads,
    Action<string> SetStatus, Action<string, Exception> SetError,
    Action<IReadOnlyList<int>> SelectPads, Action ClearSelection, RunCadQualityOperation RunProgress);

using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Centralizes status updates for the UI.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    private UiOperationStatusReporter.Scope CreateStatusScope(string operationName)
    {
        return _statusReporter.CreateScope(operationName);
    }

    private void SetStatus(string message)
    {
        _statusReporter.Set(message);
    }

    private void SetStatusError(string prefix, Exception ex)
    {
        _statusReporter.SetError(prefix, ex);
    }
}

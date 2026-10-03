namespace FreeformHelper.UI.Services;

internal sealed class UiOperationStatusReporter
{
    private readonly Action<string> _setStatusText;

    public UiOperationStatusReporter(Action<string> setStatusText)
    {
        ArgumentNullException.ThrowIfNull(setStatusText);
        _setStatusText = setStatusText;
    }

    public Scope CreateScope(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        return new Scope(_setStatusText, operationName);
    }

    public void Set(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _setStatusText(message);
    }

    public void SetError(string prefix, Exception ex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentNullException.ThrowIfNull(ex);
        _setStatusText($"{prefix}: {ex.Message}");
    }

    internal readonly record struct Scope(Action<string> SetStatusText, string OperationName)
    {
        public void ReportStatus(string message) => Set(message);

        public void ReportProgress(string message) => Set(message);

        public void ReportSuccess(string message) => Set(message);

        public void ReportBlocked(string message) => Set(message);

        public void ReportWarning(string message) => Set(message);

        public void ReportError(string prefix, Exception ex)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
            ArgumentNullException.ThrowIfNull(ex);
            SetStatusText($"{prefix}: {ex.Message}");
        }

        private void Set(string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            SetStatusText(message);
        }
    }
}

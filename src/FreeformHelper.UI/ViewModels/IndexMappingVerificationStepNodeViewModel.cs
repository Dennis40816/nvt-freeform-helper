using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class IndexMappingVerificationStepNodeViewModel : ObservableObject
{
    public IndexMappingVerificationStepNodeViewModel(
        IndexMappingVerificationStep step,
        string title,
        string verifyHint,
        string keyChecks,
        string statusText,
        string openActionText)
    {
        Step = step;
        Title = title;
        VerifyHint = verifyHint;
        KeyChecks = keyChecks;
        StatusText = statusText;
        OpenActionText = openActionText;
    }

    public IndexMappingVerificationStep Step { get; }
    public string Title { get; }
    public string VerifyHint { get; }
    public string KeyChecks { get; }
    public string StatusText { get; }
    public string OpenActionText { get; }
    public bool IsPass => string.Equals(StatusText, "pass", StringComparison.OrdinalIgnoreCase);
    public bool IsWarning => string.Equals(StatusText, "warning", StringComparison.OrdinalIgnoreCase);
    public bool IsBlocked => string.Equals(StatusText, "blocked", StringComparison.OrdinalIgnoreCase);
    public bool IsPending => string.Equals(StatusText, "pending", StringComparison.OrdinalIgnoreCase);
    public bool HasConnector => !IsLast;

    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isLast;
}

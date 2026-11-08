using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

public sealed class DxfOverlapReportViewModel : ObservableObject
{
    // UI thread only.
    private bool _hasCopyResult;

    public DxfOverlapReportViewModel(string summary, IReadOnlyList<string> issues)
    {
        Summary = summary;
        Issues = issues;
        ReportText = issues.Count == 0
            ? "No overlap issues."
            : string.Join(System.Environment.NewLine, issues);
    }

    public string Summary { get; }
    public IReadOnlyList<string> Issues { get; }
    public bool HasIssues => Issues.Count > 0;
    public bool HasNoIssues => !HasIssues;
    public int IssueCount => Issues.Count;
    public string ReportText { get; }
    public string IssueTitle => HasIssues ? $"Issues ({IssueCount})" : "No issues";
    public bool HasCopyResult => _hasCopyResult;
    public string CopyResultText => HasCopyResult ? "Copied report text to clipboard." : string.Empty;

    internal void SetCopyCompleted(bool completed)
    {
        if (SetProperty(ref _hasCopyResult, completed, nameof(HasCopyResult)))
        {
            OnPropertyChanged(nameof(CopyResultText));
        }
    }
}

namespace FreeformHelper.UI.ViewModels;

public sealed class DxfOverlapReportViewModel
{
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
    public string IssueTitle => HasIssues ? $"Issues ({IssueCount})" : "Issues";
}

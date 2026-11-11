using System.Collections.ObjectModel;
using FreeformHelper.UI.CadImport;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public DxfOverlapResult DxfOverlapResult { get => Quality.DxfOverlapResult; set => Quality.DxfOverlapResult = value; }
    public string DxfOverlapSummary => Quality.DxfOverlapSummary;
    public ObservableCollection<string> DxfOverlapIssues => Quality.DxfOverlapIssues;
    public int DxfOverlapIssueCount => Quality.DxfOverlapIssueCount;
    public bool HasDxfOverlapIssues => Quality.HasDxfOverlapIssues;
    public bool IsCheckingDxfOverlap { get => Quality.IsCheckingDxfOverlap; set => Quality.IsCheckingDxfOverlap = value; }
    public double DxfOverlapProgress { get => Quality.DxfOverlapProgress; set => Quality.DxfOverlapProgress = value; }
    public ObservableCollection<int> DxfOverlapHighlightedCadIds => Quality.DxfOverlapHighlightedCadIds;
}

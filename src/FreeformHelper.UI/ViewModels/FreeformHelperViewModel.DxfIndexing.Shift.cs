namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public bool CanOffsetSelectedCadOutputFwDiffIndices => IndexMapping.Assignment.CanOffsetSelectedCadOutputFwDiffIndices;
    public string Step4DiffShiftSelectionSummary => IndexMapping.Assignment.Step4DiffShiftSelectionSummary;

    private void OffsetSelectedCadOutputFwDiffIndices() => IndexMapping.Assignment.OffsetSelectedCadOutputFwDiffIndices();
}

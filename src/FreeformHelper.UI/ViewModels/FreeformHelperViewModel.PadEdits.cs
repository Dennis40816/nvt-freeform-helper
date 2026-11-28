using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels.Sizing;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public void SetCadPadCustomValues(IReadOnlyList<int> cadIds, decimal value) =>
        ManualSizing.SetCadPadCustomValues(cadIds, value);

    public void ApplySizingToCells(IReadOnlyList<int> rows, IReadOnlyList<int> cols, decimal? widthMm, decimal? heightMm) =>
        ManualSizing.ApplySizingToCells(rows, cols, widthMm, heightMm);

    public void ResetSizingForCells(IReadOnlyList<int> rows, IReadOnlyList<int> cols) =>
        ManualSizing.ResetSizingForCells(rows, cols);

    private SizingMutationOutcome ApplySizingMutation(
        IReadOnlyList<int> rows, IReadOnlyList<int> cols, decimal? widthMm, decimal? heightMm)
    {
        var result = PadEditUseCase.ApplySizing(
            _projectFile.Settings.Grid, GridAlignmentMode, BuildCadPadSetForBounds(), rows, cols, widthMm, heightMm);
        return new SizingMutationOutcome(result.Success, result.Error,
            () => ManualSizingService.RestoreSnapshot(_projectFile.Settings.Grid, result.Snapshot));
    }

    private SizingMutationOutcome ResetSizingMutation(IReadOnlyList<int> rows, IReadOnlyList<int> cols)
    {
        var result = PadEditUseCase.ResetSizing(
            _projectFile.Settings.Grid, GridAlignmentMode, BuildCadPadSetForBounds(), rows, cols);
        return new SizingMutationOutcome(result.Success, result.Error,
            () => ManualSizingService.RestoreSnapshot(_projectFile.Settings.Grid, result.Snapshot));
    }
}

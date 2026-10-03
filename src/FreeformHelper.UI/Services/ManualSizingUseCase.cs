using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Evaluates manual sizing input and returns the action plan for the ViewModel.
/// </summary>
public sealed class ManualSizingUseCase
{
    private readonly ManualSizingService _manualSizingService;

    public ManualSizingUseCase(ManualSizingService manualSizingService)
    {
        _manualSizingService = manualSizingService ?? throw new ArgumentNullException(nameof(manualSizingService));
    }

    public ManualSizingPlan Evaluate(
        RegularGrid grid,
        string manualRowsRange,
        string manualColsRange,
        decimal pendingWidth,
        decimal pendingHeight)
    {
        if (!TryParseRowRange(grid, manualRowsRange, out var rows, out var rowError))
        {
            return ManualSizingPlan.Invalid(rowError);
        }

        if (!TryParseColRange(grid, manualColsRange, out var cols, out var colError))
        {
            return ManualSizingPlan.Invalid(colError);
        }

        var width = (double)pendingWidth;
        var height = (double)pendingHeight;

        if (width <= 0 && height <= 0)
        {
            return ManualSizingPlan.SyncSelection(rows, cols);
        }

        var widthValue = pendingWidth > 0 ? (decimal?)pendingWidth : null;
        var heightValue = pendingHeight > 0 ? (decimal?)pendingHeight : null;
        return ManualSizingPlan.ApplySizing(rows, cols, widthValue, heightValue);
    }

    private bool TryParseRowRange(RegularGrid grid, string input, out List<int> rows, out string error)
    {
        return _manualSizingService.TryParseRowRange(input, grid.Rows, out rows, out error);
    }

    private bool TryParseColRange(RegularGrid grid, string input, out List<int> cols, out string error)
    {
        return _manualSizingService.TryParseColRange(input, grid.Cols, out cols, out error);
    }
}

public enum ManualSizingAction
{
    None,
    SyncSelection,
    ApplySizing,
}

public sealed record ManualSizingPlan(
    ManualSizingAction Action,
    IReadOnlyList<int> Rows,
    IReadOnlyList<int> Cols,
    decimal? Width,
    decimal? Height,
    string ErrorMessage)
{
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public static ManualSizingPlan Invalid(string errorMessage)
    {
        return new ManualSizingPlan(
            ManualSizingAction.None,
            Array.Empty<int>(),
            Array.Empty<int>(),
            null,
            null,
            errorMessage);
    }

    public static ManualSizingPlan SyncSelection(IReadOnlyList<int> rows, IReadOnlyList<int> cols)
    {
        return new ManualSizingPlan(
            ManualSizingAction.SyncSelection,
            rows,
            cols,
            null,
            null,
            string.Empty);
    }

    public static ManualSizingPlan ApplySizing(
        IReadOnlyList<int> rows,
        IReadOnlyList<int> cols,
        decimal? width,
        decimal? height)
    {
        return new ManualSizingPlan(
            ManualSizingAction.ApplySizing,
            rows,
            cols,
            width,
            height,
            string.Empty);
    }
}

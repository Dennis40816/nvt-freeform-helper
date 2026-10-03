using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ManualSizingUseCaseTests
{
    [Fact]
    public void Evaluate_ReturnsInvalid_WhenRangeInputIsInvalid()
    {
        var useCase = new ManualSizingUseCase(new ManualSizingService());
        var grid = BuildGrid(rows: 4, cols: 4);

        var plan = useCase.Evaluate(
            grid,
            manualRowsRange: "1-a",
            manualColsRange: "0-1",
            pendingWidth: 0m,
            pendingHeight: 0m);

        Assert.Equal(ManualSizingAction.None, plan.Action);
        Assert.True(plan.HasError);
        Assert.Contains("Invalid numbers in range", plan.ErrorMessage);
    }

    [Fact]
    public void Evaluate_ReturnsSyncSelection_WhenNoPositiveTargetSize()
    {
        var useCase = new ManualSizingUseCase(new ManualSizingService());
        var grid = BuildGrid(rows: 4, cols: 4);

        var plan = useCase.Evaluate(
            grid,
            manualRowsRange: "0-1",
            manualColsRange: "2",
            pendingWidth: 0m,
            pendingHeight: 0m);

        Assert.Equal(ManualSizingAction.SyncSelection, plan.Action);
        Assert.False(plan.HasError);
        Assert.NotEmpty(plan.Rows);
        Assert.NotEmpty(plan.Cols);
        Assert.Null(plan.Width);
        Assert.Null(plan.Height);
    }

    [Fact]
    public void Evaluate_ReturnsApplySizing_WhenWidthOrHeightIsPositive()
    {
        var useCase = new ManualSizingUseCase(new ManualSizingService());
        var grid = BuildGrid(rows: 4, cols: 4);

        var plan = useCase.Evaluate(
            grid,
            manualRowsRange: "0",
            manualColsRange: "0-1",
            pendingWidth: 9.5m,
            pendingHeight: 0m);

        Assert.Equal(ManualSizingAction.ApplySizing, plan.Action);
        Assert.False(plan.HasError);
        Assert.Equal(9.5m, plan.Width);
        Assert.Null(plan.Height);
    }

    private static FreeformHelper.Domain.Pads.RegularGrid BuildGrid(int rows, int cols)
    {
        var settings = new GridSettings
        {
            XChannels = cols,
            YChannels = rows,
            ActiveAreaWidth = cols,
            ActiveAreaHeight = rows,
            BoundsPaddingRatio = 0,
        };

        return new RegularGridBuilder().BuildFromSettings(settings);
    }
}

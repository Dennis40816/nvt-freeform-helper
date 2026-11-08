// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportColumnFilterReopenBehaviorTests
{
    [Fact]
    public void ReopenFilterOfALargeColumn_AfterFilteringToOneValue_KeepsTheRestrictionOnOk()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModelWithDiffRows(48);
        Assert.True(viewModel.TryBuildColumnFilterDialog("diff", out var firstDialog));
        var first = Assert.IsType<NotchExportColumnFilterDialogViewModel>(firstDialog);
        viewModel.ApplyColumnFilterSelection(first.Field, ["21"], first.GetAllKeys());
        Assert.Equal(21, Assert.Single(viewModel.Rows).Row.DiffIndex);

        Assert.True(viewModel.TryBuildColumnFilterDialog("diff", out var reopenedDialog));
        var reopened = Assert.IsType<NotchExportColumnFilterDialogViewModel>(reopenedDialog);

        Assert.False(reopened.UseRangeFilter);
        Assert.Equal("21", Assert.Single(reopened.GetSelectedKeys()));
        Assert.Equal("1/48 selected", reopened.SummaryText);

        viewModel.ApplyColumnFilterSelection(reopened.Field, reopened.GetSelectedKeys(), reopened.GetAllKeys());

        Assert.True(viewModel.IsDiffColumnFilterActive);
        Assert.Equal(21, Assert.Single(viewModel.Rows).Row.DiffIndex);
    }

    [Fact]
    public void OpenFilterOfALargeColumn_WithoutAnyRestriction_StillSuggestsTheFullRange()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModelWithDiffRows(48);

        Assert.True(viewModel.TryBuildColumnFilterDialog("diff", out var dialog));
        var opened = Assert.IsType<NotchExportColumnFilterDialogViewModel>(dialog);

        Assert.True(opened.UseRangeFilter);
        Assert.Equal("2", opened.RangeFromText);
        Assert.Equal("49", opened.RangeToText);
        Assert.Equal(opened.GetAllKeys(), opened.GetSelectedKeys());
    }

    private static NotchExportSelectionViewModel CreateViewModelWithDiffRows(int count)
    {
        var rows = Enumerable.Range(2, count).Select(static diff => new NotchTableRow(
            0, diff, 100 + diff, 200 + diff, new NotchV22Node(diff, 95, diff + 1, 5, 65535, 0, 0), "transfer"));
        return new NotchExportSelectionViewModel(new NotchTable(rows.ToArray()));
    }
}

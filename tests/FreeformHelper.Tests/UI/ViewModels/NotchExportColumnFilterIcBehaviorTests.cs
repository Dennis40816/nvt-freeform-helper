// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportColumnFilterIcBehaviorTests
{
    [Fact]
    public void ApplyIcRange_UsesDisplayedIcNumbersForShownOnlyExport()
    {
        using var culture = TestCulture.UseEnUs();
        var rows = Enumerable.Range(0, 3).Select(static ic => new NotchTableRow(
            ic, 20 + ic, 100 + ic, 200 + ic, new NotchV22Node(20 + ic, 95, 30 + ic, 5, 65535, 0, 0), "transfer"));
        var selection = new NotchExportSelectionViewModel(new NotchTable(rows.ToArray()));
        Assert.True(selection.TryBuildColumnFilterDialog("ic", out var dialog));
        var filter = Assert.IsType<NotchExportColumnFilterDialogViewModel>(dialog);
        filter.UseRangeFilter = true;
        filter.RangeFromText = " 2 ";
        filter.RangeToText = " 3 ";

        Assert.True(filter.CanApply);
        Assert.Equal(["IC 2", "IC 3"], filter.GetSelectedKeys());
        selection.ApplyColumnFilterSelection(filter.Field, filter.GetSelectedKeys(), filter.GetAllKeys());

        Assert.True(selection.IsIcColumnFilterActive);
        Assert.Equal([1, 2], selection.Rows.Select(static row => row.Row.IcIndex));
        selection.UseShownOnlyCommand.Execute(null);

        var exported = selection.BuildSelectedTable();
        Assert.Equal([1, 2], exported.Rows.Select(static row => row.IcIndex));
        Assert.Equal([21, 22], exported.Rows.Select(static row => row.DiffIndex));
    }
}

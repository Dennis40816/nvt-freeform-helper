// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportColumnFilterRangeBehaviorTests
{
    [Fact]
    public void LargeNumericColumns_SuggestInclusiveRangeUsingNumericBounds()
    {
        using var culture = TestCulture.UseEnUs();
        (NotchExportColumnFilterField Field, string Prefix)[] columns =
        [
            (NotchExportColumnFilterField.RowNumber, "#"),
            (NotchExportColumnFilterField.Ic, "IC "),
            (NotchExportColumnFilterField.Diff, string.Empty),
        ];
        foreach (var (field, prefix) in columns)
        {
            var options = Enumerable.Range(2, 48).Reverse().Select(value =>
            {
                var key = prefix + value.ToString(CultureInfo.InvariantCulture);
                return new NotchExportColumnFilterOptionViewModel(key, key, isSelected: true);
            }).ToArray();
            var smaller = new NotchExportColumnFilterDialogViewModel(field, "Filter", options.Take(47).ToArray());
            Assert.False(smaller.IsRangeSuggested);
            Assert.False(smaller.UseRangeFilter);
            Assert.True(smaller.CanShowValueList);
            Assert.Equal("47/47 selected", smaller.SummaryText);

            var viewModel = new NotchExportColumnFilterDialogViewModel(field, "Filter", options);

            Assert.True(viewModel.IsRangeSuggested);
            Assert.True(viewModel.UseRangeFilter);
            Assert.False(viewModel.CanShowValueList);
            Assert.True(viewModel.CanApply);
            Assert.Equal("2", viewModel.RangeFromText);
            Assert.Equal("49", viewModel.RangeToText);
            Assert.Equal("Range: 2 ~ 49", viewModel.SummaryText);
            Assert.Equal(options.Select(static option => option.Key), viewModel.GetSelectedKeys());
        }
    }

    [Fact]
    public void RangeInput_OpenOrBlankBoundsAndBulkActionsRespectCheckedValues()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = new NotchExportColumnFilterDialogViewModel(NotchExportColumnFilterField.RowNumber, "Filter #",
        [
            new("#2", "#2", isSelected: false),
            new("#10", "#10", isSelected: true),
            new("#20", "#20", isSelected: false),
            new("#30", "#30", isSelected: true),
        ]);
        viewModel.UseRangeFilter = true;
        viewModel.RangeFromText = " 10 ";
        viewModel.RangeToText = " ";

        Assert.True(viewModel.CanApply);
        Assert.Equal("Range: 10 ~ -", viewModel.SummaryText);
        Assert.Equal(["#10", "#20", "#30"], viewModel.GetSelectedKeys());
        Assert.False(viewModel.Options[2].IsSelected);

        viewModel.RangeFromText = string.Empty;
        viewModel.RangeToText = "20";

        Assert.Equal(["#2", "#10", "#20"], viewModel.GetSelectedKeys());

        viewModel.RangeFromText = " ";
        viewModel.RangeToText = string.Empty;

        Assert.Equal("Range: - ~ -", viewModel.SummaryText);
        Assert.Equal(["#10", "#30"], viewModel.GetSelectedKeys());

        viewModel.SelectAll();

        Assert.False(viewModel.UseRangeFilter);
        Assert.True(viewModel.CanShowValueList);
        Assert.Equal("4/4 selected", viewModel.SummaryText);
        Assert.Equal(viewModel.GetAllKeys(), viewModel.GetSelectedKeys());
        Assert.All(viewModel.Options, static option => Assert.True(option.IsSelected));

        viewModel.UseRangeFilter = true;
        viewModel.RangeFromText = "20";
        viewModel.ClearAll();

        Assert.False(viewModel.UseRangeFilter);
        Assert.True(viewModel.CanShowValueList);
        Assert.Equal("0/4 selected", viewModel.SummaryText);
        Assert.Empty(viewModel.GetSelectedKeys());
        Assert.All(viewModel.Options, static option => Assert.False(option.IsSelected));
    }

    [Fact]
    public void InvalidRangeInput_PreventsApplyUntilCorrectedOrValueListRestored()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = new NotchExportColumnFilterDialogViewModel(NotchExportColumnFilterField.Diff, "Filter Diff",
        [
            new("9", "9", isSelected: true),
            new("11", "11", isSelected: true),
        ]);
        viewModel.UseRangeFilter = true;
        viewModel.RangeFromText = "oops";
        viewModel.RangeToText = "11";

        Assert.True(viewModel.HasRangeError);
        Assert.False(viewModel.CanApply);
        Assert.Equal("From must be a number", viewModel.RangeErrorText);

        viewModel.RangeFromText = "12";

        Assert.False(viewModel.CanApply);
        Assert.Equal("From must be less than or equal to To", viewModel.RangeErrorText);

        viewModel.RangeToText = "oops";

        Assert.False(viewModel.CanApply);
        Assert.Equal("To must be a number", viewModel.RangeErrorText);

        viewModel.RangeFromText = "11";
        viewModel.RangeToText = "11";

        Assert.True(viewModel.CanApply);
        Assert.False(viewModel.HasRangeError);
        Assert.Equal(string.Empty, viewModel.RangeErrorText);
        Assert.Equal("11", Assert.Single(viewModel.GetSelectedKeys()));

        viewModel.RangeFromText = "oops";
        viewModel.UseRangeFilter = false;

        Assert.True(viewModel.CanApply);
        Assert.False(viewModel.HasRangeError);
        Assert.Equal(string.Empty, viewModel.RangeErrorText);
        Assert.Equal(["9", "11"], viewModel.GetSelectedKeys());
    }
}

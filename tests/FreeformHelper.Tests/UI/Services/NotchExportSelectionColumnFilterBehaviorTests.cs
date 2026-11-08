// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportSelectionColumnFilterBehaviorTests
{
    [Fact]
    public void SupportedColumnKeys_RouteToMatchingDialogTitlesAndValues()
    {
        using var culture = TestCulture.UseEnUs();
        var selection = new NotchExportSelectionViewModel(new NotchTable(
        [
            new NotchTableRow(2, 20, 120, 220, new NotchV22Node(20, 95, 21, 5, 65535, 0, 0), "transfer"),
        ]));
        (string Key, NotchExportColumnFilterField Field, string Title, string Value)[] columns =
        [
            (" row ", NotchExportColumnFilterField.RowNumber, "Filter #", "#1"),
            (" IC ", NotchExportColumnFilterField.Ic, "Filter IC", "IC 3"),
            (" DiFf ", NotchExportColumnFilterField.Diff, "Filter Diff", "20"),
            (" mAtCh ", NotchExportColumnFilterField.Match, "Filter Match", "95%"),
            (" MAP ", NotchExportColumnFilterField.Mapping, "Filter REG -> CAD", "REG 120 -> CAD 220"),
            (" STATUS ", NotchExportColumnFilterField.Status, "Filter Status", "Linked"),
        ];
        foreach (var (key, field, title, value) in columns)
        {
            Assert.True(selection.TryBuildColumnFilterDialog(key, out var dialog));
            var viewModel = Assert.IsType<NotchExportColumnFilterDialogViewModel>(dialog);

            Assert.Equal(field, viewModel.Field);
            Assert.Equal(title, viewModel.TitleText);
            Assert.Equal(value, Assert.Single(viewModel.Options).Key);
            Assert.Equal(value, Assert.Single(viewModel.GetSelectedKeys()));
        }
    }

    [Fact]
    public void ApplySelection_WhenSourceBecomesEmpty_RemovesOnlyTheRequestedFilter()
    {
        var filters = new Dictionary<NotchExportColumnFilterField, HashSet<string>>
        {
            [NotchExportColumnFilterField.Diff] = new(StringComparer.Ordinal) { "20" },
            [NotchExportColumnFilterField.Status] = new(StringComparer.Ordinal) { "Linked" },
        };

        NotchExportSelectionColumnFilterService.ApplySelection(
            filters, NotchExportColumnFilterField.Diff, ["20"], Array.Empty<string>());

        var remaining = Assert.Single(filters);
        Assert.Equal(NotchExportColumnFilterField.Status, remaining.Key);
        Assert.Equal("Linked", Assert.Single(remaining.Value));
    }
}

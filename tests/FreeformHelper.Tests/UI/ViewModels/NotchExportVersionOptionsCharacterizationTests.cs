// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportVersionOptionsCharacterizationTests
{
    [Fact]
    public void MixedVersionsListAllFirstThenNumericVersionsWithCounts()
    {
        using var culture = TestCulture.UseEnUs();
        var table = new NotchTable(
        [
            CreateRow(NotchAlgorithmVersion.V22, 20),
            CreateRow(NotchAlgorithmVersion.V21, 10),
        ]);

        var viewModel = new NotchExportSelectionViewModel(table);

        NotchExportVersionOption[] expected =
        [
            new(null, "All versions"),
            new(NotchAlgorithmVersion.V21, "v2.1 (1)"),
            new(NotchAlgorithmVersion.V22, "v2.2 (1)"),
        ];
        Assert.Equal(expected, viewModel.VersionOptions);
        Assert.Equal(expected[0], viewModel.SelectedVersionOption);
    }

    [Fact]
    public void OfferedPinAddsAnOptionForTheVersionWithNoRows()
    {
        using var culture = TestCulture.UseEnUs();
        var table = new NotchTable([CreateRow(NotchAlgorithmVersion.V22, 20)]);

        var viewModel = new NotchExportSelectionViewModel(table, exportTypeOptions: CreateExportTypeOptions());

        NotchExportVersionOption[] expected =
        [
            new(null, "All versions"),
            new(NotchAlgorithmVersion.V21, "v2.1 (0)"),
            new(NotchAlgorithmVersion.V22, "v2.2 (1)"),
        ];
        Assert.Equal(expected, viewModel.VersionOptions);
    }

    [Fact]
    public void EmptyTableWithoutOfferedPinsListsOnlyAllVersions()
    {
        using var culture = TestCulture.UseEnUs();

        var viewModel = new NotchExportSelectionViewModel(new NotchTable([]));

        Assert.Equal(new NotchExportVersionOption(null, "All versions"), Assert.Single(viewModel.VersionOptions));
        Assert.Equal(viewModel.VersionOptions[0], viewModel.SelectedVersionOption);
        Assert.False(viewModel.CanSelectVersion);
        Assert.Empty(viewModel.BuildSelectedTable().Rows);
    }

    [Fact]
    public void EmptyTableWithOfferedPinsListsBothZeroCountVersions()
    {
        using var culture = TestCulture.UseEnUs();

        var viewModel = new NotchExportSelectionViewModel(new NotchTable([]), exportTypeOptions: CreateExportTypeOptions());

        NotchExportVersionOption[] expected =
        [
            new(null, "All versions"),
            new(NotchAlgorithmVersion.V21, "v2.1 (0)"),
            new(NotchAlgorithmVersion.V22, "v2.2 (0)"),
        ];
        Assert.Equal(expected, viewModel.VersionOptions);
        Assert.Equal(expected[0], viewModel.SelectedVersionOption);
    }

    [Fact]
    public void RowsSharingAVersionContributeToOneOptionCount()
    {
        using var culture = TestCulture.UseEnUs();
        var table = new NotchTable(
        [
            CreateRow(NotchAlgorithmVersion.V22, 20),
            CreateRow(NotchAlgorithmVersion.V22, 30),
        ]);

        var viewModel = new NotchExportSelectionViewModel(table);

        NotchExportVersionOption[] expected =
        [
            new(null, "All versions"),
            new(NotchAlgorithmVersion.V22, "v2.2 (2)"),
        ];
        Assert.Equal(expected, viewModel.VersionOptions);
    }

    [Fact]
    public void ConstructorAppliesThePreferredFormatPinBeforeProjectingRows()
    {
        using var culture = TestCulture.UseEnUs();
        var table = new NotchTable(
        [
            CreateRow(NotchAlgorithmVersion.V22, 20),
            CreateRow(NotchAlgorithmVersion.V21, 10),
        ]);
        var options = CreateExportTypeOptions();

        var viewModel = new NotchExportSelectionViewModel(
            table, exportTypeOptions: options, selectedExportTypeOption: options[1]);

        Assert.Equal(new NotchExportVersionOption(NotchAlgorithmVersion.V21, "v2.1 (1)"), viewModel.SelectedVersionOption);
        Assert.True(viewModel.IsVersionSelectionPinnedByExportType);
        Assert.False(viewModel.CanSelectVersion);
        Assert.Same(table.Rows[1], Assert.Single(viewModel.Rows).Row);
        Assert.Same(table.Rows[1], Assert.Single(viewModel.BuildSelectedTable().Rows));
    }

    [Theory]
    [InlineData(1, NotchAlgorithmVersion.V21, "v2.1 (1)")]
    [InlineData(2, NotchAlgorithmVersion.V22, "v2.2 (1)")]
    public void SelectingAPinnedFormatSetsItsVersionOption(int formatIndex, NotchAlgorithmVersion version, string display)
    {
        using var culture = TestCulture.UseEnUs();
        var table = new NotchTable(
        [
            CreateRow(NotchAlgorithmVersion.V22, 20),
            CreateRow(NotchAlgorithmVersion.V21, 10),
        ]);
        var options = CreateExportTypeOptions();
        var viewModel = new NotchExportSelectionViewModel(table, exportTypeOptions: options);

        viewModel.SelectedExportTypeOption = options[formatIndex];

        Assert.Equal(new NotchExportVersionOption(version, display), viewModel.SelectedVersionOption);
        Assert.True(viewModel.IsVersionSelectionPinnedByExportType);
        Assert.False(viewModel.CanSelectVersion);
        Assert.Equal(version, Assert.Single(viewModel.BuildSelectedTable().Rows).Version);
    }

    [Fact]
    public void SwitchingToAnUnpinnedFormatKeepsThePreviousVersionScope()
    {
        using var culture = TestCulture.UseEnUs();
        var table = new NotchTable(
        [
            CreateRow(NotchAlgorithmVersion.V21, 10),
            CreateRow(NotchAlgorithmVersion.V22, 20),
        ]);
        var options = CreateExportTypeOptions();
        var viewModel = new NotchExportSelectionViewModel(
            table, exportTypeOptions: options, selectedExportTypeOption: options[2]);

        viewModel.SelectedExportTypeOption = options[0];

        Assert.Equal(new NotchExportVersionOption(NotchAlgorithmVersion.V22, "v2.2 (1)"), viewModel.SelectedVersionOption);
        Assert.False(viewModel.IsVersionSelectionPinnedByExportType);
        Assert.True(viewModel.CanSelectVersion);
        Assert.Same(table.Rows[1], Assert.Single(viewModel.BuildSelectedTable().Rows));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PinnedFormatWithNoMatchingRowsStaysPinnedAndExportsNothing(bool selectInConstructor)
    {
        using var culture = TestCulture.UseEnUs();
        var table = new NotchTable([CreateRow(NotchAlgorithmVersion.V22, 20)]);
        var options = CreateExportTypeOptions();
        var viewModel = new NotchExportSelectionViewModel(
            table, exportTypeOptions: options, selectedExportTypeOption: selectInConstructor ? options[1] : options[0]);

        viewModel.SelectedExportTypeOption = options[1];
        var selectedTable = viewModel.BuildSelectedTable();

        Assert.Equal(new NotchExportVersionOption(NotchAlgorithmVersion.V21, "v2.1 (0)"), viewModel.SelectedVersionOption);
        Assert.Equal(options[1], viewModel.SelectedExportTypeOption);
        Assert.True(viewModel.IsVersionSelectionPinnedByExportType);
        Assert.False(viewModel.CanSelectVersion);
        Assert.Empty(viewModel.Rows);
        Assert.Empty(selectedTable.Rows);
    }

    [Fact]
    public void PinSwitchesPreserveCheckedAndUncheckedRowsAcrossVersionScopes()
    {
        using var culture = TestCulture.UseEnUs();
        var table = new NotchTable(
        [
            CreateRow(NotchAlgorithmVersion.V21, 10),
            CreateRow(NotchAlgorithmVersion.V22, 20),
            CreateRow(NotchAlgorithmVersion.V22, 30),
        ]);
        var options = CreateExportTypeOptions();
        var viewModel = new NotchExportSelectionViewModel(table, exportTypeOptions: options);
        var legacyRow = viewModel.Rows[0];
        var checkedRow = viewModel.Rows[1];
        var uncheckedRow = viewModel.Rows[2];
        legacyRow.IsSelected = false;
        uncheckedRow.IsSelected = false;

        viewModel.SelectedExportTypeOption = options[1];
        Assert.Same(legacyRow, Assert.Single(viewModel.Rows));
        Assert.False(legacyRow.IsSelected);
        Assert.Empty(viewModel.BuildSelectedTable().Rows);

        viewModel.SelectedExportTypeOption = options[2];
        Assert.True(checkedRow.IsSelected);
        Assert.False(uncheckedRow.IsSelected);
        Assert.Same(table.Rows[1], Assert.Single(viewModel.BuildSelectedTable().Rows));

        viewModel.SelectedExportTypeOption = options[0];
        viewModel.SelectedVersionOption = viewModel.VersionOptions[0];
        Assert.Same(legacyRow, viewModel.Rows[0]);
        Assert.Same(checkedRow, viewModel.Rows[1]);
        Assert.Same(uncheckedRow, viewModel.Rows[2]);
        Assert.False(legacyRow.IsSelected);
        Assert.True(checkedRow.IsSelected);
        Assert.False(uncheckedRow.IsSelected);
        Assert.Same(table.Rows[1], Assert.Single(viewModel.BuildSelectedTable().Rows));
    }

    private static NotchExportFileTypeOption[] CreateExportTypeOptions()
    {
        return
        [
            new(FreeformHelperViewModel.NotchExportFileType.Csv, "CSV review (.csv)", "csv"),
            new(FreeformHelperViewModel.NotchExportFileType.Cv21, "C v2.1 (.c)", "c", NotchAlgorithmVersion.V21),
            new(FreeformHelperViewModel.NotchExportFileType.Cv22, "C v2.2 (.c)", "c", NotchAlgorithmVersion.V22),
        ];
    }

    private static NotchTableRow CreateRow(NotchAlgorithmVersion version, int diff)
    {
        int[] values = version == NotchAlgorithmVersion.V22
            ? [diff, 95, diff + 1, 5, 65535, 0, 0]
            : [diff, 128, 65535, 0, 0, 65535, 0, 0, 0];
        return new NotchTableRow(version, 0, diff, 100 + diff, 200 + diff, values, "row");
    }
}

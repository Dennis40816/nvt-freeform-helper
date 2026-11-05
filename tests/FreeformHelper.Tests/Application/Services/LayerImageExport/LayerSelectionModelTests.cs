// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LayerSelectionModelTests
{
    private static readonly LayerListEntry[] _entries =
    [
        new("nc.drawing", 10), new("nc.pads", 2), new("other.checked", 3), new("other.unchecked", 4),
    ];

    [Fact]
    public void CheckAllVisibleLeavesFilteredRowsUnchanged()
    {
        var model = new LayerSelectionModel(_entries, new List<string> { "other.checked" });
        model.SetQuery("nc.");

        model.CheckAllVisible();

        Assert.Equal(new List<string> { "nc.drawing", "nc.pads", "other.checked" }, model.CheckedNames);
        Assert.Equal(AllLayersState.Checked, model.AllLayersState);
    }

    [Fact]
    public void UncheckAllVisibleLeavesFilteredRowsUnchanged()
    {
        var model = new LayerSelectionModel(_entries, new List<string> { "nc.drawing", "nc.pads", "other.checked" });
        model.SetQuery("nc");

        model.UncheckAllVisible();

        Assert.Equal(new List<string> { "other.checked" }, model.CheckedNames);
        Assert.Equal(AllLayersState.Unchecked, model.AllLayersState);
    }

    [Fact]
    public void ToggleChecksMixedVisibleRowsThenUnchecksThem()
    {
        var model = new LayerSelectionModel(_entries, new List<string> { "nc.drawing", "other.checked" });
        model.SetQuery("nc");
        Assert.Equal(AllLayersState.Indeterminate, model.AllLayersState);

        model.ToggleAllVisible();
        Assert.Equal(AllLayersState.Checked, model.AllLayersState);
        model.ToggleAllVisible();

        Assert.Equal(new List<string> { "other.checked" }, model.CheckedNames);
        Assert.Equal(AllLayersState.Unchecked, model.AllLayersState);
    }

    [Fact]
    public void EmptySearchResultsAreUncheckedAndToggleDoesNothing()
    {
        var model = new LayerSelectionModel(_entries, new List<string> { "nc.drawing" });
        model.SetQuery("missing");

        model.ToggleAllVisible();

        Assert.Equal(AllLayersState.Unchecked, model.AllLayersState);
        Assert.Equal(1, model.CheckedCount);
        Assert.Equal(4, model.TotalCount);
    }

    [Fact]
    public void ConstructorIgnoresUnknownNamesAndPreservesEntryOrder()
    {
        var model = new LayerSelectionModel(_entries, new List<string> { "other.checked", "NC.DRAWING", "missing", "nc.pads" });

        Assert.Equal(new List<string> { "nc.pads", "other.checked" }, model.CheckedNames);
        Assert.False(model.SetChecked("NC.PADS", true));
        Assert.False(model.SetChecked("missing", true));
    }

    [Fact]
    public void SetCheckedReportsOnlyChanges()
    {
        var model = new LayerSelectionModel(_entries, null);

        Assert.True(model.SetChecked("nc.drawing", true));
        Assert.False(model.SetChecked("nc.drawing", true));
        Assert.True(model.SetChecked("nc.drawing", false));
        Assert.False(model.SetChecked("nc.drawing", false));
    }

    [Fact]
    public void ConstructorOwnsEntriesAndSelection()
    {
        var entries = _entries.ToList();
        var checkedNames = new List<string> { "nc.drawing" };
        var model = new LayerSelectionModel(entries, checkedNames);

        entries.Clear();
        checkedNames.Clear();

        Assert.Equal(4, model.TotalCount);
        Assert.Equal(new List<string> { "nc.drawing" }, model.CheckedNames);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void BlankQueryShowsAllEntries(string? query)
    {
        var model = new LayerSelectionModel(_entries, null);

        model.SetQuery(query);

        Assert.Equal(query ?? string.Empty, model.Query);
        Assert.Equal(model.Entries, model.VisibleEntries);
    }
}

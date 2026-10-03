using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportSelectionColumnFilterServiceTests
{
    [Fact]
    public void ApplySelection_SelectedAll_RemovesFilter()
    {
        var filters = new Dictionary<NotchExportColumnFilterField, HashSet<string>>
        {
            [NotchExportColumnFilterField.Diff] = new HashSet<string>(StringComparer.Ordinal) { "9" },
        };
        var all = new[] { "7", "8", "9" };

        NotchExportSelectionColumnFilterService.ApplySelection(
            filters,
            NotchExportColumnFilterField.Diff,
            all,
            all);

        Assert.False(filters.ContainsKey(NotchExportColumnFilterField.Diff));
    }

    [Fact]
    public void ApplySelection_EmptySelectedKeys_KeepsExplicitEmptyFilter()
    {
        var filters = new Dictionary<NotchExportColumnFilterField, HashSet<string>>();
        var all = new[] { "7", "8", "9" };

        NotchExportSelectionColumnFilterService.ApplySelection(
            filters,
            NotchExportColumnFilterField.Diff,
            Array.Empty<string>(),
            all);

        Assert.True(filters.TryGetValue(NotchExportColumnFilterField.Diff, out var selected));
        Assert.Empty(selected!);
    }

    [Fact]
    public void TryParseField_ParsesSupportedColumnKey()
    {
        var parsed = NotchExportSelectionColumnFilterService.TryParseField("map", out var field);

        Assert.True(parsed);
        Assert.Equal(NotchExportColumnFilterField.Mapping, field);
    }

    [Fact]
    public void BuildDialogTitle_ReturnsExpectedTitle()
    {
        var title = NotchExportSelectionColumnFilterService.BuildDialogTitle(NotchExportColumnFilterField.Status);

        Assert.Equal("Filter Status", title);
    }
}

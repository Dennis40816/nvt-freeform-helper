using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    private static readonly int[] SuggestedBaseNameV22Payload = [21, 94, 22, 12, 65535, 0, 0];
    private static readonly int[] SuggestedBaseNameV21Payload = [10, 100, 100, 11, 1, 12, 65535, 0, 0];
    private static readonly int[] SuggestedBaseNameMixedV22Payload = [11, 94, 12, 22, 65535, 0, 0];

    [Fact]
    public void ResolveSuggestedNotchExportBaseName_WhenSingleCVersion_UsesVersionLabel()
    {
        var table = new NotchTable(new[]
        {
            new NotchTableRow(NotchAlgorithmVersion.V22, 0, 21, 21, 1021, SuggestedBaseNameV22Payload, "v22"),
        });

        var result = FreeformHelperViewModel.ResolveSuggestedNotchExportBaseName("notch_table", "c", table);

        Assert.Equal("notch_v2.2", result);
    }

    [Fact]
    public void ResolveSuggestedNotchExportBaseName_WhenMixedVersionsOrNonC_KeepsRequestedName()
    {
        var mixedTable = new NotchTable(new NotchTableRow[]
        {
            new(NotchAlgorithmVersion.V21, 0, 10, 10, 1010, SuggestedBaseNameV21Payload, "v21"),
            new(NotchAlgorithmVersion.V22, 0, 11, 11, 1011, SuggestedBaseNameMixedV22Payload, "v22"),
        });

        Assert.Equal(
            "notch_table",
            FreeformHelperViewModel.ResolveSuggestedNotchExportBaseName("notch_table", "c", mixedTable));
        Assert.Equal(
            "notch_table",
            FreeformHelperViewModel.ResolveSuggestedNotchExportBaseName("notch_table", "csv", mixedTable));
    }
}

using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ManualSizingStateCharacterizationTests
{
    private sealed record Observation(string Event, ManualSizingInput Input, string? Error, bool HasError);

    [Fact]
    public void RangeEdit_HandlersSeeTheInputUpdateBeforeTheErrorAndStatusUpdates()
    {
        var vm = new FreeformHelperViewModel { ManualRangeError = "previous error" };
        var observations = new List<Observation>();
        vm.PropertyChanging += (_, args) => observations.Add(Read(vm, "changing:" + args.PropertyName));
        vm.PropertyChanged += (_, args) => observations.Add(Read(vm, "changed:" + args.PropertyName));
        var before = new ManualSizingInput(0m, 0m, false, false, "", "", "No manual range.");
        var rangeUpdated = new ManualSizingInput(0m, 0m, false, false, "1-3", "", "No manual range.");
        var statusUpdated = new ManualSizingInput(0m, 0m, false, false, "1-3", "", "Grid not built.");

        vm.ManualRowsRange = "1-3";

        Assert.Equal<Observation>([
            new("changed:CanUndo", before, "previous error", true),
            new("changing:ManualRowsRange", before, "previous error", true),
            new("changing:ManualRangeError", rangeUpdated, "previous error", true),
            new("changed:ManualRangeError", rangeUpdated, "", false),
            new("changed:HasManualRangeError", rangeUpdated, "", false),
            new("changing:ManualRangeStatus", rangeUpdated, "", false),
            new("changed:ManualRangeStatus", statusUpdated, "", false),
            new("changed:ManualRowsRange", statusUpdated, "", false),
        ], observations);
    }

    [Fact]
    public void Error_NullAndEqualStringsKeepTheSetPropertyNotificationContract()
    {
        var vm = new FreeformHelperViewModel { ManualRangeError = "bad" };
        var observations = new List<(string Event, string? Error, bool HasError)>();
        vm.PropertyChanging += (_, args) =>
            observations.Add(("changing:" + args.PropertyName, vm.ManualRangeError, vm.HasManualRangeError));
        vm.PropertyChanged += (_, args) =>
            observations.Add(("changed:" + args.PropertyName, vm.ManualRangeError, vm.HasManualRangeError));

        vm.ManualRangeError = new string(['b', 'a', 'd']);
        Assert.Empty(observations);
        vm.ManualRangeError = null!;

        Assert.Equal([
            ("changing:ManualRangeError", "bad", true),
            ("changed:ManualRangeError", (string?)null, false),
            ("changed:HasManualRangeError", (string?)null, false),
        ], observations);
        observations.Clear();
        vm.ManualRangeError = null!;
        Assert.Empty(observations);
        vm.ManualRangeError = "";
        vm.ManualRangeError = string.Empty;

        Assert.Equal([
            ("changing:ManualRangeError", (string?)null, false),
            ("changed:ManualRangeError", "", false),
        ], observations);
        Assert.Equal("", vm.ManualRangeError);
        Assert.False(vm.HasManualRangeError);
    }

    [Fact]
    public void ViewModels_KeepTheirManualInputsAndErrorsIndependent()
    {
        var first = new FreeformHelperViewModel();
        var second = new FreeformHelperViewModel();
        first.PendingColumnWidth = 5m;
        first.PendingRowHeight = 6m;
        first.IsPendingColumnWidthMixed = true;
        first.IsPendingRowHeightMixed = true;
        first.ManualRowsRange = "1-3";
        first.ManualColsRange = "A-C";
        first.ManualRangeStatus = "first status";
        first.ManualRangeError = "first error";

        Assert.Equal(new Observation("first", new(5m, 6m, true, true, "1-3", "A-C", "first status"), "first error", true),
            Read(first, "first"));
        Assert.Equal(new Observation("second", new(0m, 0m, false, false, "", "", "No manual range."), "", false),
            Read(second, "second"));
        second.ManualRowsRange = "4";
        second.ManualRangeError = "second error";

        Assert.Equal(new Observation("first", new(5m, 6m, true, true, "1-3", "A-C", "first status"), "first error", true),
            Read(first, "first"));
        Assert.Equal(new Observation("second", new(0m, 0m, false, false, "4", "", "Grid not built."), "second error", true),
            Read(second, "second"));
    }

    private static Observation Read(FreeformHelperViewModel vm, string eventName) => new(
        eventName,
        new ManualSizingInput(vm.PendingColumnWidth, vm.PendingRowHeight, vm.IsPendingColumnWidthMixed,
            vm.IsPendingRowHeightMixed, vm.ManualRowsRange, vm.ManualColsRange, vm.ManualRangeStatus),
        vm.ManualRangeError,
        vm.HasManualRangeError);
}

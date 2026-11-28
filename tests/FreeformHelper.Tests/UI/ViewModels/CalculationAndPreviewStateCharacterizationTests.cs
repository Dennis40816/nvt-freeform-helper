using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CalculationAndPreviewStateCharacterizationTests
{
    private sealed record Observation(string Event, decimal Value, decimal LinkedValue, bool IsDirty);

    [Fact]
    public void LinkedThreshold_HandlersSeeEachRecordReplacementInOrder()
    {
        var vm = new FreeformHelperViewModel();
        var observations = Observe(vm, () => (vm.NotchThresholdQ7, vm.NotchThresholdPercent));

        vm.NotchThresholdQ7 = 64m;

        Assert.Equal<Observation>([
            new("changed:CanUndo", 0m, 0m, false),
            new("changing:NotchThresholdQ7", 0m, 0m, false),
            new("changed:CanUndo", 64m, 0m, false),
            new("changing:NotchThresholdPercent", 64m, 0m, false),
            new("changed:NotchThresholdPercent", 64m, 50m, false),
            new("changing:HasUnsavedChanges", 64m, 50m, false),
            new("changed:HasUnsavedChanges", 64m, 50m, true),
            new("changed:NotchThresholdQ7", 64m, 50m, true),
        ], observations);
    }

    [Fact]
    public void GridSetting_HandlersSeeTheOldThenNewValueBeforeTheDirtyFlag()
    {
        var vm = new FreeformHelperViewModel();
        var observations = Observe(vm, () => (vm.CoordinatePixelWidth, vm.CoordinatePixelHeight));

        vm.CoordinatePixelWidth = 40m;

        Assert.Equal<Observation>([
            new("changing:CoordinatePixelWidth", 32m, 20m, false),
            new("changed:CoordinatePixelWidth", 40m, 20m, false),
            new("changing:HasUnsavedChanges", 40m, 20m, false),
            new("changed:HasUnsavedChanges", 40m, 20m, true),
        ], observations);
    }

    [Fact]
    public void MappingTuning_UsesTheCallingPublicPropertyNameForBothEvents()
    {
        var vm = new FreeformHelperViewModel();
        var observations = Observe(vm, () => (vm.MappingCandidateNumber, vm.CoordinatePixelWidth));

        vm.MappingCandidateNumber = 16m;

        Assert.Equal<Observation>([
            new("changing:MappingCandidateNumber", 9m, 32m, false),
            new("changed:MappingCandidateNumber", 16m, 32m, false),
            new("changing:HasUnsavedChanges", 16m, 32m, false),
            new("changed:HasUnsavedChanges", 16m, 32m, true),
        ], observations);
    }

    [Fact]
    public void PreviewSetting_NotifiesTheSummaryBeforeTheSettingAndDirtyFlag()
    {
        var vm = new FreeformHelperViewModel();
        var observations = Observe(vm, () => (vm.NotchPreviewAutoPlayIntervalMs, vm.NotchPreviewVisualizationStep));
        var summaries = new List<string>();
        vm.PropertyChanged += (_, _) => summaries.Add(vm.NotchPreviewAutoPlaySummary);

        vm.NotchPreviewAutoPlayIntervalMs = 900m;

        Assert.Equal<Observation>([
            new("changing:NotchPreviewAutoPlayIntervalMs", 700m, 3m, false),
            new("changed:NotchPreviewAutoPlaySummary", 900m, 3m, false),
            new("changed:NotchPreviewAutoPlayIntervalMs", 900m, 3m, false),
            new("changing:HasUnsavedChanges", 900m, 3m, false),
            new("changed:HasUnsavedChanges", 900m, 3m, true),
        ], observations);
        Assert.Equal([
            "AutoPlay ON (900 ms, loop 1->2->3)",
            "AutoPlay ON (900 ms, loop 1->2->3)",
            "AutoPlay ON (900 ms, loop 1->2->3)",
        ], summaries);
    }

    [Fact]
    public void ViewModels_KeepAllFourCalculationAndPreviewRecordsIndependent()
    {
        var first = new FreeformHelperViewModel();
        var second = new FreeformHelperViewModel();

        Edit(first, 40m, 16m, 64m, 900m);

        Assert.Equal((40m, 16m, 64m, 50m, 900m), Read(first));
        Assert.Equal((32m, 9m, 0m, 0m, 700m), Read(second));
        Edit(second, 48m, 25m, 32m, 1100m);

        Assert.Equal((40m, 16m, 64m, 50m, 900m), Read(first));
        Assert.Equal((48m, 25m, 32m, 25m, 1100m), Read(second));
    }

    private static List<Observation> Observe(FreeformHelperViewModel vm, Func<(decimal Value, decimal LinkedValue)> read)
    {
        var observations = new List<Observation>();
        vm.PropertyChanging += (_, args) => Record("changing:" + args.PropertyName);
        vm.PropertyChanged += (_, args) => Record("changed:" + args.PropertyName);
        return observations;

        void Record(string eventName)
        {
            var (value, linkedValue) = read();
            observations.Add(new Observation(eventName, value, linkedValue, vm.HasUnsavedChanges));
        }
    }

    private static void Edit(FreeformHelperViewModel vm, decimal pixelWidth, decimal candidates, decimal threshold, decimal interval)
    {
        vm.CoordinatePixelWidth = pixelWidth;
        vm.MappingCandidateNumber = candidates;
        vm.NotchThresholdQ7 = threshold;
        vm.NotchPreviewAutoPlayIntervalMs = interval;
    }

    private static (decimal, decimal, decimal, decimal, decimal) Read(FreeformHelperViewModel vm) =>
        (vm.CoordinatePixelWidth, vm.MappingCandidateNumber, vm.NotchThresholdQ7,
            vm.NotchThresholdPercent, vm.NotchPreviewAutoPlayIntervalMs);
}

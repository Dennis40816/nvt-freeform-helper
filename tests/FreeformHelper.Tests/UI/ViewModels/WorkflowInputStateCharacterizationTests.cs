using System.ComponentModel;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class WorkflowInputStateCharacterizationTests
{
    private const string InitialAssignmentSummary = "CAD Output FW Diff assignment (per-IC): auto, scan-order based.";

    private static readonly string[] _workflowProperties =
    [
        nameof(FreeformHelperViewModel.CoordinatePreferredAaOutlineLayerName),
        nameof(FreeformHelperViewModel.MaxAreaBuckets),
        nameof(FreeformHelperViewModel.AreaBucketTolerance),
        nameof(FreeformHelperViewModel.IsHeightColumnLocal),
        nameof(FreeformHelperViewModel.IsWidthRowLocal),
        nameof(FreeformHelperViewModel.UseLocalSizing),
        nameof(FreeformHelperViewModel.RegularSourceMode),
        nameof(FreeformHelperViewModel.GridAlignmentMode),
        nameof(FreeformHelperViewModel.SelectedScanOrder),
        nameof(FreeformHelperViewModel.DxfRegularSourceHint),
        nameof(FreeformHelperViewModel.SelectedRegularSourceLayerOption),
        nameof(FreeformHelperViewModel.BoundLayerOptionSummary),
        nameof(FreeformHelperViewModel.SelectedBoundLayerOption),
        nameof(FreeformHelperViewModel.HasCadLayers),
        nameof(FreeformHelperViewModel.ShowDiffIndexOverlay),
        nameof(FreeformHelperViewModel.CadOutputFwDiffAssignmentSummary),
        nameof(FreeformHelperViewModel.CadOutputFwDiffIndexAnchorCadId),
        nameof(FreeformHelperViewModel.CadOutputFwDiffAutoMode),
        nameof(FreeformHelperViewModel.CadOutputFwDiffIndexStart),
    ];

    private sealed record Observation(string Event, bool Local, bool Width, bool Height, string Hint, string Assignment, bool Overlay);

    [AvaloniaFact]
    public async Task LocalSizingCascade_HandlersSeeEveryWorkflowInputNotificationAndIntermediateValueAsync()
    {
        var vm = new FreeformHelperViewModel
        {
            DxfRegularSourceHint = "before rebuild",
            CadOutputFwDiffAssignmentSummary = "before assignment",
        };
        var second = new FreeformHelperViewModel();
        var observations = ObserveWorkflowInputs(vm);

        await EditAndWaitForGridAsync(vm, () => vm.UseLocalSizing = false);

        Assert.Equal<Observation>([
            new("changing:UseLocalSizing", true, true, true, "before rebuild", "before assignment", true),
            new("changing:IsWidthRowLocal", false, true, true, "before rebuild", "before assignment", true),
            new("changed:IsWidthRowLocal", false, false, true, "before rebuild", "before assignment", true),
            new("changing:IsHeightColumnLocal", false, false, true, "before rebuild", "before assignment", true),
            new("changed:IsHeightColumnLocal", false, false, false, "before rebuild", "before assignment", true),
            new("changed:UseLocalSizing", false, false, false, "before rebuild", "before assignment", true),
            new("changing:DxfRegularSourceHint", false, false, false, "before rebuild", "before assignment", true),
            new("changed:DxfRegularSourceHint", false, false, false, "", "before assignment", true),
        ], observations);
        Assert.Equal(640, vm.RegularPads.Count);
        Assert.Equal(new Observation("second", true, true, true, "", InitialAssignmentSummary, true), Read(second, "second"));
    }

    [Fact]
    public void LayerAndAssignmentEdits_HandlersSeeOldThenNewValuesInOrder()
    {
        var vm = new FreeformHelperViewModel();
        var observations = new List<(string Event, string? Layer, bool Overlay, string Assignment)>();
        vm.PropertyChanging += (_, args) => Record("changing:" + args.PropertyName);
        vm.PropertyChanged += (_, args) => Record("changed:" + args.PropertyName);

        vm.SelectedRegularSourceLayerOption = new("REG", "Regular source");
        vm.ShowDiffIndexOverlay = false;
        vm.CadOutputFwDiffAssignmentSummary = "reviewed assignment";

        Assert.Equal([
            ("changing:SelectedRegularSourceLayerOption", (string?)null, true, InitialAssignmentSummary),
            ("changed:SelectedRegularSourceLayerOption", "REG", true, InitialAssignmentSummary),
            ("changed:CanUndo", "REG", true, InitialAssignmentSummary),
            ("changing:ShowDiffIndexOverlay", "REG", true, InitialAssignmentSummary),
            ("changed:ShowDiffIndexOverlay", "REG", false, InitialAssignmentSummary),
            ("changing:CadOutputFwDiffAssignmentSummary", "REG", false, InitialAssignmentSummary),
            ("changed:CadOutputFwDiffAssignmentSummary", "REG", false, "reviewed assignment"),
        ], observations);

        void Record(string eventName) => observations.Add((eventName, vm.SelectedRegularSourceLayerOption.Name,
            vm.ShowDiffIndexOverlay, vm.CadOutputFwDiffAssignmentSummary));
    }

    [Fact]
    public void ViewModels_KeepAllThreeWorkflowInputRecordsIndependent()
    {
        var first = new FreeformHelperViewModel();
        var second = new FreeformHelperViewModel();
        first.MaxAreaBuckets = 10m;
        first.DxfRegularSourceHint = "first hint";
        first.CadOutputFwDiffAssignmentSummary = "first assignment";
        first.ShowDiffIndexOverlay = false;

        Assert.Equal((10m, "first hint", "first assignment", false), ReadIndependentValues(first));
        Assert.Equal((32m, "", InitialAssignmentSummary, true), ReadIndependentValues(second));
        second.MaxAreaBuckets = 12m;
        second.DxfRegularSourceHint = "second hint";
        second.CadOutputFwDiffAssignmentSummary = "second assignment";

        Assert.Equal((10m, "first hint", "first assignment", false), ReadIndependentValues(first));
        Assert.Equal((12m, "second hint", "second assignment", true), ReadIndependentValues(second));
    }

    private static List<Observation> ObserveWorkflowInputs(FreeformHelperViewModel vm)
    {
        var observations = new List<Observation>();
        vm.PropertyChanging += (_, args) => Record("changing:", args.PropertyName);
        vm.PropertyChanged += (_, args) => Record("changed:", args.PropertyName);
        return observations;

        void Record(string prefix, string? propertyName)
        {
            // Observe every public slot in the three records throughout the rebuild.
            if (_workflowProperties.Contains(propertyName, StringComparer.Ordinal))
            {
                observations.Add(Read(vm, prefix + propertyName));
            }
        }
    }

    private static async Task EditAndWaitForGridAsync(FreeformHelperViewModel vm, Action edit)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PropertyChanged += OnChanged;
        try
        {
            edit();
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        finally
        {
            vm.PropertyChanged -= OnChanged;
        }

        void OnChanged(object? _, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(vm.IsModalLoadingSpinnerVisible) && !vm.IsModalLoadingSpinnerVisible)
            {
                completed.TrySetResult();
            }
        }
    }

    private static Observation Read(FreeformHelperViewModel vm, string eventName) => new(eventName,
        vm.UseLocalSizing, vm.IsWidthRowLocal, vm.IsHeightColumnLocal,
        vm.DxfRegularSourceHint, vm.CadOutputFwDiffAssignmentSummary, vm.ShowDiffIndexOverlay);

    private static (decimal, string, string, bool) ReadIndependentValues(FreeformHelperViewModel vm) =>
        (vm.MaxAreaBuckets, vm.DxfRegularSourceHint, vm.CadOutputFwDiffAssignmentSummary, vm.ShowDiffIndexOverlay);
}

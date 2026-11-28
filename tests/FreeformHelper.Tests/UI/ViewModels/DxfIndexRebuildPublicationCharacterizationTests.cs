// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.ComponentModel;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfIndexRebuildPublicationCharacterizationTests
{
    private static readonly string[] _expectedOrder = ["hints", "groups", "returned"];

    [AvaloniaFact]
    public async Task Rebuild_PublishesCurrentSnapshotToDuplicateObserversThenInvalidatesItAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var keep = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha");
        var removed = Assert.Single(vm.CadPads, static pad => pad.Layer == "beta");
        var original = FreeformHelperViewModelPrivateAccess.BuildWorkflowDataSnapshot(vm);
        Assert.Contains(removed.Id, original.CadDisplayIndexByCadId.Keys);
        var phases = new List<string>();
        var snapshots = new List<WorkflowDataSnapshot>();
        void Record(string phase)
        {
            phases.Add(phase);
            snapshots.Add(FreeformHelperViewModelPrivateAccess.BuildWorkflowDataSnapshot(vm));
        }

        void PropertyChanged(object? _, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(vm.CadOutputFwDiffIndexOverrideCadIds)) Record("hints");
            if (args.PropertyName == nameof(vm.Step4DuplicateDiffGroups)) Record("groups");
        }

        vm.PropertyChanged += PropertyChanged;
        try
        {
            FreeformHelperViewModelPrivateAccess.UpdateCadOutputFwDiffIndexing(vm, [keep]);
        }
        finally
        {
            vm.PropertyChanged -= PropertyChanged;
        }

        Record("returned");
        Assert.Equal(_expectedOrder, phases);
        Assert.NotSame(original, snapshots[0]);
        Assert.Same(snapshots[0], snapshots[1]);
        Assert.NotSame(snapshots[1], snapshots[2]);
        Assert.All(snapshots, snapshot =>
        {
            Assert.Equal(keep.Id, Assert.Single(snapshot.CadDisplayIndexByCadId).Key);
            Assert.DoesNotContain(removed.Id, snapshot.CadDisplayIndexByCadId.Keys);
        });
        Assert.Empty(vm.Step4DuplicateDiffGroups);
        Assert.False(vm.HasStep4DuplicateDiffGroups);
    }
}

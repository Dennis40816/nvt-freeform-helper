// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.UI.ViewModels.CoordinatePlannerWorkspaceViewModel;

namespace FreeformHelper.Tests;

public sealed class CoordinatePublicationOwnerCharacterizationTests
{
    [Fact]
    public void Drag_ChangesXThenYAgainstOldRenderAndRebuildsOnce()
    {
        var vm = CreateViewModel();
        vm.ShowCustomArray = true;
        var before = vm.Snapshot;
        var events = new List<(string Event, decimal X, decimal Y)>();
        var rebuilds = 0;
        vm.PropertyChanging += (sender, e) =>
        {
            if (e.PropertyName is not ("CustomArrayTopLeftMachineX" or "CustomArrayTopLeftMachineY")) return;
            Assert.Same(vm, sender);
            Assert.Same(before, vm.Snapshot);
            events.Add(($"changing:{e.PropertyName}", vm.CustomArrayTopLeftMachineX, vm.CustomArrayTopLeftMachineY));
        };
        vm.PropertyChanged += (sender, e) =>
        {
            Assert.Same(vm, sender);
            if (e.PropertyName is "CustomArrayTopLeftMachineX" or "CustomArrayTopLeftMachineY")
            {
                Assert.Same(before, vm.Snapshot);
                events.Add(($"changed:{e.PropertyName}", vm.CustomArrayTopLeftMachineX, vm.CustomArrayTopLeftMachineY));
            }
            if (e.PropertyName == "Snapshot") rebuilds++;
        };

        Assert.True(vm.MoveCustomArrayCornerToWorldPoint("array-tl", new Point2(2d, 3d)));

        Assert.Equal([("changing:CustomArrayTopLeftMachineX", 0m, 0m),
            ("changed:CustomArrayTopLeftMachineX", 20m, 0m),
            ("changing:CustomArrayTopLeftMachineY", 20m, 0m),
            ("changed:CustomArrayTopLeftMachineY", 20m, 20m)], events);
        Assert.Equal(1, rebuilds);
        Assert.NotSame(before, vm.Snapshot);
        Assert.Equal("array-tl", vm.SelectedArtifactRow?.Key);
    }

    [Fact]
    public void SafeCorners_AssignsClockwiseXYBeforeRebuildAndArrayActivation()
    {
        var vm = CreateViewModel();
        vm.CopperPillarDiameter = 10m;
        var before = vm.Snapshot;
        var events = new List<string>();
        vm.PropertyChanging += (_, e) =>
        {
            if (e.PropertyName?.StartsWith("CustomArray", StringComparison.Ordinal) != true) return;
            Assert.Same(before, vm.Snapshot);
            events.Add(e.PropertyName);
        };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is "Snapshot" or "ShowCustomArray") events.Add(e.PropertyName);
        };

        vm.UseActiveAreaSafeCornersCommand.Execute(null);

        Assert.Equal(["CustomArrayTopLeftMachineX", "CustomArrayTopLeftMachineY",
            "CustomArrayTopRightMachineX", "CustomArrayTopRightMachineY",
            "CustomArrayBottomRightMachineX", "CustomArrayBottomRightMachineY",
            "CustomArrayBottomLeftMachineX", "CustomArrayBottomLeftMachineY",
            "Snapshot", "Snapshot", "ShowCustomArray"], events);
        Assert.Equal((5m, 5m, 95m, 5m, 95m, 45m, 5m, 45m),
            (vm.CustomArrayTopLeftMachineX, vm.CustomArrayTopLeftMachineY,
                vm.CustomArrayTopRightMachineX, vm.CustomArrayTopRightMachineY,
                vm.CustomArrayBottomRightMachineX, vm.CustomArrayBottomRightMachineY,
                vm.CustomArrayBottomLeftMachineX, vm.CustomArrayBottomLeftMachineY));
    }

    [Fact]
    public void Clear_KeepsDraftAndCollectionIdentityBeforeAvailabilityAndRender()
    {
        var vm = CreateViewModel();
        var editor = vm.CustomEditor;
        var recipes = vm.CustomRecipeRows;
        editor.CustomPointLabelText = "Probe";
        editor.CustomPointMachineX = 12m;
        editor.CustomPointMachineY = 13m;
        editor.CustomPathLabelText = "Sweep";
        editor.CustomPathStartMachineX = 14m;
        editor.CustomPathStartMachineY = 15m;
        editor.CustomPathEndMachineX = 16m;
        editor.CustomPathEndMachineY = 17m;
        editor.CustomPathStepCount = 3m;
        vm.AddCustomPointCommand.Execute(null);
        vm.AddCustomPathCommand.Execute(null);
        var order = new List<string>();
        vm.ClearCustomArtifactsCommand.CanExecuteChanged += (_, _) =>
        {
            Assert.Empty(recipes);
            Assert.False(vm.ClearCustomArtifactsCommand.CanExecute(null));
            order.Add("availability");
        };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "Snapshot") order.Add("render");
        };

        vm.ClearCustomArtifactsCommand.Execute(null);

        Assert.Equal(["availability", "render"], order);
        Assert.Same(editor, vm.CustomEditor);
        Assert.Same(recipes, vm.CustomRecipeRows);
        Assert.Equal(("Probe", 12m, 13m, "Sweep", 14m, 15m, 16m, 17m, 3m),
            (editor.CustomPointLabelText, editor.CustomPointMachineX, editor.CustomPointMachineY,
                editor.CustomPathLabelText, editor.CustomPathStartMachineX, editor.CustomPathStartMachineY,
                editor.CustomPathEndMachineX, editor.CustomPathEndMachineY, editor.CustomPathStepCount));
        Assert.Equal("Custom coordinate artifacts cleared.", vm.StatusText);
    }

    [Fact]
    public void AddingAfterClear_ContinuesPointAndPathIdentifiers()
    {
        var vm = CreateViewModel();
        vm.AddCustomPointCommand.Execute(null);
        vm.AddCustomPathCommand.Execute(null);
        vm.ClearCustomArtifactsCommand.Execute(null);

        vm.AddCustomPointCommand.Execute(null);
        vm.AddCustomPathCommand.Execute(null);

        Assert.Equal(["custom-point-2", "custom-path-2"], vm.CustomRecipeRows.Select(static row => row.Key));
    }

    [Fact]
    public void Sort_ChangingAndChangedInputEventsStillExposeOldRows()
    {
        var vm = CreateViewModel();
        var rows = vm.ArtifactRows;
        var events = new List<(string Event, string Key, bool Descending)>();
        vm.PropertyChanging += (_, e) =>
        {
            if (e.PropertyName is not ("ArtifactSortKey" or "ArtifactSortDescending")) return;
            Assert.Same(rows, vm.ArtifactRows);
            events.Add(($"changing:{e.PropertyName}", vm.ArtifactSortKey, vm.ArtifactSortDescending));
        };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not ("ArtifactSortKey" or "ArtifactSortDescending")) return;
            Assert.Same(rows, vm.ArtifactRows);
            events.Add(($"changed:{e.PropertyName}", vm.ArtifactSortKey, vm.ArtifactSortDescending));
        };

        vm.SortArtifactRowsCommand.Execute("Label");
        Assert.NotSame(rows, vm.ArtifactRows);
        rows = vm.ArtifactRows;
        vm.SortArtifactRowsCommand.Execute("Label");

        Assert.Equal([("changing:ArtifactSortKey", "Default", false), ("changed:ArtifactSortKey", "Label", false),
            ("changing:ArtifactSortDescending", "Label", false), ("changed:ArtifactSortDescending", "Label", true)], events);
        Assert.NotSame(rows, vm.ArtifactRows);
    }

    [Fact]
    public void Rebuild_SelectionChangingSeesNewRenderAndOldSelectionBeforeStatus()
    {
        var vm = CreateViewModel();
        vm.SelectedArtifactRow = Assert.Single(vm.ArtifactRows, static row => row.Key == "h-2");
        var selection = vm.SelectedArtifactRow;
        var snapshot = vm.Snapshot;
        var status = vm.StatusText;
        var events = new List<string>();
        vm.PropertyChanging += (sender, e) =>
        {
            if (e.PropertyName != "SelectedArtifactRow") return;
            Assert.Same(vm, sender);
            Assert.NotSame(snapshot, vm.Snapshot);
            Assert.Same(selection, vm.SelectedArtifactRow);
            Assert.Equal(status, vm.StatusText);
            events.Add("selection changing");
        };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "SelectedArtifactRow")
            {
                Assert.NotSame(selection, vm.SelectedArtifactRow);
                Assert.Same(Assert.Single(vm.ArtifactRows, static row => row.Key == "h-2"), vm.SelectedArtifactRow);
                Assert.Equal(status, vm.StatusText);
                events.Add("selection changed");
            }
            if (e.PropertyName is "StatusText" or "Snapshot") events.Add(e.PropertyName);
        };

        vm.HorizontalGuideCount = 3m;

        Assert.Equal(["selection changing", "selection changed", "StatusText", "Snapshot"], events);
    }

    [Fact]
    public async Task UnavailableHosts_KeepMessagesAndDetailSelectionAsync()
    {
        var vm = CreateViewModel();
        await vm.CopySelectedArtifactCommand.ExecuteAsync(null);
        Assert.Equal("Select a coordinate row before copying a selected artifact.", vm.StatusText);
        await vm.CopyAllArtifactsCommand.ExecuteAsync(null);
        Assert.Equal("Coordinate clipboard copy is unavailable in the current host.", vm.StatusText);
        await vm.ExportArtifactsCsvCommand.ExecuteAsync(null);
        Assert.Equal("Coordinate text export is unavailable in the current host.", vm.StatusText);
        await vm.ExportArtifactsJsonCommand.ExecuteAsync(null);
        Assert.Equal("Coordinate text export is unavailable in the current host.", vm.StatusText);
        await vm.ExportPreviewPngCommand.ExecuteAsync(null);
        Assert.Equal("Coordinate image export is unavailable in the current host.", vm.StatusText);
        var row = vm.ArtifactRows[0];

        await vm.ShowArtifactDetailCommand.ExecuteAsync(row);
        await vm.ShowArtifactDetailCommand.ExecuteAsync(null);

        Assert.Same(row, vm.SelectedArtifactRow);
        Assert.Equal("Coordinate image export is unavailable in the current host.", vm.StatusText);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HostResults_KeepCopyExportAndPreviewOutcomesAsync(bool success)
    {
        var vm = CreateViewModel();
        var exports = new List<CoordinatePlannerTextExportRequest>();
        var copies = new List<string>();
        vm.RequestSetClipboardTextAsync = text => { copies.Add(text); return Task.FromResult(success); };
        vm.RequestSaveTextFileAsync = request => { exports.Add(request); return Task.FromResult(success); };
        vm.RequestExportPreviewPngAsync = () => Task.FromResult(success);
        vm.SelectedArtifactRow = vm.ArtifactRows[0];
        await vm.CopySelectedArtifactCommand.ExecuteAsync(null);
        Assert.Equal(success ? "Copied 1 selected coordinate row." : "Coordinate clipboard copy failed.", vm.StatusText);
        await vm.CopyAllArtifactsCommand.ExecuteAsync(null);
        Assert.Equal(success ? $"Copied {vm.ArtifactRows.Count} coordinate row(s)." : "Coordinate clipboard copy failed.", vm.StatusText);
        await vm.ExportArtifactsCsvCommand.ExecuteAsync(null);
        Assert.Equal(success ? $"Exported CSV {vm.ArtifactRows.Count} coordinate row(s)." : "Coordinate CSV export cancelled.", vm.StatusText);
        await vm.ExportArtifactsJsonCommand.ExecuteAsync(null);
        Assert.Equal(success ? $"Exported JSON {vm.ArtifactRows.Count} coordinate row(s)." : "Coordinate JSON export cancelled.", vm.StatusText);
        await vm.ExportPreviewPngCommand.ExecuteAsync(null);
        Assert.Equal(success ? "Coordinate preview exported." : "Coordinate preview export cancelled.", vm.StatusText);
        Assert.Equal(2, copies.Count);
        Assert.Equal(["coordinate_artifacts.csv", "coordinate_artifacts.json"], exports.Select(static request => request.SuggestedFileName));
        Assert.All(exports, static request => Assert.NotEmpty(request.Content));
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("csv")]
    [InlineData("json")]
    [InlineData("preview")]
    [InlineData("detail")]
    public async Task HostExceptions_PropagateWithoutReplacingStatusAsync(string action)
    {
        var vm = CreateViewModel();
        var status = vm.StatusText;
        vm.RequestSetClipboardTextAsync = _ => Task.FromException<bool>(new InvalidOperationException("host failure"));
        vm.RequestSaveTextFileAsync = _ => Task.FromException<bool>(new InvalidOperationException("host failure"));
        vm.RequestExportPreviewPngAsync = () => Task.FromException<bool>(new InvalidOperationException("host failure"));
        vm.RequestArtifactDetailAsync = _ => Task.FromException(new InvalidOperationException("host failure"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => action switch
        {
            "copy" => vm.CopyAllArtifactsCommand.ExecuteAsync(null),
            "csv" => vm.ExportArtifactsCsvCommand.ExecuteAsync(null),
            "json" => vm.ExportArtifactsJsonCommand.ExecuteAsync(null),
            "preview" => vm.ExportPreviewPngCommand.ExecuteAsync(null),
            _ => vm.ShowArtifactDetailCommand.ExecuteAsync(vm.ArtifactRows[0]),
        });

        Assert.Equal("host failure", exception.Message);
        Assert.Equal(status, vm.StatusText);
    }

    [Fact]
    public async Task DetachingCallbacks_PreservesOtherCallbacksCommandsAndWorkspaceStateAsync()
    {
        var vm = CreateViewModel();
        var rows = vm.ArtifactRows;
        var command = vm.CopyAllArtifactsCommand;
        Func<CoordinateArtifactRow, Task> detail = row =>
        {
            Assert.Same(row, vm.SelectedArtifactRow);
            return Task.CompletedTask;
        };
        Func<Task<bool>> preview = () => Task.FromResult(true);
        Func<string, Task<bool>> copy = _ => Task.FromResult(true);
        Func<CoordinatePlannerTextExportRequest, Task<bool>> save = _ => Task.FromResult(true);
        vm.RequestArtifactDetailAsync = detail;
        vm.RequestExportPreviewPngAsync = preview;
        vm.RequestSetClipboardTextAsync = copy;
        vm.RequestSaveTextFileAsync = save;
        await vm.ShowArtifactDetailCommand.ExecuteAsync(rows[0]);

        vm.RequestArtifactDetailAsync = null;
        Assert.Same(preview, vm.RequestExportPreviewPngAsync);
        Assert.Same(copy, vm.RequestSetClipboardTextAsync);
        Assert.Same(save, vm.RequestSaveTextFileAsync);
        vm.RequestExportPreviewPngAsync = null;
        Assert.Same(copy, vm.RequestSetClipboardTextAsync);
        Assert.Same(save, vm.RequestSaveTextFileAsync);
        vm.RequestSetClipboardTextAsync = null;
        Assert.Same(save, vm.RequestSaveTextFileAsync);
        vm.RequestSaveTextFileAsync = null;

        Assert.Same(rows, vm.ArtifactRows);
        Assert.Same(rows[0], vm.SelectedArtifactRow);
        Assert.Same(command, vm.CopyAllArtifactsCommand);
    }

    private static CoordinatePlannerWorkspaceViewModel CreateViewModel() => new(
        new FreeformHelper.UI.Services.CoordinatePlannerWorkspaceUseCase(),
        new FreeformHelper.UI.Services.CoordinatePlannerWorkspaceSession(
            TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10d, cellHeight: 5d), [], [],
            SourceRevision: 0, DefaultMachineWidth: 100d, DefaultMachineHeight: 50d,
            DefaultPixelWidth: 1000, DefaultPixelHeight: 500));
}

// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfExportContentEditsTests
{
    [AvaloniaFact]
    public async Task ExportDxfVisibleCommand_RotatedPad_WritesCurrentVerticesOnceWithoutOriginalVerticesAsync()
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await fixture.OpenAsync();
        var original = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha");
        await SelectCadPadsAsync(vm, [original.Id]);
        vm.DxfEditRotationDegrees = 30m;
        vm.RotateSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var rotated = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha");
        Assert.Equal(1, vm.RotatedCadPadCount);

        var text = await ExportAsync(vm, files.PathFor("rotated.dxf"), visibleOnly: true);

        var pads = ReadExportedPads(text);
        Assert.Equal(2, pads.Count);
        var exported = Assert.Single(pads, static pad => pad.Layer == "alpha");
        AssertVerticesEqual(rotated.Polygon.Vertices, exported.Vertices);
        Assert.DoesNotContain(exported.Vertices, point => original.Polygon.Vertices.Any(vertex => vertex.DistanceTo(point) < 1e-8));
        Assert.Single(pads, static pad => pad.Layer == "beta");
        Assert.Equal("DXF exported (visible). Pads=2.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task ExportDxfCommands_RestoredExactDuplicate_WriteOneEntityPerActivePadWithoutExtraCopiesAsync()
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await OpenPadsAsync(fixture,
            ("alpha", 0, 0, 10, 10), ("alpha", 0, 0, 10, 10), ("beta", 20, 0, 30, 10));
        var canonical = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha");
        var initialText = await ExportAsync(vm, files.PathFor("duplicates.dxf"), visibleOnly: true);
        Assert.Equal(2, ReadExportedPads(initialText).Count);
        Assert.Single(ReadExportedPads(initialText), static pad => pad.Layer == "alpha");
        DxfEditChangeListViewModel? openedList = null;
        vm.OpenDxfEditChangeListAsync = list =>
        {
            openedList = list;
            return Task.CompletedTask;
        };
        await vm.OpenDuplicateDxfEditChangeListCommand.ExecuteAsync(null);
        var changeList = Assert.IsType<DxfEditChangeListViewModel>(openedList);
        changeList.SelectedEntry = Assert.Single(changeList.Rows);
        changeList.ApplySelectedEntryCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(3, vm.CadPads.Count);

        var visibleText = await ExportAsync(vm, files.PathFor("duplicates.dxf"), visibleOnly: true);
        var allText = await ExportAsync(vm, files.PathFor("duplicates.dxf"), visibleOnly: false);

        AssertDuplicateContent(visibleText, canonical.Polygon.Vertices);
        AssertDuplicateContent(allText, canonical.Polygon.Vertices);
        Assert.Equal("DXF exported (all). Pads=3.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task ExportDxfVisibleCommand_RestoreLastDeletedPad_WritesRestoredGeometryExactlyOnceAsync()
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await fixture.OpenAsync();
        var original = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha");
        await SelectCadPadsAsync(vm, [original.Id]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.DoesNotContain(vm.CadPads, pad => pad.Id == original.Id);
        Assert.Equal(1, vm.DeletedCadPadCount);
        vm.RestoreLastDeletedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(0, vm.DeletedCadPadCount);

        var text = await ExportAsync(vm, files.PathFor("restored.dxf"), visibleOnly: true);

        var pads = ReadExportedPads(text);
        Assert.Equal(2, pads.Count);
        AssertVerticesEqual(original.Polygon.Vertices, Assert.Single(pads, static pad => pad.Layer == "alpha").Vertices);
        Assert.Single(pads, static pad => pad.Layer == "beta");
        Assert.Equal("DXF exported (visible). Pads=2.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task ExportDxfCommands_UnselectedLayers_VisibleOmitsAllIncludesSurvivorsBothExcludeHiddenPadsAsync()
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await OpenPadsAsync(fixture,
            ("alpha", 0, 0, 10, 10), ("beta", 20, 0, 30, 10), ("gamma", 40, 0, 50, 10));
        var hidden = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha");
        var unselected = Assert.Single(vm.CadPads, static pad => pad.Layer == "gamma");
        await SelectCadPadsAsync(vm, [hidden.Id]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        vm.LayerToggles.Single(static layer => layer.Name == "alpha").IsSelected = false;
        vm.LayerToggles.Single(static layer => layer.Name == "gamma").IsSelected = false;
        await WaitForGridRebuildAsync(vm);
        Assert.Empty(vm.SelectedCadPadIds);
        var visible = Assert.Single(vm.CadPads);
        var path = files.PathFor("modes.dxf");

        var visibleText = await ExportAsync(vm, path, visibleOnly: true);

        var visiblePad = Assert.Single(ReadExportedPads(visibleText));
        Assert.Equal("beta", visiblePad.Layer);
        AssertVerticesEqual(visible.Polygon.Vertices, visiblePad.Vertices);
        Assert.Equal("DXF exported (visible). Pads=1.", vm.StatusText);

        var allText = await ExportAsync(vm, path, visibleOnly: false);

        var allPads = ReadExportedPads(allText);
        Assert.Equal(2, allPads.Count);
        Assert.DoesNotContain(allPads, static pad => pad.Layer == "alpha");
        AssertVerticesEqual(visible.Polygon.Vertices, Assert.Single(allPads, static pad => pad.Layer == "beta").Vertices);
        AssertVerticesEqual(unselected.Polygon.Vertices, Assert.Single(allPads, static pad => pad.Layer == "gamma").Vertices);
        Assert.Equal("DXF exported (all). Pads=2.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task ExportDxfVisibleCommand_RoundTripThroughAppLoader_PreservesPadCountsLayersAndEditedGeometryAsync()
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await OpenPadsAsync(fixture,
            ("alpha", 0, 0, 10, 20), ("beta", 30, 0, 40, 10), ("beta", 50, 0, 60, 10));
        var alpha = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha");
        await SelectCadPadsAsync(vm, [alpha.Id]);
        vm.DxfEditRotationDegrees = 90m;
        vm.RotateSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var expected = vm.CadPads.ToArray();
        var path = files.PathFor("round-trip.dxf");

        var text = await ExportAsync(vm, path, visibleOnly: true);
        var reloaded = new DxfImportService().ImportFromPath(path, new DxfImportOptions());

        Assert.Equal(expected.Length, ReadExportedPads(text).Count);
        Assert.Equal(expected.Length, reloaded.Pads.Count);
        Assert.Equal(expected.Select(static pad => pad.Layer).Order(StringComparer.Ordinal),
            reloaded.Pads.Select(static pad => pad.Layer).Order(StringComparer.Ordinal));
        AssertVerticesEqual(Assert.Single(expected, static pad => pad.Layer == "alpha").Polygon.Vertices,
            Assert.Single(reloaded.Pads, static pad => pad.Layer == "alpha").Polygon.Vertices);
    }

    [AvaloniaFact]
    public async Task ExportDxfVisibleCommand_ReadOnlyTarget_ReportsFailurePreservesFileAndLeavesNoPartialOutputAsync()
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await fixture.OpenAsync();
        var path = files.PathFor("read-only.dxf");
        var original = await ExportAsync(vm, path, visibleOnly: true);
        await SelectCadPadsAsync(vm, [Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha").Id]);
        vm.DxfEditRotationDegrees = 30m;
        vm.RotateSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        using var readOnly = IoFailureScope.ApplyReadOnlyProtection(path);
        var before = files.Snapshot();

        await vm.ExportDxfVisibleCommand.ExecuteAsync(null);

        Assert.StartsWith("Export DXF failed", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains(path, vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(original, await TestFiles.ReadContentAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(before, files.Snapshot());
    }

    private static async Task<FreeformHelperViewModel> OpenPadsAsync(WorkspaceFixture fixture,
        params (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] pads)
    {
        var path = CreateTempDxfFile(pads);
        try
        {
            var vm = fixture.CreateViewModel();
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            return vm;
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<string> ExportAsync(FreeformHelperViewModel vm, string path, bool visibleOnly)
    {
        vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(path);
        var command = visibleOnly ? vm.ExportDxfVisibleCommand : vm.ExportDxfAllCommand;
        await command.ExecuteAsync(null);
        return await TestFiles.ReadContentAsync(path, TestContext.Current.CancellationToken);
    }

    private static List<(string Layer, List<Point2> Vertices)> ReadExportedPads(string text)
    {
        var pads = new List<(string Layer, List<Point2> Vertices)>();
        using var reader = new StringReader(text);
        List<Point2>? vertices = null;
        double x = 0;
        while (reader.ReadLine() is { } code && reader.ReadLine() is { } value)
        {
            switch (code.Trim())
            {
                case "0":
                    vertices = value == "LWPOLYLINE" ? [] : null;
                    if (vertices is not null)
                    {
                        pads.Add((string.Empty, vertices));
                    }
                    break;
                case "8" when vertices is not null:
                    pads[^1] = (value, vertices);
                    break;
                case "10" when vertices is not null:
                    x = double.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "20" when vertices is not null:
                    vertices.Add(new Point2(x, double.Parse(value, CultureInfo.InvariantCulture)));
                    break;
            }
        }

        return pads;
    }

    private static void AssertVerticesEqual(IReadOnlyList<Point2> expected, IReadOnlyList<Point2> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index].X, actual[index].X, precision: 8);
            Assert.Equal(expected[index].Y, actual[index].Y, precision: 8);
        }
    }

    private static void AssertDuplicateContent(string text, IReadOnlyList<Point2> expected)
    {
        var pads = ReadExportedPads(text);
        Assert.Equal(3, pads.Count);
        var duplicates = pads.Where(static pad => pad.Layer == "alpha").ToArray();
        Assert.Equal(2, duplicates.Length);
        Assert.All(duplicates, pad => AssertVerticesEqual(expected, pad.Vertices));
        Assert.Single(pads, static pad => pad.Layer == "beta");
    }
}

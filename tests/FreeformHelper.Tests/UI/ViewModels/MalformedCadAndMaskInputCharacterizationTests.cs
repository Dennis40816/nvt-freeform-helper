// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.DxfPadImporterMalformedInputTests;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class MalformedCadAndMaskInputCharacterizationTests
{
    [AvaloniaFact]
    public async Task OpenDxf_EmptyHeaderOnlyOrUnsupportedOnly_ReportsNoUsablePadsAndKeepsPreviousStateAsync()
    {
        string[][] documents =
        [
            [],
            [.. SectionHeader("HEADER"), .. SectionEnd(), "0", "EOF"],
            [.. SectionHeader("ENTITIES"), "0", "CIRCLE", "8", "UNSUPPORTED", "10", "4", "20", "5", "40", "2", .. SectionEnd(), "0", "EOF"],
        ];
        foreach (var lines in documents)
        {
            using var files = new IoFailureScope();
            var vm = await OpenBaselineAsync(files);
            vm.ShowCad = false;
            vm.NotchThresholdQ7 = 96m;
            var previousCad = FreeformHelperViewModelPrivateAccess.Cad(vm);
            var previousPads = vm.CadPads.ToArray();
            var previousGrid = FreeformHelperViewModelPrivateAccess.Grid(vm);
            var previousCatalog = FreeformHelperViewModelPrivateAccess.LayerCatalogState(vm).Catalog;
            var previousLayers = vm.LayerToggles.Select(static layer => (layer.Name, layer.IsSelected)).ToArray();
            var path = WriteDxf(lines);

            await OpenDxfAsync(vm, path);

            Assert.StartsWith("DXF import failed:", vm.StatusText, StringComparison.Ordinal);
            Assert.Contains("no usable pads", vm.StatusText, StringComparison.Ordinal);
            Assert.Contains(Path.GetFileName(path), vm.StatusText, StringComparison.Ordinal);
            Assert.Same(previousCad, FreeformHelperViewModelPrivateAccess.Cad(vm));
            Assert.Equal(previousPads, vm.CadPads);
            Assert.Same(previousGrid, FreeformHelperViewModelPrivateAccess.Grid(vm));
            Assert.Same(previousCatalog, FreeformHelperViewModelPrivateAccess.LayerCatalogState(vm).Catalog);
            Assert.Equal(previousLayers, vm.LayerToggles.Select(static layer => (layer.Name, layer.IsSelected)));
            Assert.False(vm.ShowCad);
            Assert.Equal(96m, vm.NotchThresholdQ7);
            Assert.True(vm.HasUnsavedChanges);
        }
    }

    [AvaloniaFact]
    public async Task OpenDxf_TruncatedAfterCompletePad_ReportsErrorAndKeepsPreviousStateAsync()
    {
        string[][] tails =
        [
            ["0", "LWPOLYLINE", "8", "PARTIAL", "70", "1", "10", "0", "20", "0", "10", "5"],
            ["0", "LWPOLYLINE", "8", "PARTIAL", "70", "1", "10", "0", "20", "0", "10", "5", "20"],
            ["0", "POLYLINE", "8", "PARTIAL", "70", "1", "0", "VERTEX", "10", "0", "20", "0", "0", "VERTEX", "10", "5", "20", "0", "0", "VERTEX", "10", "5", "20", "5"],
        ];
        foreach (var tail in tails)
        {
            using var files = new IoFailureScope();
            var vm = await OpenBaselineAsync(files);
            vm.ShowCad = false;
            vm.NotchThresholdQ7 = 96m;
            var previousCad = FreeformHelperViewModelPrivateAccess.Cad(vm);
            var previousPads = vm.CadPads.ToArray();
            var previousGrid = FreeformHelperViewModelPrivateAccess.Grid(vm);
            var previousCatalog = FreeformHelperViewModelPrivateAccess.LayerCatalogState(vm).Catalog;
            var previousLayers = vm.LayerToggles.Select(static layer => (layer.Name, layer.IsSelected)).ToArray();
            var path = WriteDxf([.. SectionHeader("ENTITIES"), .. ClosedSquare("PARTIAL", 40, 50, 2), .. tail]);

            await OpenDxfAsync(vm, path);

            Assert.StartsWith("DXF import failed:", vm.StatusText, StringComparison.Ordinal);
            Assert.Contains("incomplete", vm.StatusText, StringComparison.Ordinal);
            Assert.Same(previousCad, FreeformHelperViewModelPrivateAccess.Cad(vm));
            Assert.Equal(previousPads, vm.CadPads);
            Assert.Same(previousGrid, FreeformHelperViewModelPrivateAccess.Grid(vm));
            Assert.Same(previousCatalog, FreeformHelperViewModelPrivateAccess.LayerCatalogState(vm).Catalog);
            Assert.Equal(previousLayers, vm.LayerToggles.Select(static layer => (layer.Name, layer.IsSelected)));
            Assert.False(vm.ShowCad);
            Assert.Equal(96m, vm.NotchThresholdQ7);
        }
    }

    [AvaloniaFact]
    public async Task OpenDxf_NonNumericCoordinate_ReportsGroupCodeAndKeepsPreviousGeometryLayersAndSettingsAsync()
    {
        using var files = new IoFailureScope();
        var vm = await OpenBaselineAsync(files);
        vm.ShowCad = false;
        vm.NotchThresholdQ7 = 96m;
        var previousPads = vm.CadPads.ToArray();
        var previousVertices = previousPads.SelectMany(static pad => pad.Polygon.Vertices).ToArray();
        var previousGrid = FreeformHelperViewModelPrivateAccess.Grid(vm);
        var previousLayers = vm.LayerToggles.Select(static layer => (layer.Name, layer.IsSelected)).ToArray();
        var path = WriteDxf(
        [
            .. SectionHeader("ENTITIES"), .. ClosedSquare("REPLACEMENT", 40, 50, 2),
            "0", "LWPOLYLINE", "8", "BROKEN", "70", "1", "10", "abc", "20", "0", .. SectionEnd(), "0", "EOF",
        ]);

        await OpenDxfAsync(vm, path);

        Assert.Equal("DXF import failed: Invalid DXF number for group code 10: 'abc'.", vm.StatusText);
        Assert.Equal(previousPads, vm.CadPads);
        Assert.Equal(previousVertices, vm.CadPads.SelectMany(static pad => pad.Polygon.Vertices));
        Assert.Same(previousGrid, FreeformHelperViewModelPrivateAccess.Grid(vm));
        Assert.Equal(previousLayers, vm.LayerToggles.Select(static layer => (layer.Name, layer.IsSelected)));
        Assert.False(vm.ShowCad);
        Assert.Equal(96m, vm.NotchThresholdQ7);
        Assert.True(vm.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public async Task ImportMask_InvalidOrWrongShapeJson_ReportsFailureAndKeepsPreviousMaskEnabledStatePathAndPadsAsync()
    {
        foreach (var content in new[] { "not valid JSON", "{}", "[]" })
        {
            using var files = new IoFailureScope();
            var vm = await OpenBaselineAsync(files);
            var validPath = TestFiles.WriteTempCsv("Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,1,0\n");
            vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(validPath);
            await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);
            Assert.True(vm.RegularVisibilityMaskUi.IsEnabled);
            Assert.Equal(0, Assert.Single(vm.RegularVisibilityMaskForTests.Mask.ActiveRegularPadIds));
            var previousMask = vm.RegularVisibilityMaskForTests.Mask;
            var cad = vm.CadPads.ToArray();
            var regular = vm.RegularPads.ToArray();
            var grid = FreeformHelperViewModelPrivateAccess.Grid(vm);
            var invalidPath = files.PathFor("bad-mask.json");
            TestFiles.WriteText(invalidPath, content);
            vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(invalidPath);

            await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);

            Assert.Equal("Regular visibility mask: failed to load bad-mask.json. Ignored. CSV does not contain any frames.", vm.StatusText);
            Assert.Equal(vm.StatusText, vm.RegularVisibilityMaskUi.Summary);
            Assert.True(vm.RegularVisibilityMaskUi.IsEnabled);
            var mask = vm.RegularVisibilityMaskForTests.Mask;
            Assert.Same(previousMask, mask);
            Assert.Equal(RegularVisibilityMaskStatus.Loaded, mask.Status);
            Assert.Equal(validPath, mask.SourcePath);
            Assert.Equal(validPath, vm.RegularVisibilityMaskForTests.SourcePath);
            Assert.Equal(0, Assert.Single(mask.ActiveRegularPadIds));
            Assert.Equal(1, Assert.Single(mask.InactiveRegularPadIds));
            Assert.Equal(cad, vm.CadPads);
            Assert.Equal(regular, vm.RegularPads);
            Assert.Same(grid, FreeformHelperViewModelPrivateAccess.Grid(vm));
        }
    }

    private static async Task<FreeformHelperViewModel> OpenBaselineAsync(IoFailureScope files)
    {
        var vm = files.CreateViewModel();
        vm.CascadeNum = 1;
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 2;
        cascade.YChannels = 1;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        await WaitForGridRebuildAsync(vm);
        var path = CreateTempDxfFile(("PREVIOUS", 0, 0, 10, 10), ("PREVIOUS", 20, 0, 30, 10));
        await OpenDxfAsync(vm, path);
        Assert.Equal(2, vm.CadPads.Count);
        Assert.Equal(2, vm.RegularPads.Count);
        Assert.Equal("Ready.", vm.StatusText);
        return vm;
    }

    private static async Task<List<string>> OpenDxfAsync(FreeformHelperViewModel vm, string path)
    {
        var statuses = new List<string>();
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
        void RecordStatus(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(vm.StatusText))
            {
                statuses.Add(vm.StatusText);
            }
        }

        vm.PropertyChanged += RecordStatus;
        try
        {
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            return statuses;
        }
        finally
        {
            vm.PropertyChanged -= RecordStatus;
        }
    }

    private static string WriteDxf(IEnumerable<string> lines)
    {
        var path = TestFiles.NewTempFilePath("malformed-ui-", ".dxf");
        TestFiles.WriteBytes(path, ToBytes(lines));
        return path;
    }
}

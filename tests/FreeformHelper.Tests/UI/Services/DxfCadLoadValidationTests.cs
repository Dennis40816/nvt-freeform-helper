// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;
using static FreeformHelper.Tests.DxfPadImporterMalformedInputTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfCadLoadValidationTests
{
    [AvaloniaFact]
    public async Task OpenDxf_NoPreviousCad_ReportsNoUsablePadsAsync()
    {
        using var files = new IoFailureScope();
        var vm = files.CreateViewModel();
        await vm.WaitForGridRebuildIdleAsync();
        var path = files.PathFor("empty.dxf");
        TestFiles.WriteBytes(path, []);
        var previousCad = FreeformHelperViewModelPrivateAccess.Cad(vm);
        var wasUnsaved = vm.HasUnsavedChanges;
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);

        await vm.OpenDxfCommand.ExecuteAsync(null);
        await vm.WaitForGridRebuildIdleAsync();

        Assert.StartsWith("DXF import failed:", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("no usable pads", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("empty.dxf", vm.StatusText, StringComparison.Ordinal);
        Assert.Same(previousCad, FreeformHelperViewModelPrivateAccess.Cad(vm));
        Assert.Empty(vm.CadPads);
        Assert.Equal(wasUnsaved, vm.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void CadLoad_PathAndEmbedded_RejectBadInputAndAcceptCompleteEntitiesWithoutEof()
    {
        using var files = new IoFailureScope();
        var loader = new CadLoadUseCase(new DxfImportService());
        var options = new DxfImportOptions();
        string[][] invalidDocuments =
        [
            [.. SectionHeader("HEADER"), .. SectionEnd(), "0", "EOF"],
            [.. SectionHeader("ENTITIES"), .. ClosedSquare("PAD", 0, 0, 2), "0", "CIRCLE", "10"],
            [.. SectionHeader("ENTITIES"), .. ClosedSquare("PAD", 0, 0, 2), "0", "CIRCLE", "10", "3"],
            [.. SectionHeader("ENTITIES"), .. ClosedSquare("PAD", 0, 0, 2),
                "0", "POLYLINE", "70", "1", "0", "VERTEX", "10", "0", "20", "0", .. SectionEnd(), "0", "EOF"],
        ];
        var path = files.PathFor("bad.dxf");
        foreach (var lines in invalidDocuments)
        {
            var bytes = ToBytes(lines);
            TestFiles.WriteBytes(path, bytes);

            Assert.Throws<InvalidDataException>(() => loader.TryLoadFromPath(path, options));
            Assert.Throws<InvalidDataException>(() => loader.TryLoadFromEmbedded(bytes, options));
        }

        var validBytes = ToBytes([.. SectionHeader("ENTITIES"), .. ClosedSquare("PAD", 0, 0, 2), .. SectionEnd()]);
        TestFiles.WriteBytes(path, validBytes);

        var fromPath = loader.TryLoadFromPath(path, options);
        var fromEmbedded = loader.TryLoadFromEmbedded(validBytes, options);

        Assert.NotNull(fromPath);
        Assert.NotNull(fromEmbedded);
        var pad = Assert.Single(fromPath.Cad.Pads);
        Assert.Equal("PAD", pad.Layer);
        Assert.Equal(pad.Polygon.Vertices, Assert.Single(fromEmbedded.Cad.Pads).Polygon.Vertices);
    }
}

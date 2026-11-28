// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class AlgorithmExportBoundsTests
{
    [Theory]
    [InlineData(NotchAlgorithmVersion.V21, 0)]
    [InlineData(NotchAlgorithmVersion.V21, 1)]
    [InlineData(NotchAlgorithmVersion.V21, 2)]
    [InlineData(NotchAlgorithmVersion.V22, 0)]
    [InlineData(NotchAlgorithmVersion.V22, 1)]
    [InlineData(NotchAlgorithmVersion.V22, 2)]
    public void ExportRejectsDiffAtIcCellCount(NotchAlgorithmVersion version, int slot)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(2, 3, icIndexSelector: (_, col) => col < 2 ? 0 : 1);
        var settings = new ProjectSettings { Grid = new GridSettings { XChannels = 3, YChannels = 2, CascadeNum = 2, PerIcXChannels = [2, 1] } };
        var row = Row(version, slot == 0 ? 4 : 0, slot == 1 ? 4 : 1);
        row.Values[version == NotchAlgorithmVersion.V21 ? 6 : 4] = slot == 2 ? 4 : 65535;
        row.Values[version == NotchAlgorithmVersion.V21 ? 7 : 5] = 1;
        row.Values[version == NotchAlgorithmVersion.V21 ? 8 : 5] = 32;
        row = new NotchTableRow(version, row.IcIndex, row.DiffIndex, row.RegularPadIndex, row.CadPadId, row.Values, row.Comment);

        var error = Assert.Throws<InvalidOperationException>(() => Export(row, grid, settings));

        Assert.Contains("rows × columns (4)", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21)]
    [InlineData(NotchAlgorithmVersion.V22)]
    public void ExportAcceptsLastDiffAndNullTargets(NotchAlgorithmVersion version)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(2, 2);

        var text = Export(Row(version, 3, 65535), grid, new ProjectSettings());

        Assert.Contains("castNHC_TABLE_IC1", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21)]
    [InlineData(NotchAlgorithmVersion.V22)]
    public void ExportRejectsOutOfGridSourceEvenWhenItsRowIsANoOp(NotchAlgorithmVersion version)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(2, 2);
        var row = new NotchTableRow(version, 0, 4, 0, null, version == NotchAlgorithmVersion.V21
            ? [4, 100, 100, 65535, 0, 0, 65535, 0, 0]
            : [4, 100, 65535, 0, 65535, 0, 0], "no-op");

        var error = Assert.Throws<InvalidOperationException>(() => Export(row, grid, new ProjectSettings()));

        Assert.Contains("rows × columns (4)", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.00000001, false)]
    [InlineData(0, true)]
    public void ExportCacheUsesExactCadCoordinates(double shift, bool expectedHit)
    {
        var cache = new NotchExportGenerationCacheService();
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(1, "PAD", 0, 0, 1, 1)]);
        var changed = new CadPadSet([TestGeometryFactory.CreateCadPad(1, "PAD", shift, 0, 1 + shift, 1)]);
        cache.Store(cad, grid, 0, 0, new NotchTable([]));

        var hit = cache.TryGet(changed, grid, 0, 0, out _);

        Assert.Equal(expectedHit, hit);
    }

    private static NotchTableRow Row(NotchAlgorithmVersion version, int source, int target) =>
        new(version, 0, source, 0, 10, version == NotchAlgorithmVersion.V21
            ? [source, 100, 110, target, 1, 32, 65535, 0, 0]
            : [source, 110, target, 25, 65535, 0, 0], "bounds");

    private static string Export(NotchTableRow row, RegularGrid grid, ProjectSettings settings) =>
        NotchTableExporter.ExportAsCInitializer(new NotchTable(new[] { row }), new CadPadSet([]), grid, settings);
}

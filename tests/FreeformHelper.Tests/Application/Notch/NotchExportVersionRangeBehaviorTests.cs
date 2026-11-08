// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportVersionRangeBehaviorTests
{
    [Fact]
    public void ExportAsCInitializer_MixedVersions_RequiresSeparateFirmwareTables()
    {
        using var culture = TestCulture.UseEnUs();
        var v21 = new NotchTableRow(NotchAlgorithmVersion.V21, 0, 0, 0, 42,
            [0, 100, 110, 1, 1, 32, ushort.MaxValue, 0, 0], "legacy");
        var v22 = new NotchTableRow(0, 0, 0, 42,
            new NotchV22Node(0, 110, 1, 25, ushort.MaxValue, 0, 0), "current");
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(42, "Copper", 0, 0, 1, 1)]);
        var grid = TestGeometryFactory.CreateRegularGrid(1, 2);
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                NullValue = ushort.MaxValue,
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
            },
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            NotchTableExporter.ExportAsCInitializer(new NotchTable([v21, v22]), cad, grid, settings));

        Assert.Equal(
            "C export is version-specific. Select C v2.1 or C v2.2 so the generated FW file has one table layout.",
            error.Message);
        var legacyText = NotchTableExporter.ExportAsCInitializer(new NotchTable([v21]), cad, grid, settings);
        var currentText = NotchTableExporter.ExportAsCInitializer(new NotchTable([v22]), cad, grid, settings);
        Assert.Contains("Notch v2.1 compensation export", legacyText, StringComparison.Ordinal);
        Assert.Contains("Notch v2.2 compensation export", currentText, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCInitializer_EmptyGrid_UsesTableIcIndicesWithoutDroppingTheRow()
    {
        using var culture = TestCulture.UseEnUs();
        var row = new NotchTableRow(2, 17, 0, null,
            new NotchV22Node(7, 80, ushort.MaxValue, 0, ushort.MaxValue, 0, 0), "table-only");
        var grid = new RegularGrid(0, 0, [0d], [0d], []);
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings { NullValue = ushort.MaxValue },
        };

        var text = NotchTableExporter.ExportAsCInitializer(
            new NotchTable([row]), new CadPadSet([]), grid, settings);

        Assert.Contains("#define USER_NHC_IC_NUM          (3u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM_IC1 (0u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM_IC2 (0u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM_IC3 (1u)", text, StringComparison.Ordinal);
        Assert.Contains("// table-only; NODE KEEP=80% MOVE=0%", text, StringComparison.Ordinal);
    }
}

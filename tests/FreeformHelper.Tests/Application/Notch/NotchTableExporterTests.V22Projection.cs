using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchTableExporterTests
{
    [Fact]
    public void ExportAndSimulation_V22Projection_PreservesSourceDiagnosticsWhileDroppingInvalidFirmwareIc()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10]);
        var table = new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 94,
                v22Node: new NotchV22Node(10, 150, 99, 50, 65535, 0, 0),
                comment: "valid-ic"),
            new(
                icIndex: -1,
                diffIndex: 10,
                regularPadIndex: 1,
                cadPadId: 95,
                v22Node: new NotchV22Node(10, 100, 65535, 0, 65535, 0, 0),
                comment: "invalid-ic"),
        });
        var projection = CreateRuntimeParityProjection(grid, [100d]);

        var simulation = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            [projection],
            table,
            NotchAlgorithmVersion.V22,
            65535));
        var text = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSet(),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Debug);

        Assert.Equal(
            [
                "Missing target diff IC1/diff99 in projected grid.",
                "Missing anchor diff IC0/diff10 in projected grid.",
            ],
            simulation.Diagnostics);
        var action = Assert.Single(simulation.Actions);
        var leg = Assert.Single(action.Legs);
        Assert.Equal((99, 50, 50d),
            (leg.TargetDiffIndex, leg.RatioPercent, leg.DeltaValue));
        Assert.Contains("#define USER_NHC_NODE_NUM        (1u)", text, StringComparison.Ordinal);
        Assert.Contains("valid-ic", text, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid-ic", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAndSimulation_V22Projection_DoesNotFabricateActionForDroppedInvalidIc()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10]);
        Assert.Single(grid.Pads).IcIndex = -1;
        var table = new NotchTable([
            new NotchTableRow(
                icIndex: -1,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 96,
                v22Node: new NotchV22Node(10, 50, 65535, 0, 65535, 0, 0),
                comment: "invalid-ic-source")
        ]);
        var projection = CreateRuntimeParityProjection(grid, [100d]);

        var simulation = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            [projection],
            table,
            NotchAlgorithmVersion.V22,
            65535));
        var text = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSet(),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Debug);

        Assert.Equal((100d, 100d, 0d),
            (Assert.Single(simulation.Cells).BeforeValue,
                simulation.Cells[0].AfterValue,
                simulation.Cells[0].DeltaValue));
        Assert.Empty(simulation.Actions);
        Assert.Empty(simulation.Diagnostics);
        Assert.Contains("#define USER_NHC_NODE_NUM        (0u)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid-ic-source", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCInitializer_EmitsPerIcDispatchTable_WhenRowsAreSparseAcrossIcs()
    {
        const int nullDiff = 65535;
        var table = new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 10,
                cadPadId: 100,
                v22Node: new NotchV22Node(10, 120, 20, 30, nullDiff, 0, 0),
                comment: "ic0-v22"),
            new(
                icIndex: 2,
                diffIndex: 30,
                regularPadIndex: 30,
                cadPadId: 300,
                v22Node: new NotchV22Node(30, 110, 31, 15, nullDiff, 0, 0),
                comment: "ic2-v22"),
        });

        var text = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSetWithIcCount(3),
            CreateGridWithIcCount(3),
            CreateProjectSettings(),
            NotchExportProfile.Debug);

        Assert.Contains("#define USER_NHC_IC_NUM          (3u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM_IC1 (1u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM_IC2 (0u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM_IC3 (1u)", text, StringComparison.Ordinal);
        Assert.Contains("static const ST_PRI_NHC_TABLE_NODE_INFO castNHC_TABLE_IC1[USER_NHC_NODE_NUM_IC1]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("static const ST_PRI_NHC_TABLE_NODE_INFO castNHC_TABLE_IC2", text, StringComparison.Ordinal);
        Assert.Contains("static const ST_PRI_NHC_TABLE_NODE_INFO castNHC_TABLE_IC3[USER_NHC_NODE_NUM_IC3]", text, StringComparison.Ordinal);
        Assert.Contains("{ NULL, USER_NHC_NODE_NUM_IC2 }, // IC2", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NotchExportProfile.Release)]
    [InlineData(NotchExportProfile.Debug)]
    public void ExportAsCInitializer_NeverEmitsFwBaseMask(NotchExportProfile profile)
    {
        var grid = CreateRuntimeParityGrid();
        var table = CreateRuntimeParityV22Table();

        var text = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSetWithIcCount(2),
            grid,
            CreateProjectSettings(),
            profile);

        Assert.DoesNotContain("// FW simulation base mask", text, StringComparison.Ordinal);
        Assert.DoesNotContain("USER_NHC_FW_MASK_NUM", text, StringComparison.Ordinal);
        Assert.DoesNotContain("USER_NHC_SIM_BASE_VALUE", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ST_PRI_NHC_FW_BASE_MASK_INFO", text, StringComparison.Ordinal);
        Assert.DoesNotContain("cau16NHC_FW_BASE_MASK", text, StringComparison.Ordinal);
        Assert.DoesNotContain("FwBaseMask", text, StringComparison.Ordinal);
        Assert.Contains("void FUNC_NHC_DiffCompensationByIc(UINT8 u8Ic)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCInitializer_EmitsFwStyleV22File_AndPreservesKnownFlagBits()
    {
        var settings = CreateProjectSettings();
        settings.Notch.CompensationModel = NotchCompensationModel.ConservativeNoGain;
        var table = new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 10,
                cadPadId: 100,
                v22Node: new NotchV22Node(10, 120, 20, 30, 30, -30, 3),
                comment: "flagged"),
        });

        var text = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSet(),
            CreateGrid(),
            settings,
            NotchExportProfile.Debug);

        Assert.DoesNotContain("#include <stdint.h>", text, StringComparison.Ordinal);
        Assert.Contains("#include \"notch.h\"", text, StringComparison.Ordinal);
        Assert.Contains("// Requires FW platform typedefs from notch.h / ap_gvariable.h: UINT8, UINT16, INT8, INT16, INT32.", text, StringComparison.Ordinal);
        Assert.Contains("UINT16 NHC_IDX;", text, StringComparison.Ordinal);
        Assert.Contains("INT8   NHC_1ST_RATIO;", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ST_PRI_NHC_TABLE_META_INFO", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PriNhc_GetMeta", text, StringComparison.Ordinal);
        Assert.Contains("// Generation metadata", text, StringComparison.Ordinal);
        Assert.Contains("//   CompensationModel: ConservativeNoGain", text, StringComparison.Ordinal);
        Assert.Contains("//   SourceCombineRule: C = sum(target regular coverage); ToFull excludes source gain", text, StringComparison.Ordinal);
        Assert.Contains("//   TargetAllocationRule: Target-regular coverage; legs use overlap/regular-area coverage, ToFull is support/cap/allowance.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ST_PRI_NHC_TABLE_EXPORT", text, StringComparison.Ordinal);
        Assert.Contains("static const ST_PRI_NHC_IC_TABLE_INFO castNHC_TABLE_BY_IC[USER_NHC_IC_NUM]", text, StringComparison.Ordinal);
        Assert.Contains("void FUNC_NHC_DiffCompensationByIc(UINT8 u8Ic)", text, StringComparison.Ordinal);
        Assert.Contains("void FUNC_NHC_DiffCompensation(void)", text, StringComparison.Ordinal);
        Assert.Contains("NHC_FLAG_CONTINUATION | NHC_FLAG_ANCHOR_OVERRIDE", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCInitializer_EmitsVersionSpecificFwFiles()
    {
        var v22OnlyTable = new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 10,
                cadPadId: 100,
                v22Node: new NotchV22Node(10, 120, 20, 30, 65535, 0, 0),
                comment: "v22-only"),
        });
        var v21OnlyTable = new NotchTable(new NotchTableRow[]
        {
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 10,
                cadPadId: 100,
                values: _v21OnlyPayload,
                comment: "v21-only"),
        });

        var v22OnlyText = NotchTableExporter.ExportAsCInitializer(
            v22OnlyTable,
            CreateCadSet(),
            CreateGrid(),
            CreateProjectSettings(),
            NotchExportProfile.Debug);
        var v21OnlyText = NotchTableExporter.ExportAsCInitializer(
            v21OnlyTable,
            CreateCadSet(),
            CreateGrid(),
            CreateProjectSettings(),
            NotchExportProfile.Debug);

        Assert.Contains("Notch v2.2 compensation export", v22OnlyText, StringComparison.Ordinal);
        Assert.Contains("UINT8  NHC_COMBINE;", v22OnlyText, StringComparison.Ordinal);
        Assert.DoesNotContain("UINT8  NHC_REGU_TO_FULL;", v22OnlyText, StringComparison.Ordinal);

        Assert.Contains("Notch v2.1 compensation export", v21OnlyText, StringComparison.Ordinal);
        Assert.Contains("UINT8  NHC_REGU_TO_FULL;", v21OnlyText, StringComparison.Ordinal);
        Assert.DoesNotContain("UINT8  NHC_COMBINE;", v21OnlyText, StringComparison.Ordinal);
    }
}

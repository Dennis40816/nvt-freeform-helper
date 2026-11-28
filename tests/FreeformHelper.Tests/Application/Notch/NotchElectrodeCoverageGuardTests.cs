// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchElectrodeCoverageGuardTests
{
    [Theory]
    [InlineData(NotchAlgorithmVersion.V22, true, 30)]
    [InlineData(NotchAlgorithmVersion.V21, true, 38)]
    [InlineData(NotchAlgorithmVersion.V22, false, 50)]
    [InlineData(NotchAlgorithmVersion.V21, false, 64)]
    public void OutsideElectrodeReservesCoverageOnlyWhenItHasAnOutput(
        NotchAlgorithmVersion version, bool assigned, int expectedTransfer)
    {
        var (cad, grid, settings, _) = CreateScenario(true, version);
        cad = new CadPadSet([cad.Pads[0], TestGeometryFactory.CreateCadPad(2, "PAD", 40, 0, 50, 10)]);
        var assignment = CadOutputFwDiffIndexAssignmentService.Assign(cad.Pads, grid, settings.Grid,
            CadOutputFwDiffAutoMode.BestMatchDirect, new Dictionary<int, int?> { [1] = 0, [2] = null },
            assigned ? new Dictionary<int, int> { [1] = 0, [2] = 1 } : null);
        var outputs = assignment.AssignedIndexByCadId.ToDictionary();
        Assert.Equal(assigned, outputs.ContainsKey(2));
        var generator = new NotchTableGenerator();
        var batch = generator.ResolveCadAllocationBatch(cad, grid, settings,
            cadOutputFwDiffIndexByCadId: outputs);

        var table = NotchTableGenerator.ProjectCadAllocationResolvedBatch(batch, settings);

        var row = Assert.Single(table.Rows);
        Assert.Equal(expectedTransfer, row.Values[version == NotchAlgorithmVersion.V22 ? 3 : 5]);
        foreach (var profile in new[] { NotchExportProfile.Debug, NotchExportProfile.Release })
        {
            Assert.NotEmpty(NotchTableExporter.ExportAsCInitializer(table, cad, grid, settings, profile));
        }
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21)]
    [InlineData(NotchAlgorithmVersion.V22)]
    public void UnassignedCadWithGeometryDoesNotReserveOriginalCoverage(NotchAlgorithmVersion version)
    {
        var (cad, grid, settings, outputs) = CreateScenario(true, version);
        cad = new CadPadSet([cad.Pads[0], TestGeometryFactory.CreateCadPad(2, "PAD", 10, 0, 20, 10)]);
        grid.Pads[1].MatchedCadPadId = 2;
        grid.Pads[2].MatchedCadPadId = null;
        outputs.Remove(2);

        var table = new NotchTableGenerator().Generate(cad, grid, settings,
            cadOutputFwDiffIndexByCadId: outputs);

        var row = Assert.Single(table.Rows);
        Assert.Equal(version == NotchAlgorithmVersion.V22 ? 50 : 64,
            row.Values[version == NotchAlgorithmVersion.V22 ? 3 : 5]);
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21)]
    [InlineData(NotchAlgorithmVersion.V22)]
    public void RowlessElectrodeWithoutIncomingShareDoesNotChangeExportBytes(NotchAlgorithmVersion version)
    {
        var (cad, grid, settings, outputs) = CreateScenario(true, version);
        cad = new CadPadSet([TestGeometryFactory.CreateCadPad(1, "PAD", 0, 0, 5, 10), cad.Pads[1]]);
        outputs[2] = 3;
        settings.Notch.TargetCoverageCapPercent = 90;
        var generator = new NotchTableGenerator();
        var enabled = generator.Generate(cad, grid, settings, cadOutputFwDiffIndexByCadId: outputs);
        settings.Notch.EnableTargetCoverageGuard = false;
        var disabled = generator.Generate(cad, grid, settings, cadOutputFwDiffIndexByCadId: outputs);
        Assert.Equal(Assert.Single(disabled.Rows).Values, Assert.Single(enabled.Rows).Values);
        foreach (var profile in new[] { NotchExportProfile.Debug, NotchExportProfile.Release })
        {
            settings.Notch.EnableTargetCoverageGuard = true;
            var guardedC = NotchTableExporter.ExportAsCInitializer(enabled, cad, grid, settings, profile);
            var unguardedC = NotchTableExporter.ExportAsCInitializer(disabled, cad, grid, settings, profile);
            Assert.Equal(unguardedC, guardedC);
        }
    }

    [Theory]
    [InlineData(true, NotchAlgorithmVersion.V22, 30)]
    [InlineData(false, NotchAlgorithmVersion.V22, 50)]
    [InlineData(true, NotchAlgorithmVersion.V21, 38)]
    [InlineData(false, NotchAlgorithmVersion.V21, 64)]
    public void RowlessTargetReservesOriginalCoverageOnlyForAssignedElectrode(
        bool hasElectrode, NotchAlgorithmVersion version, int expectedTransfer)
    {
        var (cad, grid, settings, outputs) = CreateScenario(hasElectrode, version);
        var generator = new NotchTableGenerator();
        var batch = generator.ResolveCadAllocationBatch(cad, grid, settings,
            cadOutputFwDiffIndexByCadId: outputs);

        var table = NotchTableGenerator.ProjectCadAllocationResolvedBatch(batch, settings);

        var row = Assert.Single(table.Rows);
        Assert.Equal(0, row.DiffIndex);
        Assert.Equal(1, row.Values[version == NotchAlgorithmVersion.V22 ? 2 : 3]);
        Assert.Equal(expectedTransfer, row.Values[version == NotchAlgorithmVersion.V22 ? 3 : 5]);
        foreach (var profile in new[] { NotchExportProfile.Debug, NotchExportProfile.Release })
        {
            Assert.NotEmpty(NotchTableExporter.ExportAsCInitializer(table, cad, grid, settings, profile));
        }
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21)]
    [InlineData(NotchAlgorithmVersion.V22)]
    public void NoGainKeepsExistingTransfersWithGuardEnabled(NotchAlgorithmVersion version)
    {
        var (cad, grid, settings, outputs) = CreateScenario(true, version);
        settings.Notch.CompensationModel = NotchCompensationModel.ConservativeNoGain;
        var generator = new NotchTableGenerator();
        var enabled = generator.Generate(cad, grid, settings, cadOutputFwDiffIndexByCadId: outputs);
        settings.Notch.EnableTargetCoverageGuard = false;

        var disabled = generator.Generate(cad, grid, settings, cadOutputFwDiffIndexByCadId: outputs);

        Assert.Equal(Assert.Single(disabled.Rows).Values, Assert.Single(enabled.Rows).Values);
    }

    [Fact]
    public void ReleaseNoOpFilteringKeepsTheOtherTargetsTransfer()
    {
        var (cad, grid, settings, outputs) = CreateScenario(true, NotchAlgorithmVersion.V22);
        grid.Pads[2].Freeform = FreeformType.XWay;
        outputs[2] = 2;
        var table = new NotchTableGenerator().Generate(cad, grid, settings,
            cadOutputFwDiffIndexByCadId: outputs);

        var debug = NotchV22ExportRows.Project(table.Rows, settings.Notch.NullValue, NotchExportProfile.Debug);
        var release = NotchV22ExportRows.Project(table.Rows, settings.Notch.NullValue, NotchExportProfile.Release);

        Assert.Equal(2, debug.Length);
        Assert.Single(release);
        Assert.Equal(50, release[0].V22Node!.TargetRatioPercent1);
        Assert.Equal(100, Assert.Single(debug, row => row.DiffIndex == 2).V22Node!.CombinePercent);
    }

    private static (CadPadSet Cad, RegularGrid Grid, ProjectSettings Settings, Dictionary<int, int> Outputs)
        CreateScenario(bool hasElectrode, NotchAlgorithmVersion version)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 4, 10, 10,
            diffIndexSelector: static (_, col) => col);
        var cad = new CadPadSet([
            TestGeometryFactory.CreateCadPad(1, "PAD", 0, 0, 15, 10),
            TestGeometryFactory.CreateCadPad(2, "PAD", 20, 0, 30, 10),
        ]);
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = 1;
        grid.Pads[2].MatchedCadPadId = 2;
        var settings = new ProjectSettings
        {
            Grid = new GridSettings { XChannels = 4, YChannels = 1, ActiveAreaWidth = 40, ActiveAreaHeight = 10 },
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                CompensationModel = NotchCompensationModel.CurrentGain,
                EnableToRegular = true,
                EnableToFull = false,
                EnableTargetCoverageGuard = true,
                TargetCoverageCapPercent = 130,
                EnabledVersions = [version],
            },
        };
        var assignment = CadOutputFwDiffIndexAssignmentService.Assign(cad.Pads, grid, settings.Grid,
            CadOutputFwDiffAutoMode.BestMatchDirect, new Dictionary<int, int?> { [1] = 0, [2] = 2 },
            new Dictionary<int, int> { [1] = 0, [2] = hasElectrode ? 1 : 3 });
        return (cad, grid, settings, assignment.AssignedIndexByCadId.ToDictionary());
    }
}

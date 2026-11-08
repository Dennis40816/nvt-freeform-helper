using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchTableGeneratorTests
{
    [Fact]
    public void Generate_CadAllocationMode_V22_UsesUndoNfAndToFullRatios()
    {
        var grid = CreateGrid();
        var cad = CreateCadRightTriangleHalfCell();
        var reg = grid.Pads[0];
        reg.Freeform = FreeformType.XWay;
        reg.MatchedCadPadId = cad.Pads[0].Id;
        reg.MatchScore = 1.0;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnableToFull = true,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        var row = Assert.Single(table.Rows);
        Assert.NotNull(row.V22Node);
        Assert.Equal(row.Values[0], row.V22Node!.AnchorDiffIndex);
        Assert.Equal(row.Values[1], row.V22Node.CombinePercent);
        Assert.Equal(100, row.Values[1]); // Combined = 50% * 200% = 100%
        Assert.Equal(settings.Notch.NullValue, row.Values[2]); // No transfer leg in this simple case.
        Assert.Equal(0, row.Values[3]);
        Assert.Equal(settings.Notch.NullValue, row.Values[4]);
        Assert.Equal(0, row.Values[5]);
        Assert.Contains("R=50%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("F=200%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("C=100%", row.Comment, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_SnapshotStable_ForSimpleTriangle()
    {
        var grid = CreateGrid();
        var cad = CreateCadRightTriangleHalfCell();
        var reg = grid.Pads[0];
        reg.Freeform = FreeformType.XWay;
        reg.MatchedCadPadId = cad.Pads[0].Id;
        reg.MatchScore = 1.0;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnableToFull = true,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);
        var row = Assert.Single(table.Rows);

        var snapshot = BuildRowSnapshot(row);
        Assert.Equal(
            "V22|IC0|DIFF0|REG0|CAD1|0,100,65535,0,65535,0,0|CAD=1 R=50% F=200% C=100% NT",
            snapshot);
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_ConservativeModel_UsesTargetRegularSourceCoverage()
    {
        var grid = CreateGrid();
        var cad = CreateCadRightTriangleHalfCell();
        var reg = grid.Pads[0];
        reg.Freeform = FreeformType.XWay;
        reg.MatchedCadPadId = cad.Pads[0].Id;
        reg.MatchScore = 1.0;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                CompensationModel = NotchCompensationModel.ConservativeNoGain,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        var row = Assert.Single(table.Rows);
        Assert.Equal(50, row.Values[1]);
        Assert.Contains("R=50%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("F=200%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("C=50%", row.Comment, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_CurrentGain_UsesTargetRegularStage3Coverage()
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = CreateCadWithBounds(0, 0, 15, 10);
        foreach (var regular in grid.Pads)
        {
            regular.Freeform = FreeformType.XWay;
            regular.MatchedCadPadId = cad.Pads[0].Id;
            regular.MatchScore = 1.0;
        }

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                CompensationModel = NotchCompensationModel.CurrentGain,
                EnableToRegular = true,
                EnableToFull = true,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        var row = Assert.Single(table.Rows);
        Assert.Equal([0, 200, 1, 100, settings.Notch.NullValue, 0, 0], row.Values);
        Assert.Contains("R=150%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("F=133%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("C=200%", row.Comment, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_DisabledModel_ForcesUnityRatios()
    {
        var grid = CreateGrid();
        var cad = CreateCadRightTriangleHalfCell();
        var reg = grid.Pads[0];
        reg.Freeform = FreeformType.XWay;
        reg.MatchedCadPadId = cad.Pads[0].Id;
        reg.MatchScore = 1.0;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                CompensationModel = NotchCompensationModel.Disabled,
                EnableToRegular = true,
                EnableToFull = true,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        var row = Assert.Single(table.Rows);
        Assert.Equal(100, row.Values[1]);
        Assert.Contains("R=100%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("F=100%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("C=100%", row.Comment, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_UsesActiveRegularPadSet_ForToFullBoundaryDecision()
    {
        var grid = CreateGrid(rows: 3, cols: 3);
        var center = grid.Pads.Single(pad => pad.Row == 1 && pad.Col == 1);
        var cadPolygon = new Polygon2(
            new[]
            {
                new Point2(10, 10),
                new Point2(20, 10),
                new Point2(10, 20),
            });
        var cad = new CadPadSet(new[]
        {
            new CadPad(1, "C1", "PAD", cadPolygon),
        });
        center.Freeform = FreeformType.XWay;
        center.MatchedCadPadId = 1;
        center.MatchScore = 1.0;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnableToRegular = true,
                EnableToFull = true,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var withoutActiveRegularFilter = generator.Generate(cad, grid, settings);
        var withActiveRegularFilter = generator.Generate(
            cad,
            grid,
            settings,
            activeRegularPadIds: new HashSet<int> { center.RegularPadId });

        var withoutRow = Assert.Single(withoutActiveRegularFilter.Rows);
        var withRow = Assert.Single(withActiveRegularFilter.Rows);

        Assert.Equal(100, withoutRow.Values[1]);
        Assert.Equal(100, withRow.Values[1]);
        Assert.Contains("F=200%", withoutRow.Comment, StringComparison.Ordinal);
        Assert.Contains("F=200%", withRow.Comment, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_EmitsRowsPerIc_WhenCadOverlapsMultipleIcs()
    {
        var grid = CreateGrid(rows: 2, cols: 1);
        grid.Pads[0].IcIndex = 0;
        grid.Pads[0].DiffIndex = 0;
        grid.Pads[1].IcIndex = 1;
        grid.Pads[1].DiffIndex = 0;

        var cad = CreateCadWithBounds(0, 0, 10, 20);
        foreach (var regular in grid.Pads)
        {
            regular.Freeform = FreeformType.XWay;
            regular.MatchedCadPadId = cad.Pads[0].Id;
            regular.MatchScore = 1.0;
        }

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(_crossIcIndices, table.Rows.Select(row => row.IcIndex).ToArray());
        Assert.All(table.Rows, row => Assert.Contains("XIC=1/2", row.Comment, StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_EmitsToFullRow_WhenAnchorIsNotFreeform()
    {
        var grid = CreateGrid();
        var cad = CreateCadRightTriangleHalfCell();
        var regular = grid.Pads[0];
        regular.Freeform = FreeformType.None;
        regular.MatchedCadPadId = null;
        regular.MatchScore = 0.0;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnableToFull = true,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        var row = Assert.Single(table.Rows);
        Assert.Equal(regular.DiffIndex, row.DiffIndex);
        Assert.Equal(cad.Pads[0].Id, row.CadPadId);
        Assert.Contains("F=200%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("C=100%", row.Comment, StringComparison.Ordinal);
    }
}

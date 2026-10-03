using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchCadOutputFwDiffProjectionServiceTests
{
    private static readonly int[] DiffIndices101112 = [10, 11, 12];
    private static readonly int[] DiffIndices2021 = [20, 21];
    private static readonly int[] ProjectedAnchorDiffs101213 = [10, 12, 13];
    private static readonly int[] RowValues10 = [10, 90, 110, 0, 0, 0, 0, 0, 0];
    private static readonly int[] RowValues11 = [11, 90, 110, 0, 0, 0, 10, 1, 19];
    private static readonly int[] RowValues12 = [12, 90, 110, 11, 1, 19, 0, 0, 0];
    private static readonly int[] CadPadIds200201 = [200, 201];

    [Fact]
    public void Project_WhenCadDiffsShiftLeaveGap_GapDoesNotRemainAsAnchor()
    {
        var grid = BuildLinearGrid(diffIndices: DiffIndices101112);
        var table = new NotchTable(new NotchTableRow[]
        {
            new(NotchAlgorithmVersion.V21, 0, 10, 0, 100, RowValues10, "r0"),
            new(NotchAlgorithmVersion.V21, 0, 11, 1, 101, RowValues11, "r1"),
            new(NotchAlgorithmVersion.V21, 0, 12, 2, 102, RowValues12, "r2"),
        });

        var projection = NotchCadOutputFwDiffProjectionService.Project(
            grid,
            table,
            new Dictionary<int, int>
            {
                [101] = 12,
                [102] = 13,
            });

        Assert.Equal(2, projection.ChangedRowCount);
        Assert.Equal(0, projection.ConflictedRawDiffKeyCount);
        Assert.Equal(ProjectedAnchorDiffs101213, projection.Table.Rows.Select(static row => row.DiffIndex).ToArray());
        Assert.DoesNotContain(projection.Table.Rows, static row => row.CadPadId == 101 && row.Values[0] == 11);
        var rowCad102 = Assert.Single(projection.Table.Rows, static row => row.CadPadId == 102);
        Assert.Equal(11, rowCad102.Values[3]);

        var projectedPads = projection.Grid.Pads.OrderBy(static pad => pad.RegularPadId).ToArray();
        Assert.Equal(10, projectedPads[0].DiffIndex);
        Assert.Equal(12, projectedPads[1].DiffIndex);
        Assert.Equal(13, projectedPads[2].DiffIndex);
    }

    [Fact]
    public void Project_WhenV22TypedRowShifted_ProjectsAnchorOnly_AndKeepsRegularTargets()
    {
        var grid = BuildLinearGrid(diffIndices: DiffIndices2021);
        var row = new NotchTableRow(
            icIndex: 0,
            diffIndex: 20,
            regularPadIndex: 0,
            cadPadId: 200,
            v22Node: new NotchV22Node(20, 94, 21, 44, 65535, 0, 0),
            comment: "typed");
        var anchorForTarget = new NotchTableRow(
            icIndex: 0,
            diffIndex: 21,
            regularPadIndex: 1,
            cadPadId: 201,
            v22Node: new NotchV22Node(21, 100, 65535, 0, 65535, 0, 0),
            comment: "anchor-target");
        var projection = NotchCadOutputFwDiffProjectionService.Project(
            grid,
            new NotchTable(new[] { row, anchorForTarget }),
            new Dictionary<int, int>
            {
                [200] = 24,
                [201] = 25,
            });

        Assert.Equal(2, projection.ChangedRowCount);
        Assert.Equal(2, projection.ChangedAnchorCount);
        Assert.Equal(0, projection.ConflictedRawDiffKeyCount);
        var projectedRow = Assert.Single(projection.Table.Rows, static row => row.CadPadId == 200);
        Assert.Equal(24, projectedRow.DiffIndex);
        Assert.Equal(24, projectedRow.Values[0]);
        Assert.NotNull(projectedRow.V22Node);
        Assert.Equal(24, projectedRow.V22Node!.AnchorDiffIndex);
        Assert.Equal(21, projectedRow.V22Node.TargetDiffIndex1);
        Assert.Equal(21, projectedRow.Values[2]);

        var projectedPads = projection.Grid.Pads.OrderBy(static pad => pad.RegularPadId).ToArray();
        Assert.Equal(24, projectedPads[0].DiffIndex);
        Assert.Equal(25, projectedPads[1].DiffIndex);
    }

    [Fact]
    public void Project_WhenRawDiffHasConflictingVisibleMapping_KeepsRawTargetDiff()
    {
        var grid = BuildLinearGrid(diffIndices: [20, 21, 22]);
        var source = new NotchTableRow(
            icIndex: 0,
            diffIndex: 20,
            regularPadIndex: 0,
            cadPadId: 200,
            v22Node: new NotchV22Node(20, 94, 21, 44, 65535, 0, 0),
            comment: "src");
        var conflictA = new NotchTableRow(
            icIndex: 0,
            diffIndex: 21,
            regularPadIndex: 1,
            cadPadId: 201,
            v22Node: new NotchV22Node(21, 100, 65535, 0, 65535, 0, 0),
            comment: "conflict-a");
        var conflictB = new NotchTableRow(
            icIndex: 0,
            diffIndex: 21,
            regularPadIndex: 2,
            cadPadId: 202,
            v22Node: new NotchV22Node(21, 100, 65535, 0, 65535, 0, 0),
            comment: "conflict-b");

        var projection = NotchCadOutputFwDiffProjectionService.Project(
            grid,
            new NotchTable(new[] { source, conflictA, conflictB }),
            new Dictionary<int, int>
            {
                [200] = 24,
                [201] = 25,
                [202] = 26,
            });

        var projectedSource = Assert.Single(projection.Table.Rows, static row => row.CadPadId == 200);
        Assert.NotNull(projectedSource.V22Node);
        Assert.Equal(24, projectedSource.V22Node!.AnchorDiffIndex);
        Assert.Equal(21, projectedSource.V22Node.TargetDiffIndex1);
        Assert.Equal(1, projection.ConflictedRawDiffKeyCount);
    }

    [Fact]
    public void Project_PreservesToFullCoverageAudit()
    {
        var grid = BuildLinearGrid(diffIndices: DiffIndices2021);
        var audit = new NotchToFullCoverageAudit(
            BucketCount: 1,
            ExpectedTargetDiffCount: 2,
            CoveredTargetDiffCount: 1,
            MissingGaps: new[]
            {
                new NotchToFullCoverageGap(
                    IcIndex: 0,
                    SourceDiffIndex: 20,
                    TargetDiffIndex: 22,
                    CadPadIds: CadPadIds200201),
            });
        var row = new NotchTableRow(
            icIndex: 0,
            diffIndex: 20,
            regularPadIndex: 0,
            cadPadId: 200,
            v22Node: new NotchV22Node(20, 94, 21, 44, 65535, 0, 0),
            comment: "typed");
        var anchorForTarget = new NotchTableRow(
            icIndex: 0,
            diffIndex: 21,
            regularPadIndex: 1,
            cadPadId: 201,
            v22Node: new NotchV22Node(21, 100, 65535, 0, 65535, 0, 0),
            comment: "anchor-target");
        var projection = NotchCadOutputFwDiffProjectionService.Project(
            grid,
            new NotchTable(new[] { row, anchorForTarget }, audit),
            new Dictionary<int, int>
            {
                [200] = 24,
                [201] = 25,
            });

        Assert.Same(audit, projection.Table.ToFullCoverageAudit);
    }

    [Fact]
    public void Project_PreservesGenerationPhaseTimings()
    {
        var grid = BuildLinearGrid(diffIndices: DiffIndices2021);
        var timings = new NotchGenerationPhaseTimings(
            BuildProfilesElapsedMs: 11,
            BuildCanonicalCandidatesElapsedMs: 22,
            MergeCanonicalCandidatesElapsedMs: 33,
            BuildLegacyRowsElapsedMs: 44,
            FinalizeCanonicalExportsElapsedMs: 55);
        var row = new NotchTableRow(
            icIndex: 0,
            diffIndex: 20,
            regularPadIndex: 0,
            cadPadId: 200,
            v22Node: new NotchV22Node(20, 94, 21, 44, 65535, 0, 0),
            comment: "typed");
        var anchorForTarget = new NotchTableRow(
            icIndex: 0,
            diffIndex: 21,
            regularPadIndex: 1,
            cadPadId: 201,
            v22Node: new NotchV22Node(21, 100, 65535, 0, 65535, 0, 0),
            comment: "anchor-target");
        var projection = NotchCadOutputFwDiffProjectionService.Project(
            grid,
            new NotchTable(new[] { row, anchorForTarget }, generationPhaseTimings: timings),
            new Dictionary<int, int>
            {
                [200] = 24,
                [201] = 25,
            });

        Assert.Same(timings, projection.Table.GenerationPhaseTimings);
    }

    private static RegularGrid BuildLinearGrid(int[] diffIndices)
    {
        return TestGeometryFactory.CreateLinearRegularGrid(diffIndices);
    }
}

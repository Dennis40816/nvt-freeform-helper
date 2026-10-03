using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadOutputFwDiffIndexAssignmentServiceTests
{
    [Fact]
    public void Assign_BestMatchDirect_UsesStrictSeedsWithoutRowSequenceCompression()
    {
        var gridSettings = new GridSettings
        {
            XChannels = 4,
            YChannels = 2,
            ActiveAreaWidth = 4,
            ActiveAreaHeight = 2,
            BoundsPaddingRatio = 0,
        };

        var grid = new RegularGridBuilder().BuildFromSettings(gridSettings);
        DiffIndexMapper.ApplyMapping(grid, gridSettings);

        var cadPads = BuildCadPads(grid);
        var ordered = DxfIndexAssigner.OrderPads(cadPads, grid, gridSettings.ScanOrder);
        var rows = GroupOrderedPadsByRow(ordered, grid);
        var firstRow = rows[0];
        var secondRow = rows[1];
        var firstRowDiffs = GetRowDiffs(grid, firstRow.RowIndex);
        var secondRowDiffs = GetRowDiffs(grid, secondRow.RowIndex);

        var seeds = new Dictionary<int, int?>();
        seeds[firstRow.Cads[0].Id] = firstRowDiffs[0];
        seeds[firstRow.Cads[1].Id] = firstRowDiffs[1];
        seeds[firstRow.Cads[2].Id] = firstRowDiffs[2];
        seeds[firstRow.Cads[3].Id] = firstRowDiffs[3];
        for (var i = 0; i < secondRow.Cads.Count; i++)
        {
            seeds[secondRow.Cads[i].Id] = secondRowDiffs[i];
        }

        var service = new CadOutputFwDiffIndexAssignmentService();
        var result = service.Assign(
            ordered,
            grid,
            gridSettings,
            CadOutputFwDiffAutoMode.BestMatchDirect,
            seeds,
            manualOverrides: null,
            anchorCadIdByIc: null);

        Assert.Equal(firstRowDiffs[0], result.AssignedIndexByCadId[firstRow.Cads[0].Id]);
        Assert.Equal(firstRowDiffs[1], result.AssignedIndexByCadId[firstRow.Cads[1].Id]);
        Assert.Equal(firstRowDiffs[2], result.AssignedIndexByCadId[firstRow.Cads[2].Id]);
        Assert.Equal(firstRowDiffs[3], result.AssignedIndexByCadId[firstRow.Cads[3].Id]);

        for (var i = 0; i < secondRow.Cads.Count; i++)
        {
            Assert.Equal(secondRowDiffs[i], result.AssignedIndexByCadId[secondRow.Cads[i].Id]);
        }

        Assert.Equal(0, result.OverrideAssignedCount);
        Assert.Equal(0, result.SequenceAdjustedCount);
        Assert.Equal(0, result.RowSequenceAppliedCount);
        Assert.Equal(0, result.RowSequenceFallbackCount);
    }

    [Fact]
    public void Assign_StrictUnique_LeavesConflictingSeedsUnresolved()
    {
        var gridSettings = new GridSettings
        {
            XChannels = 4,
            YChannels = 1,
            ActiveAreaWidth = 4,
            ActiveAreaHeight = 1,
            BoundsPaddingRatio = 0,
        };

        var grid = new RegularGridBuilder().BuildFromSettings(gridSettings);
        DiffIndexMapper.ApplyMapping(grid, gridSettings);

        var cadPads = BuildCadPads(grid);
        var ordered = DxfIndexAssigner.OrderPads(cadPads, grid, gridSettings.ScanOrder);
        var row = GroupOrderedPadsByRow(ordered, grid).Single();
        var rowDiffs = GetRowDiffs(grid, row.RowIndex);

        var seeds = new Dictionary<int, int?>
        {
            [row.Cads[0].Id] = rowDiffs[0],
            [row.Cads[1].Id] = rowDiffs[1],
            [row.Cads[2].Id] = rowDiffs[1],
            [row.Cads[3].Id] = rowDiffs[3],
        };

        var service = new CadOutputFwDiffIndexAssignmentService();
        var result = service.Assign(
            ordered,
            grid,
            gridSettings,
            CadOutputFwDiffAutoMode.StrictUnique,
            seeds,
            manualOverrides: null,
            anchorCadIdByIc: null);

        Assert.Equal(rowDiffs[0], result.AssignedIndexByCadId[row.Cads[0].Id]);
        Assert.Equal(rowDiffs[1], result.AssignedIndexByCadId[row.Cads[1].Id]);
        Assert.Equal(rowDiffs[3], result.AssignedIndexByCadId[row.Cads[3].Id]);
        Assert.False(result.AssignedIndexByCadId.ContainsKey(row.Cads[2].Id));
        Assert.Equal(1, result.AutoConflictCount);
        Assert.Equal(0, result.AnchorIgnoredCount);
        Assert.Equal(0, result.SequenceAdjustedCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Assign_OptionalManualOverrides_PreserveAbsentAndDuplicateBehavior(bool useGrid)
    {
        var gridSettings = new GridSettings
        {
            XChannels = 2,
            YChannels = 1,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 1,
            BoundsPaddingRatio = 0,
        };
        var grid = new RegularGridBuilder().BuildFromSettings(gridSettings);
        DiffIndexMapper.ApplyMapping(grid, gridSettings);
        var ordered = DxfIndexAssigner.OrderPads(
            BuildCadPads(grid),
            grid,
            gridSettings.ScanOrder);
        var first = ordered[0];
        var second = ordered[1];
        var strictSeeds = new Dictionary<int, int?>
        {
            [first.Id] = 10,
            [second.Id] = 11,
        };
        var service = new CadOutputFwDiffIndexAssignmentService();

        var absent = service.Assign(
            ordered,
            useGrid ? grid : null,
            gridSettings,
            CadOutputFwDiffAutoMode.StrictUnique,
            strictSeeds,
            manualOverrides: null);
        var duplicate = service.Assign(
            ordered,
            useGrid ? grid : null,
            gridSettings,
            CadOutputFwDiffAutoMode.StrictUnique,
            strictSeeds,
            manualOverrides: new Dictionary<int, int>
            {
                [first.Id] = 77,
                [second.Id] = 77,
            });

        Assert.Equal(
            new[] { (first.Id, 10), (second.Id, 11) },
            absent.AssignedIndexByCadId.Select(static pair => (pair.Key, pair.Value)));
        Assert.Equal((0, 2, 0, 0, 0, 0),
            (absent.OverrideAssignedCount,
             absent.AutoAssignedCount,
             absent.DuplicateOverrides,
             absent.InvalidOverrides,
             absent.OverrideMismatchCount,
             absent.AutoConflictCount));
        Assert.Equal(
            new[] { (first.Id, 77), (second.Id, 11) },
            duplicate.AssignedIndexByCadId.Select(static pair => (pair.Key, pair.Value)));
        Assert.Equal((1, 1, 1, 1),
            (duplicate.OverrideAssignedCount,
             duplicate.AutoAssignedCount,
             duplicate.DuplicateOverrides,
             duplicate.OverrideMismatchCount));
        Assert.Equal((0, 0), (duplicate.InvalidOverrides, duplicate.AutoConflictCount));
    }

    [Fact]
    public void Assign_WithGridDirect_PreservesFirstSeenRowAndPadOrder()
    {
        var gridSettings = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 2,
            BoundsPaddingRatio = 0,
            ScanOrder = ScanOrder.LeftToRight_TopToBottom,
        };
        var grid = new RegularGridBuilder().BuildFromSettings(gridSettings);
        DiffIndexMapper.ApplyMapping(grid, gridSettings);
        var cadIdsByCell = new Dictionary<(int Row, int Col), int>
        {
            [(1, 0)] = 900,
            [(1, 1)] = 100,
            [(0, 0)] = 700,
            [(0, 1)] = 200,
        };
        var ordered = DxfIndexAssigner.OrderPads(
            grid.Pads.Select(pad => BuildCad(cadIdsByCell[(pad.Row, pad.Col)], pad.Bounds)).ToList(),
            grid,
            gridSettings.ScanOrder);
        var strictSeeds = ordered
            .Select((pad, index) => (pad.Id, DiffIndex: (int?)(10 + index)))
            .ToDictionary(static item => item.Id, static item => item.DiffIndex);
        var duplicateOverrides = ordered.ToDictionary(static pad => pad.Id, static _ => 77);

        var result = new CadOutputFwDiffIndexAssignmentService().Assign(
            ordered,
            grid,
            gridSettings,
            CadOutputFwDiffAutoMode.StrictUnique,
            strictSeeds,
            duplicateOverrides);

        Assert.Equal("900,100,700,200", string.Join(',', ordered.Select(static pad => pad.Id)));
        Assert.Equal(
            "900:77,100:11,700:12,200:13",
            string.Join(',', result.AssignedIndexByCadId.Select(static pair => $"{pair.Key}:{pair.Value}")));
        Assert.Equal((1, 3, 3, 1, 0, 0, 0),
            (result.OverrideAssignedCount,
             result.AutoAssignedCount,
             result.DuplicateOverrides,
             result.OverrideMismatchCount,
             result.InvalidOverrides,
             result.AutoConflictCount,
             result.AutoUnmatchedCount));
    }

    [Theory]
    [MemberData(nameof(AllAutoModes))]
    public void Assign_AllAutoModes_UseDirectSeedsWithoutRowSequenceOrAnchorAdjustment(
        CadOutputFwDiffAutoMode autoMode)
    {
        var gridSettings = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 2,
            BoundsPaddingRatio = 0,
        };

        var grid = new RegularGridBuilder().BuildFromSettings(gridSettings);
        DiffIndexMapper.ApplyMapping(grid, gridSettings);

        var cadPads = BuildCadPads(grid);
        var ordered = DxfIndexAssigner.OrderPads(cadPads, grid, gridSettings.ScanOrder);
        var rows = GroupOrderedPadsByRow(ordered, grid);

        var seeds = new Dictionary<int, int?>
        {
            [rows[0].Cads[0].Id] = 35,
            [rows[0].Cads[1].Id] = 36,
            [rows[1].Cads[0].Id] = 42,
            [rows[1].Cads[1].Id] = 43,
        };

        var service = new CadOutputFwDiffIndexAssignmentService();
        var result = service.Assign(
            ordered,
            grid,
            gridSettings,
            autoMode,
            seeds,
            manualOverrides: null,
            anchorCadIdByIc: new Dictionary<int, int> { [0] = rows[1].Cads[0].Id });

        Assert.Equal(35, result.AssignedIndexByCadId[rows[0].Cads[0].Id]);
        Assert.Equal(36, result.AssignedIndexByCadId[rows[0].Cads[1].Id]);
        Assert.Equal(42, result.AssignedIndexByCadId[rows[1].Cads[0].Id]);
        Assert.Equal(43, result.AssignedIndexByCadId[rows[1].Cads[1].Id]);
        Assert.Equal(0, result.SequenceAdjustedCount);
        Assert.Equal(0, result.AnchorAppliedCount);
        Assert.Equal(0, result.AnchorIgnoredCount);
        Assert.Equal(0, result.RowSequenceAppliedCount);
        Assert.Equal(0, result.RowSequenceFallbackCount);
    }

    public static IEnumerable<object[]> AllAutoModes()
    {
        return Enum.GetValues<CadOutputFwDiffAutoMode>()
            .Select(mode => new object[] { mode });
    }

    private static List<CadPad> BuildCadPads(RegularGrid grid)
    {
        return grid.Pads
            .Select(reg => BuildCad(1000 + reg.RegularPadId, reg.Bounds))
            .ToList();
    }

    private static List<RowCadGroup> GroupOrderedPadsByRow(IReadOnlyList<CadPad> ordered, RegularGrid grid)
    {
        return ordered
            .Select((pad, orderIndex) =>
            {
                var (row, _) = DxfIndexAssigner.ResolveGridCell(grid, pad.Centroid);
                return new { pad, orderIndex, row };
            })
            .GroupBy(item => item.row)
            .Select(group => new RowCadGroup(
                group.Key,
                group.Min(item => item.orderIndex),
                group.OrderBy(item => item.orderIndex).Select(item => item.pad).ToList()))
            .OrderBy(group => group.FirstOrder)
            .ToList();
    }

    private static List<int> GetRowDiffs(RegularGrid grid, int rowIndex)
    {
        return grid.Pads
            .Where(pad => pad.Row == rowIndex)
            .OrderBy(pad => pad.DiffIndex)
            .Select(pad => pad.DiffIndex)
            .ToList();
    }

    private static CadPad BuildCad(int id, Rect2 bounds)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(bounds.MinX, bounds.MinY),
            new Point2(bounds.MaxX, bounds.MinY),
            new Point2(bounds.MaxX, bounds.MaxY),
            new Point2(bounds.MinX, bounds.MaxY),
        });

        return new CadPad(id, $"C{id}", "L1", polygon);
    }

    private sealed record RowCadGroup(int RowIndex, int FirstOrder, IReadOnlyList<CadPad> Cads);
}

using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    private const double Stage3CoverageTolerance = 1e-5;
    private const double Stage3OverlapContactTolerance = 1e-4;

    [ExampleDataFact]
    public async Task GetCadV22StageOverlays_TM81_CAD364_CoversWholeSoleOwnerInteriorRegular()
    {
        var vm = new FreeformHelperViewModel();
        var projectPath = Path.Combine(TestPaths.RepoRoot, "example", "TM 8.1", "TM8.1.json");
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        var compensation = vm.GetCadV22CompensationResult(364);
        var overlay = vm.GetCadV22StageOverlays(364);

        Assert.NotNull(compensation);
        Assert.NotNull(overlay);
        Assert.True(overlay.Value.IsToFullEnabled);

        var reg291 = Assert.Single(vm.RegularPads, static pad => pad.Index == 291);
        var debug = Assert.Single(compensation!.RegularDebugInfos, static info => info.RegularIndex == 291);
        Assert.False(debug.IsBoundaryRegular);
        Assert.True(debug.IsToFullApplied);
        Assert.Equal("EXPAND_CLEAR_PATH", debug.ToFullRuleCode);

        var stage2Coverage = ComputeRectCoverage(overlay.Value.Stage2Candidate, reg291.Bounds);
        var stage3Coverage = ComputeRectCoverage(overlay.Value.Stage3Final, reg291.Bounds);
        Assert.True(stage2Coverage >= reg291.Area - Stage3CoverageTolerance);
        Assert.True(stage3Coverage >= reg291.Area - Stage3CoverageTolerance);
    }

    [ExampleDataFact]
    public async Task GetCadV22StageOverlays_TM81_CAD490_And_CAD491_UseSharedSafeFillWithoutStage3Overlap()
    {
        var vm = new FreeformHelperViewModel();
        var projectPath = Path.Combine(TestPaths.RepoRoot, "example", "TM 8.1", "TM8.1.json");
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        var compensation490 = vm.GetCadV22CompensationResult(490);
        var compensation491 = vm.GetCadV22CompensationResult(491);
        var overlay490 = vm.GetCadV22StageOverlays(490);
        var overlay491 = vm.GetCadV22StageOverlays(491);

        Assert.NotNull(compensation490);
        Assert.NotNull(compensation491);
        Assert.NotNull(overlay490);
        Assert.NotNull(overlay491);
        Assert.True(overlay490.Value.IsToFullEnabled);
        Assert.True(overlay491.Value.IsToFullEnabled);

        var shared490 = Assert.Single(compensation490!.RegularDebugInfos, static info => info.RegularIndex == 643);
        var shared491 = Assert.Single(compensation491!.RegularDebugInfos, static info => info.RegularIndex == 643);
        Assert.True(shared490.IsToFullApplied);
        Assert.True(shared491.IsToFullApplied);
        Assert.Equal("EXPAND_SHARED_REACHABLE", shared490.ToFullRuleCode);
        Assert.Equal("EXPAND_SHARED_REACHABLE", shared491.ToFullRuleCode);

        var stage3Overlap = ComputeRectangleIntersectionArea(overlay490.Value.Stage3Final, overlay491.Value.Stage3Final);
        Assert.True(stage3Overlap <= 1e-6, $"Stage3 overlap should be zero, actual={stage3Overlap}.");
    }

    [ExampleDataFact]
    public async Task GetCadV22StageOverlays_TM81_CAD113_DisablesToFull_WhenCadHasNoExpansionPotential()
    {
        var vm = new FreeformHelperViewModel();
        var projectPath = Path.Combine(TestPaths.RepoRoot, "example", "TM 8.1", "TM8.1.json");
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        var compensation = vm.GetCadV22CompensationResult(113);
        var overlay = vm.GetCadV22StageOverlays(113);

        Assert.NotNull(compensation);
        Assert.NotNull(overlay);
        Assert.False(overlay.Value.IsToFullEnabled);
        Assert.Equal(1.0, compensation!.ToFullRatio, 6);
        Assert.Empty(overlay.Value.Stage1Seed);
        Assert.Empty(overlay.Value.Stage2Candidate);
        Assert.Empty(overlay.Value.Stage3Final);

        var main = Assert.Single(compensation.RegularDebugInfos, static info => info.RegularIndex == 445);
        Assert.False(main.IsToFullApplied);
        Assert.Equal("NO_EXPANSION_NEEDED", main.ToFullRuleCode);
    }

    [ExampleDataFact]
    public async Task GetCadV22CompensationResult_TM81_CAD402_NoLongerSeesExactDuplicateSharedOwner()
    {
        var vm = new FreeformHelperViewModel();
        var projectPath = Path.Combine(TestPaths.RepoRoot, "example", "TM 8.1", "TM8.1.json");
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        var compensation = vm.GetCadV22CompensationResult(402);

        Assert.NotNull(compensation);
        var reg624 = Assert.Single(compensation!.RegularDebugInfos, static info => info.RegularIndex == 624);
        Assert.Equal(1, reg624.OwnerCadPadCount);
        Assert.Equal([402], reg624.OwnerCadPadIds);
        Assert.Equal([402], vm.GetMatchedCadPadIds(reg624.RegularPadId));
    }

    [ExampleDataFact]
    public async Task BuildRegularPadInspectorSnapshot_TM81_REG387_IgnoresMinorTailAndReturnsNoneAfterOverrideRemoval()
    {
        var vm = new FreeformHelperViewModel();
        var projectPath = Path.Combine(TestPaths.RepoRoot, "example", "TM 8.1", "TM8.1.json");
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        var snapshot387 = vm.BuildRegularPadInspectorSnapshot(387);
        var snapshot388 = vm.BuildRegularPadInspectorSnapshot(388);
        var snapshot389 = vm.BuildRegularPadInspectorSnapshot(389);

        var reg387 = Assert.IsType<RegularPadInspectorSnapshot>(snapshot387?.Regular);
        var reg388 = Assert.IsType<RegularPadInspectorSnapshot>(snapshot388?.Regular);
        var reg389 = Assert.IsType<RegularPadInspectorSnapshot>(snapshot389?.Regular);

        Assert.Equal(FreeformType.None, reg387.Freeform);
        Assert.Equal("None", reg387.FreeformSource);

        Assert.Equal(FreeformType.XWay, reg388.Freeform);
        Assert.Equal("Override", reg388.FreeformSource);

        Assert.Equal(FreeformType.XWay, reg389.Freeform);
        Assert.Equal("Override", reg389.FreeformSource);
    }

    private static double ComputeRectCoverage(IReadOnlyList<Polygon2> polygons, Rect2 rect)
    {
        var area = 0.0;
        foreach (var polygon in polygons)
        {
            area += Polygon2.IntersectionAreaWithRect(polygon, rect);
        }

        return area;
    }

    private static double ComputeRectangleIntersectionArea(
        IReadOnlyList<Polygon2> a,
        IReadOnlyList<Polygon2> b)
    {
        var area = 0.0;
        foreach (var left in a)
        {
            var leftBounds = left.Bounds;
            foreach (var right in b)
            {
                var rightBounds = right.Bounds;
                var overlapWidth = Math.Min(leftBounds.MaxX, rightBounds.MaxX) - Math.Max(leftBounds.MinX, rightBounds.MinX);
                var overlapHeight = Math.Min(leftBounds.MaxY, rightBounds.MaxY) - Math.Max(leftBounds.MinY, rightBounds.MinY);
                if (overlapWidth <= Stage3OverlapContactTolerance || overlapHeight <= Stage3OverlapContactTolerance)
                {
                    continue;
                }

                area += overlapWidth * overlapHeight;
            }
        }

        return area;
    }

    [ExampleDataFact]
    public async Task GetCadV22StageOverlays_CAD4818_ReturnsFinalOutlineCoveringFullReg384()
    {
        var vm = new FreeformHelperViewModel();
        var projectPath = Path.Combine(TestPaths.RepoRoot, "example", "BOE36.35", "project_3635.json");
        using var fixture = ExampleProjectFixture.Create(projectPath);
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(fixture.ProjectPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        var overlay = vm.GetCadV22StageOverlays(4818);

        Assert.NotNull(overlay);
        Assert.True(overlay.Value.IsToFullEnabled);
        var stage2 = Assert.Single(overlay.Value.Stage2Candidate);
        Assert.Equal(2.94776, stage2.Bounds.MinX, 5);
        Assert.Equal(15.82673, stage2.Bounds.MinY, 5);
        Assert.Equal(5.49116, stage2.Bounds.MaxX, 5);
        Assert.Equal(20.34833, stage2.Bounds.MaxY, 5);

        var stage3 = Assert.Single(overlay.Value.Stage3Final);
        Assert.True(stage3.Area() > 24.2782 && stage3.Area() < 24.2784);
        Assert.Equal(2.94776, stage3.Bounds.MinX, 5);
        Assert.Equal(15.82673, stage3.Bounds.MinY, 5);
        Assert.Equal(8.31716, stage3.Bounds.MaxX, 5);
        Assert.Equal(20.34833, stage3.Bounds.MaxY, 5);
    }

    [Fact]
    public async Task RebuildGrid_WhenDxfBoundsWithoutDxf_FallsBackToPanelAaAlignment()
    {
        var vm = new FreeformHelperViewModel
        {
            PanelBiasX = 12.5m,
            PanelBiasY = -3.75m,
            GridAlignmentMode = GridAlignmentMode.FromCadBounds,
            ActiveAreaWidth = 120m,
            ActiveAreaHeight = 80m,
        };

        await WaitForGridRebuildAsync(vm);

        Assert.Equal("Ready (DXF not loaded; using panel AA alignment).", vm.StatusText);
        Assert.NotEmpty(vm.RegularPads);
        Assert.Equal(12.5, vm.RegularPads.Min(p => p.Bounds.MinX), 3);
        Assert.Equal(-3.75, vm.RegularPads.Min(p => p.Bounds.MinY), 3);
    }


    [Fact]
    public void PitchSizeSummary_ListsDistinctGridWidthsAndHeights()
    {
        var vm = new FreeformHelperViewModel();

        var pads = new List<RegularPad>
        {
            CreateRectRegular(0, 0, 0, 0, 0, 10, 4),
            CreateRectRegular(0, 1, 1, 10, 0, 30, 4),
            CreateRectRegular(1, 0, 2, 0, 4, 10, 10),
            CreateRectRegular(1, 1, 3, 10, 4, 30, 10),
        };
        var pitchXEdges = new[] { 0.0, 10.0, 30.0 };
        var pitchYEdges = new[] { 0.0, 4.0, 10.0 };
        var grid = new RegularGrid(
            rows: 2,
            cols: 2,
            xEdges: pitchXEdges,
            yEdges: pitchYEdges,
            pads: pads);

        FreeformHelperViewModelPrivateAccess.Grid(vm) = grid;

        Assert.Equal("Pitch X: X1=10 mm, X2=20 mm", vm.PitchSizeXSummary);
        Assert.Equal("Pitch Y: Y1=4 mm, Y2=6 mm", vm.PitchSizeYSummary);
    }


    [Fact]
    public async Task RebuildGrid_RaisesPitchSizeSummaryPropertyChanged()
    {
        var vm = new FreeformHelperViewModel
        {
            ActiveAreaWidth = 120m,
            ActiveAreaHeight = 80m,
            XChannels = 4m,
            YChannels = 2m,
        };
        var changed = new System.Collections.Concurrent.ConcurrentQueue<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.PropertyName))
            {
                changed.Enqueue(e.PropertyName!);
            }
        };

        await WaitForGridRebuildAsync(vm);
        changed.Clear();
        vm.GridPaddingPercent += 5m;
        await WaitForGridRebuildAsync(vm);

        Assert.Contains(nameof(FreeformHelperViewModel.PitchSizeXSummary), changed);
        Assert.Contains(nameof(FreeformHelperViewModel.PitchSizeYSummary), changed);
        Assert.StartsWith("Pitch X: X1=", vm.PitchSizeXSummary, StringComparison.Ordinal);
        Assert.StartsWith("Pitch Y: Y1=", vm.PitchSizeYSummary, StringComparison.Ordinal);
    }
}

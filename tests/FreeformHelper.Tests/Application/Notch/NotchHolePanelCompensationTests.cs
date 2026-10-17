using FreeformHelper.Domain.Geometry;
using Xunit;
using static FreeformHelper.Tests.NotchHolePanelFixture;

namespace FreeformHelper.Tests;

/// <summary>
/// A panel with a hole in it must get a notch table that behaves the same way wherever the hole
/// is, in whatever order the CAD arrives, and that touches only the cells next to the hole.
/// These tests compare tables with each other (symmetry, shift, order, growth). They do not use
/// the current table as an answer.
/// Two layouts run. "aligned" has one regular pad under every CAD pad, so the table has no
/// target legs. With the boundary caps off it only shows which pads exist, with the caps on a
/// partly cut pad also changes its percent. "staggered" moves the regular grid by 2 so each CAD pad
/// lies across two regular pads and the table carries cross-cell target legs. The comparison tests
/// run with the boundary caps off and on, because partial cells compensate differently. The anchor
/// test runs with the caps off and the determinism test with one setting each.
/// </summary>
public sealed class NotchHolePanelCompensationTests
{
    private const string Aligned = "aligned";
    private const string Staggered = "staggered";

    private static readonly string[] _variantNames =
        ["circle-center", "rect-center", "circle-off-center", "circle-near-edge", "two-holes", "rect-large", "strip"];

    public static TheoryData<string> AllVariantNames => [.. _variantNames];

    public static TheoryData<string, string, bool> AllTableCases => BuildCases(_variantNames);

    public static TheoryData<string, string, bool> ShiftCases => BuildCases(["circle-center", "rect-center", "circle-off-center"]);

    public static TheoryData<string, string, bool> OrderCases => BuildCases(["circle-center", "circle-off-center", "two-holes", "rect-large"]);

    public static TheoryData<string, bool> GrowthCases
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (var layout in new[] { Aligned, Staggered })
            {
                foreach (var caps in new[] { false, true })
                {
                    data.Add(layout, caps);
                }
            }

            return data;
        }
    }

    [Theory]
    [InlineData("circle-center", 4, 12)]
    [InlineData("rect-center", 4, 4)]
    [InlineData("circle-off-center", 0, 12)]
    [InlineData("circle-near-edge", 0, 6)]
    [InlineData("two-holes", 4, 6)]
    [InlineData("rect-large", 36, 28)]
    [InlineData("strip", 24, 0)]
    public void Fixture_RemovedAndPartialCellCounts_MatchGeometry(string variant, int expectedRemoved, int expectedPartial)
    {
        var panel = Build(Holes(variant));

        Assert.Equal(expectedRemoved, panel.FullyRemovedCellIndexes.Count);
        Assert.Equal(expectedPartial, panel.PartiallyRemovedCellIndexes.Count);
        Assert.All(panel.FullyRemovedCellIndexes, id => Assert.Equal(0d, panel.RemainingRatioByCellIndex[id]));
        Assert.All(panel.PartiallyRemovedCellIndexes, id => Assert.InRange(panel.RemainingRatioByCellIndex[id], 0.0001d, 0.9999d));
    }

    [Fact]
    public void Fixture_RectHoleCuttingCellsInHalf_RemainsHalfOfEachCell()
    {
        var panel = Build(Holes("rect-center"));

        var halfCells = panel.PartiallyRemovedCellIndexes.Select(id => panel.RemainingRatioByCellIndex[id]).ToList();

        Assert.Equal(4, halfCells.Count);
        Assert.All(halfCells, ratio => Assert.Equal(0.5d, ratio, 3));
    }

    [Theory]
    [MemberData(nameof(AllVariantNames))]
    public void Fixture_CadPads_HaveTheAreaTheOracleSays(string variant)
    {
        var panel = Build(Holes(variant));

        var expectedArea = panel.RemainingRatioByCellIndex.Sum() * PadArea;
        var cadArea = panel.Cad.Pads.Sum(static pad => pad.Polygon.Area());

        // A cut can leave two pieces in one cell, so there can be more pads than remaining cells.
        Assert.True(panel.Cad.Pads.Count >= (GridSize * GridSize) - panel.FullyRemovedCellIndexes.Count);
        Assert.Equal(expectedArea, cadArea, 3);
    }

    [Theory]
    [InlineData(Aligned, false)]
    [InlineData(Aligned, true)]
    [InlineData(Staggered, false)]
    [InlineData(Staggered, true)]
    public void Table_NoHolePanel_HasOneAnchorPerPad_AndLegsOnlyWhenStaggered(string layout, bool caps)
    {
        var table = Describe(Build([], gridOrigin: Origin(layout)), caps);

        Assert.Equal(GridSize * GridSize, table.Count);
        var anchorsWithLegs = table.Values.Count(static text => !text.EndsWith('|'));
        if (layout == Aligned)
        {
            Assert.Equal(0, anchorsWithLegs);
        }
        else
        {
            Assert.True(anchorsWithLegs > 100, $"only {anchorsWithLegs} anchors carry target legs");
        }
    }

    [Theory]
    [MemberData(nameof(AllVariantNames))]
    public void Table_AlignedHolePanel_HasAnAnchorForEveryPadThatIsNotRemoved(string variant)
    {
        var panel = Build(Holes(variant));

        var anchors = Describe(panel, caps: false).Keys.ToHashSet();

        var expected = Enumerable.Range(0, GridSize * GridSize).Where(index => !panel.FullyRemovedCellIndexes.Contains(index)).ToHashSet();
        Assert.True(expected.SetEquals(anchors), "anchors with rows differ from the pads that remain");
    }

    [Theory]
    [InlineData("circle-off-center")]
    [InlineData("circle-near-edge")]
    public void Table_AlignedPanelWithOnlyPartialCells_DiffersBetweenCapsOffAndOn(string variant)
    {
        var panel = Build(Holes(variant));

        var capsOff = Describe(panel, caps: false);
        var capsOn = Describe(panel, caps: true);

        Assert.Empty(panel.FullyRemovedCellIndexes);
        Assert.NotEmpty(panel.PartiallyRemovedCellIndexes);
        Assert.NotEqual(capsOff, capsOn);
    }

    [Theory]
    [MemberData(nameof(AllTableCases))]
    public void Table_HalfTurnRotatedPanel_MapsToTheOriginalTable(string variant, string layout, bool caps)
    {
        var holes = Holes(variant);
        var original = Describe(Build(holes, gridOrigin: Origin(layout)), caps);
        var rotatedHoles = holes.Select(static hole => Transform(hole, RotateHalfTurn)).ToList();
        var rotated = Describe(Build(rotatedHoles, gridOrigin: HalfTurnOrigin(layout)), caps, RotateHalfTurnIndex);

        Assert.NotEmpty(original);
        Assert.Equal(original, rotated);
    }

    [Theory]
    [MemberData(nameof(ShiftCases))]
    public void Table_HoleShiftedByOneCell_ShiftsEveryAnchorNextToTheHoleTheSameWay(string variant, string layout, bool caps)
    {
        var holes = Holes(variant);
        var origin = Origin(layout);
        var panel = Build(holes, gridOrigin: origin);
        var original = Describe(panel, caps);
        var noHole = Describe(Build([], gridOrigin: origin), caps);
        var shiftedHoles = holes.Select(static hole => Transform(hole, static point => new Point2(point.X + Pitch, point.Y))).ToList();
        var shifted = Describe(Build(shiftedHoles, gridOrigin: origin), caps, OneColumnLeftIndex);
        var near = AnchorsWithinOneCell(new HashSet<int>(panel.FullyRemovedCellIndexes.Concat(panel.PartiallyRemovedCellIndexes)));

        if (layout == Staggered || caps || panel.FullyRemovedCellIndexes.Count > 0)
        {
            Assert.Contains(near, anchor => original.GetValueOrDefault(anchor) != noHole.GetValueOrDefault(anchor));
        }

        Assert.All(near, anchor => Assert.Equal(original.GetValueOrDefault(anchor), shifted.GetValueOrDefault(anchor)));
    }

    [Theory]
    [MemberData(nameof(OrderCases))]
    public void Table_CadOrderReversed_GivesTheSameDescription(string variant, string layout, bool caps)
    {
        var holes = Holes(variant);
        var origin = Origin(layout);
        var forward = Describe(Build(holes, gridOrigin: origin), caps);
        var reversed = Describe(Build(holes, reverseCadOrder: true, gridOrigin: origin), caps);

        Assert.NotEmpty(forward);
        Assert.Equal(forward, reversed);
    }

    [Theory]
    [InlineData("circle-center", Staggered, true)]
    [InlineData("rect-large", Staggered, false)]
    public void Table_SameInputTwice_GivesIdenticalRows(string variant, string layout, bool caps)
    {
        var panel = Build(Holes(variant), gridOrigin: Origin(layout));

        var first = Generate(panel, caps);
        var second = Generate(panel, caps);

        Assert.NotEmpty(first.Rows);
        Assert.Equal(
            first.Rows.Select(static row => (row.IcIndex, row.DiffIndex, row.CadPadId, Values: string.Join(",", row.Values), row.Comment)),
            second.Rows.Select(static row => (row.IcIndex, row.DiffIndex, row.CadPadId, Values: string.Join(",", row.Values), row.Comment)));
    }

    [Theory]
    [MemberData(nameof(AllTableCases))]
    public void Table_DiffersFromTheNoHolePanelOnlyNextToTheHole(string variant, string layout, bool caps)
    {
        var origin = Origin(layout);
        var panel = Build(Holes(variant), gridOrigin: origin);
        var noHole = Describe(Build([], gridOrigin: origin), caps);
        var withHole = Describe(panel, caps);

        var affected = new HashSet<int>(panel.FullyRemovedCellIndexes.Concat(panel.PartiallyRemovedCellIndexes));
        var changedAnchors = ChangedAnchors(noHole, withHole);

        if (layout == Staggered || caps || panel.FullyRemovedCellIndexes.Count > 0)
        {
            Assert.NotEmpty(changedAnchors);
        }

        Assert.All(changedAnchors, anchor => Assert.True(
            IsWithinOneCell(anchor, affected),
            $"anchor {anchor} changed but is more than one cell away from a cell the hole touches"));
    }

    [Theory]
    [MemberData(nameof(GrowthCases))]
    public void Table_GrowingCircleHole_NeverShrinksTheChangedAnchorSet(string layout, bool caps)
    {
        var origin = Origin(layout);
        var noHole = Describe(Build([], gridOrigin: origin), caps);
        HashSet<int>? previous = null;
        var sizes = new List<int>();
        foreach (var radius in new[] { 1d, 3d, 5d, 7d, 9d, 11d })
        {
            var table = Describe(Build([Circle(PanelSize / 2d, PanelSize / 2d, radius)], gridOrigin: origin), caps);
            var changed = ChangedAnchors(noHole, table).ToHashSet();
            if (previous is not null)
            {
                Assert.True(previous.IsSubsetOf(changed), $"radius {radius}: anchors {string.Join(",", previous.Except(changed))} stopped changing");
            }

            sizes.Add(changed.Count);
            previous = changed;
        }

        Assert.True(sizes[^1] > sizes[0], $"changed anchor counts did not grow: {string.Join(",", sizes)}");
    }

    private static TheoryData<string, string, bool> BuildCases(IEnumerable<string> variants)
    {
        var data = new TheoryData<string, string, bool>();
        foreach (var variant in variants)
        {
            foreach (var layout in new[] { Aligned, Staggered })
            {
                foreach (var caps in new[] { false, true })
                {
                    data.Add(variant, layout, caps);
                }
            }
        }

        return data;
    }

    private static double Origin(string layout) => layout == Aligned ? 0d : StaggerForwardOrigin;

    private static double HalfTurnOrigin(string layout) => layout == Aligned ? 0d : StaggerBackwardOrigin;

    private static IReadOnlyDictionary<int, string> Describe(HolePanel panel, bool caps, Func<int, int>? mapIndex = null) =>
        DescribeByAnchor(Generate(panel, caps), mapIndex ?? (static index => index));

    private static List<int> ChangedAnchors(IReadOnlyDictionary<int, string> before, IReadOnlyDictionary<int, string> after) =>
        before.Keys.Union(after.Keys)
            .Where(anchor => before.GetValueOrDefault(anchor) != after.GetValueOrDefault(anchor))
            .OrderBy(static anchor => anchor)
            .ToList();

    private static bool IsWithinOneCell(int anchor, IReadOnlySet<int> affected)
    {
        var row = anchor / GridSize;
        var col = anchor % GridSize;
        return affected.Any(index => Math.Abs((index / GridSize) - row) <= 1 && Math.Abs((index % GridSize) - col) <= 1);
    }

    private static List<int> AnchorsWithinOneCell(IReadOnlySet<int> affected) =>
        Enumerable.Range(0, GridSize * GridSize).Where(anchor => IsWithinOneCell(anchor, affected)).ToList();

    private static List<Polygon2> Holes(string variant) => variant switch
    {
        "circle-center" => [Circle(30d, 30d, 7.5d)],
        "rect-center" => [Rect(22.5d, 25d, 37.5d, 35d)],
        "circle-off-center" => [Circle(20d, 40d, 6d)],
        "circle-near-edge" => [Circle(8d, 30d, 5d)],
        "two-holes" => [Circle(18d, 30d, 4d), Rect(40d, 25d, 50d, 35d)],
        "rect-large" => [Rect(14d, 14d, 46d, 46d)],
        "strip" => [Rect(25d, 0d, 35d, PanelSize)],
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "unknown hole panel variant"),
    };
}

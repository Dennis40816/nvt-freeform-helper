using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class AllocationAnchorClassificationCharacterizationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameRatioAndScorePreference_SelectsSamePad(bool reverseGridOrder)
    {
        var (cad, grid, _, _) = CreateScenario(reverseGridOrder, cadMaxX: 15);

        // REG90 has 2/3 of the CAD area and score 0.9; REG10 has 1/3 and score 0.1.
        AssertProjections(cad, grid, expectedAnchorId: 90, expectedClassification: FreeformType.XWay);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpposingRatioAndScorePreferences_PreserveDifferentSelections(bool reverseGridOrder)
    {
        var (cad, grid, _, right) = CreateScenario(reverseGridOrder, cadMaxX: 15);
        right.MatchScore = 0.95;

        // Allocation keeps the larger overlap; classification follows the higher score.
        AssertProjections(cad, grid, expectedAnchorId: 90, expectedClassification: FreeformType.YWay);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchedFreeformPriority_OverridesRatioForAllocationButNotScoreForClassification(bool reverseGridOrder)
    {
        var (cad, grid, left, _) = CreateScenario(reverseGridOrder, cadMaxX: 15);
        left.Freeform = FreeformType.None;

        // REG10 is freeform despite its smaller overlap and lower score.
        AssertProjections(cad, grid, expectedAnchorId: 10, expectedClassification: FreeformType.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameCadNonFreeformFallback_PrecedesUnrelatedFreeformAllocation(bool reverseGridOrder)
    {
        var (cad, grid, left, right) = CreateScenario(reverseGridOrder, cadMaxX: 15);
        left.MatchedCadPadId = 99;
        right.Freeform = FreeformType.None;

        AssertProjections(cad, grid, expectedAnchorId: 10, expectedClassification: FreeformType.None);
    }

    [Theory]
    [InlineData(0, false, 10, FreeformType.XWay)]
    [InlineData(0, true, 10, FreeformType.YWay)]
    [InlineData(1, false, 90, FreeformType.XWay)]
    [InlineData(1, true, 90, FreeformType.YWay)]
    public void EqualRatioAndScore_AllocationOrdersByIcThenDiff_ClassificationKeepsGridFirst(
        int rightIcIndex, bool reverseGridOrder, int expectedAnchorId, FreeformType expectedClassification)
    {
        var (cad, grid, left, right) = CreateScenario(reverseGridOrder);
        right.MatchScore = left.MatchScore;
        right.IcIndex = rightIcIndex;

        // Both ratios are 1/2 (Q7=64). REG90 is IC0/diff9; REG10 is the supplied IC/diff1.
        AssertProjections(cad, grid, expectedAnchorId, expectedClassification);
    }

    [Theory]
    [InlineData(false, 90, FreeformType.XWay)]
    [InlineData(true, 10, FreeformType.YWay)]
    public void AllOrderingKeysTied_BothKeepGridFirstWithoutRegularPadIdTieBreak(
        bool reverseGridOrder, int expectedAnchorId, FreeformType expectedClassification)
    {
        var (cad, grid, left, right) = CreateScenario(reverseGridOrder);
        right.MatchScore = left.MatchScore;
        right.DiffIndex = left.DiffIndex;

        // Synthetic duplicate IC/diff keys isolate the final stable-order fallback at these public seams.
        // This does not declare duplicate mappings valid for generation or introduce an ID tie-break.
        AssertProjections(cad, grid, expectedAnchorId, expectedClassification);
    }

    [Theory]
    [InlineData(null, FreeformType.YWay, false, 10)]
    [InlineData(null, FreeformType.YWay, true, 10)]
    [InlineData(99, FreeformType.YWay, false, 10)]
    [InlineData(99, FreeformType.YWay, true, 10)]
    [InlineData(null, FreeformType.None, false, 90)]
    [InlineData(null, FreeformType.None, true, 90)]
    [InlineData(99, FreeformType.None, false, 90)]
    [InlineData(99, FreeformType.None, true, 90)]
    public void NoMatchingCad_AllocationFallsBackToFreeformThenAnyOverlap_ClassificationIsNone(
        int? matchedCadId, FreeformType rightFreeform, bool reverseGridOrder, int expectedAnchorId)
    {
        var (cad, grid, left, right) = CreateScenario(reverseGridOrder, cadMaxX: 15);
        left.MatchedCadPadId = matchedCadId;
        right.MatchedCadPadId = matchedCadId;
        left.Freeform = FreeformType.None;
        right.Freeform = rightFreeform;

        AssertProjections(cad, grid, expectedAnchorId, expectedClassification: FreeformType.None);
    }

    private static void AssertProjections(
        CadPad cad, RegularGrid grid, int expectedAnchorId, FreeformType expectedClassification)
    {
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnabledVersions = [NotchAlgorithmVersion.V22],
                LinkVersionThresholds = false,
                ThresholdPercentV22 = 0,
            },
        };

        // Compare the same CAD/grid through existing public readers without generating a table or firmware C.
        var eligibility = new NotchTableGenerator().EvaluateCadRowEligibility(cad, grid, settings);
        var classification = NotchAllocationService.GetFreeformType(cad, grid);

        Assert.Equal(expectedAnchorId, eligibility.AnchorRegularPadId);
        Assert.Equal(expectedClassification, classification);
    }

    private static (CadPad Cad, RegularGrid Grid, RegularPad Left, RegularPad Right) CreateScenario(
        bool reverseGridOrder, double cadMaxX = 20)
    {
        var cad = new CadPad(7, "Synthetic CAD", "Test", Rectangle(0, cadMaxX));
        var left = new RegularPad(0, 0, 90, Rectangle(0, 10))
        {
            IcIndex = 0,
            DiffIndex = 9,
            MatchedCadPadId = cad.Id,
            MatchScore = 0.9,
            Freeform = FreeformType.XWay,
        };
        var right = new RegularPad(0, 1, 10, Rectangle(10, 20))
        {
            IcIndex = 0,
            DiffIndex = 1,
            MatchedCadPadId = cad.Id,
            MatchScore = 0.1,
            Freeform = FreeformType.YWay,
        };

        // Both CAD shapes query both cells, including when the synthetic pad enumeration is reversed.
        var grid = new RegularGrid(1, 2, [0, 10, 20], [0, 10], reverseGridOrder ? [right, left] : [left, right]);
        return (cad, grid, left, right);
    }

    private static Polygon2 Rectangle(double minX, double maxX) => new(
    [
        new Point2(minX, 0),
        new Point2(maxX, 0),
        new Point2(maxX, 10),
        new Point2(minX, 10),
    ]);
}

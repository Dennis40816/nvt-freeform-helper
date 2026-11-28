// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchToFullDiagnosticsTests
{
    private const string EmptyDiagnostics = " : ov=0, src=0, blk=0, reach=0, blocker=0, owners=-, ownerCnt=0, ownerShare=-, "
        + "boundary=N, candidate=N, tofull=N";

    [Fact]
    public void DiagnosticsWithoutDebugInfosReturnsNull()
    {
        Assert.Null(NotchToFullDiagnostics.BuildToFullDiagnosticsText(CreateCompensation()));
    }

    [Fact]
    public void DiagnosticsFormatsFullLineWithInvariantAreas()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var info = CreateDebugInfo() with
            {
                OverlapArea = 0.5,
                SourceArea = 1234.567891,
                BlockedArea = 0.0000001,
                ReachableArea = 7.1234567,
                BlockerCandidateCount = 4,
                OwnerCadPadCount = 2,
                OwnerCadPadIds = [42, 9],
                IsBoundaryRegular = true,
                IsToFullApplied = true,
            };

            var result = NotchToFullDiagnostics.BuildToFullDiagnosticsText(CreateCompensation(info));

            Assert.Equal(
                "IC2/diff3 reg7 : ov=0.5, src=1234.567891, blk=0, reach=7.123457, blocker=4, owners=42;9, "
                    + "ownerCnt=2, ownerShare=-, boundary=Y, candidate=N, tofull=Y (RULE)",
                result);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void DiagnosticsOrdersRowsByIcThenDiffThenRegularId()
    {
        var compensation = CreateCompensation(
            CreateDebugInfo(regularPadId: 1, icIndex: 1, diffIndex: 0),
            CreateDebugInfo(regularPadId: 7, icIndex: 0, diffIndex: 3),
            CreateDebugInfo(regularPadId: 9, icIndex: 0, diffIndex: 2),
            CreateDebugInfo(regularPadId: 4, icIndex: 0, diffIndex: 3));
        var expected = string.Join(Environment.NewLine,
            "IC1/diff2 reg9" + EmptyDiagnostics + " (RULE)",
            "IC1/diff3 reg4" + EmptyDiagnostics + " (RULE)",
            "IC1/diff3 reg7" + EmptyDiagnostics + " (RULE)",
            "IC2/diff0 reg1" + EmptyDiagnostics + " (RULE)");

        Assert.Equal(expected, NotchToFullDiagnostics.BuildToFullDiagnosticsText(compensation));
    }

    [Theory]
    [InlineData(8, "")]
    [InlineData(10, "+2 more regular(s)")]
    public void DiagnosticsShowsEightSortedRowsAndOnlyReportsHiddenRows(int infoCount, string expectedSuffix)
    {
        var infos = Enumerable.Range(1, infoCount).Reverse()
            .Select(static id => CreateDebugInfo(regularPadId: id)).ToArray();
        var expected = string.Join(Environment.NewLine,
            "IC2/diff3 reg1" + EmptyDiagnostics + " (RULE)",
            "IC2/diff3 reg2" + EmptyDiagnostics + " (RULE)",
            "IC2/diff3 reg3" + EmptyDiagnostics + " (RULE)",
            "IC2/diff3 reg4" + EmptyDiagnostics + " (RULE)",
            "IC2/diff3 reg5" + EmptyDiagnostics + " (RULE)",
            "IC2/diff3 reg6" + EmptyDiagnostics + " (RULE)",
            "IC2/diff3 reg7" + EmptyDiagnostics + " (RULE)",
            "IC2/diff3 reg8" + EmptyDiagnostics + " (RULE)");
        if (expectedSuffix.Length > 0)
        {
            expected += Environment.NewLine + expectedSuffix;
        }

        Assert.Equal(expected, NotchToFullDiagnostics.BuildToFullDiagnosticsText(CreateCompensation(infos)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t")]
    public void DiagnosticsWithoutOwnersUsesDashAndFallsBackForBlankRule(string ruleCode)
    {
        var info = CreateDebugInfo() with { ToFullRuleCode = ruleCode };

        Assert.Equal("IC2/diff3 reg7" + EmptyDiagnostics + " (UNKNOWN_RULE)",
            NotchToFullDiagnostics.BuildToFullDiagnosticsText(CreateCompensation(info)));
    }

    [Fact]
    public void DiagnosticsPreservesOwnerOrderAndTruncatesAfterTwelveIds()
    {
        var info = CreateDebugInfo() with
        {
            OwnerCadPadCount = 13,
            OwnerCadPadIds = [13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1],
            IsToFullBoundaryCandidate = true,
        };

        Assert.Equal(
            "IC2/diff3 reg7 : ov=0, src=0, blk=0, reach=0, blocker=0, owners=13;12;11;10;9;8;7;6;5;4;3;2;+1, "
                + "ownerCnt=13, ownerShare=-, boundary=N, candidate=Y, tofull=N (RULE)",
            NotchToFullDiagnostics.BuildToFullDiagnosticsText(CreateCompensation(info)));
    }

    [Fact]
    public void OwnerInfosWithoutVisibleCadPadsIsEmpty()
    {
        var regular = TestGeometryFactory.CreateRegularPad(0, 0, 7, 0, 0, 10, 10);

        Assert.Empty(NotchToFullDiagnostics.ComputeOwnerInfosForRegular(regular, [], 0.2));
    }

    [Fact]
    public void OwnerInfosSkipsCadBoundsOutsideRegular()
    {
        var regular = TestGeometryFactory.CreateRegularPad(0, 0, 7, 0, 0, 10, 10);
        var cad = TestGeometryFactory.CreateCadPad(99, "signal", 11, 0, 12, 10);

        Assert.Empty(NotchToFullDiagnostics.ComputeOwnerInfosForRegular(regular, [cad], 0.0));
    }

    [Theory]
    [InlineData(1.0, 0.2)]
    [InlineData(2.0, 0.2)]
    [InlineData(5e-14, 0.0)]
    [InlineData(1e-13, 0.0)]
    public void OwnerInfosSkipsOverlapAtOrBelowRatioThresholdOrAreaFloor(double width, double strictOverlapRatio)
    {
        var regular = TestGeometryFactory.CreateRegularPad(0, 0, 7, 0, 0, 10, 10);
        var cad = TestGeometryFactory.CreateCadPad(5, "signal", 0, 0, width, 10);

        Assert.Empty(NotchToFullDiagnostics.ComputeOwnerInfosForRegular(regular, [cad], strictOverlapRatio));
    }

    [Fact]
    public void OwnerInfosUsesClippedRegularAreaPercentAndOrdersByDescendingRatioThenCadId()
    {
        var regular = TestGeometryFactory.CreateRegularPad(0, 0, 7, 0, 0, 10, 10);
        var cad30 = TestGeometryFactory.CreateCadPad(30, "signal", 0, 0, 3, 10);
        var cad20 = TestGeometryFactory.CreateCadPad(20, "signal", 7, 0, 12, 10);
        var cad10 = TestGeometryFactory.CreateCadPad(10, "signal", -5, 0, 5, 10);

        var owners = NotchToFullDiagnostics.ComputeOwnerInfosForRegular(regular, [cad30, cad20, cad10], 0.2);

        (int CadId, double RatioPercent)[] expected = [(10, 50.0), (20, 30.0), (30, 30.0)];
        Assert.Equal(expected, owners);
    }

    private static NotchV22CompensationResult CreateCompensation(params NotchV22RegularDebugInfo[] infos) => new(
        ToRegularRatio: 1.0,
        ToFullRatio: 1.0,
        CombinedRatio: 1.0,
        IsToFullEnabled: true,
        Stage3Area: 100,
        OverlapAreaTotal: 0,
        OverlappedRegularAreaTotal: 0,
        OverlapRegularCount: infos.Length,
        ToFullSeedPolygons: [],
        ToFullCandidatePolygons: [],
        ToFullPolygons: [],
        RegularDebugInfos: infos);

    private static NotchV22RegularDebugInfo CreateDebugInfo(int regularPadId = 7, int icIndex = 1, int diffIndex = 3) => new(
        RegularPadId: regularPadId,
        RegularIndex: regularPadId,
        IcIndex: icIndex,
        DiffIndex: diffIndex,
        Row: 0,
        Col: 0,
        RegularArea: 100,
        OverlapArea: 0,
        SourceArea: 0,
        BlockedArea: 0,
        ReachableArea: 0,
        Stage3EffectiveArea: 0,
        SourceCellCount: 0,
        BlockedCellCount: 0,
        ReachableCellCount: 0,
        TotalCellCount: 0,
        BlockerCandidateCount: 0,
        OwnerCadPadCount: 0,
        OwnerCadPadIds: [],
        IsBoundaryRegular: false,
        IsToFullBoundaryCandidate: false,
        HasEffectiveExpansion: false,
        HasDirectionalBlocker: false,
        ToFullRuleTrace: [],
        ToFullRuleCode: "RULE",
        IsToFullApplied: false);
}

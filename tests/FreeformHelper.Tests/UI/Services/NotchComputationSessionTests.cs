// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchComputationSessionTests
{
    [Fact]
    public void SameRequestReusesResolvedResultAndCountsHit()
    {
        var session = new NotchComputationSession();
        var request = CreateRequest();
        var first = Assert.IsType<NotchV22ResolvedResult>(session.GetOrBuild(request));

        var second = session.GetOrBuild(request);

        Assert.Same(first, second);
        Assert.True(session.Contains(request.CadPad.Id));
        Assert.Equal(new NotchComputationCacheMetrics(0, 1, 0, 1, 1, 0), session.GetMetrics());
    }

    [Fact]
    public void ChangedCacheKeyBuildsAnotherResolvedResult()
    {
        var session = new NotchComputationSession();
        var request = CreateRequest();
        var first = Assert.IsType<NotchV22ResolvedResult>(session.GetOrBuild(request));

        var second = Assert.IsType<NotchV22ResolvedResult>(
            session.GetOrBuild(request with { EnableToFull = false }));

        Assert.NotSame(first, second);
        Assert.False(second.Compensation.IsToFullEnabled);
        Assert.Equal(new NotchComputationCacheMetrics(0, 1, 0, 0, 2, 0), session.GetMetrics());
    }

    [Fact]
    public void InvalidateClearsEntriesAdvancesRevisionAndCountsNextMiss()
    {
        var session = new NotchComputationSession();
        var request = CreateRequest();
        var first = Assert.IsType<NotchV22ResolvedResult>(session.GetOrBuild(request));
        var revision = session.Revision;

        session.Invalidate();

        Assert.False(session.Contains(request.CadPad.Id));
        Assert.Equal(revision + 1, session.Revision);
        Assert.Equal(new NotchComputationCacheMetrics(1, 0, 1, 0, 1, 1), session.GetMetrics());

        var second = Assert.IsType<NotchV22ResolvedResult>(session.GetOrBuild(request));

        Assert.NotSame(first, second);
        Assert.Equal(new NotchComputationCacheMetrics(1, 1, 1, 0, 2, 1), session.GetMetrics());
    }

    [Fact]
    public void ColdCacheWithoutBuildReturnsNullAndDoesNotCountMiss()
    {
        var session = new NotchComputationSession();
        var request = CreateRequest();

        var resolved = session.GetOrBuild(request, buildIfMissing: false);

        Assert.Null(resolved);
        Assert.False(session.Contains(request.CadPad.Id));
        Assert.Equal(new NotchComputationCacheMetrics(0, 0, 0, 0, 0, 0), session.GetMetrics());
    }

    private static NotchComputationRequest CreateRequest()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(
            1, 1, cellWidth: 10, cellHeight: 10, diffIndexSelector: static (_, _) => 0);
        var regular = grid.Pads[0];
        regular.Freeform = FreeformType.XWay;
        regular.MatchedCadPadId = 7;
        regular.MatchScore = 1.0;
        var cad = TestGeometryFactory.CreateCadPad(7, "signal", 0, 0, 5, 10);
        return new NotchComputationRequest(
            grid,
            cad,
            [cad],
            new HashSet<int> { regular.RegularPadId },
            new ProjectSettings(),
            NotchCompensationModel.CurrentGain,
            EnableToRegular: true,
            EnableToFull: true,
            EnableToFullRuleEngine: true,
            EnableToFullRuleTrace: true,
            StrictOverlapRatio: 0.001,
            AnchorIcIndex: 0,
            AnchorDiffIndex: 0,
            AllocationAreaMode: NotchV22TargetAllocationAreaMode.TargetRegularStage3Coverage,
            CadPoolFingerprint: 1,
            ActiveRegularHash: 2,
            GridFingerprint: 3);
    }
}

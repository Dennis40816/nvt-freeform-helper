// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchDisplayProjectorOwnerCountBehaviorTests
{
    private static readonly int[] _ownerIds = [123, 124];

    [Fact]
    public void Build_DiagnosticLineWrittenByTheProducer_ParsesOwnersAndReason()
    {
        using var culture = TestCulture.UseEnUs();
        // The To Full diagnostics producer writes ownerCnt between owners and ownerShare.
        const string diagnostics =
            "IC1/diff7 reg42 : ov=10, src=10, blk=0, reach=15, blocker=0, " +
            "owners=123;124, ownerCnt=2, ownerShare=-, boundary=Y, candidate=Y, tofull=Y (EXPAND_CLEAR_PATH)";

        var display = Build(diagnostics);

        var entry = Assert.Single(display.DiagnosticEntries);
        Assert.Equal(42, entry.RegularPadId);
        Assert.Equal("EXPAND_CLEAR_PATH", entry.RuleCode);
        Assert.True(entry.IsToFull);
        Assert.Equal(_ownerIds, entry.OwnerCadPadIds);
        Assert.Equal(_ownerIds, display.OwnerCadIds);
        Assert.NotEmpty(display.OwnerSummaryText);
        Assert.Contains("EXPAND_CLEAR_PATH", display.ToFullReasonFullText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DiagnosticLineWithoutOwnerCount_StillParsesOwnersAndReason()
    {
        using var culture = TestCulture.UseEnUs();
        const string diagnostics =
            "IC1/diff7 reg42 : ov=10, src=10, blk=0, reach=15, blocker=0, " +
            "owners=123;124, ownerShare=-, boundary=Y, candidate=Y, tofull=Y (EXPAND_CLEAR_PATH)";

        var display = Build(diagnostics);

        var entry = Assert.Single(display.DiagnosticEntries);
        Assert.Equal("EXPAND_CLEAR_PATH", entry.RuleCode);
        Assert.Equal(_ownerIds, entry.OwnerCadPadIds);
        Assert.Equal(_ownerIds, display.OwnerCadIds);
        Assert.Contains("EXPAND_CLEAR_PATH", display.ToFullReasonFullText, StringComparison.Ordinal);
    }

    private static NotchDisplayProjection Build(string diagnostics)
    {
        return NotchDisplayProjector.Build(
            toRegularRatio: 1, toFullRatio: 1.5, combinedRatio: 1.5,
            isToFullEnabled: true, stage3Area: 15, targets: null, diagnostics: diagnostics);
    }
}

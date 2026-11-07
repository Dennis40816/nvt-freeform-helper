// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadInspectorSnapshotDetailsTests
{
    [Theory]
    [InlineData("Auto", "112", 0.6156)]
    [InlineData("Manual", "-", 0.0)]
    public void CadDetailsPreserveTheFourBoundValues(string diffSource, string diffIndex, double confidence)
    {
        var snapshot = PadInspectorDisplayBehaviorTests.CreateCadSnapshot(274);
        var cad = snapshot.Cad! with
        {
            CadOutputFwDiffAssignmentModeText = diffSource,
            DxfIndexDisplayText = diffIndex,
            MatchConfidence = confidence,
        };
        snapshot = snapshot with { Cad = cad };

        var details = Assert.IsType<PadInspectorSnapshotDetails>(snapshot.Details);

        Assert.Equal(cad.CadOutputFwDiffAssignmentModeText, details.DiffSource);
        Assert.Equal(cad.DxfIndexDisplayText, details.DiffIndex);
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, "{0:P1}", cad.MatchConfidence), details.MatchConfidenceText);
        Assert.Equal(cad.MatchText, details.MatchText);
    }

    [Theory]
    [InlineData("Auto", 72, 0.6156)]
    [InlineData("Unmatched", -1, 0.0)]
    public void RegularDetailsPreserveTheFourBoundValues(string diffSource, int diffIndex, double confidence)
    {
        var snapshot = PadInspectorDisplayBehaviorTests.CreateRegularSnapshot(4616);
        var regular = snapshot.Regular! with
        {
            DiffSource = diffSource,
            DiffIndex = diffIndex,
            MatchConfidence = confidence,
        };
        snapshot = snapshot with { Regular = regular };

        var details = Assert.IsType<PadInspectorSnapshotDetails>(snapshot.Details);

        Assert.Equal(regular.DiffSource, details.DiffSource);
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, "{0}", regular.DiffIndex), details.DiffIndex);
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, "{0:P1}", regular.MatchConfidence), details.MatchConfidenceText);
        Assert.Equal(regular.MatchText, details.MatchText);
    }

    [Fact]
    public void SnapshotWithoutEitherPadHasNoDetails()
    {
        var snapshot = new PadInspectorSnapshot("none", null, null);

        Assert.Null(snapshot.Details);
    }
}

// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchCandidateSelectionTests
{
    [Theory]
    [InlineData(0, "To Full influence")]
    [InlineData(1, "has targets")]
    [InlineData(2, "anchor matched CAD")]
    [InlineData(3, "larger source area")]
    [InlineData(4, "higher combine")]
    [InlineData(5, "lower CAD id")]
    public void WinnerAndReasonUseTheSameOrderedKey(int key, string expected)
    {
        var winner = new NotchV22CandidateExplanation(1, 0, true, true, true, 100, 100, 1);
        var rival = key switch
        {
            0 => winner with { CadPadId = 2, HasToFullInfluence = false },
            1 => winner with { CadPadId = 2, HasLegs = false },
            2 => winner with { CadPadId = 2, IsAnchorMatchedCad = false },
            3 => winner with { CadPadId = 2, SourceAreaScore = 99 },
            4 => winner with { CadPadId = 2, CombinePercent = 99 },
            _ => winner with { CadPadId = 2 },
        };

        var selected = NotchV22CandidateSelection.Select(new[] { rival, winner }, static candidate =>
            new NotchV22CandidateSelection.Rank(candidate.HasToFullInfluence, candidate.HasLegs,
                candidate.IsAnchorMatchedCad, candidate.SourceAreaScore, candidate.CombinePercent, candidate.CadPadId));

        Assert.Same(winner, selected);
        Assert.Equal(expected, NotchV22CandidateSelection.DecidingKey([rival, winner]));
        Assert.NotSame(rival, selected);
    }
}

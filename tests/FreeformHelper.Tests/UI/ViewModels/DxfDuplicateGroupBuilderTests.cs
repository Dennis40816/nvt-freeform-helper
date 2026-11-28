// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

public sealed class DxfDuplicateGroupBuilderTests
{
    private static readonly string[] _expectedGroupTitles = ["IC1 · diff 20", "IC2 · diff 10"];

    [Fact]
    public void BuildFormatsDuplicateGroupWithRawAndMaskedGridCandidates()
    {
        using var culture = TestCulture.UseEnUs();
        var pads = ImportPads(("signal", 0, 0, 15, 10), ("signal", 0, 0, 14, 10));
        var inputs = CreateInputs(pads, [7, 7], [0, 0]) with
        {
            CadToRegularByCadId = new Dictionary<int, IReadOnlyList<PadMatchLink>>
            {
                [pads[0].Id] = [new(pads[0].Id, 20, 100, 1, 1), new(pads[0].Id, 21, 90, 0.9, 0.9)],
                [pads[1].Id] = [new(pads[1].Id, 21, 100, 1, 1)],
            },
            ActiveRegularVisibilityMaskPadIds = new HashSet<int> { 21 },
        };
        var grid = CreateGrid(pads);

        var result = DxfDuplicateGroupBuilder.Build(pads, inputs, grid);

        var group = Assert.Single(result.Groups);
        Assert.Equal(0, group.IcIndex);
        Assert.Equal(7, group.DiffIndex);
        Assert.Equal(new[] { pads[0].Id, pads[1].Id }, group.CadPadIds);
        Assert.Equal("IC1 · diff 7", group.TitleText);
        Assert.Equal(
            $"CAD {pads[0].Id} [signal] -> current 7, raw 10, masked 11{Environment.NewLine}" +
            $"CAD {pads[1].Id} [signal] -> current 7, raw 11, masked 11", group.CadSummaryText);
        Assert.Equal(
            "Use Select to load these CAD pads into the shared diff-shift workflow. Raw candidates: 10/11 · Masked candidates: 11.",
            group.SuggestionText);
        Assert.Equal(
            $"Duplicate CAD Output FW Diff: 1 group(s), 2 CAD pad(s). Sample: IC1/diff7 -> CAD {pads[0].Id}, {pads[1].Id}.",
            result.Summary);
    }

    [Fact]
    public void BuildUsesAssignmentDecisionsBeforeGridCandidates()
    {
        var pads = ImportPads(("signal", 0, 0, 15, 10), ("signal", 0, 0, 14, 10));
        var inputs = CreateInputs(pads, [7, 7], [0, 0]) with
        {
            CadOutputFwDiffAssignmentDecisionsByCadId = new Dictionary<int, CadOutputFwDiffAssignmentDecision>
            {
                [pads[0].Id] = CreateDecision(pads[0].Id, 31),
                [pads[1].Id] = CreateDecision(pads[1].Id, 30),
            },
            CadToRegularByCadId = new Dictionary<int, IReadOnlyList<PadMatchLink>>
            {
                [pads[0].Id] = [new(pads[0].Id, 20, 100, 1, 1)],
                [pads[1].Id] = [new(pads[1].Id, 21, 100, 1, 1)],
            },
        };

        var result = DxfDuplicateGroupBuilder.Build(pads, inputs, CreateGrid(pads));

        var group = Assert.Single(result.Groups);
        Assert.Equal(
            $"CAD {pads[0].Id} [signal] -> current 7, raw 31, masked -{Environment.NewLine}" +
            $"CAD {pads[1].Id} [signal] -> current 7, raw 30, masked -", group.CadSummaryText);
        Assert.Equal(
            "Use Select to load these CAD pads into the shared diff-shift workflow. Raw candidates: 30/31 · Masked candidates: -.",
            group.SuggestionText);
    }

    [Fact]
    public void BuildReturnsEmptyGroupsAndNoneSummaryWhenDiffIndicesAreUnique()
    {
        var pads = ImportPads(("alpha", 0, 0, 10, 10), ("beta", 20, 0, 30, 10));
        var inputs = CreateInputs(pads, [10, 11], [0, 0]);

        var result = DxfDuplicateGroupBuilder.Build(pads, inputs, grid: null);

        Assert.Empty(result.Groups);
        Assert.Equal("Duplicate CAD Output FW Diff: none.", result.Summary);
    }

    [Fact]
    public void BuildOrdersTwoGroupsByIcAndSummarizesAllImpactedPads()
    {
        var pads = ImportPads(("alpha", 0, 0, 15, 10), ("alpha", 0, 0, 14, 10),
            ("beta", 20, 0, 35, 10), ("beta", 20, 0, 34, 10));
        var inputs = CreateInputs(pads, [10, 10, 20, 20], [1, 1, 0, 0]);

        var result = DxfDuplicateGroupBuilder.Build(pads.Reverse().ToArray(), inputs, grid: null);

        Assert.Equal(_expectedGroupTitles, result.Groups.Select(static group => group.TitleText));
        Assert.Equal(new[] { pads[2].Id, pads[3].Id }, result.Groups[0].CadPadIds);
        Assert.Equal(new[] { pads[0].Id, pads[1].Id }, result.Groups[1].CadPadIds);
        Assert.Equal(
            $"Duplicate CAD Output FW Diff: 2 group(s), 4 CAD pad(s). Sample: IC1/diff20 -> CAD {pads[2].Id}, {pads[3].Id}.",
            result.Summary);
    }

    private static CadPad[] ImportPads(params (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] pads)
    {
        var path = CreateTempDxfFile(pads);
        try
        {
            return new DxfImportService().ImportFromPath(path, new DxfImportOptions()).Pads.OrderBy(static pad => pad.Id).ToArray();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static DxfDuplicateGroupInputs CreateInputs(CadPad[] pads, int[] diffs, int[] ics) => new(
        pads.Select((pad, index) => (pad.Id, Diff: diffs[index])).ToDictionary(static pair => pair.Id, static pair => pair.Diff),
        new Dictionary<int, CadOutputFwDiffAssignmentDecision>(),
        pads.Select((pad, index) => (pad.Id, Ic: ics[index])).ToDictionary(static pair => pair.Id, static pair => pair.Ic),
        new Dictionary<int, IReadOnlyList<PadMatchLink>>(),
        null);

    private static RegularGrid CreateGrid(CadPad[] pads) => new(1, 2, [0, 10, 20], [0, 10],
    [
        new RegularPad(0, 0, 20, pads[0].Polygon) { IcIndex = 0, DiffIndex = 10 },
        new RegularPad(0, 1, 21, pads[1].Polygon) { IcIndex = 0, DiffIndex = 11 },
    ]);

    private static CadOutputFwDiffAssignmentDecision CreateDecision(int cadId, int rawDiff) => new(
        CadPadId: cadId,
        OrderedCadIndex: 0,
        Mode: CadOutputFwDiffAssignmentMode.GeometryOnly,
        RawSeed: new CadBestMatchSeed(20, 0, rawDiff, 1, 1),
        MaskedSeed: CadBestMatchSeed.Empty,
        GeometryCandidates: [],
        CsvConfirmedCandidates: [],
        CurrentPrimaryDiffIndex: 7,
        PassiveCompensationDiffIndex: null,
        RepairSuggestionDiffIndex: null,
        ReasonCode: default,
        DecisionSource: default,
        Confidence: null);
}

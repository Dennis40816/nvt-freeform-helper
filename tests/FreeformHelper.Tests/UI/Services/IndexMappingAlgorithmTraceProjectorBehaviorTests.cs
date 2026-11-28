// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class IndexMappingAlgorithmTraceProjectorBehaviorTests
{
    [Fact]
    public void Build_MaskedDecision_ExplainsPassiveCompensationRepairAndCoverage()
    {
        using var culture = TestCulture.UseEnUs();
        var audit = new DxfRegularMaskAuditRow(
            CadPadId: 101, OrderedCadIndex: 0,
            RawSeed: new(501, 1, 10, 0.9, 0.25), MaskedSeed: new(502, 1, 12, 0.85, 0.3),
            CsvConfirmedCandidates: [new(502, 1, 12, 0.85, 0.3)], CsvConfirmedConfidence: 0.75,
            SuggestedDiffIndex: 12, CurrentAssignedDiffIndex: 10,
            Status: DxfRegularMaskAuditStatus.ChangedByMask, Message: "Repair the masked gap",
            Mode: CadOutputFwDiffAssignmentMode.CsvConstrained,
            PassiveCompensationDiffIndex: 11, RepairSuggestionDiffIndex: 12,
            IcIndex: 1, RowIndex: 7, SegmentIndex: 0, SegmentMemberCount: 4);
        var row = IndexMappingDecisionRowViewModel.FromMaskAudit(new IndexMappingMaskAuditRowViewModel(audit));

        var entries = IndexMappingAlgorithmTraceProjector.Build(row, 3, new DateTime(2026, 11, 8, 12, 34, 56, DateTimeKind.Utc));

        var values = entries.ToDictionary(entry => entry.Label, entry => entry.Value, StringComparer.Ordinal);
        Assert.Equal("11", values["derived.passiveCompensationDiff"]);
        Assert.Equal("12", values["derived.repairSuggestionDiff"]);
        Assert.Equal("Coverage: regular=90.0%, cad=25.0%", values["coverage"]);
        Assert.Equal("75.0 %", values["confidence"]);
        Assert.Equal("diff 10", values["input.current"]);
        Assert.Equal("diff 10", values["input.raw"]);
        Assert.Equal("diff 12", values["output.suggested"]);
        Assert.Equal("1", values["candidateCount"]);
        Assert.Equal("IC 1, row 7, segment 0", values["segment"]);
        Assert.Equal("3", values["traceSchemaVersion"]);
        Assert.Equal("2026-11-08 12:34:56 UTC", values["generatedAt"]);
    }

    [Fact]
    public void Build_UnmappedCadIssue_ReportsUnknownConfidenceWithoutInventingDerivedDiffsOrCoverage()
    {
        using var culture = TestCulture.UseEnUs();
        var row = IndexMappingDecisionRowViewModel.FromIssue(new IndexMappingReportIssueViewModel(
            new DxfRegularMappingIssue(DxfRegularMappingIssueKind.UnmappedCad, "No counterpart", CadPadId: 101)));

        var entries = IndexMappingAlgorithmTraceProjector.Build(row, 3, new DateTime(2026, 11, 8, 12, 34, 56, DateTimeKind.Utc));

        Assert.Equal("n/a", Assert.Single(entries, entry => entry.Label == "confidence").Value);
        Assert.Equal("No counterpart", Assert.Single(entries, entry => entry.Label == "final reason").Value);
        Assert.Equal("0", Assert.Single(entries, entry => entry.Label == "candidateCount").Value);
        Assert.DoesNotContain(entries, entry =>
            entry.Label is "derived.passiveCompensationDiff" or "derived.repairSuggestionDiff" or "coverage");
    }
}

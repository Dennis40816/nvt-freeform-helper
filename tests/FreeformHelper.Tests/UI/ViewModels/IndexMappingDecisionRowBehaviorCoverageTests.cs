// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class IndexMappingDecisionRowBehaviorCoverageTests
{
    [Fact]
    public void FromIssue_UnmappedCadWithCandidates_OffersBestRegularOverrideWithoutInventingLocation()
    {
        using var culture = TestCulture.UseEnUs();
        var issue = new IndexMappingReportIssueViewModel(new DxfRegularMappingIssue(
            DxfRegularMappingIssueKind.UnmappedCad, "No assigned Regular pad", DxfIndex: 7, CadPadId: 101,
            TopCandidates: [new(37, 1, 11, 0.9), new(38, 1, 12, 0.8)]));

        var row = IndexMappingDecisionRowViewModel.FromIssue(issue);

        Assert.Equal("Current diff idx 7 has no regular match.", row.Subtitle);
        Assert.Equal("diff 7", row.CurrentDisplay);
        Assert.Equal("diff 11", row.RawDisplay);
        Assert.Equal("diff 11", row.SuggestedDisplay);
        Assert.Equal("Raw geometry seed: none", row.RawDetailText);
        Assert.Equal("Suggested: REG 37 / diff 11", row.SuggestedDetailText);
        Assert.Null(row.LocateRegularPadIndex);
        Assert.Equal(37, row.ApplyRegularPadIndex);
        Assert.True(row.CanLocate);
        Assert.True(row.CanApplyOverride);
        Assert.False(row.CanApplyDiffOverride);
        Assert.Equal("Diff preview: no repair suggestion.", row.DiffRepairPreviewText);
        Assert.True(row.Matches("  #2 DIFF12  "));
        Assert.False(row.Matches("diff999"));
    }

    [Theory]
    [InlineData(DxfRegularMappingIssueKind.CountMismatch, false, "Count mismatch")]
    [InlineData(DxfRegularMappingIssueKind.UnmappedRegular, true, "REG 37")]
    public void FromIssue_WithoutCad_DisablesOverridesAndLocatesOnlyAnIdentifiedRegularPad(
        DxfRegularMappingIssueKind kind, bool hasRegularPad, string expectedTitle)
    {
        using var culture = TestCulture.UseEnUs();
        var issue = new IndexMappingReportIssueViewModel(new DxfRegularMappingIssue(
            kind, "Review visible pad counts", RegularPadIndex: hasRegularPad ? 37 : null,
            DiffIndex: hasRegularPad ? 11 : null));

        var row = IndexMappingDecisionRowViewModel.FromIssue(issue);

        Assert.Equal(expectedTitle, row.Title);
        Assert.Equal(!hasRegularPad, row.IsAggregateRow);
        Assert.Equal(hasRegularPad, row.CanLocate);
        Assert.Equal(hasRegularPad ? 37 : (int?)null, row.LocateRegularPadIndex);
        Assert.False(row.CanApplyOverride);
        Assert.False(row.CanApplyDiffOverride);
        Assert.False(row.CanClearOverride);
        Assert.False(row.HasSegmentScope);
        Assert.False(row.HasCoverageText);
        Assert.Equal("IC/row/segment: n/a", row.SegmentKey);
        Assert.Equal("Segment scope: n/a", row.SegmentScopeText);
        Assert.Equal("-", row.IcDisplayText);
        Assert.Equal("-", row.CadDisplayText);
        Assert.Equal(hasRegularPad ? "37" : "-", row.RegularDisplayText);
        Assert.Equal("-", row.ConfidenceDisplayText);
        Assert.Equal("S5", row.StepTagText);
        Assert.Equal(hasRegularPad, row.IsReasonTagNoCad);
        Assert.Equal(!hasRegularPad, row.IsReasonTagLegacy);
        Assert.False(row.IsReasonTagDirect);
        Assert.Contains("confidence=n/a", row.DecisionContractText, StringComparison.Ordinal);
        Assert.Equal(hasRegularPad ? "Suggested: REG 37 / diff 11" : "Suggested: none", row.SuggestedDetailText);
    }

    [Theory]
    [InlineData(CadOutputFwDiffAssignmentMode.GeometryOnly)]
    [InlineData(CadOutputFwDiffAssignmentMode.CsvConstrained)]
    public void FromMaskAudit_CandidatePreview_AppendsOnlyThreeGeometryChoicesAndOmitsThemInCsvMode(
        CadOutputFwDiffAssignmentMode mode)
    {
        using var culture = TestCulture.UseEnUs();
        var csvConstrained = mode == CadOutputFwDiffAssignmentMode.CsvConstrained;
        var geometryCandidates = Enumerable.Range(0, 4)
            .Select(index => new DxfRegularMaskAuditCandidate(501 + index, 1, 100 + index, 0.9, 0.25)).ToArray();
        var suggestedDiff = csvConstrained ? 101 : 100;
        var audit = new DxfRegularMaskAuditRow(
            CadPadId: 101, OrderedCadIndex: 0,
            RawSeed: new(501, 1, 100, 0.9, 0.25),
            MaskedSeed: new(csvConstrained ? 502 : 501, 1, suggestedDiff, 0.9, 0.25),
            CsvConfirmedCandidates: csvConstrained ? [geometryCandidates[1]] : geometryCandidates,
            CsvConfirmedConfidence: csvConstrained ? 1 : 0.25,
            SuggestedDiffIndex: suggestedDiff, CurrentAssignedDiffIndex: 99,
            Status: csvConstrained ? DxfRegularMaskAuditStatus.ChangedByMask : DxfRegularMaskAuditStatus.Unchanged,
            Message: "Review the geometry candidates", Mode: mode, RepairSuggestionDiffIndex: suggestedDiff,
            GeometryCandidates: geometryCandidates,
            IcIndex: 1, RowIndex: 7, SegmentIndex: 0, SegmentMemberCount: 4);

        var row = IndexMappingDecisionRowViewModel.FromMaskAudit(new IndexMappingMaskAuditRowViewModel(audit));

        string[] expectedCandidates = csvConstrained
            ? ["#1 diff101 (IC1, regId 502), regular=90.0%, cad=25.0%"]
            : [
                "#1 diff100 (IC1, regId 501), regular=90.0%, cad=25.0%",
                "#2 diff101 (IC1, regId 502), regular=90.0%, cad=25.0%",
                "#3 diff102 (IC1, regId 503), regular=90.0%, cad=25.0%",
                "#4 diff103 (IC1, regId 504), regular=90.0%, cad=25.0%",
                "G#1 diff100 (IC1, regId 501), regular=90.0%, cad=25.0%",
                "G#2 diff101 (IC1, regId 502), regular=90.0%, cad=25.0%",
                "G#3 diff102 (IC1, regId 503), regular=90.0%, cad=25.0%",
            ];
        Assert.Equal(expectedCandidates, row.CandidateLines);
        Assert.Equal($"diff {suggestedDiff}", row.SuggestedDisplay);
        Assert.Equal(suggestedDiff, row.ApplyDiffIndex);
        Assert.Equal($"Diff preview: diff 99 -> diff {suggestedDiff} (passive=no).", row.DiffRepairPreviewText);
        Assert.True(row.CanApplyDiffOverride);
        Assert.True(row.HasCoverageText);
        Assert.True(row.HasSegmentScope);
        Assert.Equal("Segment scope: IC 1, row 7, segment 0, members=4", row.SegmentScopeText);
        Assert.Equal("1", row.IcDisplayText);
        Assert.Equal("101", row.CadDisplayText);
        Assert.Equal(csvConstrained ? "502" : "501", row.RegularDisplayText);
        Assert.Equal(csvConstrained ? "SIM" : "S4", row.StepTagText);
        Assert.Equal(csvConstrained, row.IsReasonTagIncoming);
        Assert.Equal(!csvConstrained, row.IsReasonTagDirect);
        Assert.Equal(csvConstrained ? "1.00" : "0.25", row.ConfidenceDisplayText);
        Assert.Contains(csvConstrained ? "confidence=100.0 %" : "confidence=25.0 %",
            row.DecisionContractText, StringComparison.Ordinal);
    }
}

// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class IndexMappingDecisionRowSelectionTests
{
    [Theory]
    [InlineData(DxfRegularMappingIssueKind.LowConfidence, IndexMappingDecisionFilterMode.LowConfidence,
        "Assigned diff 11 is below confidence threshold.", "S4")]
    [InlineData(DxfRegularMappingIssueKind.Ambiguous, IndexMappingDecisionFilterMode.Ambiguous,
        "Assigned diff 11 is close to another candidate.", "S4")]
    [InlineData(DxfRegularMappingIssueKind.DuplicateDiff, IndexMappingDecisionFilterMode.DuplicateDiff,
        "Current diff idx 7 is assigned more than once.", "S6")]
    public void FromIssue_AssignedDiagnostic_KeepsItsActionTargetAndSearchesTheCurrentOverride(
        DxfRegularMappingIssueKind kind, IndexMappingDecisionFilterMode filter, string subtitle, string stepTag)
    {
        using var culture = TestCulture.UseEnUs();
        var issue = new IndexMappingReportIssueViewModel(new DxfRegularMappingIssue(
            kind, "Review the assigned geometry", DxfIndex: 7, CadPadId: 101,
            RegularPadIndex: 37, IcIndex: 0, DiffIndex: 11,
            TopCandidates: [new(37, 0, 11, 0.6), new(38, 0, 12, 0.59)]), static _ => 55);

        var row = IndexMappingDecisionRowViewModel.FromIssue(issue);

        Assert.Equal(filter, row.FilterMode);
        Assert.Equal(subtitle, row.Subtitle);
        Assert.Equal(stepTag, row.StepTagText);
        Assert.True(row.IsEntityRow);
        Assert.True(row.IsReasonTagOutgoing);
        Assert.False(row.IsReasonTagIncoming);
        Assert.False(row.IsReasonTagDirect);
        Assert.Equal(37, row.LocateRegularPadIndex);
        Assert.Equal(37, row.ApplyRegularPadIndex);
        Assert.True(row.CanApplyOverride);
        Assert.False(row.CanApplyDiffOverride);
        Assert.Equal("Raw geometry seed: REG 37 / diff 11", row.RawDetailText);
        Assert.Equal("Suggested: REG 37 / diff 11", row.SuggestedDetailText);
        Assert.Equal(55, row.OverrideRegularPadIndex);
        Assert.True(row.Matches("  REGULAR ID 55  "));

        row.SetOverride(56);

        Assert.Equal("Override: CAD -> regular id 56.", row.OverrideText);
        Assert.True(row.Matches("regular id 56"));
        Assert.False(row.Matches("regular id 55"));

        row.SetOverride(null);

        Assert.Null(row.OverrideRegularPadIndex);
        Assert.Equal("Override: none.", row.OverrideText);
        Assert.False(row.Matches("regular id 56"));
    }

    [Fact]
    public void FromMaskAudit_WithoutGeometryOrCandidates_DoesNotInventAnOverrideTarget()
    {
        using var culture = TestCulture.UseEnUs();
        var audit = new DxfRegularMaskAuditRow(
            CadPadId: 101, OrderedCadIndex: 0,
            RawSeed: CadBestMatchSeed.Empty, MaskedSeed: CadBestMatchSeed.Empty,
            CsvConfirmedCandidates: [], CsvConfirmedConfidence: null,
            SuggestedDiffIndex: null, CurrentAssignedDiffIndex: null,
            Status: DxfRegularMaskAuditStatus.NoRawBestMatch, Message: "CAD lies outside the regular grid");

        var row = IndexMappingDecisionRowViewModel.FromMaskAudit(new IndexMappingMaskAuditRowViewModel(audit));

        Assert.Equal("CAD 101", row.Title);
        Assert.Equal("No raw geometry seed", row.CategoryText);
        Assert.Equal("Primary diff idx none.", row.Subtitle);
        Assert.Equal("-", row.CurrentDisplay);
        Assert.Equal("-", row.RawDisplay);
        Assert.Equal("-", row.SuggestedDisplay);
        Assert.Equal("Raw geometry seed: none", row.RawDetailText);
        Assert.Equal("Masked geometry seed: none", row.MaskedDetailText);
        Assert.Equal("Suggested: none", row.SuggestedDetailText);
        Assert.True(row.CanLocate);
        Assert.Null(row.LocateRegularPadIndex);
        Assert.Null(row.ApplyRegularPadIndex);
        Assert.Null(row.ApplyDiffIndex);
        Assert.False(row.CanApplyOverride);
        Assert.False(row.CanApplyDiffOverride);
        Assert.Empty(row.CandidateLines);
        Assert.False(row.HasCoverageText);
        Assert.Equal("Diff preview: no repair suggestion.", row.DiffRepairPreviewText);
    }

    [Fact]
    public void FromMaskAudit_RemovedMatch_KeepsRawLocationAndOffersThePassiveCompensationDiff()
    {
        using var culture = TestCulture.UseEnUs();
        var audit = new DxfRegularMaskAuditRow(
            CadPadId: 101, OrderedCadIndex: 0,
            RawSeed: new(37, 0, 11, 0.9, 0.5), MaskedSeed: CadBestMatchSeed.Empty,
            CsvConfirmedCandidates: [], CsvConfirmedConfidence: null,
            SuggestedDiffIndex: null, CurrentAssignedDiffIndex: 10,
            Status: DxfRegularMaskAuditStatus.MaskRemovedMatch, Message: "CSV excludes the geometry seed",
            Mode: CadOutputFwDiffAssignmentMode.CsvConstrained,
            ReasonCode: CadOutputFwDiffAssignmentReasonCode.InactiveBlocked,
            PassiveCompensationDiffIndex: 11,
            GeometryCandidates: [new(37, 0, 11, 0.9, 0.5)]);

        var row = IndexMappingDecisionRowViewModel.FromMaskAudit(new IndexMappingMaskAuditRowViewModel(audit));

        Assert.Equal(IndexMappingDecisionFilterMode.RemovedByMask, row.FilterMode);
        Assert.Equal("SIM", row.StepTagText);
        Assert.True(row.IsReasonTagOutgoing);
        Assert.False(row.IsReasonTagIncoming);
        Assert.False(row.IsReasonTagDirect);
        Assert.Equal(37, row.LocateRegularPadIndex);
        Assert.Equal(37, row.ApplyRegularPadIndex);
        Assert.Equal("diff 11", row.RawDisplay);
        Assert.Equal("-", row.SuggestedDisplay);
        Assert.Equal("Masked geometry seed: none", row.MaskedDetailText);
        Assert.Equal("Suggested: none", row.SuggestedDetailText);
        Assert.Empty(row.CandidateLines);
        Assert.Equal(11, row.PassiveCompensationDiffIndex);
        Assert.Equal(11, row.ApplyDiffIndex);
        Assert.True(row.CanApplyDiffOverride);
        Assert.Equal("Diff preview: diff 10 -> diff 11 (passive=yes).", row.DiffRepairPreviewText);
        Assert.Equal("Action: inspect CSV-active candidates before overriding Step 4.", row.ActionHint);
        Assert.True(row.Matches("passive=yes"));
    }
}

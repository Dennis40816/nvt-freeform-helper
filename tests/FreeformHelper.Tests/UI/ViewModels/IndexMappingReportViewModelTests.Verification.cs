using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class IndexMappingReportViewModelTests
{
    private static readonly string[] _expectedKeyChecks =
    [
        "rows=8", "changed=1, lowConf=1", "mismatch=1, unmapped=2", "duplicate=1", "maskChanged=1, maskRemoved=1",
    ];

    private static readonly string[] _expectedStepStatuses = ["pass", "warning", "pass", "pass", "warning"];


    [Fact]
    public void VerificationStepRail_SelectStep5_DoesNotOverrideLeftTableFilter()
    {
        var report = new DxfRegularMappingReport(
            CadCount: 1,
            RegularCount: 2,
            MappedCount: 1,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 1,
            LowConfidenceCount: 0,
            AmbiguousCount: 0,
            HasIssues: true,
            Summary: "summary",
            TotalIssueCount: 1,
            IssuesTruncated: false,
            Issues: new[]
            {
                new DxfRegularMappingIssue(
                    DxfRegularMappingIssueKind.CountMismatch,
                    "count mismatch")
            },
            SampleIssues: Array.Empty<DxfRegularMappingIssue>());

        var vm = new IndexMappingReportViewModel("summary", report);

        Assert.Equal(5, vm.VerificationSteps.Count);
        vm.SelectedFilterMode = IndexMappingDecisionFilterMode.Unmapped;
        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step5);

        Assert.Equal(IndexMappingVerificationStep.Step5, vm.ActiveVerificationStep);
        Assert.Equal(IndexMappingDecisionFilterMode.Unmapped, vm.SelectedFilterMode);
    }


    [Fact]
    public void VerificationStepRail_SelectStep5_WithSyncEnabled_SwitchesPreferredFilter()
    {
        var report = new DxfRegularMappingReport(
            CadCount: 1,
            RegularCount: 2,
            MappedCount: 1,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 1,
            LowConfidenceCount: 0,
            AmbiguousCount: 0,
            HasIssues: true,
            Summary: "summary",
            TotalIssueCount: 1,
            IssuesTruncated: false,
            Issues: new[]
            {
                new DxfRegularMappingIssue(
                    DxfRegularMappingIssueKind.CountMismatch,
                    "count mismatch")
            },
            SampleIssues: Array.Empty<DxfRegularMappingIssue>());

        var vm = new IndexMappingReportViewModel("summary", report)
        {
            SyncFilterWithStepRail = true,
            SelectedFilterMode = IndexMappingDecisionFilterMode.Unmapped,
        };

        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step5);

        Assert.Equal(IndexMappingVerificationStep.Step5, vm.ActiveVerificationStep);
        Assert.Equal(IndexMappingDecisionFilterMode.CountMismatch, vm.SelectedFilterMode);
    }


    [Fact]
    public void VerificationStepRail_LastNode_HasNoConnector()
    {
        var report = new DxfRegularMappingReport(
            CadCount: 1,
            RegularCount: 1,
            MappedCount: 1,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 0,
            LowConfidenceCount: 0,
            AmbiguousCount: 0,
            HasIssues: false,
            Summary: "summary",
            TotalIssueCount: 0,
            IssuesTruncated: false,
            Issues: Array.Empty<DxfRegularMappingIssue>(),
            SampleIssues: Array.Empty<DxfRegularMappingIssue>());

        var vm = new IndexMappingReportViewModel("summary", report);

        Assert.Equal(5, vm.VerificationSteps.Count);
        for (var i = 0; i < vm.VerificationSteps.Count; i++)
        {
            var node = vm.VerificationSteps[i];
            if (i == vm.VerificationSteps.Count - 1)
            {
                Assert.True(node.IsLast);
                Assert.False(node.HasConnector);
            }
            else
            {
                Assert.False(node.IsLast);
                Assert.True(node.HasConnector);
            }
        }
    }


    [Fact]
    public void HeaderSubtitle_UsesCompactStatus_NotRawSummary()
    {
        const string verboseSummary = "Diagnostics warnings: mapped=4838, unmappedCad=0, unmappedRegular=154, lowConf=42, ambiguous=4, duplicateDiff=0, maskChanged=0, maskRemoved=0";
        var report = new DxfRegularMappingReport(
            CadCount: 1,
            RegularCount: 2,
            MappedCount: 1,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 1,
            LowConfidenceCount: 0,
            AmbiguousCount: 0,
            HasIssues: true,
            Summary: verboseSummary,
            TotalIssueCount: 1,
            IssuesTruncated: false,
            Issues: new[]
            {
                new DxfRegularMappingIssue(DxfRegularMappingIssueKind.CountMismatch, "count mismatch"),
            },
            SampleIssues: Array.Empty<DxfRegularMappingIssue>());

        var vm = new IndexMappingReportViewModel(verboseSummary, report);

        Assert.Contains("Open issues", vm.HeaderSubtitle, StringComparison.Ordinal);
        Assert.DoesNotContain("mapped=4838", vm.HeaderSubtitle, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void StepRail_UsesSelectedEntityJourney_WhenDecisionSelected()
    {
        var report = new DxfRegularMappingReport(
            CadCount: 1,
            RegularCount: 1,
            MappedCount: 1,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 0,
            LowConfidenceCount: 0,
            AmbiguousCount: 0,
            HasIssues: false,
            Summary: "summary",
            TotalIssueCount: 0,
            IssuesTruncated: false,
            Issues: Array.Empty<DxfRegularMappingIssue>(),
            SampleIssues: Array.Empty<DxfRegularMappingIssue>(),
            MaskAuditRows: new[]
            {
                new DxfRegularMaskAuditRow(
                    CadPadId: 4823,
                    OrderedCadIndex: 0,
                    RawSeed: new CadBestMatchSeed(3844, 1, 324, 0.91, 0.89),
                    MaskedSeed: new CadBestMatchSeed(3843, 1, 323, 0.74, 0.72),
                    CsvConfirmedCandidates: new[]
                    {
                        new DxfRegularMaskAuditCandidate(3843, 1, 323, 0.74, 0.72),
                    },
                    CsvConfirmedConfidence: 0.83,
                    SuggestedDiffIndex: 323,
                    CurrentAssignedDiffIndex: 324,
                    Status: DxfRegularMaskAuditStatus.ChangedByMask,
                    Message: "changed",
                    Mode: CadOutputFwDiffAssignmentMode.CsvConstrained,
                    ReasonCode: CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected,
                    DecisionSource: CadOutputFwDiffAssignmentDecisionSource.AutoRepaired,
                    PrimaryAssignedDiffIndex: 324,
                    RepairSuggestionDiffIndex: 323,
                    IcIndex: 1,
                    RowIndex: 6,
                    SegmentIndex: 0,
                    SegmentMemberCount: 2),
            },
            MaskChangedCount: 1,
            MaskRemovedCount: 0);

        var vm = new IndexMappingReportViewModel("summary", report);
        var selected = Assert.Single(vm.DecisionRows);
        vm.SelectDecisionCommand.Execute(selected);

        Assert.Equal("CAD 4823", vm.SelectedEntityText);
        Assert.Contains("raw diff 324 -> assigned diff 324 -> suggested diff 323", vm.SelectedJourneyText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("assigned=diff 324", vm.VerificationSteps[1].KeyChecks, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task AlgorithmTrace_ExpandsAndLoadsForSelectedDecisionAsync()
    {
        var report = new DxfRegularMappingReport(
            CadCount: 1,
            RegularCount: 1,
            MappedCount: 1,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 0,
            LowConfidenceCount: 1,
            AmbiguousCount: 0,
            HasIssues: true,
            Summary: "summary",
            TotalIssueCount: 1,
            IssuesTruncated: false,
            Issues: new[]
            {
                new DxfRegularMappingIssue(
                    DxfRegularMappingIssueKind.LowConfidence,
                    "cad 777",
                    DxfIndex: 77,
                    CadPadId: 777,
                    RegularPadIndex: 88,
                    DiffIndex: 88)
            },
            SampleIssues: Array.Empty<DxfRegularMappingIssue>());

        var vm = new IndexMappingReportViewModel("summary", report);
        vm.IsAlgorithmTraceExpanded = true;

        var completed = await WaitUntilAsync(
            () => vm.AlgorithmTraceState is IndexMappingAlgorithmTraceState.Ready or IndexMappingAlgorithmTraceState.Failed,
            timeoutMs: 2000);

        Assert.True(completed);
        Assert.Equal(IndexMappingAlgorithmTraceState.Ready, vm.AlgorithmTraceState);
        Assert.NotEmpty(vm.AlgorithmTraceEntries);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VerificationCards_WithoutEntitySelection_ShowReportChecksAndGeneralActions(bool searchHidesAllRows)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new IndexMappingReportViewModel("mapping", CreateDecisionFilterReport());
        Assert.NotNull(vm.SelectedDecision);
        if (searchHidesAllRows)
        {
            vm.SearchKeyword = "no such CAD or REG";
            Assert.Null(vm.SelectedDecision);
            Assert.Equal("No rows under all diagnostics.", vm.DecisionSummaryText);
            Assert.Equal("Aggregate diagnostics", vm.AggregateDecisionTitle);
            Assert.Equal("No aggregate diagnostics.", vm.AggregateDecisionSummaryText);
            AssertEmptyVerificationSelectionDetails(vm);
        }
        else
        {
            vm.SetDecisionFilterModeCommand.Execute(IndexMappingDecisionFilterMode.CountMismatch);
            Assert.Same(Assert.Single(vm.AggregateDecisionRows), vm.SelectedDecision);
            Assert.Equal("No entity rows under Count mismatch. Aggregate diagnostics: 1.", vm.DecisionSummaryText);
            Assert.Equal("Aggregate diagnostics (1)", vm.AggregateDecisionTitle);
            Assert.Equal("Aggregate signals (for example count mismatch) are listed separately and do not block entity review.",
                vm.AggregateDecisionSummaryText);
        }

        Assert.True(vm.HasNoDecisionRows);
        Assert.False(vm.CanLocateSelectedDecision);
        Assert.False(vm.CanApplySelectedIssueOverride);
        Assert.False(vm.CanApplySelectedDiffOverride);
        Assert.False(vm.CanApplySelectedSegmentDiffOverrides);
        Assert.False(vm.CanClearSelectedIssueOverride);
        Assert.Equal("warning", vm.OverallStatusText);
        Assert.Equal(_expectedKeyChecks, vm.VerificationSteps.Select(static node => node.KeyChecks));

        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step3);
        Assert.Equal("rows=8", vm.ActiveStepInputText);
        Assert.Equal("rows=8", vm.ActiveStepDecisionText);
        Assert.Equal("Confirm input rows are coherent before table export.", vm.ActiveStepOutputText);
        Assert.Equal("Confirm input rows are coherent before table export.", vm.ActiveStepActionText);
        Assert.Equal("countMismatch=1", vm.ActiveStepImpactText);

        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step4);
        Assert.Equal("Review reason code and seed/mask decision source.", vm.ActiveStepActionText);
        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step5);
        Assert.Equal("Check final notch row payload before export.", vm.ActiveStepActionText);
        Assert.Equal("visible rows 0/8", vm.ActiveStepImpactText);
        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step6);
        Assert.Equal("Validate trace direction and resolve duplicate conflicts.", vm.ActiveStepActionText);
        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Simulation);
        Assert.Equal("Replay notch impact using the same exported table contract.", vm.ActiveStepActionText);
        Assert.Equal("visible rows 0/8", vm.ActiveStepImpactText);
    }

    private static void AssertEmptyVerificationSelectionDetails(IndexMappingReportViewModel vm)
    {
        Assert.Equal("No row selected.", vm.SelectedDecisionKeyText);
        Assert.Equal("Select a row in the verification table.", vm.SelectedDecisionSubtitleText);
        Assert.Equal("entity: none", vm.SelectedEntityText);
        Assert.Equal("journey: select a CAD/REG row to inspect the full step path.", vm.SelectedJourneyText);
        Assert.Equal("reasonCode: n/a", vm.Step4ReasonCodeText);
        Assert.Equal("decisionSource: n/a", vm.Step4DecisionSourceText);
        Assert.Equal("Raw geometry seed: n/a", vm.Step4RawBestText);
        Assert.Equal("Masked geometry seed: n/a", vm.Step4MaskedBestText);
        Assert.Equal("primaryAssigned: -", vm.Step4AssignedText);
        Assert.Equal("passiveCompensationDiff: none", vm.Step4PassiveCompensationText);
        Assert.Equal("repairSuggestion: none", vm.Step4RepairSuggestionText);
        Assert.Equal("confidence: n/a", vm.Step4ConfidenceText);
        Assert.Equal("Offset support: n/a", vm.Step4OffsetSupportText);
        Assert.Equal("candidate attribution (0)", vm.Step4CandidateAttributionTitle);
        Assert.Empty(vm.Step4CandidateAttributionLines);
        Assert.Equal("row: n/a", vm.Step5RowNumberText);
        Assert.Equal("IC: n/a · diff: -", vm.Step5IcDiffText);
        Assert.Equal("regular: n/a · CAD: n/a", vm.Step5RegularCadText);
        Assert.Equal("payload: n/a", vm.Step5PayloadSummaryText);
        Assert.Equal("status: n/a", vm.Step5StatusText);
        Assert.Equal("validationRegularPadId: n/a", vm.Step6ValidationRegularText);
        Assert.Equal("traceCount: 0", vm.Step6TraceCountText);
        Assert.Equal("trace direction: n/a", vm.Step6TraceDirectionText);
        Assert.Equal("impact: select a row to inspect linked regular/notch rows.", vm.SimulationImpactText);
        Assert.Equal("Raw geometry seed: n/a; Masked geometry seed: n/a", vm.ActiveStepInputText);
        Assert.Equal("reasonCode: n/a", vm.ActiveStepDecisionText);
        Assert.Equal("primaryAssigned: -; repairSuggestion: none", vm.ActiveStepOutputText);
        Assert.Equal("Diff preview: n/a.", vm.ActiveStepImpactText);
    }

    [Theory]
    [InlineData(IndexMappingVerificationStep.Step3, IndexMappingDecisionFilterMode.All, "Step 3", "all diagnostics")]
    [InlineData(IndexMappingVerificationStep.Step4, IndexMappingDecisionFilterMode.ChangedByMask, "Step 4", "Changed by mask")]
    [InlineData(IndexMappingVerificationStep.Step6, IndexMappingDecisionFilterMode.DuplicateDiff, "Step 6", "Duplicate diff")]
    [InlineData(IndexMappingVerificationStep.Simulation, IndexMappingDecisionFilterMode.ChangedByMask, "Simulation", "Changed by mask")]
    public void VerificationStepSync_WithCompetingDiagnostics_SelectsPreferredRowsAndKeepsActiveStep(
        IndexMappingVerificationStep step, IndexMappingDecisionFilterMode filter, string title, string label)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new IndexMappingReportViewModel("mapping", CreateDecisionFilterReport())
        {
            SyncFilterWithStepRail = true,
            SelectedFilterMode = IndexMappingDecisionFilterMode.Unmapped,
        };

        vm.SelectVerificationStepCommand.Execute(step);

        Assert.Equal(filter, vm.SelectedFilterMode);
        Assert.Equal(label, vm.SelectedFilterLabel);
        Assert.Equal(title, vm.ActiveVerificationStepText);
        Assert.Equal(title, vm.ActiveStepCardTitle);
        Assert.Equal(step, Assert.Single(vm.VerificationSteps, static node => node.IsActive).Step);
        Assert.Equal("Step sync on", vm.StepFilterSyncText);
        Assert.Equal($"Open issues 8 · Active {title} · rev 1", vm.HeaderSubtitle);
        Assert.Equal("Changed by mask: 1, removed by mask: 1.", vm.MaskAuditSummary);
        if (filter == IndexMappingDecisionFilterMode.All)
        {
            Assert.Equal(7, vm.VisibleDecisionCount);
            Assert.Single(vm.AggregateDecisionRows);
            Assert.Equal("Showing 7 entity rows under all diagnostics.", vm.DecisionSummaryText);
        }
        else
        {
            Assert.Equal(filter, Assert.Single(vm.DecisionRows).FilterMode);
            Assert.Empty(vm.AggregateDecisionRows);
            Assert.Equal($"Showing 1 entity rows under {label}.", vm.DecisionSummaryText);
        }

        var selected = vm.SelectedDecision;
        vm.SelectVerificationStepCommand.Execute(step);

        Assert.Equal(filter, vm.SelectedFilterMode);
        Assert.Same(selected, vm.SelectedDecision);
        Assert.Equal(step, Assert.Single(vm.VerificationSteps, static node => node.IsActive).Step);
    }

    [Theory]
    [InlineData(IndexMappingVerificationStep.Step4, IndexMappingDecisionFilterMode.LowConfidence, "Low confidence")]
    [InlineData(IndexMappingVerificationStep.Step4, IndexMappingDecisionFilterMode.Ambiguous, "Ambiguous")]
    [InlineData(IndexMappingVerificationStep.Step4, IndexMappingDecisionFilterMode.RemovedByMask, "Removed by mask")]
    [InlineData(IndexMappingVerificationStep.Step4, IndexMappingDecisionFilterMode.Unmapped, "Unmapped")]
    [InlineData(IndexMappingVerificationStep.Step4, IndexMappingDecisionFilterMode.CountMismatch, "all diagnostics")]
    [InlineData(IndexMappingVerificationStep.Step5, IndexMappingDecisionFilterMode.Unmapped, "Unmapped")]
    [InlineData(IndexMappingVerificationStep.Step5, IndexMappingDecisionFilterMode.Ambiguous, "all diagnostics")]
    [InlineData(IndexMappingVerificationStep.Step6, IndexMappingDecisionFilterMode.LowConfidence, "all diagnostics")]
    [InlineData(IndexMappingVerificationStep.Simulation, IndexMappingDecisionFilterMode.RemovedByMask, "Removed by mask")]
    [InlineData(IndexMappingVerificationStep.Simulation, IndexMappingDecisionFilterMode.LowConfidence, "all diagnostics")]
    [InlineData(IndexMappingVerificationStep.Step4, IndexMappingDecisionFilterMode.All, "all diagnostics")]
    public void VerificationStepSync_WithoutFirstChoice_UsesAvailableDiagnosticOrShowsAll(
        IndexMappingVerificationStep step, IndexMappingDecisionFilterMode available, string expectedLabel)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new IndexMappingReportViewModel("mapping", CreateVerificationFallbackReport(available))
        {
            SyncFilterWithStepRail = true,
            SelectedFilterMode = IndexMappingDecisionFilterMode.DuplicateDiff,
        };

        vm.SelectVerificationStepCommand.Execute(step);

        var expectedFilter = expectedLabel == "all diagnostics" ? IndexMappingDecisionFilterMode.All : available;
        Assert.Equal(expectedFilter, vm.SelectedFilterMode);
        Assert.Equal(expectedLabel, vm.SelectedFilterLabel);
        Assert.Equal(step, Assert.Single(vm.VerificationSteps, static node => node.IsActive).Step);
        if (available == IndexMappingDecisionFilterMode.All)
        {
            Assert.Empty(vm.DecisionRows);
            Assert.Empty(vm.AggregateDecisionRows);
            Assert.Equal("pass", vm.OverallStatusText);
            Assert.Equal("status: pass", vm.ActiveStepCardStatusText);
        }
        else
        {
            var selected = Assert.IsType<IndexMappingDecisionRowViewModel>(vm.SelectedDecision);
            Assert.Equal(available, selected.FilterMode);
            Assert.Equal("warning", vm.OverallStatusText);
        }
    }

    private static DxfRegularMappingReport CreateVerificationFallbackReport(IndexMappingDecisionFilterMode mode)
    {
        if (mode == IndexMappingDecisionFilterMode.RemovedByMask)
        {
            return CreateVerificationMaskReport(DxfRegularMaskAuditStatus.MaskRemovedMatch);
        }

        var kind = mode switch
        {
            IndexMappingDecisionFilterMode.LowConfidence => DxfRegularMappingIssueKind.LowConfidence,
            IndexMappingDecisionFilterMode.Ambiguous => DxfRegularMappingIssueKind.Ambiguous,
            IndexMappingDecisionFilterMode.Unmapped => DxfRegularMappingIssueKind.UnmappedCad,
            _ => DxfRegularMappingIssueKind.CountMismatch,
        };
        var isMismatch = kind == DxfRegularMappingIssueKind.CountMismatch;
        var isUnmapped = kind == DxfRegularMappingIssueKind.UnmappedCad;
        DxfRegularMappingIssue[] issues = mode == IndexMappingDecisionFilterMode.All ? [] :
        [
            new(kind, "Mapping needs review", DxfIndex: isMismatch ? null : 77,
                CadPadId: isMismatch ? null : 777, RegularPadIndex: isMismatch || isUnmapped ? null : 88,
                DiffIndex: isMismatch || isUnmapped ? null : 88),
        ];
        return new DxfRegularMappingReport(
            CadCount: 1, RegularCount: isUnmapped ? 0 : isMismatch && issues.Length > 0 ? 2 : 1,
            MappedCount: isUnmapped ? 0 : 1, UnmappedCadCount: isUnmapped ? 1 : 0,
            UnmappedRegularCount: isMismatch && issues.Length > 0 ? 1 : 0,
            LowConfidenceCount: mode == IndexMappingDecisionFilterMode.LowConfidence ? 1 : 0,
            AmbiguousCount: mode == IndexMappingDecisionFilterMode.Ambiguous ? 1 : 0,
            HasIssues: issues.Length > 0, Summary: "mapping", TotalIssueCount: issues.Length,
            IssuesTruncated: false, Issues: issues, SampleIssues: issues);
    }

    private static DxfRegularMappingReport CreateVerificationMaskReport(DxfRegularMaskAuditStatus status)
    {
        var row = CreateRepairOutcomeRow(9011, 22) with
        {
            CsvConfirmedCandidates = [new DxfRegularMaskAuditCandidate(10011, 1, 22, 0.9, 0.8)],
            CsvConfirmedConfidence = 0.85,
            PassiveCompensationDiffIndex = 23,
            DetectedOffset = 2,
            OffsetSupportCount = 3,
            OffsetSupportRatio = 0.75,
            SegmentConfidence = 0.9,
            SegmentMemberCount = 4,
        };
        if (status == DxfRegularMaskAuditStatus.MaskRemovedMatch)
        {
            row = row with
            {
                Status = status,
                MaskedSeed = CadBestMatchSeed.Empty,
                CsvConfirmedCandidates = [],
                CsvConfirmedConfidence = null,
                SuggestedDiffIndex = null,
                CurrentAssignedDiffIndex = null,
                RepairSuggestionDiffIndex = null,
                PassiveCompensationDiffIndex = null,
                DetectedOffset = null,
                OffsetSupportCount = 0,
                OffsetSupportRatio = null,
                SegmentConfidence = null,
            };
        }

        return CreateRepairOutcomeReport(row) with
        {
            MaskChangedCount = status == DxfRegularMaskAuditStatus.ChangedByMask ? 1 : 0,
            MaskRemovedCount = status == DxfRegularMaskAuditStatus.MaskRemovedMatch ? 1 : 0,
        };
    }

    [Fact]
    public void Step3_SelectedSegment_ShowsEntityReadinessAndSegmentMembership()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new IndexMappingReportViewModel("mapping", CreateSegmentRepairReport());
        var selected = Assert.Single(vm.DecisionRows, static row => row.CadPadId == 9101);
        vm.SelectDecisionCommand.Execute(selected);

        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step3);

        Assert.Equal("CAD 9101, Segment scope: IC 1, row 7, segment 0, members=3", vm.ActiveStepInputText);
        Assert.Equal("entity=CAD 9101, segment=IC 1, row 7, segment 0, members=3", vm.ActiveStepDecisionText);
        Assert.Equal("Confirm CAD 9101 can enter notch table generation.", vm.ActiveStepOutputText);
        Assert.Equal("countMismatch=0", vm.ActiveStepImpactText);
        Assert.Equal("Confirm CAD 9101 can enter notch table generation.", vm.ActiveStepActionText);
        Assert.Equal("status: pass", vm.ActiveStepCardStatusText);
        Assert.Equal(_expectedStepStatuses, vm.VerificationSteps.Select(static node => node.StatusText));
        Assert.True(vm.ShowStep3ActionButtons);
        Assert.False(vm.ShowStep4ActionButtons);
        Assert.False(vm.ShowStep5ActionButtons);
        Assert.False(vm.ShowSimulationActionButtons);
        Assert.Same(selected, vm.SelectedDecision);
    }

    [Theory]
    [InlineData(true, true, "Apply diff repair for CAD 9011.")]
    [InlineData(false, true, "Apply regular override for CAD 777.")]
    [InlineData(false, false, "Review decision source for CAD 777.")]
    public void Step4_SelectedDecision_PrefersDiffRepairThenRegularOverrideThenReview(
        bool hasDiffRepair, bool hasRegularTarget, string expectedAction)
    {
        using var culture = TestCulture.UseEnUs();
        var report = hasDiffRepair
            ? CreateVerificationMaskReport(DxfRegularMaskAuditStatus.ChangedByMask)
            : hasRegularTarget ? CreateOverrideOutcomeReport() : CreateVerificationFallbackReport(IndexMappingDecisionFilterMode.Unmapped);

        var vm = new IndexMappingReportViewModel("mapping", report);
        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step4);

        Assert.Equal(expectedAction, vm.ActiveStepActionText);
        Assert.Equal("status: warning", vm.ActiveStepCardStatusText);
        Assert.True(vm.ShowStep4ActionButtons);
        Assert.False(vm.ShowStep3ActionButtons);
        if (hasDiffRepair)
        {
            Assert.True(vm.CanApplySelectedDiffOverride);
            Assert.True(vm.CanApplySelectedIssueOverride);
            Assert.Equal("reasonCode: segment-offset-suspected", vm.ActiveStepDecisionText);
            Assert.Equal("decisionSource: auto-repaired", vm.Step4DecisionSourceText);
            Assert.Equal("Raw geometry seed: REG 10011 / diff 22; Masked geometry seed: REG 10011 / diff 22",
                vm.ActiveStepInputText);
            Assert.Equal("primaryAssigned: diff 20; repairSuggestion: diff 22", vm.ActiveStepOutputText);
            Assert.Equal("Diff preview: diff 20 -> diff 22 (passive=yes).", vm.ActiveStepImpactText);
            Assert.Equal("passiveCompensationDiff: diff 23", vm.Step4PassiveCompensationText);
            Assert.Equal("confidence: 85.0 %", vm.Step4ConfidenceText);
            Assert.Equal("Segment offset: +2, support=3, ratio=75.0%, confidence=90.0%", vm.Step4OffsetSupportText);
            Assert.Equal("candidate attribution (1)", vm.Step4CandidateAttributionTitle);
            Assert.Equal("#1 diff22 (IC1, regId 10011), regular=90.0%, cad=80.0%",
                Assert.Single(vm.Step4CandidateAttributionLines));
        }
        else
        {
            Assert.False(vm.CanApplySelectedDiffOverride);
            Assert.Equal(hasRegularTarget, vm.CanApplySelectedIssueOverride);
            Assert.Equal(hasRegularTarget ? "reasonCode: Low confidence" : "reasonCode: Unmapped CAD", vm.ActiveStepDecisionText);
            Assert.Equal("primaryAssigned: diff 77; repairSuggestion: none", vm.ActiveStepOutputText);
            Assert.Equal("Diff preview: no repair suggestion.", vm.ActiveStepImpactText);
            Assert.Equal("passiveCompensationDiff: none", vm.Step4PassiveCompensationText);
            Assert.Equal("confidence: n/a", vm.Step4ConfidenceText);
            Assert.Equal("candidate attribution (0)", vm.Step4CandidateAttributionTitle);
            Assert.Empty(vm.Step4CandidateAttributionLines);
            Assert.Equal("No SeeRegular-based CSV confirmation data.", vm.MaskAuditSummary);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Step5_SelectedDecision_ShowsPayloadAndChoosesSegmentOrRowReview(bool hasSegment)
    {
        using var culture = TestCulture.UseEnUs();
        var report = hasSegment ? CreateSegmentRepairReport() : CreateOverrideOutcomeReport();
        var vm = new IndexMappingReportViewModel("mapping", report);
        vm.SelectDecisionCommand.Execute(Assert.Single(vm.DecisionRows,
            row => row.CadPadId == (hasSegment ? 9101 : 777)));

        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step5);

        Assert.Equal("Step 5", vm.ActiveVerificationStepText);
        Assert.Equal("status: pass", vm.ActiveStepCardStatusText);
        Assert.True(vm.ShowStep5ActionButtons);
        Assert.False(vm.ShowStep4ActionButtons);
        if (hasSegment)
        {
            Assert.Equal("row: 7; IC: 1 · diff: diff 98", vm.ActiveStepInputText);
            Assert.Equal("csv-constrained", vm.ActiveStepDecisionText);
            Assert.Equal("status: segment-offset-suspected; regular: 501 · CAD: 9101", vm.ActiveStepOutputText);
            Assert.Equal("Segment preview: IC 1, row 7, segment 0, repair rows 2/3.", vm.ActiveStepImpactText);
            Assert.Equal("Apply segment repairs around IC 1, row 7, segment 0.", vm.ActiveStepActionText);
            Assert.Equal("reason=segment-offset-suspected, source=auto-repaired, mode=csv-constrained, confidence=92.0 %",
                vm.Step5PayloadSummaryText);
            Assert.Equal("row=7, ic=1, contract=auto-repaired", vm.VerificationSteps[2].KeyChecks);
        }
        else
        {
            Assert.Equal("row: n/a; IC: n/a · diff: diff 77", vm.ActiveStepInputText);
            Assert.Equal("n/a", vm.ActiveStepDecisionText);
            Assert.Equal("status: Low confidence; regular: 88 · CAD: 777", vm.ActiveStepOutputText);
            Assert.Equal("visible rows 1/1", vm.ActiveStepImpactText);
            Assert.Equal("Check row payload fields for CAD 777.", vm.ActiveStepActionText);
            Assert.Equal("reason=Low confidence, source=n/a, mode=n/a, confidence=n/a", vm.Step5PayloadSummaryText);
            Assert.Equal("row=n/a, ic=n/a, contract=n/a", vm.VerificationSteps[2].KeyChecks);
            Assert.Equal("entity=CAD 777, segment=n/a", vm.VerificationSteps[0].KeyChecks);
        }
    }

    [Fact]
    public void Step6_SelectionChangesFromDuplicateCadToUnmappedRegular_UpdatesTraceAndSelectedStatus()
    {
        using var culture = TestCulture.UseEnUs();
        DxfRegularMappingIssue[] issues =
        [
            new(DxfRegularMappingIssueKind.DuplicateDiff, "CAD 777 shares diff 77 with another CAD",
                DxfIndex: 77, CadPadId: 777, RegularPadIndex: 88, DiffIndex: 88,
                TopCandidates: [new(88, 1, 88, 0.9), new(89, 1, 89, 0.8)]),
            new(DxfRegularMappingIssueKind.UnmappedRegular, "REG 99 has no CAD", RegularPadIndex: 99, DiffIndex: 99),
        ];
        var report = new DxfRegularMappingReport(
            CadCount: 2, RegularCount: 3, MappedCount: 2, UnmappedCadCount: 0, UnmappedRegularCount: 1,
            LowConfidenceCount: 0, AmbiguousCount: 0, HasIssues: true, Summary: "mapping",
            TotalIssueCount: 2, IssuesTruncated: false, Issues: issues, SampleIssues: issues, DuplicateDiffCount: 1);
        var vm = new IndexMappingReportViewModel("mapping", report);

        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step6);
        vm.SelectDecisionCommand.Execute(Assert.Single(vm.DecisionRows, static row => row.CadPadId == 777));

        Assert.Equal("Step 6", vm.ActiveVerificationStepText);
        Assert.Equal("validationRegularPadId: 88; traceCount: 2", vm.ActiveStepInputText);
        Assert.Equal("trace direction: duplicate-conflict path (needs review)", vm.ActiveStepDecisionText);
        Assert.Equal("trace=2, direction=trace direction: duplicate-conflict path (needs review)", vm.ActiveStepOutputText);
        Assert.Equal("duplicate rows=1, selected=CAD 777", vm.ActiveStepImpactText);
        Assert.Equal("Locate CAD 777 on AA and verify trace direction.", vm.ActiveStepActionText);
        Assert.Equal("status: warning", vm.ActiveStepCardStatusText);
        Assert.True(vm.ShowStep3ActionButtons);
        Assert.False(vm.ShowStep4ActionButtons);

        vm.SelectDecisionCommand.Execute(Assert.Single(vm.DecisionRows, static row => row.LocateRegularPadIndex == 99));

        Assert.Equal("REG 99", vm.SelectedEntityText);
        Assert.True(vm.CanLocateSelectedDecision);
        Assert.Equal("regular: 99 · CAD: n/a", vm.Step5RegularCadText);
        Assert.Equal("impact: linked CAD n/a, suggested diff 99", vm.SimulationImpactText);
        Assert.Equal("validationRegularPadId: 99; traceCount: 0", vm.ActiveStepInputText);
        Assert.Equal("trace direction: direct/in/out path coherent", vm.ActiveStepDecisionText);
        Assert.Equal("trace=0, direction=trace direction: direct/in/out path coherent", vm.ActiveStepOutputText);
        Assert.Equal("duplicate rows=1, selected=REG 99", vm.ActiveStepImpactText);
        Assert.Equal("Locate REG 99 on AA and verify trace direction.", vm.ActiveStepActionText);
        Assert.Equal("status: pass", vm.ActiveStepCardStatusText);
        Assert.Equal("warning", vm.OverallStatusText);
    }

    [Theory]
    [InlineData(DxfRegularMaskAuditStatus.ChangedByMask, IndexMappingDecisionFilterMode.ChangedByMask,
        "Changed by mask", "diff 22")]
    [InlineData(DxfRegularMaskAuditStatus.MaskRemovedMatch, IndexMappingDecisionFilterMode.RemovedByMask,
        "Removed by mask", "-")]
    public void Simulation_SelectedMaskDecision_ShowsLinkedImpactAndFilteredScope(
        DxfRegularMaskAuditStatus status, IndexMappingDecisionFilterMode filter, string label, string suggested)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new IndexMappingReportViewModel("mapping", CreateVerificationMaskReport(status))
        {
            SelectedFilterMode = filter,
        };

        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Simulation);

        Assert.Equal("Simulation", vm.ActiveVerificationStepText);
        Assert.Equal("views: Before / After / Delta / Changed only (Simulation)", vm.ActiveStepInputText);
        Assert.Equal("color mode: AUTO / TH", vm.ActiveStepDecisionText);
        Assert.Equal($"impact: linked CAD 9011, suggested {suggested}", vm.ActiveStepOutputText);
        Assert.Equal("visible rows 1/1", vm.ActiveStepImpactText);
        Assert.Equal("Replay simulation for CAD 9011 and compare before/after.", vm.ActiveStepActionText);
        Assert.Equal($"color mode: AUTO / TH ({label})", vm.SimulationColorStateText);
        Assert.Equal($"entity=CAD 9011, impact={suggested}", vm.VerificationSteps[4].KeyChecks);
        Assert.Equal("status: warning", vm.ActiveStepCardStatusText);
        Assert.True(vm.ShowSimulationActionButtons);
        Assert.False(vm.ShowStep3ActionButtons);
        Assert.False(vm.ShowStep4ActionButtons);
        Assert.False(vm.ShowStep5ActionButtons);
    }
}

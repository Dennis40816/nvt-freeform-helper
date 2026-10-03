using System.Collections.ObjectModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchExportSelectionViewModelTests
{
    private static readonly int[] CadPadIds4823And4848 = [4823, 4848];
    private static readonly int[] CadPadIds3660 = [3660];

    [Fact]
    public void Collections_ExposeReadOnlyFacade()
    {
        var vm = new NotchExportSelectionViewModel(BuildSampleTable());

        Assert.IsType<ReadOnlyObservableCollection<NotchExportRowItemViewModel>>(vm.Rows);
        Assert.IsType<ReadOnlyObservableCollection<NotchExportIcGroupViewModel>>(vm.IcGroups);
        Assert.IsType<ReadOnlyObservableCollection<NotchExportRowItemViewModel>>(vm.WorkspaceLinkedRows);
    }


    [Fact]
    public void Ctor_SelectsAllRowsByDefault()
    {
        var table = BuildSampleTable();
        var vm = new NotchExportSelectionViewModel(table);

        Assert.Equal(3, vm.TotalCount);
        Assert.Equal(3, vm.SelectedCount);
        Assert.True(vm.HasSelection);
    }


    [Fact]
    public void SelectNoneCommand_ClearsAllSelections()
    {
        var table = BuildSampleTable();
        var vm = new NotchExportSelectionViewModel(table);

        vm.SelectNoneCommand.Execute(null);

        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.HasSelection);
    }


    [Fact]
    public void BuildSelectedTable_ReturnsOnlyCheckedRows()
    {
        var table = BuildSampleTable();
        var vm = new NotchExportSelectionViewModel(table);

        vm.Rows[1].IsSelected = false;
        var selected = vm.BuildSelectedTable();

        Assert.Equal(2, selected.Rows.Count);
        Assert.Equal(table.Rows[0].DiffIndex, selected.Rows[0].DiffIndex);
        Assert.Equal(table.Rows[2].DiffIndex, selected.Rows[1].DiffIndex);
    }


    [Fact]
    public void Ctor_BuildsIcGroupsAndSelectsFirstPreviewRow()
    {
        var table = BuildSampleTable();
        var vm = new NotchExportSelectionViewModel(table);

        Assert.Equal(2, vm.IcGroups.Count);
        Assert.Equal("IC 1", vm.IcGroups[0].HeaderText);
        Assert.Equal("IC 2", vm.IcGroups[1].HeaderText);
        Assert.True(vm.HasSelectedRow);
        Assert.Equal(vm.Rows[0], vm.SelectedRow);
        Assert.True(vm.Rows[0].IsPreviewSelected);
    }


    [Fact]
    public void SelectPreviewRowCommand_UpdatesPreviewSelection()
    {
        var table = BuildSampleTable();
        var vm = new NotchExportSelectionViewModel(table);

        vm.SelectPreviewRowCommand.Execute(vm.Rows[2]);

        Assert.Equal(vm.Rows[2], vm.SelectedRow);
        Assert.True(vm.Rows[2].IsPreviewSelected);
        Assert.False(vm.Rows[0].IsPreviewSelected);
    }


    [Fact]
    public void SelectedRowPositionText_TracksVisibleRowIndex()
    {
        var table = BuildSampleTable();
        var vm = new NotchExportSelectionViewModel(table);

        Assert.Equal("Row 1/3", vm.SelectedRowPositionText);

        vm.SelectPreviewRowCommand.Execute(vm.Rows[2]);

        Assert.Equal("Row 3/3", vm.SelectedRowPositionText);
    }


    [Fact]
    public void GroupCommands_OnlyAffectTargetIcRows()
    {
        var table = BuildSampleTable();
        var vm = new NotchExportSelectionViewModel(table);

        vm.IcGroups[1].SelectNoneGroupCommand.Execute(null);

        Assert.Equal(2, vm.SelectedCount);
        Assert.True(vm.Rows[0].IsSelected);
        Assert.True(vm.Rows[1].IsSelected);
        Assert.False(vm.Rows[2].IsSelected);
        Assert.Equal("0/1", vm.IcGroups[1].SummaryText);
    }


    [Fact]
    public void SelectPreviewRowCommand_InvokesPreviewCallback()
    {
        var table = BuildSampleTable();
        NotchTableRow? callbackRow = null;
        var vm = new NotchExportSelectionViewModel(table, row => callbackRow = row);

        vm.SelectPreviewRowCommand.Execute(vm.Rows[1]);

        Assert.Same(vm.Rows[1].Row, callbackRow);
    }


    [Fact]
    public void SelectPreviewRowCommand_ReinvokesPreviewCallback_WhenSameRowIsSelected()
    {
        var table = BuildSampleTable();
        var callbackCount = 0;
        var vm = new NotchExportSelectionViewModel(table, _ => callbackCount++);

        vm.SelectPreviewRowCommand.Execute(vm.Rows[0]);
        vm.SelectPreviewRowCommand.Execute(vm.Rows[0]);

        Assert.Equal(2, callbackCount);
    }


    [Fact]
    public void CodePreview_UsesBracketOnlyValuesFormat()
    {
        var table = BuildSampleTable();
        var vm = new NotchExportSelectionViewModel(table);

        Assert.Equal("{ 21, 1, 2 }", vm.Rows[0].CodePreviewText);
        Assert.Equal("{ 21, 1, 2 }", vm.SelectedRowCodePreviewText);
    }


    [Fact]
    public void Ctor_WithExportTypeOptions_UsesSelectedType()
    {
        var table = BuildSampleTable();
        var options = new[]
        {
            new FreeformHelperViewModel.NotchExportFileTypeOption(
                FreeformHelperViewModel.NotchExportFileType.Csv,
                "CSV review (.csv)",
                "csv"),
            new FreeformHelperViewModel.NotchExportFileTypeOption(
                FreeformHelperViewModel.NotchExportFileType.Cv22,
                "C v2.2 (.c)",
                "c",
                NotchAlgorithmVersion.V22),
        };

        var vm = new NotchExportSelectionViewModel(
            table,
            exportTypeOptions: options,
            selectedExportTypeOption: options[1]);

        Assert.True(vm.HasExportTypeOptions);
        Assert.True(vm.CanSelectExportType);
        Assert.Equal(FreeformHelperViewModel.NotchExportFileType.Cv22, vm.SelectedExportTypeOption.Value);
        Assert.Equal("C v2.2 (.c)", vm.SelectedExportTypeText);
    }


    [Fact]
    public void SelectedExportTypeOption_Cv21_PinsVersionSelection()
    {
        var table = BuildSampleTable();
        var options = new[]
        {
            new FreeformHelperViewModel.NotchExportFileTypeOption(
                FreeformHelperViewModel.NotchExportFileType.Csv,
                "CSV review (.csv)",
                "csv"),
            new FreeformHelperViewModel.NotchExportFileTypeOption(
                FreeformHelperViewModel.NotchExportFileType.Cv21,
                "C v2.1 (.c)",
                "c",
                NotchAlgorithmVersion.V21),
            new FreeformHelperViewModel.NotchExportFileTypeOption(
                FreeformHelperViewModel.NotchExportFileType.Cv22,
                "C v2.2 (.c)",
                "c",
                NotchAlgorithmVersion.V22),
        };

        var vm = new NotchExportSelectionViewModel(
            table,
            exportTypeOptions: options,
            selectedExportTypeOption: options[1]);

        Assert.True(vm.IsVersionSelectionPinnedByExportType);
        Assert.False(vm.CanSelectVersion);
        Assert.Equal(NotchAlgorithmVersion.V21, vm.SelectedVersionOption.Value);
    }


    [Fact]
    public void SelectedExportTypeOption_Cv21_StillPinsVersionWhenTableHasNoV21Rows()
    {
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 11,
                regularPadIndex: 101,
                cadPadId: 201,
                v22Node: new NotchV22Node(11, 95, 12, 5, 65535, 0, 0),
                comment: "v22-only"),
        });
        var options = new[]
        {
            new FreeformHelperViewModel.NotchExportFileTypeOption(
                FreeformHelperViewModel.NotchExportFileType.Csv,
                "CSV review (.csv)",
                "csv"),
            new FreeformHelperViewModel.NotchExportFileTypeOption(
                FreeformHelperViewModel.NotchExportFileType.Cv21,
                "C v2.1 (.c)",
                "c",
                NotchAlgorithmVersion.V21),
        };

        var vm = new NotchExportSelectionViewModel(
            table,
            exportTypeOptions: options,
            selectedExportTypeOption: options[1]);

        Assert.Equal(NotchAlgorithmVersion.V21, vm.SelectedVersionOption.Value);
        Assert.Equal(0, vm.TotalCount);
        Assert.Empty(vm.BuildSelectedTable().Rows);
    }

    [Fact]
    public void Ctor_ReportsPassingToFullCoverageAudit()
    {
        var audit = new NotchToFullCoverageAudit(
            BucketCount: 2,
            ExpectedTargetDiffCount: 3,
            CoveredTargetDiffCount: 3,
            MissingGaps: Array.Empty<NotchToFullCoverageGap>());
        var table = new NotchTable(BuildSampleTable().Rows, audit);
        var vm = new NotchExportSelectionViewModel(table);

        Assert.True(vm.HasToFullCoverageExpectations);
        Assert.True(vm.HasToFullCoveragePass);
        Assert.False(vm.HasToFullCoverageWarning);
        Assert.Equal("ToFull coverage ok 3/3", vm.ToFullCoverageBadgeText);
        Assert.Contains("All 3 ToFull target diff(s) are covered", vm.ToFullCoverageSummaryText, StringComparison.Ordinal);
    }

    [Fact]
    public void Ctor_ReportsMissingToFullCoverageAudit()
    {
        var audit = new NotchToFullCoverageAudit(
            BucketCount: 2,
            ExpectedTargetDiffCount: 4,
            CoveredTargetDiffCount: 2,
            MissingGaps: new[]
            {
                new NotchToFullCoverageGap(
                    IcIndex: 0,
                    SourceDiffIndex: 323,
                    TargetDiffIndex: 325,
                    CadPadIds: CadPadIds4823And4848),
                new NotchToFullCoverageGap(
                    IcIndex: 0,
                    SourceDiffIndex: 396,
                    TargetDiffIndex: 397,
                    CadPadIds: CadPadIds3660),
            });
        var table = new NotchTable(BuildSampleTable().Rows, audit);
        var vm = new NotchExportSelectionViewModel(table);

        Assert.True(vm.HasToFullCoverageExpectations);
        Assert.True(vm.HasToFullCoverageWarning);
        Assert.True(vm.HasToFullCoverageMissingList);
        Assert.Equal("ToFull coverage missing 2/4", vm.ToFullCoverageBadgeText);
        Assert.Contains("IC 1 / FW Diff 323 -> FW Diff 325 | CAD 4823/4848", vm.ToFullCoverageMissingListText, StringComparison.Ordinal);
        Assert.Contains("IC 1 / FW Diff 396 -> FW Diff 397 | CAD 3660", vm.ToFullCoverageMissingListText, StringComparison.Ordinal);
    }

    [Fact]
    public void AttachSimulationSafetyAudit_WhenViolationExists_BlocksExport()
    {
        var vm = new NotchExportSelectionViewModel(BuildSampleTable());
        var audit = SimulationSafetyAuditService.Analyze(
        [
            new NotchApplySimulationDiffCell(RegularPadId: 1, RegularRow: 0, RegularCol: 0, IcIndex: 0, DiffIndex: 10, BeforeValue: 400d, AfterValue: 500d),
            new NotchApplySimulationDiffCell(RegularPadId: 2, RegularRow: 0, RegularCol: 1, IcIndex: 0, DiffIndex: 11, BeforeValue: 400d, AfterValue: 452d),
        ]) with
        {
            GlobalFlowAudit = new SimulationGlobalFlowAudit(
                HasActionFlowModel: true,
                BeforeTotal: 800d,
                AfterTotal: 952d,
                DeltaTotal: 152d,
                ActionNetFlowTotal: 0d,
                ResidualValue: 152d),
        };

        vm.AttachSimulationSafetyAudit(audit);

        Assert.True(audit.HasPhysicalAuditRisks);
        Assert.False(vm.HasSimulationPhysicalAuditWarning);
        Assert.True(vm.IsExportBlockedBySimulationSafety);
        Assert.Equal("EMS risk 1", vm.SimulationSafetyExportBadgeText);
        Assert.Equal("Export blocked (EMS risk)", vm.ExportButtonText);
        Assert.True(vm.TryGetExportBlockMessage(out var title, out var message));
        Assert.Equal("Export blocked by EMS safety", title);
        Assert.Equal(
            "Blocked: 1 pad(s) exceed EMS cap 480. Max After 500 at REG 1." + Environment.NewLine + Environment.NewLine +
            "#1 REG 1 · IC 1 · FW Diff 10 · After 500 · +20 over cap" + Environment.NewLine +
            "#2 REG 2 · IC 1 · FW Diff 11 · After 452 · margin 28",
            message);
    }

    [Fact]
    public void AttachSimulationSafetyAudit_WhenClean_DoesNotBlockExport()
    {
        var vm = new NotchExportSelectionViewModel(BuildSampleTable());
        var audit = SimulationSafetyAuditService.Analyze(
        [
            new NotchApplySimulationDiffCell(RegularPadId: 1, RegularRow: 0, RegularCol: 0, IcIndex: 0, DiffIndex: 10, BeforeValue: 400d, AfterValue: 452d),
        ]);

        vm.AttachSimulationSafetyAudit(audit);

        Assert.True(vm.IsSimulationSafetyClean);
        Assert.False(vm.HasSimulationPhysicalAuditWarning);
        Assert.False(vm.IsExportBlockedBySimulationSafety);
        Assert.False(vm.TryGetExportBlockMessage(out _, out _));
        Assert.Equal("EMS OK · Max After 452", vm.SimulationSafetyExportBadgeText);
        Assert.Equal("Safe for EMS cap 480: Max After 452 at REG 1.", vm.SimulationSafetyExportSummaryText);
    }

    [Fact]
    public void AttachSimulationSafetyAudit_WhenPhysicalAuditWarns_ShowsNonBlockingWarning()
    {
        const double beforeValue = 400d;
        const double afterValue = 450d;
        const double residualValue = 50d;
        var vm = new NotchExportSelectionViewModel(BuildSampleTable());
        var audit = SimulationSafetyAuditService.Analyze(
        [
            new NotchApplySimulationDiffCell(
                RegularPadId: 1,
                RegularRow: 0,
                RegularCol: 0,
                IcIndex: 0,
                DiffIndex: 10,
                BeforeValue: beforeValue,
                AfterValue: afterValue),
        ]) with
        {
            GlobalFlowAudit = new SimulationGlobalFlowAudit(
                HasActionFlowModel: true,
                BeforeTotal: beforeValue,
                AfterTotal: afterValue,
                DeltaTotal: afterValue - beforeValue,
                ActionNetFlowTotal: 0d,
                ResidualValue: residualValue),
        };

        vm.AttachSimulationSafetyAudit(audit);

        Assert.True(audit.HasPhysicalAuditRisks);
        Assert.False(audit.HasViolations);
        Assert.True(vm.HasSimulationPhysicalAuditWarning);
        Assert.False(vm.IsExportBlockedBySimulationSafety);
        Assert.False(vm.TryGetExportBlockMessage(out _, out _));
        Assert.Equal(
            (
                false,
                "audit warning · Max After 450",
                "Safe for EMS cap 480: Max After 450 at REG 1. " +
                "Physical audit: global flow residual +50; net-flow OK; target coverage <= 120%."),
            (
                vm.IsSimulationSafetyClean,
                vm.SimulationSafetyExportBadgeText,
                vm.SimulationSafetyExportSummaryText));
    }

    [Theory]
    [InlineData(null, "480")]
    [InlineData(512d, "512")]
    public void AttachSimulationSafetyAudit_WhenUnavailable_RemainsNotRun(double? afterCap, string expectedCap)
    {
        var vm = new NotchExportSelectionViewModel(BuildSampleTable());

        vm.AttachSimulationSafetyAudit(afterCap.HasValue
            ? SimulationSafetyAuditResult.Empty(afterCap.Value)
            : null);

        Assert.True(vm.HasNoSimulationSafetyAudit);
        Assert.False(vm.IsSimulationSafetyClean);
        Assert.False(vm.HasSimulationPhysicalAuditWarning);
        Assert.False(vm.IsExportBlockedBySimulationSafety);
        Assert.Equal("Simulation audit: not run", vm.SimulationSafetyExportBadgeText);
        Assert.Equal(
            "Simulation audit is not available in the current workspace. " +
            $"Open Simulation to review After <= {expectedCap} before FW handoff.",
            vm.SimulationSafetyExportSummaryText);
    }
}

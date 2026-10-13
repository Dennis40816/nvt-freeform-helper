using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

public sealed class SimulationSafetyOverviewResultBehaviorTests
{
    private static readonly string[] _overviewNames =
    [
        nameof(FreeformHelperViewModel.HasSimulationSafetyOverviewAudit),
        nameof(FreeformHelperViewModel.HasSimulationSafetyOverviewRisk),
        nameof(FreeformHelperViewModel.HasSimulationSafetyOverviewAttention),
        nameof(FreeformHelperViewModel.SimulationSafetyOverviewStatusText),
        nameof(FreeformHelperViewModel.SimulationSafetyOverviewEmsCapText),
        nameof(FreeformHelperViewModel.SimulationSafetyOverviewMaxAfterText),
        nameof(FreeformHelperViewModel.SimulationSafetyOverviewViolationCountText),
        nameof(FreeformHelperViewModel.SimulationSafetyOverviewSummaryText),
        nameof(FreeformHelperViewModel.SimulationSafetyOverviewHighRiskDiffsText),
    ];

    [Fact]
    public void NewViewModel_BeforeAnyApply_ShowsTheNotRunOverview()
    {
        var vm = new FreeformHelperViewModel();

        Assert.False(vm.HasSimulationSafetyOverviewAudit);
        Assert.False(vm.HasSimulationSafetyOverviewRisk);
        Assert.False(vm.HasSimulationSafetyOverviewAttention);
        Assert.Equal("Simulation not run", vm.SimulationSafetyOverviewStatusText);
        Assert.Equal("480", vm.SimulationSafetyOverviewEmsCapText);
        Assert.Equal("Max After -", vm.SimulationSafetyOverviewMaxAfterText);
        Assert.Equal("-", vm.SimulationSafetyOverviewViolationCountText);
        Assert.Equal(
            "Run Simulation to audit current notch output before FW handoff.",
            vm.SimulationSafetyOverviewSummaryText);
        Assert.Equal("Top risk diffs: -", vm.SimulationSafetyOverviewHighRiskDiffsText);
    }

    [Fact]
    public void ApplySimulationSafetyOverview_AuditWithViolation_ShowsRiskAndTheProjectedTexts()
    {
        var vm = new FreeformHelperViewModel();
        var projection = SimulationSafetyOverviewProjectorForTests.Project(CreateAudit(afterValue: 530d));

        vm.ApplySimulationSafetyOverview(CreateAudit(afterValue: 530d), isStale: false, buildFailureText: null);

        Assert.True(vm.HasSimulationSafetyOverviewAudit);
        Assert.True(vm.HasSimulationSafetyOverviewRisk);
        Assert.True(vm.HasSimulationSafetyOverviewAttention);
        Assert.Equal(projection.StatusText, vm.SimulationSafetyOverviewStatusText);
        Assert.Equal(projection.EmsCapText, vm.SimulationSafetyOverviewEmsCapText);
        Assert.Equal(projection.MaxAfterText, vm.SimulationSafetyOverviewMaxAfterText);
        Assert.Equal(projection.ViolationCountText, vm.SimulationSafetyOverviewViolationCountText);
        Assert.Equal(projection.SummaryText, vm.SimulationSafetyOverviewSummaryText);
        Assert.Equal(projection.HighRiskDiffsText, vm.SimulationSafetyOverviewHighRiskDiffsText);
    }

    [Fact]
    public void ApplySimulationSafetyOverview_StaleAudit_NeedsAttentionWithoutRisk()
    {
        var vm = new FreeformHelperViewModel();

        vm.ApplySimulationSafetyOverview(CreateAudit(afterValue: 450d), isStale: true, buildFailureText: null);

        Assert.True(vm.HasSimulationSafetyOverviewAudit);
        Assert.False(vm.HasSimulationSafetyOverviewRisk);
        Assert.True(vm.HasSimulationSafetyOverviewAttention);
        Assert.EndsWith("(stale)", vm.SimulationSafetyOverviewStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplySimulationSafetyOverview_BuildFailure_ShowsUnavailableWithTheFailureText()
    {
        var vm = new FreeformHelperViewModel();

        vm.ApplySimulationSafetyOverview(audit: null, isStale: false, buildFailureText: "  Grid missing.  ");

        Assert.False(vm.HasSimulationSafetyOverviewAudit);
        Assert.True(vm.HasSimulationSafetyOverviewAttention);
        Assert.Equal("Simulation unavailable", vm.SimulationSafetyOverviewStatusText);
        Assert.Equal("Grid missing.", vm.SimulationSafetyOverviewSummaryText);
    }

    [Fact]
    public void ApplySimulationSafetyOverview_FirstAudit_RaisesEveryChangedProperty()
    {
        var vm = new FreeformHelperViewModel();
        var changes = RecordNotifications(vm, _overviewNames);

        vm.ApplySimulationSafetyOverview(CreateAudit(afterValue: 530d, afterCap: 512d), isStale: false, buildFailureText: null);

        Assert.Equal(_overviewNames.Order(StringComparer.Ordinal), changes.OfType<string>().Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ApplySimulationSafetyOverview_SameAuditTwice_RaisesNothingTheSecondTime()
    {
        var vm = new FreeformHelperViewModel();
        vm.ApplySimulationSafetyOverview(CreateAudit(afterValue: 530d), isStale: false, buildFailureText: null);
        var changes = RecordNotifications(vm, _overviewNames);

        vm.ApplySimulationSafetyOverview(CreateAudit(afterValue: 530d), isStale: false, buildFailureText: null);

        Assert.Empty(changes);
    }

    [Fact]
    public void ApplySimulationSafetyOverview_StaleFlagOnly_RaisesStatusAttentionAndSummary()
    {
        var vm = new FreeformHelperViewModel();
        vm.ApplySimulationSafetyOverview(CreateAudit(afterValue: 450d), isStale: false, buildFailureText: null);
        var changes = RecordNotifications(vm, _overviewNames);

        vm.ApplySimulationSafetyOverview(CreateAudit(afterValue: 450d), isStale: true, buildFailureText: null);

        string[] expected =
        [
            nameof(FreeformHelperViewModel.HasSimulationSafetyOverviewAttention),
            nameof(FreeformHelperViewModel.SimulationSafetyOverviewStatusText),
            nameof(FreeformHelperViewModel.SimulationSafetyOverviewSummaryText),
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal), changes.OfType<string>().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ApplySimulationSafetyOverview_CapChange_RaisesTheDependentGuidanceBeforeTheCapText()
    {
        var vm = new FreeformHelperViewModel();
        var changes = RecordNotifications(
            vm,
            nameof(FreeformHelperViewModel.SimulationSafetyOverviewEmsCapText),
            nameof(FreeformHelperViewModel.NotchEmsSafetyPolicySummary),
            nameof(FreeformHelperViewModel.NotchEmsSafetyShortText),
            nameof(FreeformHelperViewModel.NotchTargetCoverageCapHelpText),
            nameof(FreeformHelperViewModel.NotchExportSafetyPolicySummary));

        vm.ApplySimulationSafetyOverview(SimulationSafetyAuditResult.Empty(afterCap: 512d), isStale: false, buildFailureText: null);

        string[] expected =
        [
            nameof(FreeformHelperViewModel.NotchEmsSafetyPolicySummary),
            nameof(FreeformHelperViewModel.NotchEmsSafetyShortText),
            nameof(FreeformHelperViewModel.NotchTargetCoverageCapHelpText),
            nameof(FreeformHelperViewModel.NotchExportSafetyPolicySummary),
            nameof(FreeformHelperViewModel.SimulationSafetyOverviewEmsCapText),
        ];
        Assert.Equal(expected, changes);
    }

    [Fact]
    public void ApplySimulationSafetyOverview_SameCap_RaisesNoDependentGuidance()
    {
        var vm = new FreeformHelperViewModel();
        vm.ApplySimulationSafetyOverview(SimulationSafetyAuditResult.Empty(afterCap: 512d), isStale: false, buildFailureText: null);
        var changes = RecordNotifications(
            vm,
            nameof(FreeformHelperViewModel.NotchEmsSafetyPolicySummary),
            nameof(FreeformHelperViewModel.NotchEmsSafetyShortText));

        vm.ApplySimulationSafetyOverview(SimulationSafetyAuditResult.Empty(afterCap: 512d), isStale: true, buildFailureText: null);

        Assert.Empty(changes);
    }

    private static SimulationSafetyAuditResult CreateAudit(double afterValue, double afterCap = 480d)
    {
        return SimulationSafetyAuditService.Analyze(
        [
            new NotchApplySimulationDiffCell(
                RegularPadId: 1,
                RegularRow: 0,
                RegularCol: 0,
                IcIndex: 0,
                DiffIndex: 10,
                BeforeValue: 400d,
                AfterValue: afterValue),
        ], afterCap: afterCap);
    }

    private static class SimulationSafetyOverviewProjectorForTests
    {
        public static FreeformHelper.UI.Services.SimulationSafetyOverviewProjection Project(SimulationSafetyAuditResult audit)
        {
            return FreeformHelper.UI.Services.SimulationSafetyOverviewProjector.Project(
                audit,
                isStale: false,
                buildFailureText: null);
        }
    }
}

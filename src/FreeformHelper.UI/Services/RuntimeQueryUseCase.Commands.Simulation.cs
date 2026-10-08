using Nvt.Core.RuntimeQuery;
namespace FreeformHelper.UI.Services;

internal sealed partial class RuntimeQueryUseCase
{
    private async Task<RuntimeQueryResponseEnvelope> QuerySimulationAsync(IReadOnlyDictionary<string, string>? args)
    {
        var hasRegularId = RuntimeQueryArgumentParser.TryGetIntArg(args, "regular-id", 0, int.MaxValue, out var regularPadId, out var regularIdError);
        if (regularIdError is not null)
        {
            return regularIdError;
        }

        var simulation = _shellViewModel.Simulation;
        var workspace = simulation.CurrentWorkspace;
        if (workspace is null)
        {
            try
            {
                await simulation.EnsureWorkspaceAsync();
            }
            catch (Exception ex)
            {
                return RuntimeQueryResponseEnvelope.Failure(
                    code: "BUILD_FAILED",
                    message: $"Simulation workspace build failed: {ex.Message}");
            }

            workspace = simulation.CurrentWorkspace;
        }

        if (workspace is null)
        {
            var detail = string.IsNullOrWhiteSpace(simulation.BuildFailureText)
                ? "Simulation workspace is not ready for current workflow state."
                : simulation.BuildFailureText;
            return RuntimeQueryResponseEnvelope.Failure(
                code: "NOT_READY",
                message: $"Simulation workspace is not ready. {detail}");
        }

        object? regular = null;
        if (hasRegularId)
        {
            if (!workspace.TryGetRegularQuerySnapshot(regularPadId, out var snapshot))
            {
                return RuntimeQueryResponseEnvelope.Failure(
                    code: "PAD_NOT_FOUND",
                    message: $"Regular pad {regularPadId} is unavailable in the current Simulation workspace.");
            }

            regular = new
            {
                snapshot.RegularPadId,
                snapshot.Row,
                snapshot.Col,
                snapshot.IcIndex,
                snapshot.FwDiffIndex,
                snapshot.CadOutputFwDiffIndex,
                diffIndex = snapshot.FwDiffIndex,
                snapshot.BeforeValue,
                snapshot.AfterValue,
                snapshot.DeltaValue,
                snapshot.IsEmsSafetyRisk,
                snapshot.IsActiveSurface,
                snapshot.IsWithinSelectedArea,
                snapshot.InputSourceText,
                impacts = snapshot.ImpactItems.Select(static item => new
                {
                    item.RoleText,
                    item.SummaryText,
                    item.DetailText,
                    item.DeltaText,
                    item.SourceRowsText
                }).ToList()
            };
        }

        var cadOutputFwDiffAssignmentDecision = workspace.BuildCadOutputFwDiffAssignmentDecisionSummarySnapshot();

        return RuntimeQueryResponseEnvelope.Success(new
        {
            kind = "simulation",
            workspace = new
            {
                hasWorkspace = true,
                simulation.SourceRevision,
                isStale = simulation.IsWorkspaceStale,
                sourceMode = workspace.SourceModeSummaryText,
                version = workspace.SelectedVersionOption.Display,
                viewMode = workspace.SelectedCanvasViewOption.Display,
                colorMode = workspace.SelectedCanvasColorOption.Display,
                area = workspace.SelectedAreaOption.Display,
                frame = workspace.SelectedFrameDisplayText,
                activeRegularCount = workspace.ActiveSurfaceRegularCount,
                overlayCount = workspace.SimulationOverlayItems.Count,
                legend = new
                {
                    title = workspace.ColorLegendTitleText,
                    meaning = workspace.ColorLegendMeaningText,
                    start = workspace.ColorLegendStartText,
                    center = workspace.ShowLegendCenterLabel ? workspace.ColorLegendCenterText : null,
                    end = workspace.ColorLegendEndText
                },
                diffValidation = new
                {
                    workspace.HasDiffViolationPads,
                    workspace.DiffViolationPadCount,
                    workspace.DiffViolationSummaryText,
                    workspace.DiffViolationPadListText
                },
                safety = new
                {
                    workspace.SimulationEmsAfterCap,
                    workspace.SimulationMaxAfterText,
                    workspace.HasSimulationSafetyViolations,
                    workspace.SimulationSafetyViolationCount,
                    workspace.SimulationSafetySummaryText,
                    workspace.SimulationSafetyViolationListText
                },
                diffIdentity = new
                {
                    workspace.HasDuplicateDiffResolutions,
                    workspace.DuplicateDiffResolutionCount,
                    workspace.DuplicateDiffResolutionStrategyText,
                    workspace.DuplicateDiffResolutionSummaryText,
                    workspace.DuplicateDiffResolutionSampleText
                },
                cadOutputFwDiffAssignmentDecision = new
                {
                    cadOutputFwDiffAssignmentDecision.DecisionCount,
                    cadOutputFwDiffAssignmentDecision.Mode,
                    reasonCounts = cadOutputFwDiffAssignmentDecision.ReasonCounts,
                    decisionSourceCounts = cadOutputFwDiffAssignmentDecision.DecisionSourceCounts
                }
            },
            regular
        });
    }
}


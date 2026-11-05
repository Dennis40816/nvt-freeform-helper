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
            var buildFailure = await RunCommandOperationAsync(
                simulation.EnsureWorkspaceAsync,
                "BUILD_FAILED",
                ex => $"Simulation workspace build failed: {ex.Message}");
            if (buildFailure is not null)
            {
                return buildFailure;
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

            regular = BuildSimulationRegularPayload(snapshot);
        }

        return RuntimeQueryResponseEnvelope.Success(new
        {
            kind = "simulation",
            workspace = BuildSimulationWorkspacePayload(simulation, workspace),
            regular
        });
    }
}

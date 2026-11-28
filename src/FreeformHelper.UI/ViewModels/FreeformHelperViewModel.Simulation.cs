using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private readonly SimulationSessionBuilder _simulationSessionBuilder;

    public event EventHandler? SimulationWorkspaceSourceChanged;

    public int SimulationWorkspaceSourceRevision => _simulationSessionBuilder.SourceChanges.Revision;

    public string LastSimulationWorkspaceAttemptMessage => _simulationSessionBuilder.LastAttemptMessage;

    public async Task<SimulationWorkspaceSession?> CreateSimulationWorkspaceSessionAsync(bool silent)
    {
        return await CreateSimulationWorkspaceSessionAsync(silent, progressTextReporter: null);
    }

    public async Task<SimulationWorkspaceSession?> CreateSimulationWorkspaceSessionAsync(
        bool silent,
        Action<string>? progressTextReporter)
    {
        return await _simulationSessionBuilder.CreateAsync(silent, progressTextReporter);
    }

    private (UiOperationStatusReporter Status, SimulationSessionBuilder Builder) CreateSimulationServices()
    {
        var status = new UiOperationStatusReporter(message => StatusText = message);
        var ports = new SimulationSessionPorts(
            ReadSimulationSessionInputs,
            BuildWorkflowDataSnapshot,
            GenerateSimulationSessionTableAsync,
            snapshot => RefreshStep4DuplicateDiffGroups(CadPads.ToList(), snapshot),
            ReadSimulationDuplicateDiffWarning,
            () => new SimulationSessionDisplay(ShowRegular, HighlightUnmatched, HighlightFreeform),
            CreateStatusScope,
            (name, operationAsync, onError, showSpinner) =>
                RunUiOperationAsync(name, operationAsync, onError, showModalSpinner: showSpinner),
            SetStatus);
        return (status, new SimulationSessionBuilder(
            ports, _logger, () => SimulationWorkspaceSourceChanged?.Invoke(this, EventArgs.Empty)));
    }

    private SimulationSessionInputs? ReadSimulationSessionInputs(bool silent)
    {
        CadPadSet? cad;
        RegularGrid? grid;
        if (silent)
        {
            grid = _grid;
            cad = BuildFilteredCadPadSet();
            if (grid is null || cad is null || cad.Pads.Count == 0)
            {
                return null;
            }
        }
        else if (!TryGetOperationCadAndGrid(
                     BuildFilteredCadPadSet,
                     "Simulation: import DXF and build grid first.",
                     out cad!,
                     out grid!))
        {
            return null;
        }

        return new SimulationSessionInputs(cad, grid);
    }

    private async Task<SimulationSessionGeneration?> GenerateSimulationSessionTableAsync(
        SimulationSessionInputs inputs, WorkflowDataSnapshot workflowSnapshot, bool silent, Action<string>? progressTextReporter)
    {
        var generation = await GenerateCurrentNotchTableAsync(
            inputs.Cad,
            inputs.Grid,
            "Simulation workspace",
            workflowSnapshot: workflowSnapshot,
            reportStep5Progress: !silent,
            projectToCadOutputFwDiff: true,
            progressTextReporter: silent ? null : progressTextReporter);
        return generation is null ? null : new SimulationSessionGeneration(
            generation.Table, generation.RawGrid, generation.CadOutputFwDiffGrid, generation.ExportSettings, generation.SourceRevision);
    }

    private SimulationDuplicateDiffWarning? ReadSimulationDuplicateDiffWarning()
    {
        if (!HasStep4DuplicateDiffGroups)
        {
            return null;
        }

        var sample = Step4DuplicateDiffGroups[0];
        return new SimulationDuplicateDiffWarning(sample.IcIndex, sample.DiffIndex, sample.CadPadIds, Step4DuplicateDiffGroups.Count);
    }

    internal void NotifySimulationWorkspaceSourceChanged() => _simulationSessionBuilder.SourceChanges.Notify();
}

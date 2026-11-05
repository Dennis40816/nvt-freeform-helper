using System.Globalization;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private readonly SimulationSourceChangeBatchState _simulationSourceChangeBatch = new(); // Holds the suspend depth and the pending flag of the simulation workspace source change batch.

    public event EventHandler? SimulationWorkspaceSourceChanged;

    public int SimulationWorkspaceSourceRevision { get; private set; }

    public string LastSimulationWorkspaceAttemptMessage { get; private set; } = string.Empty;

    public async Task<SimulationWorkspaceSession?> CreateSimulationWorkspaceSessionAsync(bool silent)
    {
        return await CreateSimulationWorkspaceSessionAsync(silent, progressTextReporter: null);
    }

    public async Task<SimulationWorkspaceSession?> CreateSimulationWorkspaceSessionAsync(
        bool silent,
        Action<string>? progressTextReporter)
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

        LastSimulationWorkspaceAttemptMessage = string.Empty;
        SimulationWorkspaceSession? session = null;
        UiOperationStatusReporter.Scope? status = silent ? null : CreateStatusScope("SimulationWorkspace");
        var reporter = new SimulationBuildReporter(silent, status, progressTextReporter);
        await RunUiOperationAsync(
            operationName: "Build simulation workspace",
            operationAsync: async () =>
            {
                reporter.Progress("Preparing simulation workspace...");

                var workflowSnapshot = BuildWorkflowDataSnapshot();
                reporter.Progress("Generating simulation notch table...");
                var generation = await GenerateCurrentNotchTableAsync(
                    cad,
                    grid,
                    "Simulation workspace",
                    workflowSnapshot: workflowSnapshot,
                    reportStep5Progress: !silent,
                    projectToCadOutputFwDiff: true,
                    progressTextReporter: silent ? null : progressTextReporter);
                if (generation is null)
                {
                    return;
                }

                if (generation.Table.Rows.Count == 0)
                {
                    ReportSimulationNoRowsGenerated(reporter);
                    return;
                }

                reporter.Progress("Resolving simulation diff grid...");
                var activeRegularPadIds = BuildSimulationActiveRegularPadIds(
                    generation.RawGrid,
                    workflowSnapshot.ActiveRegularVisibilityMaskPadIds);

                RefreshStep4DuplicateDiffGroups(CadPads.ToList(), workflowSnapshot);
                var duplicateWarningMessage = ReportSimulationDuplicateDiffWarning(reporter);

                reporter.Progress("Finalizing simulation workspace...");
                session = CreateSimulationWorkspaceSession(generation, activeRegularPadIds, workflowSnapshot, cad);
                ReportSimulationWorkspaceReady(reporter, generation, duplicateWarningMessage);
            },
            onError: ex => ReportSimulationBuildFailed(reporter, ex),
            showModalSpinner: !silent);

        return session;
    }

    private void ReportSimulationNoRowsGenerated(SimulationBuildReporter reporter)
    {
        var message = "Simulation: no notch rows generated under current settings.";
        LastSimulationWorkspaceAttemptMessage = message;
        if (reporter.Silent)
        {
            _logger.Info(message);
        }
        else
        {
            reporter.Status?.ReportBlocked(message);
            SetStatus(message);
        }

        reporter.Progress(message, reportToStatus: false);
    }

    private string? ReportSimulationDuplicateDiffWarning(SimulationBuildReporter reporter)
    {
        string? duplicateWarningMessage = null;
        if (_step4DuplicateDiffGroups.Length > 0)
        {
            var sample = _step4DuplicateDiffGroups[0];
            duplicateWarningMessage = string.Format(
                CultureInfo.InvariantCulture,
                "Simulation warning: duplicate CAD output FW diff at IC{0}/diff{1}; results may include ambiguous mappings.",
                sample.IcIndex + 1,
                sample.DiffIndex);
            _logger.Warn(
                CultureInfo.InvariantCulture,
                "{0} duplicate-groups={1} sample-cads={2}.",
                duplicateWarningMessage,
                _step4DuplicateDiffGroups.Length,
                string.Join(", ", sample.CadPadIds));
            if (!reporter.Silent)
            {
                reporter.Status?.ReportWarning(duplicateWarningMessage);
            }

            reporter.Progress(duplicateWarningMessage, reportToStatus: false);
        }

        return duplicateWarningMessage;
    }

    private SimulationWorkspaceSession CreateSimulationWorkspaceSession(
        NotchTableGenerationResult generation,
        HashSet<int> activeRegularPadIds,
        WorkflowDataSnapshot workflowSnapshot,
        CadPadSet cad)
    {
        return new SimulationWorkspaceSession(
            generation.RawGrid,
            generation.Table,
            generation.ExportSettings.Notch.NullValue,
            generation.SourceRevision,
            activeRegularPadIds,
            ShowRegular,
            HighlightUnmatched,
            HighlightFreeform,
            workflowSnapshot.CadOutputFwDiffAssignmentDecisionsByCadId,
            generation.CadOutputFwDiffGrid,
            cad.Pads.ToArray(),
            workflowSnapshot.CadOutputFwDiffIndexByCadId,
            workflowSnapshot.CadIcIndexByCadId,
            generation.ExportSettings.Notch.ComputationMode);
    }

    private void ReportSimulationWorkspaceReady(
        SimulationBuildReporter reporter,
        NotchTableGenerationResult generation,
        string? duplicateWarningMessage)
    {
        var readyMessage = string.Format(
            CultureInfo.InvariantCulture,
            "Simulation ready. Rows={0}.",
            generation.Table.Rows.Count);
        LastSimulationWorkspaceAttemptMessage = duplicateWarningMessage is null
            ? readyMessage
            : string.Concat(readyMessage, " ", duplicateWarningMessage);
        if (reporter.Silent)
        {
            _logger.Info(
                CultureInfo.InvariantCulture,
                duplicateWarningMessage is null
                    ? "Simulation prewarm ready. Rows={0}."
                    : "Simulation prewarm ready with duplicate-diff warning. Rows={0}.",
                generation.Table.Rows.Count);
        }
        else
        {
            reporter.Status?.ReportSuccess(readyMessage);
        }

        reporter.Progress(readyMessage, reportToStatus: false);
    }

    private void ReportSimulationBuildFailed(SimulationBuildReporter reporter, Exception ex)
    {
        var message = $"Simulation build failed: {ex.Message}";
        LastSimulationWorkspaceAttemptMessage = message;
        if (!reporter.Silent)
        {
            reporter.Status?.ReportError("Simulation workspace failed", ex);
            SetStatus(message);
        }

        _logger.Error(ex, "Simulation workspace failed.");
        reporter.Progress(message, reportToStatus: false);
    }

    private readonly record struct SimulationBuildReporter(
        bool Silent,
        UiOperationStatusReporter.Scope? Status,
        Action<string>? ProgressTextReporter)
    {
        public void Progress(string message, bool reportToStatus = true)
        {
            ProgressTextReporter?.Invoke(message);
            if (reportToStatus)
            {
                Status?.ReportProgress(message);
            }
        }
    }

    private static HashSet<int> BuildSimulationActiveRegularPadIds(
        RegularGrid effectiveGrid,
        IReadOnlySet<int>? activeRegularVisibilityMaskPadIds)
    {
        ArgumentNullException.ThrowIfNull(effectiveGrid);

        var activeRegularPadIds = effectiveGrid.Pads
            .Select(static pad => pad.RegularPadId)
            .ToHashSet();
        if (activeRegularVisibilityMaskPadIds is null)
        {
            return activeRegularPadIds;
        }

        activeRegularPadIds.IntersectWith(activeRegularVisibilityMaskPadIds);
        return activeRegularPadIds;
    }

    internal void NotifySimulationWorkspaceSourceChanged()
    {
        if (_simulationSourceChangeBatch.SuspendDepth > 0)
        {
            _simulationSourceChangeBatch.Pending = true;
            return;
        }

        RaiseSimulationWorkspaceSourceChanged();
    }

    private SimulationWorkspaceSourceChangeBatchScope BeginSimulationWorkspaceSourceChangeBatch()
    {
        _simulationSourceChangeBatch.SuspendDepth++;
        return new SimulationWorkspaceSourceChangeBatchScope(this);
    }

    private void EndSimulationWorkspaceSourceChangeBatch()
    {
        if (_simulationSourceChangeBatch.SuspendDepth <= 0)
        {
            return;
        }

        _simulationSourceChangeBatch.SuspendDepth--;
        if (_simulationSourceChangeBatch.SuspendDepth == 0 && _simulationSourceChangeBatch.Pending)
        {
            _simulationSourceChangeBatch.Pending = false;
            RaiseSimulationWorkspaceSourceChanged();
        }
    }

    private void RaiseSimulationWorkspaceSourceChanged()
    {
        SimulationWorkspaceSourceRevision++;
        SimulationWorkspaceSourceChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class SimulationWorkspaceSourceChangeBatchScope : IDisposable
    {
        private FreeformHelperViewModel? _owner;

        public SimulationWorkspaceSourceChangeBatchScope(FreeformHelperViewModel owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (_owner is null)
            {
                return;
            }

            _owner.EndSimulationWorkspaceSourceChangeBatch();
            _owner = null;
        }
    }
}

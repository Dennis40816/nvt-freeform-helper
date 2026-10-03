using System.Globalization;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private int _simulationWorkspaceSourceChangeSuspendDepth;
    private bool _simulationWorkspaceSourceChangePending;

    public event EventHandler? SimulationWorkspaceSourceChanged;

    public int SimulationWorkspaceSourceRevision { get; private set; }

    public string LastSimulationWorkspaceAttemptMessage { get; private set; } = string.Empty;

    public Task<SimulationWorkspaceSession?> CreateSimulationWorkspaceSessionAsync()
    {
        return CreateSimulationWorkspaceSessionAsync(silent: false);
    }

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
        await RunUiOperationAsync(
            operationName: "Build simulation workspace",
            operationAsync: async () =>
            {
                ReportSimulationBuildProgress("Preparing simulation workspace...");

                var workflowSnapshot = BuildWorkflowDataSnapshot();
                ReportSimulationBuildProgress("Generating simulation notch table...");
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
                    var message = "Simulation: no notch rows generated under current settings.";
                    LastSimulationWorkspaceAttemptMessage = message;
                    if (silent)
                    {
                        Logger.Info(message);
                    }
                    else
                    {
                        status?.ReportBlocked(message);
                        SetStatus(message);
                    }

                    ReportSimulationBuildProgress(message, reportToStatus: false);

                    return;
                }

                ReportSimulationBuildProgress("Resolving simulation diff grid...");
                var activeRegularPadIds = BuildSimulationActiveRegularPadIds(
                    generation.RawGrid,
                    workflowSnapshot.ActiveRegularVisibilityMaskPadIds);

                RefreshStep4DuplicateDiffGroups(CadPads.ToList(), workflowSnapshot);
                string? duplicateWarningMessage = null;
                if (_step4DuplicateDiffGroups.Length > 0)
                {
                    var sample = _step4DuplicateDiffGroups[0];
                    duplicateWarningMessage = string.Format(
                        CultureInfo.InvariantCulture,
                        "Simulation warning: duplicate CAD output FW diff at IC{0}/diff{1}; results may include ambiguous mappings.",
                        sample.IcIndex + 1,
                        sample.DiffIndex);
                    Logger.Warn(
                        CultureInfo.InvariantCulture,
                        "{0} duplicate-groups={1} sample-cads={2}.",
                        duplicateWarningMessage,
                        _step4DuplicateDiffGroups.Length,
                        string.Join(", ", sample.CadPadIds));
                    if (!silent)
                    {
                        status?.ReportWarning(duplicateWarningMessage);
                    }

                    ReportSimulationBuildProgress(duplicateWarningMessage, reportToStatus: false);
                }

                ReportSimulationBuildProgress("Finalizing simulation workspace...");
                session = new SimulationWorkspaceSession(
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
                var readyMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    "Simulation ready. Rows={0}.",
                    generation.Table.Rows.Count);
                LastSimulationWorkspaceAttemptMessage = duplicateWarningMessage is null
                    ? readyMessage
                    : string.Concat(readyMessage, " ", duplicateWarningMessage);
                if (silent)
                {
                    Logger.Info(
                        CultureInfo.InvariantCulture,
                        duplicateWarningMessage is null
                            ? "Simulation prewarm ready. Rows={0}."
                            : "Simulation prewarm ready with duplicate-diff warning. Rows={0}.",
                        generation.Table.Rows.Count);
                }
                else
                {
                    status?.ReportSuccess(readyMessage);
                }

                ReportSimulationBuildProgress(readyMessage, reportToStatus: false);
            },
            onError: ex =>
            {
                var message = $"Simulation build failed: {ex.Message}";
                LastSimulationWorkspaceAttemptMessage = message;
                if (!silent)
                {
                    status?.ReportError("Simulation workspace failed", ex);
                    SetStatus(message);
                }

                Logger.Error(ex, "Simulation workspace failed.");
                ReportSimulationBuildProgress(message, reportToStatus: false);
            },
            showModalSpinner: !silent);

        return session;

        void ReportSimulationBuildProgress(string message, bool reportToStatus = true)
        {
            progressTextReporter?.Invoke(message);
            if (reportToStatus)
            {
                status?.ReportProgress(message);
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

    private void NotifySimulationWorkspaceSourceChanged()
    {
        if (_simulationWorkspaceSourceChangeSuspendDepth > 0)
        {
            _simulationWorkspaceSourceChangePending = true;
            return;
        }

        RaiseSimulationWorkspaceSourceChanged();
    }

    private SimulationWorkspaceSourceChangeBatchScope BeginSimulationWorkspaceSourceChangeBatch()
    {
        _simulationWorkspaceSourceChangeSuspendDepth++;
        return new SimulationWorkspaceSourceChangeBatchScope(this);
    }

    private void EndSimulationWorkspaceSourceChangeBatch()
    {
        if (_simulationWorkspaceSourceChangeSuspendDepth <= 0)
        {
            return;
        }

        _simulationWorkspaceSourceChangeSuspendDepth--;
        if (_simulationWorkspaceSourceChangeSuspendDepth == 0 && _simulationWorkspaceSourceChangePending)
        {
            _simulationWorkspaceSourceChangePending = false;
            RaiseSimulationWorkspaceSourceChanged();
        }
    }

    private void RaiseSimulationWorkspaceSourceChanged()
    {
        SimulationWorkspaceSourceRevision++;
        SimulationWorkspaceSourceChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void NotifySimulationWorkspaceSourceChangedForTests()
    {
        NotifySimulationWorkspaceSourceChanged();
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

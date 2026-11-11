// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using NLog;

namespace FreeformHelper.UI.Services;

/// <summary>Owns session assembly and attempt reporting. Mutable state is UI-thread only.</summary>
internal sealed class SimulationSessionBuilder(SimulationSessionPorts ports, Logger logger, Action sourceChanged)
{
    private readonly SimulationSessionPorts _ports = ports;
    private readonly Logger _logger = logger;

    public SimulationSourceChangeBatchState SourceChanges { get; } = new(sourceChanged);

    public string LastAttemptMessage { get; private set; } = string.Empty;

    public async Task<SimulationWorkspaceSession?> CreateAsync(bool silent, Action<string>? progressTextReporter)
    {
        var inputs = _ports.ReadInputs(silent);
        if (inputs is null)
        {
            return null;
        }

        LastAttemptMessage = string.Empty;
        SimulationWorkspaceSession? session = null;
        UiOperationStatusReporter.Scope? status = silent ? null : _ports.CreateStatus("SimulationWorkspace");
        var reporter = new SimulationBuildReporter(silent, status, progressTextReporter);
        await _ports.RunAsync(
            "Build simulation workspace",
            async () =>
            {
                reporter.Progress("Preparing simulation workspace...");

                var workflowSnapshot = _ports.ReadWorkflowSnapshot();
                reporter.Progress("Generating simulation notch table...");
                var generation = await _ports.GenerateAsync(inputs, workflowSnapshot, silent, progressTextReporter);
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
                var activeRegularPadIds = RegularVisibilityMaskProjection.BuildActivePadIds(
                    generation.RawGrid,
                    workflowSnapshot.ActiveRegularVisibilityMaskPadIds);

                _ports.RefreshDuplicateGroups(workflowSnapshot);
                var duplicateWarningMessage = ReportSimulationDuplicateDiffWarning(reporter);

                reporter.Progress("Finalizing simulation workspace...");
                session = CreateSession(generation, activeRegularPadIds, workflowSnapshot, inputs.Cad);
                ReportSimulationWorkspaceReady(reporter, generation, duplicateWarningMessage);
            },
            ex => ReportSimulationBuildFailed(reporter, ex),
            !silent);

        return session;
    }

    private void ReportSimulationNoRowsGenerated(SimulationBuildReporter reporter)
    {
        var message = "Simulation: no notch rows generated under current settings.";
        LastAttemptMessage = message;
        if (reporter.Silent)
        {
            _logger.Info(message);
        }
        else
        {
            reporter.Status?.ReportBlocked(message);
            _ports.SetStatus(message);
        }

        reporter.Progress(message, reportToStatus: false);
    }

    private string? ReportSimulationDuplicateDiffWarning(SimulationBuildReporter reporter)
    {
        string? duplicateWarningMessage = null;
        var sample = _ports.ReadDuplicateWarning();
        if (sample is not null)
        {
            duplicateWarningMessage = string.Format(
                CultureInfo.InvariantCulture,
                "Simulation warning: duplicate CAD output FW diff at IC{0}/diff{1}; results may include ambiguous mappings.",
                sample.IcIndex + 1,
                sample.DiffIndex);
            _logger.Warn(
                CultureInfo.InvariantCulture,
                "{0} duplicate-groups={1} sample-cads={2}.",
                duplicateWarningMessage,
                sample.GroupCount,
                string.Join(", ", sample.CadPadIds));
            if (!reporter.Silent)
            {
                reporter.Status?.ReportWarning(duplicateWarningMessage);
            }

            reporter.Progress(duplicateWarningMessage, reportToStatus: false);
        }

        return duplicateWarningMessage;
    }

    private SimulationWorkspaceSession CreateSession(
        SimulationSessionGeneration generation,
        HashSet<int> activeRegularPadIds,
        WorkflowDataSnapshot workflowSnapshot,
        CadPadSet cad)
    {
        var display = _ports.ReadDisplay();
        return new SimulationWorkspaceSession(
            generation.RawGrid,
            generation.Table,
            generation.ExportSettings.Notch.NullValue,
            generation.SourceRevision,
            activeRegularPadIds,
            display.ShowRegular,
            display.HighlightUnmatched,
            display.HighlightFreeform,
            workflowSnapshot.CadOutputFwDiffAssignmentDecisionsByCadId,
            generation.CadOutputFwDiffGrid,
            cad.Pads.ToArray(),
            workflowSnapshot.CadOutputFwDiffIndexByCadId,
            workflowSnapshot.CadIcIndexByCadId,
            generation.ExportSettings.Notch.ComputationMode);
    }

    private void ReportSimulationWorkspaceReady(
        SimulationBuildReporter reporter,
        SimulationSessionGeneration generation,
        string? duplicateWarningMessage)
    {
        var readyMessage = string.Format(
            CultureInfo.InvariantCulture,
            "Simulation ready. Rows={0}.",
            generation.Table.Rows.Count);
        LastAttemptMessage = duplicateWarningMessage is null
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
        LastAttemptMessage = message;
        if (!reporter.Silent)
        {
            reporter.Status?.ReportError("Simulation workspace failed", ex);
            _ports.SetStatus(message);
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
}

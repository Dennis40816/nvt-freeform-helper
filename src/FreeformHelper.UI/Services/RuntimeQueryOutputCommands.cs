using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Runtime query commands that return the Simulation workspace state and export the Step 5 notch table to a file.
/// </summary>
internal sealed class RuntimeQueryOutputCommands
{
    private readonly RuntimeQueryContext _context;

    public RuntimeQueryOutputCommands(RuntimeQueryContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Adds the commands of this group to the registry.</summary>
    /// <param name="registry">The registry that receives the handlers.</param>
    public void Register(RuntimeQueryCommandRegistry registry)
    {
        registry.Add("simulation", QuerySimulationAsync);
        registry.Add("export-notch", QueryExportNotchAsync);
    }

    private async Task<RuntimeQueryResponseEnvelope> QuerySimulationAsync(IReadOnlyDictionary<string, string>? args)
    {
        var hasRegularId = RuntimeQueryArgumentParser.TryGetIntArg(args, "regular-id", 0, int.MaxValue, out var regularPadId, out var regularIdError);
        if (regularIdError is not null)
        {
            return regularIdError;
        }

        var simulation = _context.Shell.Simulation;
        var workspace = simulation.CurrentWorkspace;
        if (workspace is null)
        {
            var buildFailure = await RuntimeQueryCommandOperation.RunAsync(
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

    private async Task<RuntimeQueryResponseEnvelope> QueryExportNotchAsync(IReadOnlyDictionary<string, string>? args)
    {
        if (!RuntimeQueryArgumentParser.TryGetStringArg(args, "format", out var formatText, out var formatError) || formatError is not null)
        {
            return formatError ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--format' is required for query export-notch. Use csv (CSV review)|c-v21|c-v22.");
        }

        if (!RuntimeQueryArgumentParser.TryGetStringArg(args, "path", out var outputPath, out var pathError) || pathError is not null)
        {
            return pathError ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--path' is required for query export-notch.");
        }

        var formatFailure = TryResolveNotchExportFormat(formatText, out var option, out var normalizedFormat);
        if (formatFailure is not null)
        {
            return formatFailure;
        }

        var helper = _context.Shell.FreeformHelper;
        var target = CaptureNotchExportTargetState(outputPath);

        var previousOption = helper.SelectedNotchExportFileTypeOption;
        var previousSavePathPicker = helper.PickSaveNotchPathAsync;
        var previousSelectionDialog = helper.OpenNotchExportSelectionAsync;
        var exportStopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            helper.SelectedNotchExportFileTypeOption = option;
            helper.PickSaveNotchPathAsync = (_, _) => Task.FromResult<string?>(target.ResolvedPath);
            helper.OpenNotchExportSelectionAsync = viewModel =>
            {
                var selectedTable = viewModel.BuildSelectedTable();

                // The format pins the transient selection model to one version. Reset the
                // selection after materializing the table so the shared UI command does not
                // persist that version choice back into project settings.
                viewModel.SelectedVersionOption = viewModel.VersionOptions[0];
                return Task.FromResult<NotchTable?>(selectedTable);
            };
            await helper.ExportNotchCommand.ExecuteAsync(null);
        }
        finally
        {
            exportStopwatch.Stop();
            helper.OpenNotchExportSelectionAsync = previousSelectionDialog;
            helper.PickSaveNotchPathAsync = previousSavePathPicker;
            helper.SelectedNotchExportFileTypeOption = previousOption;
        }

        return BuildNotchExportResult(
            helper,
            option,
            normalizedFormat,
            target,
            exportStopwatch.ElapsedMilliseconds);
    }

    private RuntimeQueryResponseEnvelope? TryResolveNotchExportFormat(
        string formatText,
        out NotchExportFileTypeOption option,
        out string normalizedFormat)
    {
        var requestedFormat = formatText.Trim().ToLowerInvariant();
        var mappedType = requestedFormat switch
        {
            "csv" => FreeformHelperViewModel.NotchExportFileType.Csv,
            "c-v21" => FreeformHelperViewModel.NotchExportFileType.Cv21,
            "c-v22" => FreeformHelperViewModel.NotchExportFileType.Cv22,
            _ => (FreeformHelperViewModel.NotchExportFileType?)null,
        };
        if (!mappedType.HasValue)
        {
            option = default;
            normalizedFormat = requestedFormat;
            return RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: $"Unsupported export format '{formatText}'. Use csv (CSV review)|c-v21|c-v22.");
        }

        var helper = _context.Shell.FreeformHelper;
        option = helper.NotchExportFileTypeOptions.FirstOrDefault(candidate =>
            candidate.Value == mappedType.Value);
        normalizedFormat = mappedType.Value switch
        {
            FreeformHelperViewModel.NotchExportFileType.Csv => "csv",
            FreeformHelperViewModel.NotchExportFileType.Cv21 => "c-v21",
            FreeformHelperViewModel.NotchExportFileType.Cv22 => "c-v22",
            _ => requestedFormat,
        };

        if (string.IsNullOrWhiteSpace(option.Extension))
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "STEP_EXECUTION_FAILED",
                message: $"Export format '{normalizedFormat}' is not available.");
        }

        return null;
    }

    private static NotchExportTargetState CaptureNotchExportTargetState(string outputPath)
    {
        var resolvedPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var fileExistedBefore = File.Exists(resolvedPath);
        var previousWriteUtc = fileExistedBefore
            ? File.GetLastWriteTimeUtc(resolvedPath)
            : DateTime.MinValue;
        var previousFileSize = fileExistedBefore
            ? new FileInfo(resolvedPath).Length
            : -1L;

        return new NotchExportTargetState(resolvedPath, fileExistedBefore, previousWriteUtc, previousFileSize);
    }

    private static RuntimeQueryResponseEnvelope BuildNotchExportResult(
        FreeformHelperViewModel helper,
        NotchExportFileTypeOption option,
        string normalizedFormat,
        NotchExportTargetState target,
        long elapsedMs)
    {
        var (resolvedPath, fileExistedBefore, previousWriteUtc, previousFileSize) = target;
        var summaryText = helper.NotchExportSummary;
        if (!File.Exists(resolvedPath))
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "STEP_EXECUTION_FAILED",
                message: $"Notch export did not produce output file. summary={summaryText}");
        }

        var fileInfo = new FileInfo(resolvedPath);
        var isFileUpdated = !fileExistedBefore ||
                            fileInfo.Length != previousFileSize ||
                            fileInfo.LastWriteTimeUtc > previousWriteUtc;
        if (!isFileUpdated)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "STEP_EXECUTION_FAILED",
                message: $"Notch export did not update output file. summary={summaryText}");
        }

        return RuntimeQueryResponseEnvelope.Success(new
        {
            format = normalizedFormat,
            formatKind = NotchExportFileTypeMetadata.GetExportKindLabel(option),
            path = resolvedPath,
            fileSize = fileInfo.Length,
            fileLastWriteUtc = fileInfo.LastWriteTimeUtc,
            elapsedMs,
            statusText = helper.StatusText,
            summaryText,
            workflow = RuntimeQueryResponseBuilder.BuildWorkflowPayload(helper.GetWorkflowStateSnapshot())
        });
    }

    private static object BuildSimulationRegularPayload(SimulationRegularQuerySnapshot snapshot)
    {
        return new
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

    private static object BuildSimulationWorkspacePayload(
        SimulationHostViewModel simulation,
        SimulationWorkspaceViewModel workspace)
    {
        var cadOutputFwDiffAssignmentDecision = workspace.BuildCadOutputFwDiffAssignmentDecisionSummarySnapshot();

        return new
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
        };
    }

    private readonly record struct NotchExportTargetState(
        string ResolvedPath,
        bool FileExistedBefore,
        DateTime PreviousWriteUtc,
        long PreviousFileSize);
}

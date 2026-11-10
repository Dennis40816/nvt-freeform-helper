using FreeformHelper.UI.Logging;
using Nvt.Core.LogConsole;
using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Runtime query commands that report the app state: status, selection and the in-app terminal.
/// </summary>
internal sealed class RuntimeQueryStatusCommands
{
    private readonly RuntimeQueryContext _context;

    public RuntimeQueryStatusCommands(RuntimeQueryContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Adds the commands of this group to the registry.</summary>
    /// <param name="registry">The registry that receives the handlers.</param>
    public void Register(RuntimeQueryCommandRegistry registry)
    {
        registry.AddSync("status", _ => QueryStatus());
        registry.AddSync("selection", _ => QuerySelection());
        registry.Add("terminal", QueryTerminalAsync);
        registry.Add("terminal-links", QueryTerminalLinksAsync);
    }

    private RuntimeQueryResponseEnvelope QueryStatus()
    {
        var helper = _context.Shell.FreeformHelper;
        var workflow = helper.GetWorkflowStateSnapshot();
        var selectedCadCount = helper.SelectedCadPadIds.Count;
        var selectedRegularCount = helper.SelectedRegularPadIndices.Count;
        var cadOutputFwDiffAssignmentDecision = helper.GetCadOutputFwDiffAssignmentDecisionSummarySnapshot();

        return RuntimeQueryResponseEnvelope.Success(new
        {
            processId = Environment.ProcessId,
            statusText = helper.StatusText,
            cadCount = helper.CadPads.Count,
            regularCount = helper.RegularPads.Count,
            selection = new
            {
                cadCount = selectedCadCount,
                regularCount = selectedRegularCount
            },
            workflow = new
            {
                hasCad = workflow.HasCad,
                hasGrid = workflow.HasGrid,
                hasStep1Result = workflow.HasStep1Result,
                hasStep2Result = workflow.HasStep2Result,
                hasStep3Result = workflow.HasStep3Result,
                hasStep4Result = workflow.HasStep4Result
            },
            notchPreview = new
            {
                isVisible = helper.ShowNotchCanvasPreview,
                showToRegularLabels = helper.ShowNotchToRegularLabels,
                stage = (int)Math.Round((double)helper.NotchPreviewVisualizationStep),
                autoPlayEnabled = helper.NotchPreviewAutoPlayEnabled,
                autoPlayIntervalMs = (double)helper.NotchPreviewAutoPlayIntervalMs
            },
            notchExportState = new
            {
                enableV21 = helper.EnableV21,
                enableV22 = helper.EnableV22,
                fileType = helper.SelectedNotchExportFileTypeOption.Value.ToString(),
                profile = helper.SelectedNotchExportProfileOption.Value.ToString()
            },
            cadOutputFwDiffAssignmentDecision = new
            {
                cadOutputFwDiffAssignmentDecision.DecisionCount,
                cadOutputFwDiffAssignmentDecision.Mode,
                reasonCounts = cadOutputFwDiffAssignmentDecision.ReasonCounts,
                decisionSourceCounts = cadOutputFwDiffAssignmentDecision.DecisionSourceCounts
            },
            cache = RuntimeQueryResponseBuilder.BuildNotchCachePayload(helper, _context.NotchCache.GetMetrics()),
            cadLoadSpinner = RuntimeQueryResponseBuilder.BuildCadLoadSpinnerDebugPayload()
        });
    }

    private RuntimeQueryResponseEnvelope QuerySelection()
    {
        var helper = _context.Shell.FreeformHelper;
        return RuntimeQueryResponseEnvelope.Success(RuntimeQueryResponseBuilder.BuildSelectionPayload(helper));
    }

    private async Task<RuntimeQueryResponseEnvelope> QueryTerminalAsync(IReadOnlyDictionary<string, string>? args)
    {
        var tail = RuntimeQueryLimits.DefaultTerminalTail;
        if (RuntimeQueryArgumentParser.TryGetIntArg(args, "tail", 1, RuntimeQueryLimits.MaxTerminalTail, out var parsedTail, out var tailError))
        {
            tail = parsedTail;
        }
        else if (tailError is not null)
        {
            return tailError;
        }

        var (lines, totalLines) = await AppLogFormatter.GetTailLinesAsync(_context.Shell.ConsoleStore, tail);

        return RuntimeQueryResponseEnvelope.Success(new
        {
            minLevel = LoggingBootstrapper.GetCurrentMinimumLevelName(),
            totalLines,
            returnedLines = lines.Count,
            lines
        });
    }

    private async Task<RuntimeQueryResponseEnvelope> QueryTerminalLinksAsync(IReadOnlyDictionary<string, string>? args)
    {
        var tail = RuntimeQueryLimits.DefaultTerminalTail;
        if (RuntimeQueryArgumentParser.TryGetIntArg(args, "tail", 1, RuntimeQueryLimits.MaxTerminalTail, out var parsedTail, out var tailError))
        {
            tail = parsedTail;
        }
        else if (tailError is not null)
        {
            return tailError;
        }

        var limit = RuntimeQueryLimits.DefaultTerminalLinkLimit;
        if (RuntimeQueryArgumentParser.TryGetIntArg(args, "limit", 1, RuntimeQueryLimits.MaxTerminalLinkLimit, out var parsedLimit, out var limitError))
        {
            limit = parsedLimit;
        }
        else if (limitError is not null)
        {
            return limitError;
        }

        var (lines, totalLines) = await AppLogFormatter.GetTailLinesAsync(_context.Shell.ConsoleStore, tail);

        var startLineNumber = Math.Max(1, totalLines - lines.Count + 1);
        var links = new List<object>();
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var lineText = lines[lineIndex];
            var parsedLinks = ConsoleLinkScanner.Scan(lineText);
            foreach (var link in parsedLinks)
            {
                links.Add(new
                {
                    lineNumber = startLineNumber + lineIndex,
                    startOffset = link.Start,
                    length = link.Length,
                    target = link.Target.Path,
                    isUrl = link.Target.Kind == LinkKind.Url,
                    lineText
                });

                if (links.Count >= limit)
                {
                    break;
                }
            }

            if (links.Count >= limit)
            {
                break;
            }
        }

        return RuntimeQueryResponseEnvelope.Success(new
        {
            minLevel = LoggingBootstrapper.GetCurrentMinimumLevelName(),
            totalLines,
            scannedLines = lines.Count,
            returnedLinks = links.Count,
            limit,
            links
        });
    }
}

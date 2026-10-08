using FreeformHelper.UI.ViewModels;
using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

internal sealed partial class RuntimeQueryUseCase
{
    private async Task<RuntimeQueryResponseEnvelope> QueryLoadProjectAsync(IReadOnlyDictionary<string, string>? args)
    {
        if (!RuntimeQueryArgumentParser.TryGetStringArg(args, "path", out var path, out var error) || error is not null)
        {
            return error ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--path' is required for query load-project.");
        }

        var helper = _shellViewModel.FreeformHelper;
        var result = await helper.LoadProjectFromPathAsync(path);
        if (!result.IsLoaded)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: result.Code,
                message: result.Message);
        }

        var workflow = helper.GetWorkflowStateSnapshot();
        return RuntimeQueryResponseEnvelope.Success(new
        {
            path = result.Path,
            statusText = helper.StatusText,
            cadCount = helper.CadPads.Count,
            regularCount = helper.RegularPads.Count,
            workflow = new
            {
                hasCad = workflow.HasCad,
                hasGrid = workflow.HasGrid,
                hasStep1Result = workflow.HasStep1Result,
                hasStep2Result = workflow.HasStep2Result,
                hasStep3Result = workflow.HasStep3Result,
                hasStep4Result = workflow.HasStep4Result
            },
            timings = new
            {
                result.TotalElapsedMs,
                result.PersistenceElapsedMs,
                result.ApplyStateElapsedMs,
                result.DxfElapsedMs,
                result.RebuildElapsedMs,
                stepReplay = new
                {
                    result.Step1ReplayElapsedMs,
                    result.Step2ReplayElapsedMs,
                    result.Step1Replayed,
                    result.Step2Replayed
                }
            }
        });
    }

    private async Task<RuntimeQueryResponseEnvelope> QueryRunStepAsync(IReadOnlyDictionary<string, string>? args)
    {
        if (!RuntimeQueryArgumentParser.TryGetIntArg(args, "step", 1, 4, out var step, out var stepError) || stepError is not null)
        {
            return stepError ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--step' is required for query run-step.");
        }

        var helper = _shellViewModel.FreeformHelper;
        var stepId = step switch
        {
            1 => WorkflowStepId.Step1Match,
            2 => WorkflowStepId.Step2Freeform,
            3 => WorkflowStepId.Step3NotchPreview,
            4 => WorkflowStepId.Step4IndexDiagnostics,
            _ => WorkflowStepId.Step1Match
        };

        WorkflowStepExecutionResult result;
        try
        {
            result = await helper.RunWorkflowStepAsync(stepId);
        }
        catch (Exception ex)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "STEP_EXECUTION_FAILED",
                message: ex.Message);
        }

        if (!result.IsSupported)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: $"Unsupported step '{step}'.");
        }

        if (!result.IsReady)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "STEP_NOT_READY",
                message: string.IsNullOrWhiteSpace(result.StatusText)
                    ? $"Step {step} did not produce a ready result."
                    : result.StatusText);
        }

        return RuntimeQueryResponseEnvelope.Success(new
        {
            step,
            statusText = result.StatusText,
            workflow = RuntimeQueryResponseBuilder.BuildWorkflowPayload(result.Snapshot),
            selection = RuntimeQueryResponseBuilder.BuildSelectionPayload(helper),
            notchPreview = RuntimeQueryResponseBuilder.BuildNotchPreviewPayload(helper)
        });
    }

    private RuntimeQueryResponseEnvelope QueryClearStep(IReadOnlyDictionary<string, string>? args)
    {
        if (!RuntimeQueryArgumentParser.TryGetIntArg(args, "step", 1, 5, out var step, out var stepError) || stepError is not null)
        {
            return stepError ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--step' is required for query clear-step.");
        }

        var helper = _shellViewModel.FreeformHelper;
        var stepId = step switch
        {
            1 => WorkflowStepId.Step1Match,
            2 => WorkflowStepId.Step2Freeform,
            3 => WorkflowStepId.Step3NotchPreview,
            4 => WorkflowStepId.Step4IndexDiagnostics,
            5 => WorkflowStepId.Step5Export,
            _ => WorkflowStepId.Step1Match
        };
        var result = helper.ClearWorkflowStep(stepId);
        if (!result.IsSupported)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: $"Unsupported step '{step}'.");
        }

        return RuntimeQueryResponseEnvelope.Success(new
        {
            step,
            statusText = result.StatusText,
            workflow = RuntimeQueryResponseBuilder.BuildWorkflowPayload(result.Snapshot),
            notchPreview = RuntimeQueryResponseBuilder.BuildNotchPreviewPayload(helper)
        });
    }

    private RuntimeQueryResponseEnvelope QuerySelectCad(IReadOnlyDictionary<string, string>? args)
    {
        var selectedIds = RuntimeQuerySelectionParser.ParseCadSelectionArgs(args, out var error);
        if (error is not null)
        {
            return error;
        }

        var helper = _shellViewModel.FreeformHelper;
        var visibleCadIds = helper.CadPads.Select(static pad => pad.Id).ToHashSet();
        var effective = selectedIds.Where(visibleCadIds.Contains).Distinct().OrderBy(id => id).ToList();
        var missing = selectedIds.Where(id => !visibleCadIds.Contains(id)).Distinct().OrderBy(id => id).ToList();

        if (effective.Count == 0)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "PAD_NOT_FOUND",
                message: missing.Count > 0
                    ? $"CAD pads not visible: {string.Join(", ", missing)}"
                    : "No CAD output pads selected.");
        }

        helper.ApplyCanvasSelection(effective, Array.Empty<int>());
        return RuntimeQueryResponseEnvelope.Success(new
        {
            selectedCadIds = effective,
            missingCadIds = missing,
            selection = RuntimeQueryResponseBuilder.BuildSelectionPayload(helper),
            statusText = helper.StatusText
        });
    }

    private RuntimeQueryResponseEnvelope QuerySelectRegular(IReadOnlyDictionary<string, string>? args)
    {
        var helper = _shellViewModel.FreeformHelper;
        var regularById = helper.RegularPads.ToDictionary(static pad => pad.RegularPadId, static pad => pad.Index);
        var regularIndices = RuntimeQuerySelectionParser.ParseRegularSelectionArgs(
            args,
            regularById,
            out var error,
            out var missingRegularIds,
            out var missingRegularIndices);
        if (error is not null)
        {
            return error;
        }

        if (regularIndices.Count == 0)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "PAD_NOT_FOUND",
                message: "No visible regular pads selected.");
        }

        helper.ApplyCanvasSelection(Array.Empty<int>(), regularIndices);
        return RuntimeQueryResponseEnvelope.Success(new
        {
            selectedRegularIndices = regularIndices,
            selectedRegularPadIds = helper.RegularPads
                .Where(pad => regularIndices.Contains(pad.Index))
                .Select(pad => pad.RegularPadId)
                .Distinct()
                .OrderBy(id => id)
                .ToList(),
            missingRegularPadIds = missingRegularIds,
            missingRegularIndices,
            selection = RuntimeQueryResponseBuilder.BuildSelectionPayload(helper),
            statusText = helper.StatusText
        });
    }

    private RuntimeQueryResponseEnvelope QueryClearSelection()
    {
        var helper = _shellViewModel.FreeformHelper;
        helper.ClearSelectionCommand.Execute(null);
        return RuntimeQueryResponseEnvelope.Success(new
        {
            selection = RuntimeQueryResponseBuilder.BuildSelectionPayload(helper),
            statusText = helper.StatusText
        });
    }
}

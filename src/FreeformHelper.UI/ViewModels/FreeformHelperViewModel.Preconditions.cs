using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Shared precondition helpers for long-running workflows.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Validates that both regular grid and CAD set are available for an operation.
    /// </summary>
    /// <param name="cadFactory">Factory that returns the CAD set for the operation context.</param>
    /// <param name="missingStatus">Status text to show when prerequisites are missing.</param>
    /// <param name="cad">Resolved CAD set when available.</param>
    /// <param name="grid">Resolved regular grid when available.</param>
    /// <returns><c>true</c> when both CAD and grid are ready; otherwise <c>false</c>.</returns>
    private bool TryGetOperationCadAndGrid(
        Func<CadPadSet?> cadFactory,
        string missingStatus,
        Action? onCadUnavailable,
        out CadPadSet cad,
        out RegularGrid grid)
    {
        ArgumentNullException.ThrowIfNull(cadFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(missingStatus);

        cad = null!;
        grid = null!;

        if (_grid is null)
        {
            SetStatus(missingStatus);
            return false;
        }

        var resolvedCad = cadFactory();
        if (resolvedCad is null || resolvedCad.Pads.Count == 0)
        {
            if (onCadUnavailable is not null)
            {
                onCadUnavailable();
            }
            else
            {
                SetStatus(missingStatus);
            }
            return false;
        }

        cad = resolvedCad;
        grid = _grid;
        return true;
    }

    private bool TryGetOperationCadAndGrid(
        Func<CadPadSet?> cadFactory,
        string missingStatus,
        out CadPadSet cad,
        out RegularGrid grid)
    {
        return TryGetOperationCadAndGrid(cadFactory, missingStatus, onCadUnavailable: null, out cad, out grid);
    }

    /// <summary>
    /// Validates that CAD data is available for an operation.
    /// </summary>
    /// <param name="cadFactory">Factory that returns the CAD set for the operation context.</param>
    /// <param name="missingStatus">Status text to show when CAD is unavailable.</param>
    /// <param name="onCadUnavailable">Optional custom side effect when CAD is unavailable.</param>
    /// <param name="cad">Resolved CAD set when available.</param>
    /// <returns><c>true</c> when CAD is ready; otherwise <c>false</c>.</returns>
    private bool TryGetLoadedCad(
        Func<CadPadSet?> cadFactory,
        string missingStatus,
        Action? onCadUnavailable,
        out CadPadSet cad)
    {
        ArgumentNullException.ThrowIfNull(cadFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(missingStatus);

        cad = null!;
        var resolvedCad = cadFactory();
        if (resolvedCad is null)
        {
            if (onCadUnavailable is not null)
            {
                onCadUnavailable();
            }
            else
            {
                SetStatus(missingStatus);
            }

            return false;
        }

        cad = resolvedCad;
        return true;
    }

    private bool TryGetLoadedCad(
        Func<CadPadSet?> cadFactory,
        string missingStatus,
        out CadPadSet cad)
    {
        return TryGetLoadedCad(cadFactory, missingStatus, onCadUnavailable: null, out cad);
    }

    private bool TryEnsureWorkflowStep(WorkflowStepId step, string? overrideStatus = null, bool suppressStatus = false)
    {
        var gate = WorkflowStepGateService.Validate(step, BuildWorkflowStateSnapshot());
        if (gate.IsAllowed)
        {
            return true;
        }

        if (!suppressStatus)
        {
            var message = string.IsNullOrWhiteSpace(overrideStatus) ? gate.Message : overrideStatus;
            if (!string.IsNullOrWhiteSpace(message))
            {
                SetStatus(message!);
            }
        }

        return false;
    }

    private WorkflowStateSnapshot BuildWorkflowStateSnapshot()
    {
        var hasCad = _cad is not null && _cad.Pads.Count > 0;
        var hasGrid = _grid is not null && _grid.Pads.Count > 0;
        var hasStep1 = _latestPadMatchResult.CadToRegular.Count > 0;
        var hasStep2 = _grid is not null && _grid.Pads.Any(pad => pad.Freeform != FreeformType.None);
        var hasStep3 = NotchCanvasPreviewItems.Count > 0;
        var hasStep4 = !string.Equals(DxfRegularMappingSummary, DiagnosticsNotRunSummary, StringComparison.Ordinal);

        return new WorkflowStateSnapshot(
            HasCad: hasCad,
            HasGrid: hasGrid,
            HasStep1Result: hasStep1,
            HasStep2Result: hasStep2,
            HasStep3Result: hasStep3,
            HasStep4Result: hasStep4);
    }

    public WorkflowStateSnapshot GetWorkflowStateSnapshot()
    {
        return BuildWorkflowStateSnapshot();
    }
}

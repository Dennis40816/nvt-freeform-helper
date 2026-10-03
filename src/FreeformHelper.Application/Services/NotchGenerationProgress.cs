namespace FreeformHelper.Application.Services;

/// <summary>
/// Progress payload for notch-table generation.
/// </summary>
/// <param name="ProcessedCount">Processed candidate count.</param>
/// <param name="TotalCount">Total candidate count.</param>
/// <param name="GeneratedRowCount">Generated row count so far.</param>
/// <param name="CurrentCadPadId">Current CAD pad id if available.</param>
/// <param name="CurrentRegularPadId">Current regular pad id if available.</param>
/// <param name="Phase">Current generation phase label.</param>
/// <param name="PhaseStep">Current phase step (1-based).</param>
/// <param name="PhaseStepCount">Total phase steps.</param>
public readonly record struct NotchGenerationProgress(
    int ProcessedCount,
    int TotalCount,
    int GeneratedRowCount,
    int? CurrentCadPadId,
    int? CurrentRegularPadId,
    string? Phase = null,
    int PhaseStep = 0,
    int PhaseStepCount = 0);

using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Generates a <see cref="NotchTable"/> by applying version-specific notch algorithms
/// to freeform pads.
/// </summary>
public sealed partial class NotchTableGenerator
{
    private const int V22ContinuationFlag = 1;

    private const int CadAllocationMemoMaxEntries = 8192;

    private readonly Dictionary<int, CadAllocationMemoEntry> _cadAllocationMemoByCadId = new();
    private RegularGrid? _cadAllocationMemoGrid;

    /// <summary>
    /// Holds the output-neutral CadAllocation resolution owned by this generator.
    /// Its candidate representation is intentionally opaque so callers can only
    /// reuse it through <see cref="ProjectCadAllocationResolvedBatch"/>.
    /// </summary>
    public abstract class CadAllocationResolvedBatch
    {
        private protected CadAllocationResolvedBatch(CadAllocationSparseResult? selectedSparseResult)
        {
            SelectedSparseResult = selectedSparseResult;
        }

        public CadAllocationSparseResult? SelectedSparseResult { get; }
    }

    public sealed record CadAllocationSparseResultRequest(
        int CadPadId, int AnchorIcIndex, int AnchorDiffIndex,
        NotchV22ResolvedResult? ReusableResult = null);

    public sealed record CadAllocationSparseResult(
        int CadPadId, int AnchorIcIndex, int AnchorDiffIndex,
        NotchV22ResolvedResult ResolvedResult);

    /// <summary>
    /// Generates a <see cref="NotchTable"/> based on the provided CAD data, regular grid, and project settings.
    /// </summary>
    /// <param name="cad">The set of CAD pads.</param>
    /// <param name="grid">The regular grid with pad matching information.</param>
    /// <param name="settings">The project settings, including which notch algorithm versions are enabled.</param>
    /// <returns>A new <see cref="NotchTable"/> containing the generated rows.</returns>
    public NotchTable Generate(
        CadPadSet cad,
        RegularGrid grid,
        ProjectSettings settings,
        IProgress<NotchGenerationProgress>? progress = null,
        IReadOnlySet<int>? activeRegularPadIds = null,
        IReadOnlyDictionary<int, int>? cadOutputFwDiffIndexByCadId = null)
    {
        settings.ValidateOrThrow();
        var enabled = settings.Notch.EnabledVersions;
        if (enabled.Count == 0)
        {
            progress?.Report(new NotchGenerationProgress(0, 0, 0, null, null));
            // If no notch versions are enabled, return an empty table.
            return new NotchTable(Array.Empty<NotchTableRow>());
        }

        return settings.Notch.ComputationMode switch
        {
            NotchComputationMode.CadAllocation => GenerateCadAllocationResolvedBatchCore(
                cad,
                grid,
                settings,
                progress,
                activeRegularPadIds,
                cadOutputFwDiffIndexByCadId).Table,
            _ => GenerateLegacyRegularAnchor(
                cad,
                grid,
                settings.Notch,
                progress),
        };
    }

    /// <summary>
    /// Evaluates whether a single CAD pad is expected to generate notch row(s)
    /// under current settings, without building the full table.
    /// </summary>
    public NotchCadRowEligibility EvaluateCadRowEligibility(
        CadPad cadPad,
        RegularGrid grid,
        ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(cadPad);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateOrThrow();
        var enabled = settings.Notch.EnabledVersions
            .OrderBy(v => (int)v)
            .ToList();
        if (enabled.Count == 0)
        {
            return new NotchCadRowEligibility(
                CadPadId: cadPad.Id,
                EligibleVersions: Array.Empty<NotchAlgorithmVersion>(),
                EstimatedRowCount: 0,
                AnchorRegularPadId: null,
                Reason: "No notch version enabled.");
        }

        return settings.Notch.ComputationMode switch
        {
            NotchComputationMode.CadAllocation => EvaluateCadAllocationEligibility(cadPad, grid, settings, enabled),
            _ => EvaluateLegacyEligibility(cadPad, grid, settings, enabled)
        };
    }

}

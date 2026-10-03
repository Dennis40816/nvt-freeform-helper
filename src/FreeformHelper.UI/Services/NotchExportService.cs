using System.Diagnostics.CodeAnalysis;
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Provides a service layer for generating and exporting notch tables.
/// This service orchestrates the use of <see cref="NotchTableGenerator"/> and <see cref="NotchTableExporter"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Instance service API is retained for workflow composition stability.")]
public sealed class NotchExportService
{
    // Private instances of the underlying application services.
    private readonly NotchTableGenerator _notchGenerator = new();
    private readonly NotchTableExporter _notchExporter = new();

    /// <summary>
    /// Generates a <see cref="NotchTable"/> based on the provided CAD pads, regular grid, and project settings.
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
        return _notchGenerator.Generate(
            cad,
            grid,
            settings,
            progress,
            activeRegularPadIds,
            cadOutputFwDiffIndexByCadId);
    }

    /// <summary>
    /// Generates the version-neutral CAD-allocation batch and its first requested projection.
    /// </summary>
    public (NotchTableGenerator.CadAllocationResolvedBatch Batch, NotchTable Table)
        GenerateCadAllocationResolvedBatch(
        CadPadSet cad,
        RegularGrid grid,
        ProjectSettings settings,
        IProgress<NotchGenerationProgress>? progress = null,
        IReadOnlySet<int>? activeRegularPadIds = null,
        IReadOnlyDictionary<int, int>? cadOutputFwDiffIndexByCadId = null)
    {
        return _notchGenerator.GenerateCadAllocationResolvedBatch(
            cad,
            grid,
            settings,
            progress,
            activeRegularPadIds,
            cadOutputFwDiffIndexByCadId);
    }

    /// <summary>
    /// Resolves a version-neutral CAD-allocation batch without projecting final rows.
    /// </summary>
    public NotchTableGenerator.CadAllocationResolvedBatch ResolveCadAllocationBatch(
        CadPadSet cad,
        RegularGrid grid,
        ProjectSettings settings,
        IProgress<NotchGenerationProgress>? progress = null,
        IReadOnlySet<int>? activeRegularPadIds = null,
        IReadOnlyDictionary<int, int>? cadOutputFwDiffIndexByCadId = null,
        NotchTableGenerator.CadAllocationSparseResultRequest? selectedSparseResultRequest = null)
    {
        return _notchGenerator.ResolveCadAllocationBatch(
            cad,
            grid,
            settings,
            progress,
            activeRegularPadIds,
            cadOutputFwDiffIndexByCadId,
            selectedSparseResultRequest);
    }

    /// <summary>
    /// Projects a previously generated CAD-allocation batch for the current output request.
    /// </summary>
    public NotchTable ProjectCadAllocationResolvedBatch(
        NotchTableGenerator.CadAllocationResolvedBatch batch,
        ProjectSettings settings,
        IProgress<NotchGenerationProgress>? progress = null,
        bool includeResolutionTimings = false)
    {
        return NotchTableGenerator.ProjectCadAllocationResolvedBatch(
            batch,
            settings,
            progress,
            includeResolutionTimings);
    }

    /// <summary>
    /// Evaluates whether a CAD pad will generate notch row(s) for current settings.
    /// </summary>
    public NotchCadRowEligibility EvaluateCadRowEligibility(
        CadPad cadPad,
        RegularGrid grid,
        ProjectSettings settings)
    {
        return _notchGenerator.EvaluateCadRowEligibility(cadPad, grid, settings);
    }

    /// <summary>
    /// Exports a given <see cref="NotchTable"/> to a CSV formatted string.
    /// </summary>
    /// <param name="table">The notch table to export.</param>
    /// <returns>A string containing the notch table data in CSV format.</returns>
    public string ExportCsv(NotchTable table) => NotchTableExporter.ExportAsCsv(table);

    /// <summary>
    /// Exports a given <see cref="NotchTable"/> as C-style initializer rows.
    /// </summary>
    public string ExportCInitializer(
        NotchTable table,
        CadPadSet cad,
        RegularGrid grid,
        ProjectSettings settings,
        NotchExportProfile profile = NotchExportProfile.Release,
        IReadOnlySet<int>? activeRegularPadIds = null)
        => NotchTableExporter.ExportAsCInitializer(table, cad, grid, settings, profile, activeRegularPadIds);
}

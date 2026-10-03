using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

public sealed class NotchApplySimulationReviewUseCase
{
    // CSV frame rows are user-facing top-down data; map them to the AA visual top row.
    public static DiffFrameGridRowOrigin CsvProjectionRowOrigin => DiffFrameGridRowOrigin.TopRowIsRegularLastRow;
    private readonly DiffFrameGridRowOrigin _projectionRowOrigin = CsvProjectionRowOrigin;

    public NotchApplySimulationImportedDataset ImportSources(
        IReadOnlyList<string> sourcePaths,
        RegularGrid grid)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentNullException.ThrowIfNull(grid);

        if (sourcePaths.Count == 0)
        {
            return NotchApplySimulationImportedDataset.Empty;
        }

        var files = new List<NotchApplySimulationImportedFile>(sourcePaths.Count);
        var frames = new List<NotchApplySimulationImportedFrame>();
        var diagnostics = new List<string>();
        var globalFrameIndex = 0;

        for (var sourceIndex = 0; sourceIndex < sourcePaths.Count; sourceIndex++)
        {
            var sourcePath = sourcePaths[sourceIndex];
            var source = DiffFrameCsvImporter.Import(sourcePath);
            var compatibleFrameCount = 0;

            diagnostics.AddRange(source.Diagnostics);

            for (var frameIndex = 0; frameIndex < source.Frames.Count; frameIndex++)
            {
                var frame = source.Frames[frameIndex];
                var projection = DiffFrameGridProjectionService.ProjectFrame(frame, grid, _projectionRowOrigin);
                if (projection.IsCompatible)
                {
                    compatibleFrameCount++;
                }
                else if (!string.IsNullOrWhiteSpace(projection.Diagnostic))
                {
                    diagnostics.Add(
                        $"{Path.GetFileName(sourcePath)} frame {BuildFrameIdentityText(frame, frameIndex)}: {projection.Diagnostic}");
                }

                frames.Add(new NotchApplySimulationImportedFrame(
                    globalFrameIndex++,
                    sourceIndex,
                    frameIndex,
                    Path.GetFileName(sourcePath),
                    frame,
                    projection));
            }

            files.Add(new NotchApplySimulationImportedFile(
                sourcePath,
                Path.GetFileName(sourcePath),
                source.Frames.Count,
                compatibleFrameCount,
                source.DeclaredCols,
                source.DeclaredRows,
                source.Frames.Count > 0 ? source.Frames[0].ColCount : null,
                source.Frames.Count > 0 ? source.Frames[0].RowCount : null,
                source.Diagnostics));
        }

        return new NotchApplySimulationImportedDataset(files, frames, diagnostics);
    }

    public static NotchApplySimulationReviewSnapshot BuildSnapshot(
        NotchApplySimulationImportedDataset dataset,
        RegularGrid grid,
        IReadOnlySet<int> activeRegularPadIds,
        NotchTable table,
        NotchAlgorithmVersion version,
        int nullDiffValue,
        NotchApplySimulationAggregationMode aggregationMode,
        int selectedFrameIndex,
        NotchComputationMode computationMode = NotchComputationMode.CadAllocation)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(activeRegularPadIds);
        ArgumentNullException.ThrowIfNull(table);

        var filteredRows = table.Rows
            .Where(row => row.Version == version)
            .ToList();
        var filteredTable = new NotchTable(filteredRows);

        var simulation = NotchApplySimulationService.Simulate(
            new NotchApplySimulationRequest(
                grid,
                activeRegularPadIds,
                dataset.Frames.Select(static frame => frame.Projection).ToList(),
                filteredTable,
                version,
                nullDiffValue,
                aggregationMode,
                selectedFrameIndex,
                computationMode));

        var diagnostics = new List<string>(dataset.Diagnostics);
        diagnostics.AddRange(simulation.Diagnostics);

        return new NotchApplySimulationReviewSnapshot(
            dataset,
            filteredTable,
            simulation,
            diagnostics);
    }

    private static string BuildFrameIdentityText(DiffFrameCsvFrame frame, int frameIndex)
    {
        if (frame.FrameIndex.HasValue)
        {
            return frame.FrameIndex.Value.ToString(CultureInfo.InvariantCulture);
        }

        return (frameIndex + 1).ToString(CultureInfo.InvariantCulture);
    }
}

public sealed record NotchApplySimulationImportedFile(
    string SourcePath,
    string FileName,
    int FrameCount,
    int CompatibleFrameCount,
    int? DeclaredCols,
    int? DeclaredRows,
    int? FirstFrameCols,
    int? FirstFrameRows,
    IReadOnlyList<string> Diagnostics)
{
    public string SummaryText => $"{CompatibleFrameCount}/{FrameCount} compatible";

    public string ShapeText
    {
        get
        {
            var declared = DeclaredCols.HasValue && DeclaredRows.HasValue
                ? $"Xch/Ych {DeclaredCols.Value} x {DeclaredRows.Value}"
                : "Xch/Ych not declared";
            var detected = FirstFrameCols.HasValue && FirstFrameRows.HasValue
                ? $"frame {FirstFrameCols.Value} x {FirstFrameRows.Value}"
                : "frame shape unknown";
            return $"{declared} · {detected}";
        }
    }
}

public sealed record NotchApplySimulationImportedFrame(
    int GlobalIndex,
    int SourceIndex,
    int SourceFrameIndex,
    string SourceFileName,
    DiffFrameCsvFrame Frame,
    DiffFrameGridProjectionResult Projection)
{
    public string DisplayText
    {
        get
        {
            var frameLabel = Frame.FrameIndex.HasValue
                ? $"Frame {Frame.FrameIndex.Value}"
                : $"Frame {SourceFrameIndex + 1}";
            if (!string.IsNullOrWhiteSpace(Frame.TimestampText))
            {
                return $"{SourceFileName} · {frameLabel} · {Frame.TimestampText}";
            }

            return $"{SourceFileName} · {frameLabel}";
        }
    }
}

public sealed record NotchApplySimulationImportedDataset(
    IReadOnlyList<NotchApplySimulationImportedFile> Files,
    IReadOnlyList<NotchApplySimulationImportedFrame> Frames,
    IReadOnlyList<string> Diagnostics)
{
    public static NotchApplySimulationImportedDataset Empty { get; } = new(
        Array.Empty<NotchApplySimulationImportedFile>(),
        Array.Empty<NotchApplySimulationImportedFrame>(),
        Array.Empty<string>());

    public int CompatibleFrameCount => Frames.Count(static frame => frame.Projection.IsCompatible);
}

public sealed record NotchApplySimulationReviewSnapshot(
    NotchApplySimulationImportedDataset Dataset,
    NotchTable FilteredTable,
    NotchApplySimulationResult Result,
    IReadOnlyList<string> Diagnostics);

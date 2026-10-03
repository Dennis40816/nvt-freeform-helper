using System.Text;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public enum RegularVisibilityMaskStatus
{
    NotFound = 0,
    Loaded = 1,
    Incompatible = 2,
    Failed = 3,
}

public sealed record RegularVisibilityMaskResult(
    RegularVisibilityMaskStatus Status,
    string? SourcePath,
    int FrameCount,
    IReadOnlySet<int> ActiveRegularPadIds,
    IReadOnlySet<int> InactiveRegularPadIds,
    string? Diagnostic)
{
    public bool HasLoadedMask => Status == RegularVisibilityMaskStatus.Loaded;

    public int ActiveRegularCount => ActiveRegularPadIds.Count;

    public int InactiveRegularCount => InactiveRegularPadIds.Count;

    public static RegularVisibilityMaskResult NotFound(string? sourcePath = null) =>
        new(
            RegularVisibilityMaskStatus.NotFound,
            sourcePath,
            0,
            new HashSet<int>(),
            new HashSet<int>(),
            null);

    public static RegularVisibilityMaskResult Incompatible(string sourcePath, int frameCount, string diagnostic) =>
        new(
            RegularVisibilityMaskStatus.Incompatible,
            sourcePath,
            frameCount,
            new HashSet<int>(),
            new HashSet<int>(),
            diagnostic);

    public static RegularVisibilityMaskResult Failed(string sourcePath, string diagnostic) =>
        new(
            RegularVisibilityMaskStatus.Failed,
            sourcePath,
            0,
            new HashSet<int>(),
            new HashSet<int>(),
            diagnostic);
}

public static class RegularVisibilityMaskService
{
    private const double NonZeroEpsilon = 1e-9;

    public static RegularVisibilityMaskResult Load(
        string? sourcePath,
        RegularGrid grid,
        DiffFrameGridRowOrigin rowOrigin = DiffFrameGridRowOrigin.TopRowIsRegularLastRow)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return RegularVisibilityMaskResult.NotFound(sourcePath);
        }

        try
        {
            var file = DiffFrameCsvImporter.Import(sourcePath);
            return ProjectCsvFile(file, sourcePath, grid, rowOrigin);
        }
        catch (Exception ex)
        {
            return RegularVisibilityMaskResult.Failed(sourcePath, ex.Message);
        }
    }

    public static RegularVisibilityMaskResult LoadEmbedded(
        byte[]? content,
        string? sourceName,
        RegularGrid grid,
        DiffFrameGridRowOrigin rowOrigin = DiffFrameGridRowOrigin.TopRowIsRegularLastRow)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (content is null || content.Length == 0)
        {
            return RegularVisibilityMaskResult.NotFound(sourceName);
        }

        var resolvedSourceName = string.IsNullOrWhiteSpace(sourceName)
            ? "embedded SeeRegular.csv"
            : sourceName.Trim();

        try
        {
            var text = DecodeText(content);
            var file = DiffFrameCsvImporter.ImportContent(resolvedSourceName, text);
            return ProjectCsvFile(file, resolvedSourceName, grid, rowOrigin);
        }
        catch (Exception ex)
        {
            return RegularVisibilityMaskResult.Failed(resolvedSourceName, ex.Message);
        }
    }

    private static RegularVisibilityMaskResult ProjectCsvFile(
        DiffFrameCsvSourceFile file,
        string sourcePath,
        RegularGrid grid,
        DiffFrameGridRowOrigin rowOrigin)
    {
        if (file.Frames.Count == 0)
        {
            return RegularVisibilityMaskResult.Failed(sourcePath, "CSV does not contain any frames.");
        }

        var activeRegularPadIds = new HashSet<int>();
        foreach (var frame in file.Frames)
        {
            var projection = DiffFrameGridProjectionService.ProjectFrame(frame, grid, rowOrigin);
            if (!projection.IsCompatible)
            {
                return RegularVisibilityMaskResult.Incompatible(
                    sourcePath,
                    file.Frames.Count,
                    projection.Diagnostic ?? "CSV frame dimensions are incompatible with the current regular grid.");
            }

            foreach (var cell in projection.Cells)
            {
                if (Math.Abs(cell.Value) > NonZeroEpsilon)
                {
                    activeRegularPadIds.Add(cell.RegularPadId);
                }
            }
        }

        var inactiveRegularPadIds = new HashSet<int>(grid.Pads.Count);
        foreach (var pad in grid.Pads)
        {
            if (!activeRegularPadIds.Contains(pad.RegularPadId))
            {
                inactiveRegularPadIds.Add(pad.RegularPadId);
            }
        }

        return new RegularVisibilityMaskResult(
            RegularVisibilityMaskStatus.Loaded,
            sourcePath,
            file.Frames.Count,
            activeRegularPadIds,
            inactiveRegularPadIds,
            file.Diagnostics.Count > 0 ? string.Join(" | ", file.Diagnostics) : null);
    }

    private static string DecodeText(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}

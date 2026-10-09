using System.Diagnostics.CodeAnalysis;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Single entry point for project file persistence (save/load).
/// Handles disk I/O and schema-compatible round-tripping; UI is responsible for status updates and applying loaded state.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Instance service API is retained for workflow composition stability.")]
public sealed class ProjectPersistenceUseCase
{
    private readonly JsonProjectStore _store;
    private readonly PadOverrideService _padOverrideService;

    public ProjectPersistenceUseCase(JsonProjectStore store, PadOverrideService padOverrideService)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _padOverrideService = padOverrideService ?? throw new ArgumentNullException(nameof(padOverrideService));
    }

    public async Task<ProjectSaveResult> SaveAsync(ProjectSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.ApplyUiToSettings(request.Project.Settings);
        PadOverrideService.Capture(request.Project, request.Grid, request.CadPadCustomValues);
        request.Project.UiSnapshot = request.BuildUiSnapshot();

        var path = await request.PickSaveProjectPathAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return ProjectSaveResult.Cancelled();
        }

        string? embedWarning = null;
        if (request.HasCadLoaded && request.ConfirmEmbedDxfAsync is not null)
        {
            var embed = await request.ConfirmEmbedDxfAsync();
            if (embed)
            {
                embedWarning = CombineWarnings(
                    TryEmbedDxf(request.Project),
                    TryEmbedRegularVisibilityMask(request.Project));
            }
            else
            {
                request.Project.EmbeddedDxf = null;
                request.Project.EmbeddedDxfName = null;
                request.Project.EmbeddedRegularVisibilityMask = null;
                request.Project.EmbeddedRegularVisibilityMaskName = null;
            }
        }

        await _store.SaveAsync(path, request.Project, CancellationToken.None);
        return ProjectSaveResult.Saved(path, embedWarning);
    }

    public async Task<ProjectLoadResult> LoadAsync(ProjectLoadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.CanProceedWithPendingEdits())
        {
            return ProjectLoadResult.Cancelled();
        }

        var path = await request.PickLoadProjectPathAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return ProjectLoadResult.Cancelled();
        }

        var file = JsonProjectStore.Load(path);
        return ProjectLoadResult.Loaded(path, file);
    }

    private static string? TryEmbedDxf(ProjectFile file)
    {
        if (!string.IsNullOrWhiteSpace(file.LastDxfPath) && File.Exists(file.LastDxfPath))
        {
            file.EmbeddedDxf = File.ReadAllBytes(file.LastDxfPath);
            file.EmbeddedDxfName = Path.GetFileName(file.LastDxfPath);
            return null;
        }

        if (file.EmbeddedDxf is not null)
        {
            file.EmbeddedDxfName ??= "embedded.dxf";
            return null;
        }

        return "Embed DXF skipped: source file not available.";
    }

    private static string? TryEmbedRegularVisibilityMask(ProjectFile file)
    {
        var sourcePath = file.UiSnapshot.Import.RegularVisibilityMaskSourcePath;
        if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
        {
            file.EmbeddedRegularVisibilityMask = File.ReadAllBytes(sourcePath);
            file.EmbeddedRegularVisibilityMaskName = Path.GetFileName(sourcePath);
            return null;
        }

        if (file.EmbeddedRegularVisibilityMask is not null)
        {
            file.EmbeddedRegularVisibilityMaskName ??= "SeeRegular.csv";
            return null;
        }

        return string.IsNullOrWhiteSpace(sourcePath)
            ? null
            : "Embed SeeRegular mask skipped: source file not available.";
    }

    private static string? CombineWarnings(params string?[] warnings)
    {
        var activeWarnings = warnings
            .Where(static warning => !string.IsNullOrWhiteSpace(warning))
            .ToArray();
        return activeWarnings.Length == 0
            ? null
            : string.Join(" ", activeWarnings);
    }
}

public sealed record ProjectSaveRequest(
    ProjectFile Project,
    RegularGrid? Grid,
    IReadOnlyDictionary<int, double> CadPadCustomValues,
    Func<Task<string?>> PickSaveProjectPathAsync,
    Func<Task<bool>>? ConfirmEmbedDxfAsync,
    Func<ProjectUiSnapshot> BuildUiSnapshot,
    Action<ProjectSettings> ApplyUiToSettings,
    bool HasCadLoaded);

public sealed record ProjectSaveResult(bool IsSaved, bool IsCancelled, string? Path, string? EmbedWarning)
{
    public static ProjectSaveResult Cancelled() => new(false, true, null, null);
    public static ProjectSaveResult Saved(string path, string? embedWarning) => new(true, false, path, embedWarning);
}

public sealed record ProjectLoadRequest(
    Func<Task<string?>> PickLoadProjectPathAsync,
    Func<bool> CanProceedWithPendingEdits);

public sealed record ProjectLoadResult(bool IsLoaded, bool IsCancelled, string? Path, ProjectFile? Project)
{
    public static ProjectLoadResult Cancelled() => new(false, true, null, null);
    public static ProjectLoadResult Loaded(string path, ProjectFile project) => new(true, false, path, project);
}

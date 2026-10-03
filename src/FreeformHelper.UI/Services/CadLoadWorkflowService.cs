using System.Diagnostics;
using System.Globalization;
using FreeformHelper.Infrastructure.Dxf;
using NLog;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Orchestrates the Open-DXF workflow while keeping ViewModel as the single entry.
/// </summary>
public sealed class CadLoadWorkflowService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly CadLoadUseCase _cadLoadUseCase;

    public CadLoadWorkflowService(CadLoadUseCase cadLoadUseCase)
    {
        _cadLoadUseCase = cadLoadUseCase ?? throw new ArgumentNullException(nameof(cadLoadUseCase));
    }

    public async Task OpenDxfAsync(OpenDxfWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        await request.RunUiOperationAsync(
            "Open DXF",
            async () =>
            {
                var totalSw = Stopwatch.StartNew();
                request.SetStatus("Importing DXF...");
                Logger.Info(CultureInfo.InvariantCulture, "Importing DXF.");

                var options = request.BuildDxfOptions();
                var importElapsedMs = 0L;
                var layerCatalogElapsedMs = 0L;
                var applyElapsedMs = 0L;
                var rebuildElapsedMs = 0L;

                await request.RunWithCadLoadCanvasOverlayAsync(async () =>
                {
                    var importSw = Stopwatch.StartNew();
                    var outcome = await Task.Run(() => _cadLoadUseCase.TryLoadFromPath(request.Path, options));
                    importSw.Stop();
                    importElapsedMs = importSw.ElapsedMilliseconds;
                    if (outcome is null)
                    {
                        request.SetStatus("DXF import failed: file not found.");
                        return;
                    }

                    var layerCatalogSw = Stopwatch.StartNew();
                    await request.LoadLayerCatalogFromPathAsync(request.Path);
                    layerCatalogSw.Stop();
                    layerCatalogElapsedMs = layerCatalogSw.ElapsedMilliseconds;

                    var applySw = Stopwatch.StartNew();
                    request.ResetHiddenCadPads();
                    await request.ApplyCadLoadOutcomeAsync(outcome);
                    request.SetProjectLastDxfPath(request.Path);
                    request.ClearLastSavedPath();
                    request.MarkUnsaved();
                    applySw.Stop();
                    applyElapsedMs = applySw.ElapsedMilliseconds;

                    request.SetStatus(request.BuildStatus(outcome));
                    request.LogCadLoadOutcome(outcome);

                    var rebuildSw = Stopwatch.StartNew();
                    await request.TriggerGridRebuildAsync();
                    rebuildSw.Stop();
                    rebuildElapsedMs = rebuildSw.ElapsedMilliseconds;
                });

                totalSw.Stop();

                Logger.Info(CultureInfo.InvariantCulture, "Open DXF timing: import={0} ms, layerCatalog={1} ms, apply={2} ms, rebuild={3} ms, total={4} ms, pads={5}, includeBlock={6}, onlyClosed={7}.",
                    importElapsedMs,
                    layerCatalogElapsedMs,
                    applyElapsedMs,
                    rebuildElapsedMs,
                    totalSw.ElapsedMilliseconds,
                    request.GetCadPadCountForLog(),
                    options.IncludeBlockPolylines ? "Y" : "N",
                    options.OnlyClosedPolylines ? "Y" : "N");
            },
            request.OnError);
    }
}

public sealed class OpenDxfWorkflowRequest
{
    public required string Path { get; init; }
    public required Func<DxfImportOptions> BuildDxfOptions { get; init; }
    public required Action<string> SetStatus { get; init; }
    public required Func<string, Task> LoadLayerCatalogFromPathAsync { get; init; }
    public required Action ResetHiddenCadPads { get; init; }
    public required Func<CadLoadOutcome, Task> ApplyCadLoadOutcomeAsync { get; init; }
    public required Action<string> SetProjectLastDxfPath { get; init; }
    public required Action ClearLastSavedPath { get; init; }
    public required Action MarkUnsaved { get; init; }
    public required Func<CadLoadOutcome, string> BuildStatus { get; init; }
    public required Action<CadLoadOutcome> LogCadLoadOutcome { get; init; }
    public required Func<Task> TriggerGridRebuildAsync { get; init; }
    public required Func<Func<Task>, Task> RunWithCadLoadCanvasOverlayAsync { get; init; }
    public required Func<int> GetCadPadCountForLog { get; init; }
    public required Func<string, Func<Task>, Action<Exception>, Task> RunUiOperationAsync { get; init; }
    public required Action<Exception> OnError { get; init; }
}


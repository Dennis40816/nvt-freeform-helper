using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class Cad3635LoadBenchmarkTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private const string RunEnv = "FREEFORMHELPER_RUN_DXF_LOAD_BENCHMARK";
    private const string IterationsEnv = "FREEFORMHELPER_DXF_LOAD_BENCHMARK_ITERATIONS";
    private const string WarmupEnv = "FREEFORMHELPER_DXF_LOAD_BENCHMARK_WARMUP";
    private const string OutputEnv = "FREEFORMHELPER_DXF_LOAD_BENCHMARK_OUT";

    [Fact]
    public async Task Boe3635_DxfLoadBenchmark_WhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunEnv), "1", StringComparison.Ordinal))
        {
            return;
        }

        var iterations = GetPositiveInt(IterationsEnv, 5);
        var warmupIterations = GetNonNegativeInt(WarmupEnv, 1);
        var dxfPath = Path.Combine(
            TestPaths.RepoRoot,
            "example",
            "BOE36.35",
            "cz_36d2_6480x848_Lucid_panel_touch_block_CAD_20250729_NVTint_regular.dxf");
        var outputPath = ResolveOutputPath(Environment.GetEnvironmentVariable(OutputEnv));

        var samples = new List<Cad3635LoadBenchmarkSample>(warmupIterations + iterations);
        for (var run = 0; run < warmupIterations + iterations; run++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            samples.Add(await RunSampleAsync(run + 1, run < warmupIterations, dxfPath));
        }

        var measured = samples.Where(static sample => !sample.Warmup).ToArray();
        var result = new Cad3635LoadBenchmarkResult(
            TestPaths.RepoRoot,
            dxfPath,
            new FileInfo(dxfPath).Length,
            iterations,
            warmupIterations,
            samples,
            BuildSummary(measured));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var json = JsonSerializer.Serialize(result, JsonOptions);
        await File.WriteAllTextAsync(outputPath, json, TestContext.Current.CancellationToken);
    }

    private static async Task<Cad3635LoadBenchmarkSample> RunSampleAsync(int run, bool warmup, string dxfPath)
    {
        var vm = new FreeformHelperViewModel();
        var importService = GetPrivateField<DxfImportService>(vm, "_dxfImportService");
        var catalogState = GetPrivateField<LayerCatalogStateService>(vm, "_layerCatalogStateService");
        var useCase = new CadLoadUseCase(importService);
        var options = InvokePrivate<DxfImportOptions>(vm, "BuildDxfOptions");

        var total = Stopwatch.StartNew();

        var import = Stopwatch.StartNew();
        var outcome = useCase.TryLoadFromPath(dxfPath, options);
        import.Stop();
        Assert.NotNull(outcome);

        var layerCatalog = Stopwatch.StartNew();
        // Open DXF now hands the catalog built during the import to the catalog state (single read).
        catalogState.SetFromPath(dxfPath, importService.ImportedCatalog);
        layerCatalog.Stop();

        var apply = Stopwatch.StartNew();
        InvokePrivate(vm, "ResetHiddenCadPads");
        await InvokePrivateTaskAsync(vm, "ApplyCadLoadOutcomeAsync", [outcome, null]);
        apply.Stop();

        var rebuild = Stopwatch.StartNew();
        await InvokePrivateTaskAsync(vm, "TriggerGridRebuildAsync", [true]);
        rebuild.Stop();

        total.Stop();

        return new Cad3635LoadBenchmarkSample(
            run,
            warmup,
            total.ElapsedMilliseconds,
            import.ElapsedMilliseconds,
            layerCatalog.ElapsedMilliseconds,
            apply.ElapsedMilliseconds,
            rebuild.ElapsedMilliseconds,
            outcome!.Cad.Pads.Count,
            vm.CadPads.Count,
            vm.LayerToggles.Count);
    }

    private static string ResolveOutputPath(string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return Path.Combine(TestPaths.RepoRoot, "build", "perf", "3635-dxf-load-benchmark.json");
        }

        return Path.IsPathRooted(outputPath)
            ? outputPath
            : Path.Combine(TestPaths.RepoRoot, outputPath);
    }

    private static Cad3635LoadBenchmarkSummary BuildSummary(IReadOnlyList<Cad3635LoadBenchmarkSample> samples) =>
        new(
            BuildStat(samples.Select(static sample => sample.TotalMs)),
            BuildStat(samples.Select(static sample => sample.ImportMs)),
            BuildStat(samples.Select(static sample => sample.LayerCatalogMs)),
            BuildStat(samples.Select(static sample => sample.ApplyMs)),
            BuildStat(samples.Select(static sample => sample.RebuildMs)));

    private static Cad3635LoadBenchmarkStat BuildStat(IEnumerable<long> values)
    {
        var sorted = values.OrderBy(static value => value).ToArray();
        if (sorted.Length == 0)
        {
            return new Cad3635LoadBenchmarkStat(0, 0, 0, 0, 0);
        }

        var average = sorted.Average();
        return new Cad3635LoadBenchmarkStat(
            sorted.Length,
            sorted[0],
            sorted[(sorted.Length - 1) / 2],
            Math.Round(average, 2),
            sorted[^1]);
    }

    private static TField GetPrivateField<TField>(object instance, string fieldName) =>
        (TField)instance.GetType().GetField(fieldName, PrivateInstance)!.GetValue(instance)!;

    private static TResult InvokePrivate<TResult>(object instance, string methodName, object?[]? args = null)
    {
        var result = InvokePrivate(instance, methodName, args);
        return Assert.IsType<TResult>(result);
    }

    private static async Task InvokePrivateTaskAsync(object instance, string methodName, object?[]? args = null)
    {
        var task = Assert.IsAssignableFrom<Task>(InvokePrivate(instance, methodName, args));
        await task;
    }

    private static object? InvokePrivate(object instance, string methodName, object?[]? args = null)
    {
        var method = instance.GetType().GetMethod(methodName, PrivateInstance);
        Assert.NotNull(method);
        return method!.Invoke(instance, args);
    }

    private static int GetPositiveInt(string name, int fallback)
    {
        var value = GetNonNegativeInt(name, fallback);
        return value <= 0 ? fallback : value;
    }

    private static int GetNonNegativeInt(string name, int fallback)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(name), out var parsed) && parsed >= 0
            ? parsed
            : fallback;
    }

    private sealed record Cad3635LoadBenchmarkResult(
        string RepoRoot,
        string DxfPath,
        long DxfBytes,
        int Iterations,
        int WarmupIterations,
        IReadOnlyList<Cad3635LoadBenchmarkSample> Samples,
        Cad3635LoadBenchmarkSummary Summary);

    private sealed record Cad3635LoadBenchmarkSample(
        int Run,
        bool Warmup,
        long TotalMs,
        long ImportMs,
        long LayerCatalogMs,
        long ApplyMs,
        long RebuildMs,
        int RawCadPadCount,
        int VisibleCadPadCount,
        int LayerCount);

    private sealed record Cad3635LoadBenchmarkSummary(
        Cad3635LoadBenchmarkStat TotalMs,
        Cad3635LoadBenchmarkStat ImportMs,
        Cad3635LoadBenchmarkStat LayerCatalogMs,
        Cad3635LoadBenchmarkStat ApplyMs,
        Cad3635LoadBenchmarkStat RebuildMs);

    private sealed record Cad3635LoadBenchmarkStat(
        int Count,
        long Min,
        long P50,
        double Avg,
        long Max);
}

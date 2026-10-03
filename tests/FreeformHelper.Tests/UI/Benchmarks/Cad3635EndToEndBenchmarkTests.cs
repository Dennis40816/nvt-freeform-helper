using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class Cad3635EndToEndBenchmarkTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private const string RunEnv = "FREEFORMHELPER_RUN_3635_E2E_BENCHMARK";
    private const string IterationsEnv = "FREEFORMHELPER_3635_E2E_BENCHMARK_ITERATIONS";
    private const string WarmupEnv = "FREEFORMHELPER_3635_E2E_BENCHMARK_WARMUP";
    private const string OutputEnv = "FREEFORMHELPER_3635_E2E_BENCHMARK_OUT";

    [AvaloniaFact]
    public async Task Boe3635_EndToEndWorkflowBenchmark_WhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunEnv), "1", StringComparison.Ordinal))
        {
            return;
        }

        HeadlessAppBootstrap.EnsureInitialized();
        var iterations = GetPositiveInt(IterationsEnv, 3);
        var warmupIterations = GetNonNegativeInt(WarmupEnv, 1);
        var projectPath = Path.Combine(TestPaths.RepoRoot, "example", "BOE36.35", "project_3635.json");
        var outputPath = ResolveOutputPath(Environment.GetEnvironmentVariable(OutputEnv));
        var exportDirectory = Path.Combine(TestPaths.RepoRoot, "build", "perf", "3635-e2e-export");
        Directory.CreateDirectory(exportDirectory);

        var originalFactory = FreeformHelperView.CadLoadSpinnerHostFactory;
        FreeformHelperView.CadLoadSpinnerHostFactory = static () => new BenchmarkCadLoadSpinnerHost();
        try
        {
            var samples = new List<Cad3635EndToEndBenchmarkSample>(warmupIterations + iterations);
            for (var run = 0; run < warmupIterations + iterations; run++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                samples.Add(await RunSampleAsync(
                    run + 1,
                    run < warmupIterations,
                    projectPath,
                    exportDirectory));
            }

            var measured = samples.Where(static sample => !sample.Warmup).ToArray();
            var result = new Cad3635EndToEndBenchmarkResult(
                TestPaths.RepoRoot,
                projectPath,
                iterations,
                warmupIterations,
                samples,
                BuildSummary(measured));

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            var json = JsonSerializer.Serialize(result, JsonOptions);
            await File.WriteAllTextAsync(outputPath, json);
        }
        finally
        {
            FreeformHelperView.CadLoadSpinnerHostFactory = originalFactory;
        }
    }

    private static async Task<Cad3635EndToEndBenchmarkSample> RunSampleAsync(
        int run,
        bool warmup,
        string projectPath,
        string exportDirectory)
    {
        var shell = new ShellViewModel();
        var vm = shell.FreeformHelper;
        var exportPath = Path.Combine(exportDirectory, $"run-{run.ToString("00", System.Globalization.CultureInfo.InvariantCulture)}.c");

        var window = new MainWindow();
        window.SetShellViewModel(shell);
        window.Width = 1280;
        window.Height = 900;

        try
        {
            window.Show();
            await FlushUiQueueAsync();
            vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);
            vm.PickSaveNotchPathAsync = (_, _) => Task.FromResult<string?>(exportPath);
            vm.OpenNotchExportSelectionAsync = null;

            var total = Stopwatch.StartNew();
            var loadProject = await MeasureStageAsync(
                "Load project",
                () => vm.LoadProjectCommand.ExecuteAsync(null),
                window);
            var step1Match = await MeasureStageAsync(
                "Step 1 match",
                () => vm.MatchCommand.ExecuteAsync(null),
                window);
            var step2Freeform = await MeasureStageAsync(
                "Step 2 freeform",
                () => vm.AutoDetectFreeformsCommand.ExecuteAsync(null),
                window);
            var step5Export = await MeasureStageAsync(
                "Step 5 export",
                () => vm.ExportNotchCommand.ExecuteAsync(null),
                window);
            total.Stop();
            Assert.True(vm.CadPads.Count > 0, "Benchmark load did not populate CAD pads.");
            Assert.True(vm.LayerToggles.Count > 0, "Benchmark load did not populate DXF layer toggles.");
            Assert.True(File.Exists(exportPath), "Benchmark Step 5 export did not write an output file.");

            return new Cad3635EndToEndBenchmarkSample(
                run,
                warmup,
                total.ElapsedMilliseconds,
                loadProject,
                step1Match,
                step2Freeform,
                step5Export,
                vm.CadPads.Count,
                vm.RegularPads.Count,
                vm.LayerToggles.Count,
                File.Exists(exportPath) ? new FileInfo(exportPath).Length : 0);
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task<Cad3635EndToEndStageSample> MeasureStageAsync(
        string name,
        Func<Task> action,
        Window window)
    {
        var compute = Stopwatch.StartNew();
        await action();
        compute.Stop();

        var renderFlush = Stopwatch.StartNew();
        await FlushUiQueueAsync();
        renderFlush.Stop();

        var capture = Stopwatch.StartNew();
        using (var frame = window.CaptureRenderedFrame())
        {
            Assert.NotNull(frame);
        }

        capture.Stop();
        return new Cad3635EndToEndStageSample(
            name,
            compute.ElapsedMilliseconds,
            renderFlush.ElapsedMilliseconds,
            capture.ElapsedMilliseconds);
    }

    private static async Task FlushUiQueueAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
    }

    private static string ResolveOutputPath(string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return Path.Combine(TestPaths.RepoRoot, "build", "perf", "3635-end-to-end-benchmark.json");
        }

        return Path.IsPathRooted(outputPath)
            ? outputPath
            : Path.Combine(TestPaths.RepoRoot, outputPath);
    }

    private static Cad3635EndToEndBenchmarkSummary BuildSummary(IReadOnlyList<Cad3635EndToEndBenchmarkSample> samples) =>
        new(
            BuildStat(samples.Select(static sample => sample.TotalMs)),
            BuildStageSummary(samples.Select(static sample => sample.LoadProject)),
            BuildStageSummary(samples.Select(static sample => sample.Step1Match)),
            BuildStageSummary(samples.Select(static sample => sample.Step2Freeform)),
            BuildStageSummary(samples.Select(static sample => sample.Step5Export)));

    private static Cad3635EndToEndStageSummary BuildStageSummary(IEnumerable<Cad3635EndToEndStageSample> samples)
    {
        var array = samples.ToArray();
        return new Cad3635EndToEndStageSummary(
            BuildStat(array.Select(static sample => sample.ComputeMs)),
            BuildStat(array.Select(static sample => sample.RenderFlushMs)),
            BuildStat(array.Select(static sample => sample.CaptureFrameMs)));
    }

    private static Cad3635EndToEndBenchmarkStat BuildStat(IEnumerable<long> values)
    {
        var sorted = values.OrderBy(static value => value).ToArray();
        if (sorted.Length == 0)
        {
            return new Cad3635EndToEndBenchmarkStat(0, 0, 0, 0, 0);
        }

        var average = sorted.Average();
        return new Cad3635EndToEndBenchmarkStat(
            sorted.Length,
            sorted[0],
            sorted[(sorted.Length - 1) / 2],
            Math.Round(average, 2),
            sorted[^1]);
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

    private sealed class BenchmarkCadLoadSpinnerHost : ICadLoadSpinnerHost
    {
        public void Warmup()
        {
        }

        public void Show(Window owner)
        {
        }

        public void Hide()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed record Cad3635EndToEndBenchmarkResult(
        string RepoRoot,
        string ProjectPath,
        int Iterations,
        int WarmupIterations,
        IReadOnlyList<Cad3635EndToEndBenchmarkSample> Samples,
        Cad3635EndToEndBenchmarkSummary Summary);

    private sealed record Cad3635EndToEndBenchmarkSample(
        int Run,
        bool Warmup,
        long TotalMs,
        Cad3635EndToEndStageSample LoadProject,
        Cad3635EndToEndStageSample Step1Match,
        Cad3635EndToEndStageSample Step2Freeform,
        Cad3635EndToEndStageSample Step5Export,
        int VisibleCadPadCount,
        int RegularPadCount,
        int LayerCount,
        long ExportBytes);

    private sealed record Cad3635EndToEndStageSample(
        string Name,
        long ComputeMs,
        long RenderFlushMs,
        long CaptureFrameMs);

    private sealed record Cad3635EndToEndBenchmarkSummary(
        Cad3635EndToEndBenchmarkStat TotalMs,
        Cad3635EndToEndStageSummary LoadProject,
        Cad3635EndToEndStageSummary Step1Match,
        Cad3635EndToEndStageSummary Step2Freeform,
        Cad3635EndToEndStageSummary Step5Export);

    private sealed record Cad3635EndToEndStageSummary(
        Cad3635EndToEndBenchmarkStat ComputeMs,
        Cad3635EndToEndBenchmarkStat RenderFlushMs,
        Cad3635EndToEndBenchmarkStat CaptureFrameMs);

    private sealed record Cad3635EndToEndBenchmarkStat(
        int Count,
        long Min,
        long P50,
        double Avg,
        long Max);
}

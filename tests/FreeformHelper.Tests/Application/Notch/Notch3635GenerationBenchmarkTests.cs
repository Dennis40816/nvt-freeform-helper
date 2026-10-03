using System.Diagnostics;
using System.Text.Json;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class Notch3635GenerationBenchmarkTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private const string RunEnv = "FREEFORMHELPER_RUN_NOTCH_BENCHMARK";
    private const string IterationsEnv = "FREEFORMHELPER_NOTCH_BENCHMARK_ITERATIONS";
    private const string WarmupEnv = "FREEFORMHELPER_NOTCH_BENCHMARK_WARMUP";
    private const string OutputEnv = "FREEFORMHELPER_NOTCH_BENCHMARK_OUT";

    [Fact]
    public async Task Boe3635_NotchGenerationBenchmark_WhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunEnv), "1", StringComparison.Ordinal))
        {
            return;
        }

        var iterations = GetPositiveInt(IterationsEnv, 5);
        var warmupIterations = GetNonNegativeInt(WarmupEnv, 1);
        var outputPath = Environment.GetEnvironmentVariable(OutputEnv);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = Path.Combine(TestPaths.RepoRoot, "build", "perf", "3635-notch-generation-benchmark.json");
        }
        else if (!Path.IsPathRooted(outputPath))
        {
            outputPath = Path.Combine(TestPaths.RepoRoot, outputPath);
        }

        var projectPath = Path.Combine(TestPaths.RepoRoot, "example", "BOE36.35", "project_3635.json");
        var samples = new List<Notch3635BenchmarkSample>(warmupIterations + iterations);
        for (var run = 0; run < warmupIterations + iterations; run++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var vm = new FreeformHelperViewModel
            {
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            await vm.LoadProjectCommand.ExecuteAsync(null);

            var wall = Stopwatch.StartNew();
            var rawTable = await NotchCurrentWorkflowTableTestHelper.GenerateRawTableAsync(
                vm,
                "3635 notch generation benchmark");
            wall.Stop();

            samples.Add(BuildSample(run + 1, run < warmupIterations, wall.ElapsedMilliseconds, rawTable.GenerationPhaseTimings));
        }

        var measured = samples.Where(static sample => !sample.Warmup).ToArray();
        var result = new Notch3635BenchmarkResult(
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

    private static Notch3635BenchmarkSample BuildSample(
        int run,
        bool warmup,
        long wallMs,
        NotchGenerationPhaseTimings timings)
    {
        var candidateBreakdown = timings.CandidateBreakdown ?? NotchCandidatePhaseTimings.Empty;
        var compensationStages = candidateBreakdown.CompensationStageTimings;
        var stageCBreakdown = compensationStages.StageCBreakdown ?? NotchV22StageCSubphaseTimings.Empty;
        return new Notch3635BenchmarkSample(
            run,
            warmup,
            wallMs,
            timings.TotalElapsedMs,
            timings.BuildProfilesElapsedMs,
            timings.BuildCanonicalCandidatesElapsedMs,
            timings.MergeCanonicalCandidatesElapsedMs,
            timings.BuildLegacyRowsElapsedMs,
            timings.FinalizeCanonicalExportsElapsedMs,
            candidateBreakdown.CandidateCount,
            candidateBreakdown.BuildV22CadCandidateElapsedMs,
            candidateBreakdown.CompensationComputeElapsedMs,
            compensationStages.StageAElapsedMs,
            compensationStages.StageBElapsedMs,
            compensationStages.StageCElapsedMs,
            compensationStages.StageDElapsedMs,
            stageCBreakdown.OwnerAndBlockerLookupElapsedMs,
            stageCBreakdown.ExactOccupiedAreaElapsedMs,
            stageCBreakdown.SourceCoverageElapsedMs,
            stageCBreakdown.ReachabilityElapsedMs,
            stageCBreakdown.RuleDecisionElapsedMs,
            stageCBreakdown.PreviewAndDebugElapsedMs);
    }

    private static Notch3635BenchmarkSummary BuildSummary(IReadOnlyList<Notch3635BenchmarkSample> samples) =>
        new(
            BuildStat(samples.Select(static sample => sample.WallMs)),
            BuildStat(samples.Select(static sample => sample.GenerationTotalMs)),
            BuildStat(samples.Select(static sample => sample.BuildCanonicalCandidatesMs)),
            BuildStat(samples.Select(static sample => sample.CompensationComputeMs)),
            BuildStat(samples.Select(static sample => sample.StageCMs)),
            BuildStat(samples.Select(static sample => sample.OwnerAndBlockerLookupMs)));

    private static Notch3635BenchmarkStat BuildStat(IEnumerable<long> values)
    {
        var sorted = values.OrderBy(static value => value).ToArray();
        if (sorted.Length == 0)
        {
            return new Notch3635BenchmarkStat(0, 0, 0, 0, 0);
        }

        var average = sorted.Average();
        return new Notch3635BenchmarkStat(
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

    private sealed record Notch3635BenchmarkResult(
        string RepoRoot,
        string ProjectPath,
        int Iterations,
        int WarmupIterations,
        IReadOnlyList<Notch3635BenchmarkSample> Samples,
        Notch3635BenchmarkSummary Summary);

    private sealed record Notch3635BenchmarkSample(
        int Run,
        bool Warmup,
        long WallMs,
        long GenerationTotalMs,
        long BuildProfilesMs,
        long BuildCanonicalCandidatesMs,
        long MergeCanonicalCandidatesMs,
        long BuildLegacyRowsMs,
        long FinalizeCanonicalExportsMs,
        int CandidateCount,
        long BuildV22CadCandidateMs,
        long CompensationComputeMs,
        long StageAMs,
        long StageBMs,
        long StageCMs,
        long StageDMs,
        long OwnerAndBlockerLookupMs,
        long OccupiedAreaMs,
        long SourceCoverageMs,
        long ReachabilityMs,
        long RuleDecisionMs,
        long PreviewAndDebugMs);

    private sealed record Notch3635BenchmarkSummary(
        Notch3635BenchmarkStat WallMs,
        Notch3635BenchmarkStat GenerationTotalMs,
        Notch3635BenchmarkStat BuildCanonicalCandidatesMs,
        Notch3635BenchmarkStat CompensationComputeMs,
        Notch3635BenchmarkStat StageCMs,
        Notch3635BenchmarkStat OwnerAndBlockerLookupMs);

    private sealed record Notch3635BenchmarkStat(
        int Count,
        long Min,
        long P50,
        double Avg,
        long Max);
}

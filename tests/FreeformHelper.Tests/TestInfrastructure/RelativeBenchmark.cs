using System.Globalization;
using System.Text;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>
/// A small timer and allocation probe for relative performance comparisons.
/// Results are meant to be read as ratios between variants measured in the same run, never as absolute budgets.
/// </summary>
internal static class RelativeBenchmark
{
    public static BenchmarkSample Measure(string name, Action operation, int iterations, int warmupIterations = 10)
    {
        ArgumentNullException.ThrowIfNull(operation);

        for (var i = 0; i < warmupIterations; i++)
        {
            operation();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = TimeProvider.System.GetTimestamp();
        for (var i = 0; i < iterations; i++)
        {
            operation();
        }

        var elapsed = TimeProvider.System.GetElapsedTime(started);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
        return new BenchmarkSample(
            name,
            iterations,
            elapsed.TotalMilliseconds * 1_000_000.0 / iterations,
            bytes / (double)iterations);
    }

    public static string FormatTable(string title, IReadOnlyList<BenchmarkSample> samples, BenchmarkSample baseline)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"## {title}");
        builder.AppendLine("| variant | time/op (rel) | bytes/op | alloc (rel) |");
        builder.AppendLine("|---|---:|---:|---:|");
        foreach (var sample in samples)
        {
            var timeRatio = sample.NanosecondsPerOperation / Math.Max(baseline.NanosecondsPerOperation, 1e-9);
            var allocRatio = baseline.BytesPerOperation <= 0
                ? double.NaN
                : sample.BytesPerOperation / baseline.BytesPerOperation;
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"| {sample.Name} | {timeRatio:0.00}x | {sample.BytesPerOperation:0} | {allocRatio:0.00}x |");
        }

        return builder.ToString();
    }
}

internal readonly record struct BenchmarkSample(
    string Name,
    int Iterations,
    double NanosecondsPerOperation,
    double BytesPerOperation);

using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests.Architecture;

/// <summary>
/// Guards the offender lists of <c>debt-baseline.json</c> against stale keys.
/// </summary>
public sealed class DebtBaselineKeyTests
{
    [Fact]
    public void OffenderLists_NameTheCurrentSourceLines()
    {
        var baseline = ArchitectureBaseline.Load(TestPaths.RepoRoot);
        var measured = DebtBaselineTests.Measure(ArchitectureSource.Read(TestPaths.RepoRoot, ".cs"), TestPaths.RepoRoot);
        var errors = new List<string>();
        foreach (var (key, offenders) in measured.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            // The count test reports a different count. This test reports a key that no longer names an offender,
            // which stays silent when a line shift keeps the count equal.
            var stale = baseline.MetricOffenders[key].Except(offenders, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (stale.Length > 0 && offenders.Length == baseline.Metrics[key])
            {
                errors.Add($"{key}: stale keys {string.Join(", ", stale)}; current entries: {string.Join(", ", offenders.Except(baseline.MetricOffenders[key], StringComparer.Ordinal).Order(StringComparer.Ordinal))}");
            }
        }

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }
}

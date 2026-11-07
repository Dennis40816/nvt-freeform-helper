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
        var errors = FindStaleKeyErrors(measured, baseline.MetricOffenders, baseline.Metrics);

        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void StaleKeys_ReportCountAndNewItemsButNotTheStaleKeys()
    {
        var measured = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["asyncVoid"] = ["src/A.cs:12", "src/B.cs:7"],
        };
        var offenders = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["asyncVoid"] = ["src/A.cs:10", "src/B.cs:7"],
        };
        var metrics = new Dictionary<string, int>(StringComparer.Ordinal) { ["asyncVoid"] = 2 };

        var error = Assert.Single(FindStaleKeyErrors(measured, offenders, metrics));

        Assert.Contains("1 stale key", error, StringComparison.Ordinal);
        Assert.Contains("src/A.cs:12", error, StringComparison.Ordinal);
        // The stale key and the keys that are still valid stay out of the message, so it cannot be pasted back as a list.
        Assert.DoesNotContain("src/A.cs:10", error, StringComparison.Ordinal);
        Assert.DoesNotContain("src/B.cs:7", error, StringComparison.Ordinal);
        Assert.DoesNotContain(", ", error, StringComparison.Ordinal);
    }

    [Fact]
    public void KeysThatStillMatchOrAMetricWithAnotherCount_ReportNothing()
    {
        var measured = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["same"] = ["src/A.cs:1"],
            ["moved"] = ["src/B.cs:9", "src/B.cs:10"],
        };
        var offenders = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["same"] = ["src/A.cs:1"],
            ["moved"] = ["src/B.cs:3"],
        };
        var metrics = new Dictionary<string, int>(StringComparer.Ordinal) { ["same"] = 1, ["moved"] = 1 };

        Assert.Empty(FindStaleKeyErrors(measured, offenders, metrics));
    }

    // The count test reports a different count. This check reports a key that no longer names an offender,
    // which stays silent when a line shift keeps the count equal. The message gives the number of stale keys and
    // the new items. It does not repeat the baseline keys, so the output is not a list to paste into the ledger.
    private static string[] FindStaleKeyErrors(
        IReadOnlyDictionary<string, string[]> measured,
        Dictionary<string, string[]> baselineOffenders,
        Dictionary<string, int> baselineMetrics)
    {
        var errors = new List<string>();
        foreach (var (key, offenders) in measured.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            var stale = baselineOffenders[key].Except(offenders, StringComparer.Ordinal).ToArray();
            if (stale.Length == 0 || offenders.Length != baselineMetrics[key])
            {
                continue;
            }

            var added = offenders.Except(baselineOffenders[key], StringComparer.Ordinal).Order(StringComparer.Ordinal);
            errors.Add($"{key}: {stale.Length} stale key(s). New item(s):{Environment.NewLine}" +
                string.Join(Environment.NewLine, added.Select(static item => $"  {item}")));
        }

        return errors.ToArray();
    }
}

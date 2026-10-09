using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class UiVisualSnapshotTests
{
    private static readonly JsonSerializerOptions BaselineJsonOptions = new()
    {
        WriteIndented = true
    };

    [Fact]
    public void CriticalUiFiles_MatchMinimalVisualBaseline()
    {
        var mode = UiBaselineUpdateModeResolver.Resolve();
        var repoRoot = TestPaths.RepoRoot;
        var baselinePath = Path.Combine(repoRoot, "tests", "FreeformHelper.Tests", "Snapshots", "ui-visual-minimal-baseline.json");
        Assert.True(File.Exists(baselinePath), $"Baseline file not found: {baselinePath}");

        var baselineText = File.ReadAllText(baselinePath, Encoding.UTF8);
        var baseline = JsonSerializer.Deserialize<UiVisualBaseline>(baselineText);
        Assert.NotNull(baseline);
        Assert.NotNull(baseline!.Files);
        Assert.NotEmpty(baseline.Files);

        var hashMismatches = new List<string>();
        var fatalIssues = new List<string>();
        var computedHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in baseline.Files.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            var relativePath = entry.Key.Replace('/', Path.DirectorySeparatorChar);
            var filePath = Path.Combine(repoRoot, relativePath);
            if (!File.Exists(filePath))
            {
                fatalIssues.Add($"{entry.Key}: file missing");
                continue;
            }

            var actual = ComputeNormalizedSha256(filePath);
            computedHashes[entry.Key] = actual;
            var expected = entry.Value.Trim().ToLowerInvariant();
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                hashMismatches.Add($"{entry.Key}: expected={expected}, actual={actual}");
            }
        }

        if (mode == UiBaselineUpdateMode.Apply)
        {
            Assert.True(
                fatalIssues.Count == 0,
                "Cannot apply UI baseline update because required source files are missing:\n" + string.Join('\n', fatalIssues));

            if (hashMismatches.Count > 0)
            {
                baseline.Files = computedHashes
                    .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                    .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
                var updatedBaselineText = JsonSerializer.Serialize(baseline, BaselineJsonOptions);
                File.WriteAllText(baselinePath, updatedBaselineText + Environment.NewLine, new UTF8Encoding(false));
            }

            return;
        }

        if (mode == UiBaselineUpdateMode.DryRun)
        {
            Assert.True(
                fatalIssues.Count == 0 && hashMismatches.Count == 0,
                "UI visual baseline dry-run found changes. Use FH_UI_BASELINE_MODE=apply to write baseline.\n"
                + string.Join('\n', fatalIssues.Concat(hashMismatches)));
            return;
        }

        Assert.True(
            fatalIssues.Count == 0 && hashMismatches.Count == 0,
            "UI visual baseline mismatch. If this is intentional UI change, update tests/FreeformHelper.Tests/Snapshots/ui-visual-minimal-baseline.json.\n"
            + string.Join('\n', fatalIssues.Concat(hashMismatches)));
    }

    private static string ComputeNormalizedSha256(string filePath)
    {
        var raw = File.ReadAllText(filePath, Encoding.UTF8);
        var normalized = NormalizeVisualSource(raw);
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // Root binding compilation metadata is guarded by CompiledBindingsGuardTests.
    // Keep the visual source baseline stable while existing runtime bindings become explicit.
    private static string NormalizeVisualSource(string raw)
    {
        var normalized = raw.Replace("\r\n", "\n", StringComparison.Ordinal);
        return Regex.Replace(normalized, @"\A(?:\s|<\?xml[\s\S]*?\?>|<!--[\s\S]*?-->)*<[^!?][^>]*>",
            static root => Regex.Replace(root.Value, @"\s+x:CompileBindings=""False""", string.Empty, RegexOptions.CultureInvariant),
            RegexOptions.CultureInvariant);
    }

    [Fact]
    public void VisualSourceNormalization_IgnoresOnlyRootBindingCompilationException()
    {
        const string original = """<UserControl><TextBlock Text="{Binding Title}"/></UserControl>""";
        string rootException = original.Replace("<UserControl>", """<UserControl x:CompileBindings="False">""", StringComparison.Ordinal);
        Assert.Equal(original, NormalizeVisualSource(rootException));
        string nestedException = original.Replace("<TextBlock ", """<TextBlock x:CompileBindings="False" """, StringComparison.Ordinal);
        Assert.NotEqual(original, NormalizeVisualSource(nestedException));
        Assert.NotEqual(original, NormalizeVisualSource(original.Replace("Title", "OtherTitle", StringComparison.Ordinal)));
    }

    private sealed class UiVisualBaseline
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("files")]
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.Ordinal);
    }
}

using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests.Architecture;

public sealed class DebtBaselineTests
{
    // Counts are occurrences, not lines or unique files. Patterns allow whitespace between
    // tokens. Comments/literals are excluded; inactive #if source is included. Source paths
    // and diagnostics use ordinal ordering. This inventory counts storage, not independent
    // domain facts; extraction into another owner is outside this particular FHVM metric.
    internal static Dictionary<string, string[]> Measure(ArchitectureSource[] sources, string repoRoot)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        void Count(string key, string pattern) => result.Add(key, sources.SelectMany(source => source.Find(pattern)).ToArray());

        // Method/local-function declarations with the adjacent modifiers async void.
        Count("asyncVoid", @"\basync\s+void\s+\w+\s*\(");
        // Attribute applications, including qualified names and the Attribute suffix.
        Count("suppressMessage", @"\[\s*(?:(?:global::)?System\.Diagnostics\.CodeAnalysis\.)?SuppressMessage(?:Attribute)?\s*\(");
        // Each disable directive counts once, irrespective of how many IDs it contains.
        Count("pragmaWarningDisable", @"(?m)^\s*#\s*pragma\s+warning\s+disable\b");
        // Simple or compound assignments to this field, including its declaration initializer;
        // equality tests are excluded. Increment/decrement are not assignments here.
        Count("suppressUndoAssignments", @"\b_suppressUndo\s*(?:\?\?=|[+\-*/%&|^]=|=(?!=|>))");
        // Unqualified or System.IO-qualified calls to exactly these four write methods.
        Count("fileWriteAllText", @"\bFile\s*\.\s*WriteAll(?:Text|Bytes)(?:Async)?\s*\(");
        // Process.Start invocations plus Process construction (not ProcessStartInfo).
        Count("processStart", @"\bProcess\s*\.\s*Start\s*\(|\bnew\s+(?:(?:global::)?System\.Diagnostics\.)?Process\s*[({]");
        // Direct reads of the four named clock properties; one occurrence per access.
        Count("directClockReads", @"\b(?:DateTime|DateTimeOffset)\s*\.\s*(?:Now|UtcNow)\b");
        // Catch clauses without a declared type, including catch when (...).
        Count("bareCatch", @"\bcatch\s*(?=\{|when\b)");
        // Catch clauses typed exactly Exception/System.Exception, with or without a variable.
        Count("catchException", @"\bcatch\s*\(\s*(?:(?:global::)?System\.)?Exception\b");

        // Private bool field declarators across all src, including static/const/readonly fields.
        result.Add("suppressFlagFields", sources.SelectMany(source => source.Fields(instanceOnly: false)
            .Where(static field => field.Type == "bool" && field.Modifiers.Contains("private", StringComparison.Ordinal) &&
                (field.Name.StartsWith("_suppress", StringComparison.Ordinal) || field.Name == "_isLoadingSettings"))
            .Select(field => source.Location(field.Offset))).ToArray());

        // All private instance field declarators of this class across its partial files,
        // plus non-private ObservableProperty field declarators, each counted only once.
        // Readonly service references count. Stored public properties do not. Nested types
        // are excluded; filenames do not determine ownership. See ArchitectureSource.Fields.
        result.Add("freeformViewModelStateMembers", sources.SelectMany(source => source.Fields("FreeformHelperViewModel")
            .Where(field => field.Modifiers.Contains("private", StringComparison.Ordinal) ||
                ArchitectureSource.Pattern(@"\[\s*ObservableProperty(?:Attribute)?\s*\][\s\w\[\](),.]*$")
                    .IsMatch(source.Code[..field.Offset]))
            .Select(field => source.Location(field.Offset))).ToArray());

        // These five public bool properties may be explicit or generated from attributed
        // fields. Never count both. Missing/duplicate names fail the metric definition so
        // an unrecognized source shape cannot silently produce a lower inventory.
        string[] navigationNames = ["IsWorkspaceActive", "IsHowToUseActive", "IsDevActive", "IsSimulationActive", "IsCoordinateActive"];
        var shell = sources.Where(static source => source.Path.StartsWith("src/FreeformHelper.UI/ViewModels/", StringComparison.Ordinal));
        var navigation = new List<string>();
        foreach (string name in navigationNames)
        {
            var entries = shell.SelectMany(source =>
                source.Fields("ShellViewModel").Where(field => field.Type == "bool" &&
                    field.Name == "_" + char.ToLowerInvariant(name[0]) + name[1..] &&
                    ArchitectureSource.Pattern(@"\[\s*ObservableProperty\s*\]\s*private\s+bool\s+$")
                        .IsMatch(source.Code[..field.Offset]))
                    .Select(field => source.Location(field.Offset))
                    .Concat(source.Find(@"\bpublic\s+bool\s+" + Regex.Escape(name) + @"\s*(?=\{|=>)", "ShellViewModel"))).ToArray();
            if (entries.Length != 1)
            {
                throw new InvalidOperationException($"shellNavigationBooleans definition: expected one bool property {name}, found {entries.Length}.");
            }

            navigation.Add(entries[0]);
        }

        result.Add("shellNavigationBooleans", navigation.Order(StringComparer.Ordinal).ToArray());

        // One occurrence per literal NoWarn ID per XML element in root Directory.Build.props
        // and all repository csproj files (excluding bin/obj/build). Repeated IDs count again.
        // Semicolon/comma/whitespace separators are accepted; $(NoWarn) is not an ID.
        var noWarn = new List<string>();
        var projects = ArchitectureSource.Read(repoRoot, ".csproj", ".")
            .Append(new ArchitectureSource("Directory.Build.props", File.ReadAllText(Path.Combine(repoRoot, "Directory.Build.props"))))
            .OrderBy(static source => source.Path, StringComparer.Ordinal);
        foreach (var project in projects)
        {
            var xml = XDocument.Parse(project.Text, LoadOptions.SetLineInfo);
            foreach (var element in xml.Descendants().Where(static element => element.Name.LocalName == "NoWarn"))
            {
                string value = ArchitectureSource.Pattern(@"\$\([^)]*\)").Replace(element.Value, "");
                foreach (string id in Regex.Split(value, @"[;,\s]+").Where(static id => id.Length > 0))
                {
                    noWarn.Add($"{project.Path}:{((IXmlLineInfo)element).LineNumber} ({id})");
                }
            }
        }

        result.Add("noWarnIds", noWarn.ToArray());
        return result;
    }

    [Fact]
    public void CountedDebt_MatchesBaselineAndRequiresRatchetAfterCleanup()
    {
        var baseline = ArchitectureBaseline.Load(TestPaths.RepoRoot);
        var measured = Measure(ArchitectureSource.Read(TestPaths.RepoRoot, ".cs"), TestPaths.RepoRoot);
        Assert.Equal(baseline.Metrics.Keys.Order(StringComparer.Ordinal), measured.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(baseline.Metrics.Keys.Order(StringComparer.Ordinal), baseline.MetricOffenders.Keys.Order(StringComparer.Ordinal));
        var errors = new List<string>();
        foreach (var (key, offenders) in measured.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            Assert.Equal(baseline.Metrics[key], baseline.MetricOffenders[key].Length);
            int count = offenders.Length;
            if (count > baseline.Metrics[key])
            {
                var added = offenders.Except(baseline.MetricOffenders[key], StringComparer.Ordinal).Order(StringComparer.Ordinal);
                errors.Add($"{key}: {count} exceeds {baseline.Metrics[key]}; new file:line entries: {string.Join(", ", added)}");
            }
            else if (count < baseline.Metrics[key])
            {
                errors.Add($"{key}: lower the baseline in debt-baseline.json to {count} (and update its offender list).");
            }
        }

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void SourceInventory_ExcludesLiteralsNestedTypesAndGeneratedDuplicates()
    {
        var source = new ArchitectureSource("fixture.cs", """
            class FreeformHelperViewModel
            {
                // private bool _suppressComment;
                [ObservableProperty] private bool _suppressReal;
                private readonly Dictionary<int, (int A, bool B)> _cache = new();
                private int _one, _two;
                private static bool _suppressStatic;
                private const int Limit = 1;
                private string _text = "async void Fake() catch { DateTime.Now";
                private bool Computed => true;
                private void Run(int first, int second = 0) { bool _local = false; }
                class Nested { private bool _nested; }
            }
            """);
        Assert.Equal(["_suppressReal", "_cache", "_one", "_two", "_text"],
            source.Fields("FreeformHelperViewModel").Select(static field => field.Name));
        Assert.Empty(source.Find(@"\basync\s+void\b|\bcatch\b|\bDateTime\.Now\b"));
        Assert.Equal(2, source.Fields(instanceOnly: false)
            .Count(static field => field.Name.StartsWith("_suppress", StringComparison.Ordinal)));
    }

    [Fact]
    public void SourceInventory_PreservesExecutableInterpolationExpressions()
    {
        var source = new ArchitectureSource("fixture.cs", """"
            var a = $"{DateTime.Now} {File.ReadAllText("path")}";
            var b = $@"{{literal DateTime.Now}} {DateTimeOffset.UtcNow}";
            var c = $$"""literal DateTime.Now {{DateTime.UtcNow}}""";
            var literal = "DateTime.Now";
            // DateTime.Now
            """");
        Assert.Equal(3, source.Find(@"\b(?:DateTime|DateTimeOffset)\.(?:Now|UtcNow)\b").Length);
        Assert.Single(source.Find(@"\bFile\.ReadAllText\s*\("));
    }

}

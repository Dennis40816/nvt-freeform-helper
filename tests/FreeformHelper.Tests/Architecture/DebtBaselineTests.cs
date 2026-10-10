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
    internal const string AsyncVoidPattern = @"\basync\s+void\b";
    internal const string AsyncVoidLambdaPattern = @"[+\-]=\s*async\b";

    internal static Dictionary<string, string[]> Measure(ArchitectureSource[] sources, string repoRoot)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        void Count(string key, string pattern) => result.Add(key, sources.SelectMany(source => source.Find(pattern)).ToArray());

        // Declarations with the adjacent modifiers async void, including generic methods.
        Count("asyncVoid", AsyncVoidPattern);
        // Async lambdas subscribed to an event with += or -= are async void handlers too.
        // Async lambdas passed as arguments or assigned to Func<Task> are not counted.
        Count("asyncVoidLambdas", AsyncVoidLambdaPattern);
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

        result.Add("shellNavigationBooleans", MeasureShellNavigationBooleans(sources));

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

    internal static string[] MeasureShellNavigationBooleans(ArchitectureSource[] sources)
    {
        // Keep all five binding names, including generated declarations, unique and present.
        // Getters have no storage unless they directly read a bool field or use an auto-accessor.
        // Unrecognized property bodies are conservatively counted.
        // This remains a lexical inventory; it does not follow getter method calls semantically.
        string[] navigationNames = ["IsWorkspaceActive", "IsHowToUseActive", "IsDevActive", "IsSimulationActive", "IsCoordinateActive"];
        var shell = sources.Where(static source => source.Path.StartsWith("src/FreeformHelper.UI/ViewModels/", StringComparison.Ordinal)).ToArray();
        var booleanFields = shell.SelectMany(static source => source.Fields("ShellViewModel", instanceOnly: false))
            .Where(static field => field.Type == "bool").Select(static field => field.Name).ToArray();
        var navigation = new List<string>();
        foreach (string name in navigationNames)
        {
            string declaration = @"\bpublic\s+bool\s+" + Regex.Escape(name) + @"\s*";
            var entries = shell.SelectMany(source =>
                source.Fields("ShellViewModel").Where(field => field.Type == "bool" &&
                    field.Name == "_" + char.ToLowerInvariant(name[0]) + name[1..] &&
                    ArchitectureSource.Pattern(@"\[\s*ObservableProperty\s*\]\s*private\s+bool\s+$")
                        .IsMatch(source.Code[..field.Offset]))
                    .Select(field => source.Location(field.Offset))
                    .Concat(source.Find(declaration + @"(?=\{|=>)", "ShellViewModel"))).ToArray();
            if (entries.Length != 1)
            {
                throw new InvalidOperationException($"shellNavigationBooleans definition: expected one bool property {name}, found {entries.Length}.");
            }

            string propertyPattern = declaration + @"(?<body>=>[^;{}]*;|\{(?:[^{}]|\{(?<depth>)|\}(?<-depth>))*(?(depth)(?!))\})";
            var bodies = shell.SelectMany(source => ArchitectureSource.Pattern(propertyPattern).Matches(source.Code)
                .Where(match => entries.Contains(source.Location(match.Index), StringComparer.Ordinal))
                .Select(static match => match.Groups["body"].Value)).ToArray();
            bool hasAutoAccessor = bodies.Any(body => ArchitectureSource.Pattern(@"\b(?:get|set|init)\s*;").IsMatch(body));
            bool readsBooleanField = booleanFields.Any(field => bodies.Any(body =>
                ArchitectureSource.Pattern(@"\b" + Regex.Escape(field) + @"\b").IsMatch(body)));
            if (bodies.Length != 1 || hasAutoAccessor || readsBooleanField)
            {
                navigation.Add(entries[0]);
            }
        }

        return navigation.Order(StringComparer.Ordinal).ToArray();
    }

    [Theory]
    [InlineData("public bool IsWorkspaceActive => _page == ShellPage.Workspace;", 0)]
    [InlineData("public bool IsWorkspaceActive { get { return _page == ShellPage.Workspace; } }", 0)]
    [InlineData("public bool IsWorkspaceActive { get; set; }", 1)]
    [InlineData("public bool IsWorkspaceActive { get; } = true;", 1)]
    [InlineData("[ObservableProperty] private bool _isWorkspaceActive;", 1)]
    [InlineData("private bool _workspace;\npublic bool IsWorkspaceActive => _workspace;", 1)]
    [InlineData("private bool _workspace;\npublic bool IsWorkspaceActive { get => _workspace; set => _workspace = value; }", 1)]
    public void ShellNavigationMetric_DeclarationShape_CountsStoredBooleans(string workspaceMembers, int expectedCount)
    {
        var source = CreateNavigationMetricFixture(workspaceMembers);

        var measured = MeasureShellNavigationBooleans([source]);

        Assert.Equal(expectedCount, measured.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("public bool IsWorkspaceActive => true;\npublic bool IsWorkspaceActive => false;")]
    public void ShellNavigationMetric_MissingOrDuplicateBindingName_RejectsDefinition(string workspaceMembers)
    {
        var source = CreateNavigationMetricFixture(workspaceMembers);

        var exception = Assert.Throws<InvalidOperationException>(() => MeasureShellNavigationBooleans([source]));

        Assert.Contains("expected one bool property IsWorkspaceActive", exception.Message, StringComparison.Ordinal);
    }

    private static ArchitectureSource CreateNavigationMetricFixture(string workspaceMembers) => new(
        "src/FreeformHelper.UI/ViewModels/fixture.cs",
        $$"""
        class ShellViewModel
        {
            private ShellPage _page;
            {{workspaceMembers}}
            public bool IsHowToUseActive => _page == ShellPage.HowToUse;
            public bool IsDevActive => _page == ShellPage.Dev;
            public bool IsSimulationActive => _page == ShellPage.Simulation;
            public bool IsCoordinateActive => _page == ShellPage.Coordinate;
        }
        """);

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
    public void AsyncVoidMetric_CountsPlainAndGenericDeclarations()
    {
        var source = new ArchitectureSource("fixture.cs", """
            class Handlers
            {
                // async void FakeInComment() { }
                private string _text = "async void FakeInString() { }";
                async void Plain() { }
                private static async void Generic<T>(T value) { }
                internal async void Constrained<TFirst, TSecond>() where TFirst : class { }
                async Task NotCounted() { await Task.Yield(); }
            }
            """);
        Assert.Equal(3, source.Find(AsyncVoidPattern).Length);
    }

    [Fact]

    public void AsyncVoidLambdaMetric_CountsEventSubscriptionsOnly()

    {

        var source = new ArchitectureSource("fixture.cs", """

            class Wiring

            {

                void Wire(Canvas canvas, Func<Task> run)

                {

                    // canvas.Opened += async (_, e) => { };

                    canvas.Opened += async (_, e) => { await Task.Yield(); };

                    canvas.Closed -= async (_, e) => { await Task.Yield(); };

                    canvas.Moved +=  async delegate { await Task.Yield(); };

                    run = async () => await Task.Yield();

                    Schedule(async () => await Task.Yield());

                }

            }

            """);

        Assert.Equal(3, source.Find(AsyncVoidLambdaPattern).Length);

    }


    [Fact]
    public void FieldInventory_FindsEveryDeclaratorAfterComparisonsAndGenerics()
    {
        var source = new ArchitectureSource("fixture.cs", """
            class FreeformHelperViewModel
            {
                private bool _suppressA = true, _suppressB;
                private bool _suppressC = 1 < 2, _suppressD;
                private bool _suppressE = 3 > 2, _suppressF;
                private object _mapA = new Dictionary<int, int>(), _mapB;
                private object _nested = new Dictionary<int, List<int>>(), _after;
            }
            """);
        Assert.Equal(
            ["_suppressA", "_suppressB", "_suppressC", "_suppressD", "_suppressE", "_suppressF", "_mapA", "_mapB", "_nested", "_after"],
            source.Fields("FreeformHelperViewModel").Select(static field => field.Name));
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

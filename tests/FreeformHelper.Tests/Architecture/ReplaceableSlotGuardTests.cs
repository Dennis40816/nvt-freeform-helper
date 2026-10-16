using System.Text.RegularExpressions;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests.Architecture;

public sealed class ReplaceableSlotGuardTests
{
    // A view model keeps its replaceable collaborators in a get-only property that holds a "...Slot" object, and
    // the slot has one settable property (for example Runner or Builder) that production leaves at its default.
    // Only tests replace it. This lexical guard finds the settable properties of every class whose name ends in
    // "Slot" and fails any file under src that assigns one of them, through a member access or an alias of the
    // slot, a null-coalescing assignment, a tuple deconstruction or an object initializer (typed or target-typed).
    internal static string[] FindSlotPropertyNames(ArchitectureSource[] sources) =>
        sources.SelectMany(static source => ArchitectureSource.Pattern(
                    @"\bclass\s+\w+Slot\b[^{;]*\{(?<body>(?:[^{}]|\{(?<depth>)|\}(?<-depth>))*(?(depth)(?!)))\}")
                .Matches(source.Code)
                .SelectMany(static slot => ArchitectureSource.Pattern(
                        @"\b(?:public|internal)\s+[\w<>?.,\[\]\s]+?\s+(?<name>\w+)\s*\{\s*get\s*;\s*(?:set|init)\s*;\s*\}")
                    .Matches(slot.Groups["body"].Value)
                    .Select(static property => property.Groups["name"].Value)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    internal static string[] FindSlotAssignments(ArchitectureSource[] sources)
    {
        var names = FindSlotPropertyNames(sources);
        if (names.Length == 0)
        {
            return [];
        }

        var alternation = string.Join("|", names.Select(Regex.Escape));
        var assignment = ArchitectureSource.Pattern(
            @"\.\s*(?:" + alternation + @")\s*(?:\?\?)?=(?![=>])" +
            @"|\bnew\s*(?:\w*Slot\b)?\s*(?:\([^()]*\))?\s*\{(?:[^{}]|\{[^{}]*\})*?\b(?:" + alternation + @")\s*=(?![=>])" +
            @"|\([^()]*\.\s*(?:" + alternation + @")\s*[,)][^()]*\)\s*=(?![=>])");
        return sources.Where(source => assignment.IsMatch(source.Code))
            .Select(static source => source.Path)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    [Fact]
    public void SourceFiles_DoNotReplaceTheSlotProperties()
    {
        var sources = ArchitectureSource.Read(TestPaths.RepoRoot, ".cs");

        var names = FindSlotPropertyNames(sources);
        var offenders = FindSlotAssignments(sources);

        Assert.Contains("Runner", names);
        Assert.Contains("Builder", names);
        Assert.True(
            offenders.Length == 0,
            "Only tests may replace the properties of a slot (" + string.Join(", ", names) + "). Assigned in: " +
            string.Join(", ", offenders));
    }

    [Theory]
    [InlineData("BackgroundWork.Runner = other;", true)]
    [InlineData("vm.BackgroundWork.Runner  =  other;", true)]
    [InlineData("var slot = BackgroundWork; slot.Runner = other;", true)]
    [InlineData("var slot = BackgroundWork; slot.Runner ??= other;", true)]
    [InlineData("var slot = new SampleSlot { Runner = other };", true)]
    [InlineData("var slot = new SampleSlot() { Runner = other };", true)]
    [InlineData("SampleSlot slot = new() { Runner = other };", true)]
    [InlineData("var slot = new SampleSlot { Count = new Inner { A = 1 }, Runner = other };", true)]
    [InlineData("var list = new List<int> { Runner };", false)]
    [InlineData("(slot.Runner, count) = (other, 1);", true)]
    [InlineData("TraceBuilder.Builder = other;", false)]
    [InlineData("Runner = other;", false)]
    [InlineData("var same = BackgroundWork.Runner == other;", false)]
    [InlineData("Func<int> f = () => BackgroundWork.Runner is null ? 1 : 2;", false)]
    [InlineData("// BackgroundWork.Runner = other;\nvar s = \"BackgroundWork.Runner = other\";", false)]
    public void SlotAssignmentScan_RecognizesAssignmentsOnly(string statement, bool expected)
    {
        var slot = "internal sealed class SampleSlot { public Sample Runner { get; set; } = Default; public int Count { get; } }";
        var user = "class User { void M() { " + statement + " } }";
        var offenders = FindSlotAssignments(
        [
            new ArchitectureSource("src/Slot.cs", slot),
            new ArchitectureSource("src/User.cs", user),
        ]);

        Assert.Equal(expected, offenders.Contains("src/User.cs"));
    }

    [Theory]
    [InlineData("internal sealed class AlphaSlot { public IThing Builder { get; set; } = Default; }", "Builder")]
    [InlineData("public sealed class BetaSlot : IThing { internal IThing Runner { get; init; } }", "Runner")]
    [InlineData("internal sealed class GammaSlot { public IThing Value { get; } = new(); }", "")]
    [InlineData("internal sealed class Gamma { public IThing Value { get; set; } }", "")]
    public void SlotPropertyScan_FindsSettablePropertiesOfSlotClassesOnly(string declaration, string expected)
    {
        var names = FindSlotPropertyNames([new ArchitectureSource("src/Slot.cs", declaration)]);

        var expectedNames = expected.Length == 0 ? Array.Empty<string>() : new[] { expected };

        Assert.Equal(expectedNames, names);
    }
}

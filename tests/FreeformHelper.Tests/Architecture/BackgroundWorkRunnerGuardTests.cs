using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests.Architecture;

public sealed class BackgroundWorkRunnerGuardTests
{
    // The view models start their fire-and-forget work through a BackgroundWorkRunnerSlot. Its Runner is the
    // production fire-and-forget runner and only tests replace it. This lexical guard fails any source file
    // under src that assigns a member named Runner, including through an alias of the slot.
    internal static string[] FindRunnerAssignments(ArchitectureSource[] sources) =>
        sources.Where(static source => ArchitectureSource.Pattern(@"\bRunner\s*=(?![=>])").IsMatch(source.Code))
            .Select(static source => source.Path)
            .Order(StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public void SourceFiles_DoNotReplaceTheBackgroundWorkRunner()
    {
        var offenders = FindRunnerAssignments(ArchitectureSource.Read(TestPaths.RepoRoot, ".cs"));

        Assert.True(
            offenders.Length == 0,
            "Only tests may replace BackgroundWork.Runner. Assigned in: " + string.Join(", ", offenders));
    }

    [Theory]
    [InlineData("class C { void M() { BackgroundWork.Runner = other; } }", true)]
    [InlineData("class C { void M() { vm.BackgroundWork.Runner  =  other; } }", true)]
    [InlineData("class C { void M() { var slot = BackgroundWork; slot.Runner = other; } }", true)]
    [InlineData("class C { void M() { var slot = new BackgroundWorkRunnerSlot { Runner = other }; } }", true)]
    [InlineData("class C { public IBackgroundWorkRunner Runner { get; set; } = Default; }", false)]
    [InlineData("class C { bool M() => BackgroundWork.Runner == other; }", false)]
    [InlineData("class C { void M() { Func<int> f = () => Runner; BackgroundWork.Run(\"name\", work); } }", false)]
    [InlineData("// BackgroundWork.Runner = other;\nclass C { string s = \"BackgroundWork.Runner = other\"; }", false)]
    public void RunnerAssignmentScan_RecognizesAssignmentsOnly(string text, bool expected)
    {
        var offenders = FindRunnerAssignments([new ArchitectureSource("src/Sample.cs", text)]);

        Assert.Equal(expected, offenders.Length == 1);
    }
}

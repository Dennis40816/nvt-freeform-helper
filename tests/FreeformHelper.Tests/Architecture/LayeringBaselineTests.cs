using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests.Architecture;

public sealed class LayeringBaselineTests
{
    // File-level lexical dependency inventory: imports (including aliases/static imports)
    // and fully-qualified namespace references count, even if an import is unused.
    // Comments/literals and bin/obj are excluded. Each offending file counts once per rule.
    internal static Dictionary<string, string[]> Measure(ArchitectureSource[] sources)
    {
        var rules = new Dictionary<string, string[]>(StringComparer.Ordinal);
        void Rule(string key, Func<ArchitectureSource, bool> predicate) => rules.Add(key,
            sources.Where(predicate).Select(static source => source.Path).Order(StringComparer.Ordinal).ToArray());
        static bool Core(ArchitectureSource source) =>
            source.Path.StartsWith("src/FreeformHelper.Domain/", StringComparison.Ordinal) ||
            source.Path.StartsWith("src/FreeformHelper.Application/", StringComparison.Ordinal);
        static bool UiFolder(ArchitectureSource source, string folder) =>
            source.Path.StartsWith("src/FreeformHelper.UI/" + folder + "/", StringComparison.Ordinal);

        Rule("domainAndApplicationAvalonia", source => Core(source) && source.References("Avalonia"));
        Rule("domainAndApplicationPlatformIo", source => Core(source) && source.CallsPlatformIo());
        Rule("viewModelsAvalonia", source => UiFolder(source, "ViewModels") && source.References("Avalonia"));
        Rule("controlsViewModels", source => UiFolder(source, "Controls") && source.References("FreeformHelper.UI.ViewModels"));
        Rule("viewModelsControls", source => UiFolder(source, "ViewModels") && source.References("FreeformHelper.UI.Controls"));
        Rule("servicesViewModels", source => UiFolder(source, "Services") && source.References("FreeformHelper.UI.ViewModels"));
        Rule("viewModelsServices", source => UiFolder(source, "ViewModels") && source.References("FreeformHelper.UI.Services"));
        return rules;
    }

    [Fact]
    public void LayerDependencies_MatchOffenderListsAndRequireRatchetAfterCleanup()
    {
        var baseline = ArchitectureBaseline.Load(TestPaths.RepoRoot);
        var measured = Measure(ArchitectureSource.Read(TestPaths.RepoRoot, ".cs"));
        Assert.Empty(baseline.Layering["domainAndApplicationAvalonia"]);
        Assert.Empty(measured["domainAndApplicationAvalonia"]);
        Assert.Equal(baseline.Layering.Keys.Order(StringComparer.Ordinal), measured.Keys.Order(StringComparer.Ordinal));
        var errors = measured.SelectMany(entry => ArchitectureBaseline.CompareFiles(
            entry.Key, baseline.Layering[entry.Key], entry.Value)).ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors));
    }

    [Theory]
    [InlineData("using IO = System.IO; class C { void M() { IO.File.ReadAllText(path); } }", true)]
    [InlineData("class C { void M() { global::System.IO.Directory.CreateDirectory(path); } }", true)]
    [InlineData("using P = System.Diagnostics.Process; class C { void M() { P.Start(path); } }", true)]
    [InlineData("using static System.IO.File; class C { void M() { ReadAllText(path); } }", true)]
    [InlineData("class C { void M() { File.ReadAllBytes(path); } }", true)]
    [InlineData("using System.Diagnostics; class C { private readonly Process _process = new(); bool M() => _process.Start(); }", true)]
    [InlineData("class C { private Process? _process; }", true)]
    [InlineData("class C { void M(Process process) { } }", true)]
    [InlineData("class C { System.Diagnostics.Process process; }", true)]
    [InlineData("class C { System.Collections.Generic.List<Process> items; }", true)]
    [InlineData("class C { void Process(int value) { } void M() { Process(1); } }", false)]
    [InlineData("class Process { }", false)]
    [InlineData("// File.ReadAllText(path);\nclass C { string s = \"System.IO.File.ReadAllText(path)\"; }", false)]
    public void PlatformIoScan_RecognizesImportsAndQualifiedCalls(string text, bool expected)
    {
        Assert.Equal(expected, new ArchitectureSource("fixture.cs", text).CallsPlatformIo());
    }

    [Fact]
    public void NamespaceScan_DistinguishesCoreAdaptersAndDetectsQualifiedReferences()
    {
        Assert.False(new ArchitectureSource("fixture.cs", "using Nvt.Core.Avalonia.Threading;").References("Avalonia"));
        Assert.True(new ArchitectureSource("fixture.cs", "using A = global::Avalonia.Media;").References("Avalonia"));
        Assert.True(new ArchitectureSource("fixture.cs", "global::FreeformHelper.UI.ViewModels.ShellViewModel vm;")
            .References("FreeformHelper.UI.ViewModels"));
    }

    [Fact]
    public void NamespaceScan_DetectsNamespacesWrittenRelativeToTheEnclosingNamespace()
    {
        var control = new ArchitectureSource("fixture.cs", """
            namespace FreeformHelper.UI.Controls;

            public sealed class SampleControl
            {
                public ViewModels.NotchApplySimulationCanvasViewMode Mode { get; set; }
            }
            """);
        Assert.True(control.References("FreeformHelper.UI.ViewModels"));
        Assert.False(control.References("FreeformHelper.UI.Services"));

        var viewModel = new ArchitectureSource("fixture.cs", """
            namespace FreeformHelper.UI.ViewModels;

            public sealed class SampleViewModel
            {
                private readonly Services.GridBuildService _grid = new();
                private int viewModels;
                public int Count => viewModels.GetHashCode();
            }
            """);
        Assert.True(viewModel.References("FreeformHelper.UI.Services"));
        Assert.False(viewModel.References("FreeformHelper.UI.Controls"));
    }

    [Fact]
    public void FileRatchet_RejectsBothReplacementOffendersAndStaleCleanupEntries()
    {
        var errors = ArchitectureBaseline.CompareFiles("fixture", ["old.cs"], ["new.cs"]);
        Assert.Contains(errors, static error => error.Contains("new offenders: new.cs", StringComparison.Ordinal));
        Assert.Contains(errors, static error => error.Contains("lower the baseline in debt-baseline.json to 1", StringComparison.Ordinal));
        Assert.Empty(ArchitectureBaseline.CompareFiles("fixture", ["same.cs"], ["same.cs"]));
    }
}

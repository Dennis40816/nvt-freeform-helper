using System.Xml.Linq;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests.Architecture;

public sealed class CompiledBindingsGuardTests
{
    [Fact]
    public void BindingFiles_DeclareRootDataTypeOrExplicitBaselinedException()
    {
        string repoRoot = TestPaths.RepoRoot;
        var project = XDocument.Load(Path.Combine(repoRoot, "src/FreeformHelper.UI/FreeformHelper.UI.csproj"));
        Assert.Equal("true", Assert.Single(project.Descendants("AvaloniaUseCompiledBindingsByDefault")).Value);
        var exceptions = new List<string>();
        var errors = new List<string>();
        foreach (var source in ArchitectureSource.Read(repoRoot, ".axaml"))
        {
            var root = ArchitectureBaseline.XamlRoot(source);
            bool binds = root.DescendantsAndSelf().Attributes().Any(static attribute =>
                attribute.Value.Contains("{Binding", StringComparison.Ordinal));
            bool typed = !string.IsNullOrWhiteSpace((string?)root.Attribute(ArchitectureBaseline.XamlNamespace + "DataType"));
            bool optedOut = string.Equals((string?)root.Attribute(ArchitectureBaseline.XamlNamespace + "CompileBindings"),
                "False", StringComparison.OrdinalIgnoreCase);
            if (binds && !typed && !optedOut)
            {
                errors.Add($"{source.Path}: bindings require root x:DataType or x:CompileBindings=\"False\".");
            }

            if (typed && root.DescendantsAndSelf().Attributes(ArchitectureBaseline.XamlNamespace + "CompileBindings")
                .Any(static attribute => string.Equals(attribute.Value, "False", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"{source.Path}: a typed root must use compiled bindings throughout.");
            }

            if (optedOut)
            {
                exceptions.Add(source.Path);
            }
        }

        errors.AddRange(ArchitectureBaseline.CompareFiles("compiledBindingExceptions",
            ArchitectureBaseline.Load(repoRoot).CompiledBindingExceptions, exceptions.Order(StringComparer.Ordinal).ToArray()));
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }
}

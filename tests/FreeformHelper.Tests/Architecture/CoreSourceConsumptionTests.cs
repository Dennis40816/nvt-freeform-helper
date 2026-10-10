using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using Xunit.Sdk;

namespace FreeformHelper.Tests.Architecture;

public sealed class CoreSourceConsumptionTests
{
    [Fact]
    public void CoreSource_CanonicalCopy_MatchesManifestWithoutCorePackageReference()
    {
        var project = XDocument.Load(TestPaths.FromRepo(
            "src", "FreeformHelper.CoreSource", "FreeformHelper.CoreSource.csproj"));
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(TestPaths.FromRepo(
            "src", "FreeformHelper.CoreSource", "manifest.json")));
        var entry = Assert.Single(manifest.RootElement.GetProperty("files").EnumerateArray());
        var bytes = File.ReadAllBytes(TestPaths.FromRepo(ArchitectureSource.VerbatimCoreSourcePath));

        Assert.DoesNotContain(project.Descendants("PackageReference"), reference =>
            string.Equals(reference.Attribute("Include")?.Value, "Nvt.Core", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.Equal(1, manifest.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("src/Nvt.Core/Threading/UiEventRunner.cs", entry.GetProperty("path").GetString());
        Assert.Equal("NVT_CORE_SOURCE_CONSUMPTION", entry.GetProperty("compileSymbol").GetString());
        Assert.Equal(entry.GetProperty("byteLength").GetInt32(), bytes.Length);
        AssertSourceHash(bytes, entry.GetProperty("sha256").GetString()!);
    }

    [Fact]
    public void CoreSource_WrongHash_FailsIntegrityGuard()
    {
        var bytes = File.ReadAllBytes(TestPaths.FromRepo(ArchitectureSource.VerbatimCoreSourcePath));

        Assert.Throws<EqualException>(() => AssertSourceHash(bytes, new string('0', 64)));
    }

    [Theory]
    [InlineData("src/FreeformHelper.CoreSource/UiEventRunner.cs", true)]
    [InlineData("src/FreeformHelper.CoreSource/Other.cs", false)]
    [InlineData("src/FreeformHelper.UI/UiEventRunner.cs", false)]
    [InlineData("src/FreeformHelper.UI/Vendor/Core/UiEventRunner.cs", false)]
    [InlineData("src/FreeformHelper.CoreSource/Nested/UiEventRunner.cs", false)]
    [InlineData("src/FreeformHelper.CoreSource/manifest.json", false)]
    public void Scanner_SourcePath_ExcludesOnlyCanonicalCopy(string path, bool excluded)
    {
        Assert.Equal(excluded, ArchitectureSource.IsVerbatimCoreSource(path));
    }

    private static void AssertSourceHash(byte[] bytes, string expectedHash) =>
        Assert.Equal(expectedHash, Convert.ToHexStringLower(SHA256.HashData(bytes)));
}

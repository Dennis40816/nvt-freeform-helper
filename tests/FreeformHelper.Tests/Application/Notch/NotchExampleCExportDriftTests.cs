using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using FreeformHelper.Application.Export;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExampleCExportDriftTests
{
    [ExampleDataFact]
    public void Boe3635_SignedGoldenManifest_MatchesCheckedInArtifacts()
    {
        var repoRoot = TestPaths.RepoRoot;
        var manifestPath = Path.Combine(repoRoot, "example", "BOE36.35", "notch_export_golden_manifest.json");
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var manifest = document.RootElement;

        AssertSignedFile(
            ResolveManifestPath(repoRoot, manifest.GetProperty("project").GetProperty("path").GetString()),
            manifest.GetProperty("project").GetProperty("sha256").GetString());
        AssertSignedFile(
            ResolveManifestPath(repoRoot, manifest.GetProperty("regularMask").GetProperty("path").GetString()),
            manifest.GetProperty("regularMask").GetProperty("sha256").GetString());
        AssertSignedOutput(repoRoot, manifest.GetProperty("outputs").GetProperty("v21"));
        AssertSignedOutput(repoRoot, manifest.GetProperty("outputs").GetProperty("v22"));
    }

    [Fact]
    public void NormalizeText_OnlyNormalizesLineEndingsAndSingleEofNewline()
    {
        Assert.Equal("a\nb", NormalizeText("a\r\nb\n"));
        Assert.Equal("a\nb", NormalizeText("a\rb"));
        Assert.Equal("a \t", NormalizeText("a \t"));
        Assert.Equal("a\n", NormalizeText("a\n\n"));
    }

    [ExampleDataTheory]
    [InlineData(NotchAlgorithmVersion.V21, "notch_export_v21_current.c")]
    [InlineData(NotchAlgorithmVersion.V22, "notch_export_v22_current.c")]
    public async Task Boe3635_CheckedInCExample_MatchesCurrentExportPipeline(
        NotchAlgorithmVersion version,
        string exampleFileName)
    {
        var repoRoot = TestPaths.RepoRoot;
        var projectPath = Path.Combine(repoRoot, "example", "BOE36.35", "project_3635.json");
        var examplePath = Path.Combine(repoRoot, "example", "BOE36.35", exampleFileName);
        var vm = new FreeformHelperViewModel
        {
            PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
        };

        await vm.LoadProjectCommand.ExecuteAsync(null);
        var cad = NotchCurrentWorkflowTableTestHelper.GetExportCadPadSet(vm);
        var grid = GetPrivateField<RegularGrid>(vm, "_grid");
        var project = GetPrivateField<ProjectFile>(vm, "_projectFile");
        var activeRegularPadIds = GetActiveRegularPadIds(vm);

        var exportTable = await NotchCurrentWorkflowTableTestHelper.GenerateExportTableAsync(
            vm,
            $"Example C drift {version.ToDisplayLabel()}");
        var versionTable = new NotchTable(exportTable.Rows.Where(row => row.Version == version).ToArray());
        Assert.NotEmpty(versionTable.Rows);

        var actualExport = NotchTableExporter.ExportAsCInitializer(
            versionTable,
            cad,
            grid,
            project.Settings,
            project.Settings.Notch.ExportProfile,
            activeRegularPadIds);

        var expected = NormalizeText(File.ReadAllText(examplePath));
        var actual = NormalizeText(actualExport);
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            var driftPath = Path.Combine(
                repoRoot,
                "build",
                $"notch-example-drift-{version.ToDisplayLabel()}.actual.c");
            Directory.CreateDirectory(Path.GetDirectoryName(driftPath)!);
            File.WriteAllText(driftPath, actualExport);
        }

        Assert.Equal(expected, actual);
    }

    private static string NormalizeText(string text)
    {
        var normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        return normalized.EndsWith('\n')
            ? normalized[..^1]
            : normalized;
    }

    private static void AssertSignedOutput(string repoRoot, JsonElement contract)
    {
        var path = ResolveManifestPath(repoRoot, contract.GetProperty("path").GetString());
        var expectedBytes = contract.GetProperty("bytes").GetInt64();
        Assert.Equal(expectedBytes, new FileInfo(path).Length);
        AssertSignedFile(path, contract.GetProperty("sha256").GetString());

        var expectedNodes = contract.GetProperty("nodes").GetInt32();
        var text = NormalizeText(File.ReadAllText(path));
        Assert.Contains(
            $"#define USER_NHC_NODE_NUM        ({expectedNodes}u)",
            text,
            StringComparison.Ordinal);
    }

    private static void AssertSignedFile(string path, string? expectedSha256)
    {
        Assert.False(string.IsNullOrWhiteSpace(expectedSha256));
        using var stream = File.OpenRead(path);
        var actualSha256 = Convert.ToHexString(SHA256.HashData(stream));
        Assert.Equal(expectedSha256, actualSha256);
    }

    private static string ResolveManifestPath(string repoRoot, string? relativePath)
    {
        Assert.False(string.IsNullOrWhiteSpace(relativePath));
        return Path.GetFullPath(Path.Combine(
            repoRoot,
            relativePath!.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static T GetPrivateField<T>(FreeformHelperViewModel vm, string fieldName)
    {
        var field = typeof(FreeformHelperViewModel).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(vm));
    }

    private static IReadOnlySet<int>? GetActiveRegularPadIds(FreeformHelperViewModel vm)
    {
        var method = typeof(FreeformHelperViewModel).GetMethod(
            "GetActiveRegularPadIdsForNotchComputation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(vm, null);
        return result is null
            ? null
            : Assert.IsAssignableFrom<IReadOnlySet<int>>(result);
    }
}

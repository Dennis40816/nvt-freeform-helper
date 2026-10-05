using System.Reflection;
using System.Text.Json.Nodes;
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ExampleProjectFixtureTests
{
    [ExampleDataFact]
    public async Task LoadProject_3635_RequiresLoadedAndEnabledRegularVisibilityMask()
    {
        var projectPath = Path.Combine(TestPaths.RepoRoot, "example", "BOE36.35", "project_3635.json");
        using var fixture = ExampleProjectFixture.Create(projectPath);
        var vm = new FreeformHelperViewModel
        {
            PickLoadProjectPathAsync = () => Task.FromResult<string?>(fixture.ProjectPath),
        };

        await vm.LoadProjectCommand.ExecuteAsync(null);

        var maskField = typeof(FreeformHelperViewModel).GetField(
            "_regularVisibilityMaskResult",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(maskField);
        var mask = Assert.IsType<RegularVisibilityMaskResult>(maskField!.GetValue(vm));
        Assert.Equal(RegularVisibilityMaskStatus.Loaded, mask.Status);
        Assert.True(mask.ActiveRegularCount > 0);
        Assert.True(vm.IsRegularVisibilityMaskEnabled);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Create_RewritesOnlyExistingMaskSourcePath_WhenSiblingCsvExists(bool hasSiblingCsv, bool hasSourcePath)
    {
        var sourceDirectory = Directory.CreateTempSubdirectory("freeform-helper-fixture-test-").FullName;
        try
        {
            var project = JsonNode.Parse("""
                {
                  "version": 3,
                  "embeddedDxf": "opaque-geometry",
                  "settings": { "enabled": true, "ratio": 3.125, "values": [1, null, false, "text"] },
                  "uiSnapshot": {
                    "import": {
                      "regularSignalMaskSourcePath": "missing-original.csv",
                      "regularVisibilityMaskSourcePath": "unchanged-mask.csv",
                      "useRegularSignalMask": true,
                      "extra": { "value": 42 }
                    },
                    "selection": [2, 4]
                  },
                  "unknown": { "preserve": "value" }
                }
                """)!;
            if (!hasSourcePath)
            {
                project["uiSnapshot"]!["import"]!.AsObject().Remove("regularSignalMaskSourcePath");
            }

            var projectPath = Path.Combine(sourceDirectory, "project.json");
            var originalText = $"\r\n  {project.ToJsonString()}\r\n";
            File.WriteAllText(projectPath, originalText);
            var csvPath = Path.Combine(sourceDirectory, "SeeRegular.csv");
            if (hasSiblingCsv)
            {
                File.WriteAllText(csvPath, "1,0");
            }

            using var fixture = ExampleProjectFixture.Create(projectPath);
            var actualText = File.ReadAllText(fixture.ProjectPath);
            var expected = project.DeepClone();
            if (hasSiblingCsv && hasSourcePath)
            {
                expected["uiSnapshot"]!["import"]!["regularSignalMaskSourcePath"] = csvPath;
            }
            else
            {
                Assert.Equal(originalText, actualText);
            }

            Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(actualText)));
            Assert.Equal(originalText, File.ReadAllText(projectPath));
        }
        finally
        {
            Directory.Delete(sourceDirectory, recursive: true);
        }
    }

    [Fact]
    public void Create_UsesUniqueTemporaryFolders_AndDisposeDeletesWorkingCopy()
    {
        var sourceDirectory = Directory.CreateTempSubdirectory("freeform-helper-fixture-test-").FullName;
        try
        {
            var projectPath = Path.Combine(sourceDirectory, "project.json");
            File.WriteAllText(projectPath, "{}");
            using var first = ExampleProjectFixture.Create(projectPath);
            using var second = ExampleProjectFixture.Create(projectPath);
            var firstDirectory = Path.GetDirectoryName(first.ProjectPath)!;
            var secondDirectory = Path.GetDirectoryName(second.ProjectPath)!;

            Assert.NotEqual(firstDirectory, secondDirectory);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(firstDirectory));
            Assert.Equal("project.json", Path.GetFileName(first.ProjectPath));
            Assert.True(File.Exists(first.ProjectPath));
            Assert.True(File.Exists(second.ProjectPath));

            first.Dispose();
            second.Dispose();

            Assert.False(Directory.Exists(firstDirectory));
            Assert.False(Directory.Exists(secondDirectory));
            Assert.True(File.Exists(projectPath));
        }
        finally
        {
            Directory.Delete(sourceDirectory, recursive: true);
        }
    }
}

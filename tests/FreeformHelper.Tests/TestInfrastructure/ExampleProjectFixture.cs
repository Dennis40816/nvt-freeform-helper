using System.Text.Json.Nodes;

namespace FreeformHelper.Tests.TestInfrastructure;

internal sealed class ExampleProjectFixture : IDisposable
{
    private readonly string _temporaryDirectory;

    private ExampleProjectFixture(string temporaryDirectory, string projectPath)
    {
        _temporaryDirectory = temporaryDirectory;
        ProjectPath = projectPath;
    }

    public string ProjectPath { get; }

    public static ExampleProjectFixture Create(string projectPath)
    {
        var originalProjectPath = Path.GetFullPath(projectPath);
        var projectText = File.ReadAllText(originalProjectPath);
        var project = JsonNode.Parse(projectText);
        var maskPath = Path.Combine(Path.GetDirectoryName(originalProjectPath)!, "SeeRegular.csv");
        if (project?["uiSnapshot"]?["import"] is JsonObject import &&
            import.ContainsKey("regularSignalMaskSourcePath") && File.Exists(maskPath))
        {
            import["regularSignalMaskSourcePath"] = maskPath;
            projectText = project.ToJsonString();
        }

        var temporaryDirectory = Directory.CreateTempSubdirectory("freeform-helper-example-").FullName;
        var fixture = new ExampleProjectFixture(
            temporaryDirectory,
            Path.Combine(temporaryDirectory, Path.GetFileName(originalProjectPath)));
        try
        {
            File.WriteAllText(fixture.ProjectPath, projectText);
            return fixture;
        }
        catch
        {
            fixture.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }
}

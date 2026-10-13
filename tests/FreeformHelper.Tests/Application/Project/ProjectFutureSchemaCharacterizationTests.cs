using System.Text.Json;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the project store does with a file whose schema version is newer than the one this
/// build writes (issue #64). The migrator accepts any non-blank
/// version. Fields that this build does not know are kept at the file root only, so nested unknown
/// fields are dropped on the next save while the original version label stays. These tests
/// describe the current result, not the wanted one. A fix that rejects or preserves future
/// versions changes the expected values here and must say so in its pull request.
/// </summary>
public sealed class ProjectFutureSchemaCharacterizationTests
{
    private const string FutureProjectJson = """
        {
          "schemaVersion": "3",
          "futureRootField": { "kept": true },
          "settings": {
            "grid": { "xChannels": 24, "yChannels": 18, "futureGridMode": 7 }
          },
          "uiSnapshot": {
            "view": { "futureViewField": "dark" }
          }
        }
        """;

    [Fact]
    public void Load_FutureSchemaVersion_KeepsTheVersionLabelAndReadsKnownFields()
    {
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "future.json");
            WriteFileContent(path, FutureProjectJson);

            var loaded = JsonProjectStore.Load(path);

            Assert.Equal("3", loaded.SchemaVersion);
            Assert.Equal(24, loaded.Settings.Grid.XChannels);
            Assert.Equal(18, loaded.Settings.Grid.YChannels);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Save_AfterLoadingFutureSchemaVersion_DropsUnknownNestedFieldsAndKeepsTheVersionLabel()
    {
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            var input = Path.Combine(directory.FullName, "future.json");
            var output = Path.Combine(directory.FullName, "resaved.json");
            WriteFileContent(input, FutureProjectJson);

            var loaded = JsonProjectStore.Load(input);
            await new JsonProjectStore().SaveAsync(output, loaded, CancellationToken.None);

            using var document = JsonDocument.Parse(ReadFileContent(output));
            var root = document.RootElement;
            Assert.Equal("3", root.GetProperty("schemaVersion").GetString());
            Assert.True(root.TryGetProperty("futureRootField", out _), "unknown root field is kept");
            Assert.False(root.GetProperty("settings").GetProperty("grid").TryGetProperty("futureGridMode", out _));
            Assert.False(root.GetProperty("uiSnapshot").GetProperty("view").TryGetProperty("futureViewField", out _));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static void WriteFileContent(string path, string text)
    {
        using var writer = new StreamWriter(path, append: false);
        writer.Write(text);
    }

    private static string ReadFileContent(string path)
    {
        using var reader = new StreamReader(path);
        return reader.ReadToEnd();
    }
}

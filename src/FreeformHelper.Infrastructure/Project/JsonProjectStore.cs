using System.Text;
using System.Text.Json;
using Nvt.Core.IO;

namespace FreeformHelper.Infrastructure.Project;

/// <summary>
/// Provides functionality to load and save <see cref="ProjectFile"/> objects to and from JSON files.
/// This class handles the serialization and deserialization of project data, including settings.
/// </summary>
public sealed class JsonProjectStore
{
    /// <summary>
    /// Specifies the JSON serialization options to use for consistent formatting and camelCase property naming.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true, // Makes the JSON output human-readable with indentation
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // Converts C# PascalCase properties to camelCase in JSON
    };

    private readonly TimeProvider _timeProvider;

    public JsonProjectStore(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    internal Func<string, ReadOnlyMemory<byte>, CancellationToken, Task> Publisher { get; set; } = AtomicOutput.WriteBytesAsync;

    /// <summary>
    /// Loads a <see cref="ProjectFile"/> from the specified JSON file path.
    /// </summary>
    /// <param name="path">The full path to the project JSON file.</param>
    /// <returns>The deserialized <see cref="ProjectFile"/> object.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the JSON parsing fails or if the loaded project settings are invalid.
    /// </exception>
    public static ProjectFile Load(string path)
    {
        var json = File.ReadAllText(path); // Read the entire JSON content from the file
        var file = JsonSerializer.Deserialize<ProjectFile>(json, Options) // Deserialize JSON to ProjectFile object
            ?? throw new InvalidOperationException("Failed to parse project file.");
        ProjectFileMigrator.MigrateInPlace(file);
        file.Settings.ValidateOrThrow(); // Validate the settings within the loaded project file
        return file;
    }

    /// <summary>
    /// Saves a <see cref="ProjectFile"/> to the specified JSON file path.
    /// </summary>
    /// <param name="path">The full path where the project JSON file will be saved.</param>
    /// <param name="file">The <see cref="ProjectFile"/> object to save.</param>
    /// <param name="cancellationToken">The token used to cancel publication.</param>
    /// <returns>A task that completes after the project file is atomically published.</returns>
    public Task SaveAsync(string path, ProjectFile file, CancellationToken cancellationToken)
    {
        file.SavedAt = _timeProvider.GetUtcNow(); // Update the 'SavedAt' timestamp from the injected clock
        ProjectFileMigrator.MigrateInPlace(file);
        file.Settings.ValidateOrThrow(); // Validate the settings before saving

        var json = JsonSerializer.Serialize(file, Options); // Serialize the ProjectFile object to JSON
        return PublishAsync(path, Encoding.UTF8.GetBytes(json), cancellationToken);
    }

    private Task PublishAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        return Publisher(path, bytes, cancellationToken);
    }
}

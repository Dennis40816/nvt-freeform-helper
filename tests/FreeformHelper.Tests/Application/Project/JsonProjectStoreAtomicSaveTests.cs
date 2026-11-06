using System.Text;
using System.Text.Json;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using Nvt.Core.IO;
using Nvt.Core.TestSupport;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class JsonProjectStoreAtomicSaveTests
{
    private static readonly DateTimeOffset _savedAt = new(2026, 10, 9, 8, 30, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public async Task SaveAsync_PublisherThrows_PreservesExistingBytesAndLeavesNoTemporaryFiles()
    {
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "project.json");
            var original = Encoding.UTF8.GetBytes("original project bytes");
            TestFiles.WriteBytes(path, original);
            var failure = new IOException("Publication failed.");
            var store = new JsonProjectStore(new ManualTimeProvider(_savedAt))
            {
                Publisher = (_, _, _) => throw failure,
            };

            var actual = await Assert.ThrowsAsync<IOException>(
                () => store.SaveAsync(path, new ProjectFile(), CancellationToken.None));

            Assert.Same(failure, actual);
            Assert.Equal(original, TestFiles.ReadBytes(path));
            Assert.Equal(path, Assert.Single(Directory.GetFileSystemEntries(directory.FullName)));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_CancelledBeforePublication_PreservesExistingBytesAndLeavesNoTemporaryFiles()
    {
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "project.json");
            var original = Encoding.UTF8.GetBytes("original project bytes");
            TestFiles.WriteBytes(path, original);
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            var store = new JsonProjectStore(new ManualTimeProvider(_savedAt));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => store.SaveAsync(path, new ProjectFile(), cancellation.Token));

            Assert.Equal(original, TestFiles.ReadBytes(path));
            Assert.Equal(path, Assert.Single(Directory.GetFileSystemEntries(directory.FullName)));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_Success_PreservesJsonBytesAndSettingsContract()
    {
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "project.json");
            var file = new ProjectFile
            {
                LastDxfPath = "panel-面板.dxf",
                Settings = new ProjectSettings
                {
                    Grid = new GridSettings { XChannels = 40, YChannels = 20 },
                    Matching = new MatchingSettings { MatchThreshold = 0.41, NearestK = 9 },
                },
            };
            var store = new JsonProjectStore(new ManualTimeProvider(_savedAt));

            await store.SaveAsync(path, file, CancellationToken.None);

            var bytes = TestFiles.ReadBytes(path);
            var expected = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(file, _options));
            Assert.Equal(expected, bytes);
            Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
            var loaded = JsonProjectStore.Load(path);
            Assert.Equal(JsonSerializer.Serialize(file.Settings, _options), JsonSerializer.Serialize(loaded.Settings, _options));
            Assert.Equal(file.LastDxfPath, loaded.LastDxfPath);
            Assert.Equal(path, Assert.Single(Directory.GetFileSystemEntries(directory.FullName)));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_SavedAt_ComesFromInjectedTimeProvider()
    {
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "project.json");
            var file = new ProjectFile { SavedAt = DateTimeOffset.MinValue };
            var store = new JsonProjectStore(new ManualTimeProvider(_savedAt));

            await store.SaveAsync(path, file, CancellationToken.None);

            Assert.Equal(_savedAt, file.SavedAt);
            Assert.Equal(_savedAt, JsonProjectStore.Load(path).SavedAt);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_Success_ReplacesExistingFileWithoutTemporaryFiles()
    {
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "project.json");
            TestFiles.WriteText(path, "original project bytes");
            var file = new ProjectFile { LastDxfPath = "replacement.dxf" };
            var store = new JsonProjectStore(new ManualTimeProvider(_savedAt));

            await store.SaveAsync(path, file, CancellationToken.None);

            Assert.Equal(file.LastDxfPath, JsonProjectStore.Load(path).LastDxfPath);
            Assert.Equal(path, Assert.Single(Directory.GetFileSystemEntries(directory.FullName)));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_WriteFailure_PreservesExistingBytesAndRemovesTemporaryFile()
    {
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "project.json");
            var original = Encoding.UTF8.GetBytes("original project bytes");
            TestFiles.WriteBytes(path, original);
            var failure = new IOException("Writing the temporary file failed.");
            var store = new JsonProjectStore(new ManualTimeProvider(_savedAt))
            {
                Publisher = (target, bytes, token) => AtomicOutput.WriteAsync(target, async (stream, cancellationToken) =>
                {
                    await stream.WriteAsync(bytes[..32], cancellationToken);
                    throw failure;
                }, token),
            };

            var actual = await Assert.ThrowsAsync<IOException>(
                () => store.SaveAsync(path, new ProjectFile(), CancellationToken.None));

            Assert.Same(failure, actual);
            Assert.Equal(original, TestFiles.ReadBytes(path));
            Assert.Equal(path, Assert.Single(Directory.GetFileSystemEntries(directory.FullName)));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}

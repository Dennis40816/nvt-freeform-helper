// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Text;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class AppGeneralSettingsRecoveryTests : IDisposable
{
    private readonly DirectoryInfo _directory = TestFiles.CreateTempDirectory();
    private string SettingsPath => Path.Combine(_directory.FullName, "app-general-settings.json");

    [Fact]
    public void TruncatedJsonIsMovedToCorruptWithoutChangingBytes()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("{\"View\":{\"ShowCad\":false")).ToArray();
        TestFiles.WriteBytes(SettingsPath, bytes);
        var store = new AppGeneralSettingsStore(SettingsPath);

        var document = store.TryLoad();

        Assert.Null(document);
        Assert.False(File.Exists(SettingsPath));
        Assert.Equal(bytes, TestFiles.ReadBytes(SettingsPath + ".corrupt"));
    }

    [Fact]
    public void EveryCorruptOriginalIsKeptInItsOwnBackup()
    {
        TestFiles.WriteText(SettingsPath + ".corrupt", "older corrupt file");
        var store = new AppGeneralSettingsStore(SettingsPath);
        var originals = new List<string> { "older corrupt file" };
        foreach (var damaged in new[] { "{\"Import\":", "{\"View\":[" })
        {
            TestFiles.WriteText(SettingsPath, damaged);
            originals.Add(damaged);

            Assert.Null(store.TryLoad());
            Assert.False(File.Exists(SettingsPath));
        }

        var backups = _directory.EnumerateFiles().Select(static file => TestFiles.ReadContent(file.FullName)).Order().ToList();
        Assert.Equal(originals.Order().ToList(), backups);
        Assert.Equal("older corrupt file", TestFiles.ReadContent(SettingsPath + ".corrupt"));
    }

    [Fact]
    public void FailedBackupBlocksTheSaveUntilTheOriginalIsPreserved()
    {
        TestFiles.WriteText(SettingsPath, "{\"Import\":");
        var bytes = TestFiles.ReadBytes(SettingsPath);
        var store = new AppGeneralSettingsStore(SettingsPath);

        using (var holder = File.Open(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Null(store.TryLoad());
            Assert.Throws<IOException>(() => store.Save(new AppGeneralSettingsDocument()));
            Assert.Equal(SettingsPath, Assert.Single(_directory.EnumerateFiles()).FullName);
        }

        Assert.Equal(bytes, TestFiles.ReadBytes(SettingsPath));

        store.Save(new AppGeneralSettingsDocument());

        Assert.Equal(bytes, TestFiles.ReadBytes(SettingsPath + ".corrupt"));
        Assert.NotNull(store.TryLoad());
    }

    [Fact]
    public void MissingSettingsCreateNoCorruptBackup()
    {
        var store = new AppGeneralSettingsStore(SettingsPath);

        Assert.Null(store.TryLoad());

        Assert.Empty(_directory.EnumerateFiles());
    }

    [Fact]
    public void LockedSettingsAreNotRenamed()
    {
        TestFiles.WriteText(SettingsPath, "{\"View\":{}}");
        var bytes = TestFiles.ReadBytes(SettingsPath);
        var store = new AppGeneralSettingsStore(SettingsPath);

        using (var locked = File.Open(SettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Null(store.TryLoad());
            Assert.False(File.Exists(SettingsPath + ".corrupt"));
        }

        Assert.Equal(bytes, TestFiles.ReadBytes(SettingsPath));
    }

    [Fact]
    public void SaveNewSettingsRoundTripsAndLeavesNoTemporaryFile()
    {
        var store = new AppGeneralSettingsStore(SettingsPath);
        var document = new AppGeneralSettingsDocument();
        document.View.ShowCad = false;

        store.Save(document);

        var loaded = store.TryLoad();
        Assert.NotNull(loaded);
        Assert.False(loaded.View.ShowCad);
        Assert.Equal(Path.GetFileName(SettingsPath), Assert.Single(_directory.EnumerateFiles()).Name);
    }

    [Fact]
    public void SaveExistingSettingsKeepsOldReaderWholeAndPublishesNewContent()
    {
        var store = new AppGeneralSettingsStore(SettingsPath);
        store.Save(new AppGeneralSettingsDocument());
        var oldJson = TestFiles.ReadContent(SettingsPath);
        using var original = File.Open(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var replacement = new AppGeneralSettingsDocument();
        replacement.View.ShowCad = false;

        store.Save(replacement);

        using var reader = new StreamReader(original, leaveOpen: true);
        Assert.Equal(oldJson, reader.ReadToEnd());
        var loaded = store.TryLoad();
        Assert.NotNull(loaded);
        Assert.False(loaded.View.ShowCad);
        Assert.Equal(Path.GetFileName(SettingsPath), Assert.Single(_directory.EnumerateFiles()).Name);
    }

    public void Dispose() => _directory.Delete(recursive: true);
}

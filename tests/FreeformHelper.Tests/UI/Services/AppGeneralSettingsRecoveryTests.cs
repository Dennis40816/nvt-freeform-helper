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
    public void CorruptBackupKeepsOnlyTheLatestBadFile()
    {
        TestFiles.WriteText(SettingsPath + ".corrupt", "older corrupt file");
        TestFiles.WriteText(SettingsPath, "{\"Import\":");
        var bytes = TestFiles.ReadBytes(SettingsPath);

        Assert.Null(new AppGeneralSettingsStore(SettingsPath).TryLoad());

        Assert.False(File.Exists(SettingsPath));
        Assert.Equal(bytes, TestFiles.ReadBytes(SettingsPath + ".corrupt"));
        Assert.Single(_directory.EnumerateFiles());
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

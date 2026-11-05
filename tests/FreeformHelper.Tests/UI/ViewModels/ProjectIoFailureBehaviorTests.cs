// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Security.Cryptography;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Project save/load failure paths driven through the real view-model entry points.
/// Contract under test: a failed I/O step reports an error to the user, never leaves a partial
/// or temporary file next to the target, and never replaces the in-memory project with a half-loaded one.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class ProjectIoFailureBehaviorTests
{
    private const string SaveFailedPrefix = "Save project failed";
    private const string LoadFailedPrefix = "Load project failed";

    // ---------- Save ----------

    // Nvt.Core AtomicOutput documents "its parent directory is created if needed", so a vanished folder is recreated
    // rather than reported. The pinned contract is: the user gets a complete project and no temporary file.
    [AvaloniaFact]
    public async Task SaveProject_TargetFolderMissing_RecreatesFolderAndWritesCompleteProject()
    {
        using var files = new IoFailureScope();
        var vm = files.CreateViewModel();
        IoFailureScope.MakeDirty(vm);
        var folder = files.PathFor("no-such-folder");
        var target = Path.Combine(folder, "nested", "project.json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(target);

        var saved = await vm.SaveProjectAsync();

        Assert.True(saved);
        Assert.False(vm.HasUnsavedChanges);
        Assert.StartsWith("Project saved:", vm.StatusText, StringComparison.Ordinal);
        Assert.False(JsonProjectStore.Load(target).UiSnapshot.View.ShowCad);
        Assert.Equal(target, Assert.Single(Directory.GetFileSystemEntries(Path.GetDirectoryName(target)!)));
    }

    [AvaloniaFact]
    public async Task SaveProject_ParentPathIsARegularFile_ReportsErrorAndKeepsTheFile()
    {
        using var files = new IoFailureScope();
        var vm = files.CreateViewModel();
        IoFailureScope.MakeDirty(vm);
        var blocker = files.PathFor("blocker");
        IoFailureScope.WriteText(blocker, "not a folder");
        var target = Path.Combine(blocker, "project.json");
        var before = files.Snapshot();
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(target);

        var saved = await vm.SaveProjectAsync();

        Assert.False(saved);
        Assert.StartsWith(SaveFailedPrefix, vm.StatusText, StringComparison.Ordinal);
        Assert.True(vm.HasUnsavedChanges);
        Assert.Equal(before, files.Snapshot());
    }

    [AvaloniaFact]
    public async Task SaveProject_FailedSaveThenValidTarget_RetrySucceedsAndRestoresCommands()
    {
        using var files = new IoFailureScope();
        var vm = files.CreateViewModel();
        IoFailureScope.MakeDirty(vm);
        var blocker = files.PathFor("blocker");
        IoFailureScope.WriteText(blocker, "not a folder");
        var failing = Path.Combine(blocker, "project.json");
        var target = files.PathFor("project.json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(failing);
        Assert.False(await vm.SaveProjectAsync());
        Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
        Assert.True(vm.SaveProjectCommand.CanExecute(null));

        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(target);
        var saved = await vm.SaveProjectAsync();

        Assert.True(saved);
        Assert.False(vm.HasUnsavedChanges);
        Assert.StartsWith("Project saved:", vm.StatusText, StringComparison.Ordinal);
        Assert.False(JsonProjectStore.Load(target).UiSnapshot.View.ShowCad);
    }

    [AvaloniaFact]
    public async Task SaveProject_TargetIsAnExistingDirectory_ReportsErrorAndKeepsDirectory()
    {
        using var files = new IoFailureScope();
        var vm = files.CreateViewModel();
        IoFailureScope.MakeDirty(vm);
        var target = Directory.CreateDirectory(files.PathFor("project.json")).FullName;
        IoFailureScope.WriteText(Path.Combine(target, "keep.txt"), "keep");
        var before = files.Snapshot();
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(target);

        var saved = await vm.SaveProjectAsync();

        Assert.False(saved);
        Assert.StartsWith(SaveFailedPrefix, vm.StatusText, StringComparison.Ordinal);
        Assert.True(vm.HasUnsavedChanges);
        Assert.Equal(before, files.Snapshot());
    }

    [AvaloniaFact]
    public async Task SaveProject_ReadOnlyTarget_ReportsErrorAndKeepsOriginalBytes()
    {
        using var files = new IoFailureScope();
        var vm = files.CreateViewModel();
        IoFailureScope.MakeDirty(vm);
        var target = await files.WriteValidProjectAsync("project.json");
        var original = IoFailureScope.ReadBytes(target);
        using var readOnly = IoFailureScope.ApplyReadOnlyProtection(target);
        var before = files.Snapshot();
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(target);

        var saved = await vm.SaveProjectAsync();

        Assert.False(saved);
        Assert.StartsWith(SaveFailedPrefix, vm.StatusText, StringComparison.Ordinal);
        Assert.True(vm.HasUnsavedChanges);
        Assert.Equal(original, IoFailureScope.ReadBytes(target));
        Assert.Equal(before, files.Snapshot());
    }

    [AvaloniaFact]
    public async Task SaveProject_TargetHeldByAnotherHandle_NeverLeavesAPartialOrTemporaryFile()
    {
        using var files = new IoFailureScope();
        var vm = files.CreateViewModel();
        IoFailureScope.MakeDirty(vm);
        var target = await files.WriteValidProjectAsync("project.json");
        var original = IoFailureScope.ReadBytes(target);
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(target);

        bool saved;
        using (new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            saved = await vm.SaveProjectAsync();
        }

        // Windows refuses to replace an open file; Unix rename ignores the advisory lock. Both are acceptable,
        // but each outcome must be internally consistent: either an error with the original intact,
        // or a complete new project and a clean dirty flag.
        if (saved)
        {
            Assert.False(vm.HasUnsavedChanges);
            Assert.False(JsonProjectStore.Load(target).UiSnapshot.View.ShowCad);
        }
        else
        {
            Assert.StartsWith(SaveFailedPrefix, vm.StatusText, StringComparison.Ordinal);
            Assert.True(vm.HasUnsavedChanges);
            Assert.Equal(original, IoFailureScope.ReadBytes(target));
        }

        Assert.Equal(target, Assert.Single(Directory.GetFileSystemEntries(files.Root)));
    }

    [Fact]
    public async Task JsonProjectStoreSave_InvalidSettings_ThrowsBeforeTouchingTheExistingFile()
    {
        using var files = new IoFailureScope();
        var target = await files.WriteValidProjectAsync("project.json");
        var original = IoFailureScope.ReadBytes(target);
        var invalid = new ProjectFile { Settings = new ProjectSettings { Matching = new MatchingSettings { MatchThreshold = 5 } } };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new JsonProjectStore().SaveAsync(target, invalid, CancellationToken.None));

        Assert.Equal(original, IoFailureScope.ReadBytes(target));
        Assert.Equal(target, Assert.Single(Directory.GetFileSystemEntries(files.Root)));
    }

    [Fact]
    public async Task JsonProjectStoreSave_ParentPathIsARegularFile_ThrowsAndLeavesNoTemporaryFile()
    {
        using var files = new IoFailureScope();
        var blocker = files.PathFor("blocker");
        IoFailureScope.WriteText(blocker, "not a folder");
        var before = files.Snapshot();

        await Assert.ThrowsAnyAsync<IOException>(
            () => new JsonProjectStore().SaveAsync(Path.Combine(blocker, "project.json"), new ProjectFile(), CancellationToken.None));

        Assert.Equal(before, files.Snapshot());
    }

    [Fact]
    public async Task JsonProjectStoreSave_FolderMissing_CreatesFolderWithOnlyTheProject()
    {
        using var files = new IoFailureScope();
        var target = Path.Combine(files.PathFor("missing"), "project.json");

        await new JsonProjectStore().SaveAsync(target, new ProjectFile(), CancellationToken.None);

        Assert.Equal(target, Assert.Single(Directory.GetFileSystemEntries(files.PathFor("missing"))));
        Assert.NotNull(JsonProjectStore.Load(target));
    }

    // ---------- Load ----------

    public static TheoryData<string, string> CorruptProjectContents => new()
    {
        { "truncated", "TRUNCATED" },
        { "empty", "" },
        { "null-literal", "null" },
        { "wrong-shape", "[]" },
        { "binary-garbage", "\0\0\0\u0001\u0002not json" },
        { "invalid-settings", "{\"settings\":{\"matching\":{\"matchThreshold\":5}}}" },
    };

    [AvaloniaTheory]
    [MemberData(nameof(CorruptProjectContents))]
    public async Task LoadProjectFromPath_CorruptFile_FailsWithMessageAndKeepsCurrentProject(string name, string content)
    {
        using var files = new IoFailureScope();
        var vm = files.CreateViewModel();
        IoFailureScope.MakeDirty(vm);
        var corrupt = await files.WriteCorruptProjectAsync($"{name}.json", content);
        var bytes = IoFailureScope.ReadBytes(corrupt);
        var expected = IoFailureScope.CaptureState(vm);

        var result = await vm.LoadProjectFromPathAsync(corrupt);

        Assert.False(result.IsLoaded);
        Assert.Equal("LOAD_FAILED", result.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.StartsWith(LoadFailedPrefix, vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(expected, IoFailureScope.CaptureState(vm));
        Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
        Assert.Equal(bytes, IoFailureScope.ReadBytes(corrupt));
    }

    [AvaloniaTheory]
    [MemberData(nameof(CorruptProjectContents))]
    public async Task LoadProjectCommand_CorruptFile_ShowsErrorAndLaterSaveWritesTheUntouchedProject(string name, string content)
    {
        using var files = new IoFailureScope();
        var vm = files.CreateViewModel();
        IoFailureScope.MakeDirty(vm);
        var corrupt = await files.WriteCorruptProjectAsync($"{name}.json", content);
        var expected = IoFailureScope.CaptureState(vm);
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(corrupt);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.StartsWith(LoadFailedPrefix, vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(expected, IoFailureScope.CaptureState(vm));

        var saveTarget = files.PathFor("after-failed-load.json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(saveTarget);
        Assert.True(await vm.SaveProjectAsync());
        var saved = JsonProjectStore.Load(saveTarget);
        Assert.False(saved.UiSnapshot.View.ShowCad);
    }

    [AvaloniaFact]
    public async Task LoadProjectFromPath_FileHeldExclusivelyByAnotherHandle_FailsWithMessage()
    {
        using var files = new IoFailureScope();
        var vm = files.CreateViewModel();
        IoFailureScope.MakeDirty(vm);
        var target = await files.WriteValidProjectAsync("locked.json");
        var expected = IoFailureScope.CaptureState(vm);

        Task<ProjectLoadCommandResult> load;
        using (new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            load = vm.LoadProjectFromPathAsync(target);
            await load;
        }

        var result = await load;
        Assert.False(result.IsLoaded);
        Assert.Equal("LOAD_FAILED", result.Code);
        Assert.StartsWith(LoadFailedPrefix, vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(expected, IoFailureScope.CaptureState(vm));
    }

    [AvaloniaFact]
    public async Task LoadProjectFromPath_FileDeletedBetweenPickAndLoad_ReturnsPathNotFound()
    {
        using var files = new IoFailureScope();
        var vm = files.CreateViewModel();
        var target = await files.WriteValidProjectAsync("vanishing.json");
        File.Delete(target);

        var result = await vm.LoadProjectFromPathAsync(target);

        Assert.False(result.IsLoaded);
        Assert.Equal("PATH_NOT_FOUND", result.Code);
        Assert.Contains(target, result.Message, StringComparison.Ordinal);
        Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
    }

    [Fact]
    public async Task ProjectPersistenceUseCaseLoad_FileDeletedAfterPick_ThrowsFileNotFoundForTheViewModelToReport()
    {
        using var files = new IoFailureScope();
        var target = await files.WriteValidProjectAsync("vanishing.json");

        await Assert.ThrowsAsync<FileNotFoundException>(() => ProjectPersistenceUseCase.LoadAsync(new ProjectLoadRequest(
            PickLoadProjectPathAsync: () =>
            {
                File.Delete(target);
                return Task.FromResult<string?>(target);
            },
            CanProceedWithPendingEdits: () => true)));
    }

    // ---------- Export (DXF + notch table) ----------

    public static TheoryData<string> ExportKinds => new() { "dxf", "notch" };

    [AvaloniaTheory]
    [MemberData(nameof(ExportKinds))]
    public async Task Export_NormalTarget_WritesTheFileAndReportsSuccess(string kind)
    {
        using var files = new IoFailureScope();
        using var export = await ExportHarness.CreateAsync(kind);
        var target = files.PathFor($"out.{export.Extension}");

        await export.RunAsync(target);

        export.AssertReportedSuccess(target);
    }

    [AvaloniaTheory]
    [MemberData(nameof(ExportKinds))]
    public async Task Export_TargetFolderMissing_ReportsErrorAndCreatesNothing(string kind)
    {
        using var files = new IoFailureScope();
        using var export = await ExportHarness.CreateAsync(kind);
        var missingFolder = files.PathFor("no-such-folder");
        var target = Path.Combine(missingFolder, $"out.{export.Extension}");
        var before = files.Snapshot();

        await export.RunAsync(target);

        export.AssertReportedFailure(target);
        Assert.False(Directory.Exists(missingFolder));
        Assert.Equal(before, files.Snapshot());
    }

    [AvaloniaTheory]
    [MemberData(nameof(ExportKinds))]
    public async Task Export_TargetIsAnExistingDirectory_ReportsErrorAndKeepsDirectory(string kind)
    {
        using var files = new IoFailureScope();
        using var export = await ExportHarness.CreateAsync(kind);
        var target = Directory.CreateDirectory(files.PathFor($"out.{export.Extension}")).FullName;
        IoFailureScope.WriteText(Path.Combine(target, "keep.txt"), "keep");
        var before = files.Snapshot();

        await export.RunAsync(target);

        export.AssertReportedFailure(target);
        Assert.Equal(before, files.Snapshot());
    }

    [AvaloniaTheory]
    [MemberData(nameof(ExportKinds))]
    public async Task Export_ReadOnlyTarget_ReportsErrorAndKeepsOriginalBytes(string kind)
    {
        using var files = new IoFailureScope();
        using var export = await ExportHarness.CreateAsync(kind);
        var target = files.PathFor($"out.{export.Extension}");
        IoFailureScope.WriteText(target, "previous export");
        using var readOnly = IoFailureScope.ApplyReadOnlyProtection(target);
        var before = files.Snapshot();

        await export.RunAsync(target);

        export.AssertReportedFailure(target);
        Assert.Equal("previous export", IoFailureScope.DecodeUtf8(target));
        Assert.Equal(before, files.Snapshot());
    }

    [AvaloniaTheory]
    [MemberData(nameof(ExportKinds))]
    public async Task Export_TargetHeldExclusivelyByAnotherHandle_ReportsErrorAndKeepsOriginalBytes(string kind)
    {
        using var files = new IoFailureScope();
        using var export = await ExportHarness.CreateAsync(kind);
        var target = files.PathFor($"out.{export.Extension}");
        IoFailureScope.WriteText(target, "previous export");
        var before = files.Snapshot();

        using (new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await export.RunAsync(target);
        }

        export.AssertReportedFailure(target);
        Assert.Equal("previous export", IoFailureScope.DecodeUtf8(target));
        Assert.Equal(before, files.Snapshot());
    }

    private sealed class ExportHarness : IDisposable
    {
        private readonly string _kind;
        private readonly OperationProgressLifecycleTestScope _scope;
        private readonly FreeformHelperViewModel _vm;
        private int _pickerCalls;

        private ExportHarness(string kind, OperationProgressLifecycleTestScope scope, FreeformHelperViewModel vm)
        {
            _kind = kind;
            _scope = scope;
            _vm = vm;
        }

        public string Extension => _kind == "dxf" ? "dxf" : "c";

        public static async Task<ExportHarness> CreateAsync(string kind)
        {
            var scope = new OperationProgressLifecycleTestScope();
            try
            {
                var vm = kind == "dxf" ? await scope.CreateLoadedAsync() : await scope.CreateExportReadyAsync();
                return new ExportHarness(kind, scope, vm);
            }
            catch
            {
                scope.Dispose();
                throw;
            }
        }

        public async Task RunAsync(string target)
        {
            if (_kind == "dxf")
            {
                _vm.PickSaveExportPathAsync = (_, _) => PickTargetAsync(target);
                await _vm.ExportDxfAllCommand.ExecuteAsync(null);
            }
            else
            {
                _vm.PickSaveNotchPathAsync = (_, _) => PickTargetAsync(target);
                await _vm.ExportNotchCommand.ExecuteAsync(null);
            }
        }

        private Task<string?> PickTargetAsync(string target)
        {
            _pickerCalls++;
            return Task.FromResult<string?>(target);
        }

        /// <summary>Proves the export reached the file system: the path picker ran once and the status names the target file.</summary>
        public void AssertReportedFailure(string target)
        {
            Assert.Equal(1, _pickerCalls);
            AssertFailureText();
            var fileName = Path.GetFileName(target);
            Assert.Contains(fileName, _vm.StatusText, StringComparison.Ordinal);
            if (_kind == "notch")
            {
                Assert.Contains(fileName, _vm.NotchExportProgressText, StringComparison.Ordinal);
            }
        }

        public void AssertReportedSuccess(string target)
        {
            Assert.Equal(1, _pickerCalls);
            Assert.Contains("exported", _vm.StatusText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("failed", _vm.StatusText, StringComparison.OrdinalIgnoreCase);
            Assert.True(new FileInfo(target).Length > 0);
        }

        private void AssertFailureText()
        {
            Assert.Contains("failed", _vm.StatusText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("exported", _vm.StatusText, StringComparison.OrdinalIgnoreCase);
            if (_kind == "notch")
            {
                Assert.Contains("export failed", _vm.NotchExportProgressText, StringComparison.Ordinal);
                Assert.False(_vm.IsNotchExporting);
            }
        }

        public void Dispose()
        {
            IoFailureScope.StopAppSettingsTimer(_vm);
            _scope.Dispose();
        }
    }
}

internal sealed class IoFailureScope : IDisposable
{
    private readonly DirectoryInfo _directory = TestFiles.CreateTempDirectory();

    // The app settings store lives in its own folder, so a late app-settings.json never lands in the folder under assertion.
    private readonly DirectoryInfo _settingsDirectory = TestFiles.CreateTempDirectory();
    private readonly List<FreeformHelperViewModel> _viewModels = [];

    public string Root => _directory.FullName;

    public string PathFor(string name) => Path.Combine(_directory.FullName, name);

    public FreeformHelperViewModel CreateViewModel()
    {
        var vm = new FreeformHelperViewModel(new AppGeneralSettingsStore(Path.Combine(_settingsDirectory.FullName, "app-settings.json")))
        {
            ApplyAppVisualPreferencesOnProjectLoad = false,
            ConfirmEmbedDxfAsync = () => Task.FromResult(false),
        };
        _viewModels.Add(vm);
        return vm;
    }

    public static void StopAppSettingsTimer(FreeformHelperViewModel vm) => vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();

    public static void MakeDirty(FreeformHelperViewModel vm)
    {
        vm.ShowCad = false;
        vm.NotchThresholdQ7 = 96m;
        Assert.True(vm.HasUnsavedChanges);
    }

    // The convenience File.* readers/writers are banned in tests; plain streams keep the new code warning-free.
    public static byte[] ReadBytes(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    public static string DecodeUtf8(string path) => System.Text.Encoding.UTF8.GetString(ReadBytes(path));

    public static void WriteBytes(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
    }

    public static void WriteText(string path, string text) => WriteBytes(path, System.Text.Encoding.UTF8.GetBytes(text));

    public static (bool ShowCad, decimal NotchThresholdQ7, bool HasUnsavedChanges) CaptureState(FreeformHelperViewModel vm) =>
        (vm.ShowCad, vm.NotchThresholdQ7, vm.HasUnsavedChanges);

    public async Task<string> WriteValidProjectAsync(string name)
    {
        var path = PathFor(name);
        await new JsonProjectStore().SaveAsync(path, new ProjectFile(), CancellationToken.None);
        return path;
    }

    public async Task<string> WriteCorruptProjectAsync(string name, string content)
    {
        var path = PathFor(name);
        if (content == "TRUNCATED")
        {
            var valid = await WriteValidProjectAsync($"source-{name}");
            var bytes = IoFailureScope.ReadBytes(valid);
            File.Delete(valid);
            IoFailureScope.WriteBytes(path, bytes[..(bytes.Length / 2)]);
        }
        else
        {
            WriteText(path, content);
        }

        return path;
    }

    /// <summary>Sorted relative path to content hash for every file, with directories listed as empty entries.</summary>
    public IReadOnlyList<string> Snapshot() => Directory
        .EnumerateFileSystemEntries(_directory.FullName, "*", SearchOption.AllDirectories)
        .Select(entry => (Relative: Path.GetRelativePath(_directory.FullName, entry), Entry: entry))
        .Select(item => File.Exists(item.Entry)
            ? $"{item.Relative}:{Convert.ToHexString(SHA256.HashData(IoFailureScope.ReadBytes(item.Entry)))}"
            : $"{item.Relative}/")
        .Order(StringComparer.Ordinal)
        .ToList();

    /// <summary>
    /// Makes <paramref name="path"/> unwritable for this process the way the platform does it
    /// (read-only attribute on Windows, read-only file and folder mode on Unix). Skips when the process
    /// is exempt from file permissions, for example when running as root.
    /// </summary>
    public static ReadOnlyProtection ApplyReadOnlyProtection(string path)
    {
        var protection = new ReadOnlyProtection(path);
        if (!protection.IsEnforced())
        {
            protection.Dispose();
            Assert.Skip("This process can write read-only files (running as root?); read-only protection cannot be simulated.");
        }

        return protection;
    }

    public void Dispose()
    {
        foreach (var vm in _viewModels)
        {
            StopAppSettingsTimer(vm);
        }

        _settingsDirectory.Delete(recursive: true);
        foreach (var file in Directory.EnumerateFiles(_directory.FullName, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        if (!OperatingSystem.IsWindows())
        {
            foreach (var folder in Directory.EnumerateDirectories(_directory.FullName, "*", SearchOption.AllDirectories).Append(_directory.FullName))
            {
                File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        _directory.Delete(recursive: true);
    }

    internal sealed class ReadOnlyProtection : IDisposable
    {
        private readonly string _path;
        private readonly string _folder;
        private readonly UnixFileMode? _folderMode;

        public ReadOnlyProtection(string path)
        {
            _path = path;
            _folder = Path.GetDirectoryName(path)!;
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            if (!OperatingSystem.IsWindows())
            {
                _folderMode = File.GetUnixFileMode(_folder);
                File.SetUnixFileMode(_folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            }
        }

        public bool IsEnforced()
        {
            try
            {
                using var stream = new FileStream(_path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        public void Dispose()
        {
            if (!OperatingSystem.IsWindows() && _folderMode is { } mode)
            {
                File.SetUnixFileMode(_folder, mode);
            }

            File.SetAttributes(_path, File.GetAttributes(_path) & ~FileAttributes.ReadOnly);
        }
    }
}

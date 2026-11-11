// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Text.Json.Nodes;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Observes the existing view model sequence, including mutations retained by cancelled and failed operations.</summary>
/// <remarks>TestAppSettingsIsolation also redirects default stores; these fixtures use their own isolated store and stop its timers.</remarks>
[Collection("HeadlessUiSerial")]
public sealed class ProjectPersistenceOrderCharacterizationTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task CancelledSaveCapturesSettingsSnapshotAndCadEditsBeforePickerAsync()
    {
        using var files = new ProjectLoadCharacterizationScope();
        var vm = await files.CreateSavedCadAsync("current.json");
        await ProjectLoadCharacterizationScope.HideOnlyPadAsync(vm);
        vm.MatchThreshold = 0.43m;
        vm.GlobalFontSizePercent = 128m;
        var document = FreeformHelperViewModelPrivateAccess.ProjectFile(vm);
        var picked = false;
        vm.PickSaveProjectPathAsync = () =>
        {
            picked = true;
            Assert.Equal(0.43, document.Settings.Matching.MatchThreshold);
            Assert.Equal(128.0, document.UiSnapshot.View.GlobalFontSizePercent);
            Assert.Single(document.HiddenCadPadIds);
            return Task.FromResult<string?>(null);
        };

        Assert.False(await vm.SaveProjectAsync());

        Assert.True(picked);
        Assert.Same(document, FreeformHelperViewModelPrivateAccess.ProjectFile(vm));
        Assert.Equal(0.43, document.Settings.Matching.MatchThreshold);
        Assert.Single(document.HiddenCadPadIds);
        Assert.True(vm.HasUnsavedChanges);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task SuccessfulSaveReportsStatusThenCleanThenPathThenFlushAsync()
    {
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("loaded.json");
        var vm = files.CreateViewModel();
        var loadedPath = files.PathFor("loaded.json");
        Assert.True((await vm.LoadProjectFromPathAsync(loadedPath)).IsLoaded);
        vm.GlobalFontSizePercent = 128m;
        var savedPath = files.PathFor("saved.json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(savedPath);
        using var log = new P3SaveLogScope();
        var observations = new List<(string Event, bool Dirty, string? Path, bool Deferred, bool Logged)>();
        vm.PropertyChanged += (sender, change) =>
        {
            if (change.PropertyName == nameof(vm.HasUnsavedChanges) ||
                (change.PropertyName == nameof(vm.StatusText) && vm.StatusText.StartsWith("Project saved:", StringComparison.Ordinal)))
            {
                Assert.Same(vm, sender);
                observations.Add((change.PropertyName!, vm.HasUnsavedChanges,
                    vm.ProjectSession.LastSavedPath, vm.AppGeneralPersistenceForTests.HasDeferredChanges,
                    log.Messages.Contains($"Project saved: {savedPath}")));
            }
        };

        Assert.True(await vm.SaveProjectAsync());

        Assert.Equal<(string, bool, string?, bool, bool)>(
            [(nameof(vm.StatusText), true, loadedPath, true, false), (nameof(vm.HasUnsavedChanges), false, loadedPath, true, true)],
            observations);
        Assert.Equal(savedPath, vm.ProjectSession.LastSavedPath);
        Assert.False(vm.AppGeneralPersistenceForTests.HasDeferredChanges);
        Assert.True(vm.AppGeneralPersistenceForTests.DeferAfterProjectLoad);
        Assert.Equal(128.0, files.AppStore.TryLoad()!.View.GlobalFontSizePercent);
        P3AssertBefore([.. log.Messages], $"Project saved: {savedPath}",
            $"App general settings persisted: {files.AppStore.SettingsPath}");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task PendingSaveLaterEditRemainsVisibleButIsClearedAndNotSavedAsync()
    {
        using var files = new ProjectLoadCharacterizationScope();
        var vm = files.CreateViewModel();
        vm.MatchThreshold = 0.27m;
        var path = files.PathFor("saved.json");
        using var gate = new SemaphoreSlim(0, 1);
        vm.PickSaveProjectPathAsync = () => P3WaitForPathAsync(gate, path);
        var save = vm.SaveProjectAsync();
        try
        {
            vm.MatchThreshold = 0.61m;
            Assert.True(vm.HasUnsavedChanges);
        }
        finally
        {
            gate.Release();
        }

        Assert.True(await save);
        Assert.Equal(0.61m, vm.MatchThreshold);
        Assert.Equal(0.27, JsonProjectStore.Load(path).Settings.Matching.MatchThreshold);
        Assert.False(vm.HasUnsavedChanges);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementBackupMutatesDocumentBeforePromptEvenWhenBackupFailsAsync(bool rejectBackup)
    {
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("replacement.json");
        var vm = await files.CreateSavedCadAsync("current.json");
        vm.MatchThreshold = 0.43m;
        if (rejectBackup) vm.NullValue = 70000m;
        var document = FreeformHelperViewModelPrivateAccess.ProjectFile(vm);
        var events = new List<string>();
        vm.PickLoadProjectPathAsync = () =>
        {
            events.Add("picker");
            return Task.FromResult<string?>(files.PathFor("replacement.json"));
        };
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = backup =>
        {
            events.Add("prompt");
            Assert.False(vm.IsProjectEditingEnabled);
            Assert.Equal(0.43, document.Settings.Matching.MatchThreshold);
            Assert.True(vm.HasUnsavedChanges);
            if (rejectBackup)
            {
                Assert.Null(backup);
                Assert.StartsWith("Could not back up the unsaved project:", vm.StatusText, StringComparison.Ordinal);
            }
            else
            {
                Assert.NotNull(backup);
                Assert.True(File.Exists(backup));
                Assert.Equal(0.43, JsonProjectStore.Load(backup).Settings.Matching.MatchThreshold);
            }

            return Task.FromResult<bool?>(null);
        };

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal<string>(["picker", "prompt"], events);
        Assert.Same(document, FreeformHelperViewModelPrivateAccess.ProjectFile(vm));
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.IsProjectEditingEnabled);
        Assert.Equal("Load cancelled: unsaved changes were kept.", vm.StatusText);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task SaveThenLoadLeavesLoadingBeforeSavingAndReentersForDirectLoadAsync()
    {
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("replacement.json");
        var vm = await files.CreateSavedCadAsync("current.json");
        vm.MatchThreshold = 0.43m;
        var events = new List<string>();
        vm.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(vm.IsProjectEditingEnabled))
            {
                events.Add(vm.IsProjectEditingEnabled ? "ready" : "loading");
            }
        };
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(files.PathFor("replacement.json"));
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = _ => Task.FromResult<bool?>(true);
        vm.PickSaveProjectPathAsync = () =>
        {
            events.Add("save");
            Assert.True(vm.IsProjectEditingEnabled);
            return Task.FromResult<string?>(files.PathFor("kept.json"));
        };

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal<string>(["loading", "ready", "save", "loading", "ready"], events);
        Assert.Equal(0.43, JsonProjectStore.Load(files.PathFor("kept.json")).Settings.Matching.MatchThreshold);
        Assert.Equal(0.27m, vm.MatchThreshold);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task DirectLoadSkipsReplacementPromptButStillChecksPendingEditsAsync()
    {
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("replacement.json");
        var vm = await files.CreateSavedCadAsync("current.json");
        vm.MatchThreshold = 0.43m;
        var prompts = 0;
        var pendingChecks = 0;
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = _ =>
        {
            prompts++;
            return Task.FromResult<bool?>(null);
        };
        vm.EnsureNoPendingEdits = () =>
        {
            pendingChecks++;
            return false;
        };

        var result = await vm.LoadProjectFromPathAsync(files.PathFor("replacement.json"));

        Assert.Equal(0, prompts);
        Assert.Equal(1, pendingChecks);
        Assert.Equal("CANCELLED", result.Code);
        Assert.Equal("Load cancelled (pending edits were not committed).", result.Message);
        Assert.Equal(0.43m, vm.MatchThreshold);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.IsProjectEditingEnabled);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task RejectedPathPublishesTheReturnedMessageBeforeEditingIsRestoredAsync()
    {
        using var files = new ProjectLoadCharacterizationScope();
        var vm = files.CreateViewModel();
        var events = new List<(string Event, string Status)>();
        vm.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(vm.StatusText) ||
                (change.PropertyName == nameof(vm.IsProjectEditingEnabled) && vm.IsProjectEditingEnabled))
            {
                events.Add((change.PropertyName!, vm.StatusText));
            }
        };

        var result = await vm.LoadProjectFromPathAsync(files.PathFor("missing.json"));

        Assert.Equal("PATH_NOT_FOUND", result.Code);
        Assert.Equal<(string, string)>(
            [(nameof(vm.StatusText), result.Message), (nameof(vm.IsProjectEditingEnabled), result.Message)], events);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ApplyLoadSettingsAndCleanPrecedeCadResetAndSourcePrecedesAvailabilityAsync()
    {
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("replacement.json");
        var vm = await files.CreateSavedCadAsync("current.json");
        var staleToggle = Assert.Single(vm.LayerToggles);
        vm.MatchThreshold = 0.61m;
        var events = new List<string>();
        vm.PropertyChanged += (sender, change) =>
        {
            Assert.Same(vm, sender);
            if (change.PropertyName == nameof(vm.MatchThreshold)) events.Add("settings");
            if (change.PropertyName == nameof(vm.HasUnsavedChanges) && !vm.HasUnsavedChanges) events.Add("clean");
            if (change.PropertyName == nameof(vm.StatusText) && vm.StatusText == $"Project loaded: {files.PathFor("replacement.json")}")
            {
                events.Add("loaded");
            }

            if (change.PropertyName == nameof(vm.CadPads) && vm.CadPads.Count == 0) events.Add("reset");
            if (change.PropertyName == nameof(vm.IsProjectEditingEnabled) && vm.IsProjectEditingEnabled) events.Add("ready");
        };
        vm.SimulationWorkspaceSourceChanged += (sender, _) =>
        {
            Assert.Same(vm, sender);
            Assert.False(vm.IsProjectEditingEnabled);
            Assert.True(vm.AppGeneralPersistenceForTests.SuppressPersistence);
            events.Add("source");
        };

        Assert.True((await vm.LoadProjectFromPathAsync(files.PathFor("replacement.json"))).IsLoaded);

        P3AssertBefore(events, "settings", "clean");
        P3AssertBefore(events, "clean", "loaded");
        P3AssertBefore(events, "loaded", "reset");
        P3AssertBefore(events, "reset", "source");
        P3AssertBefore(events, "source", "ready");
        Assert.False(vm.AppGeneralPersistenceForTests.SuppressPersistence);
        staleToggle.IsSelected = !staleToggle.IsSelected;
        Assert.False(vm.HasUnsavedChanges);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SnapshotConversionRestoresSuppressionAndKeepsPartialApplicationAsync(bool overflow, bool initialSuppression)
    {
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("replacement.json");
        var path = files.PathFor("replacement.json");
        if (overflow) P3OverflowSnapshot(path);
        var vm = files.CreateViewModel();
        vm.MatchThreshold = 0.61m;
        var previousDocument = FreeformHelperViewModelPrivateAccess.ProjectFile(vm);
        vm.AppGeneralPersistenceForTests.SuppressPersistence = initialSuppression;

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.Equal(!overflow, result.IsLoaded);
        Assert.Equal(overflow ? "LOAD_FAILED" : "OK", result.Code);
        Assert.Equal(0.27m, vm.MatchThreshold);
        Assert.NotSame(previousDocument, FreeformHelperViewModelPrivateAccess.ProjectFile(vm));
        Assert.Equal(path, vm.ProjectSession.LastSavedPath);
        Assert.Equal(initialSuppression, vm.AppGeneralPersistenceForTests.SuppressPersistence);
        Assert.True(vm.IsProjectEditingEnabled);
        if (overflow)
        {
            Assert.Empty(vm.CadPads);
            Assert.False(vm.AppGeneralPersistenceForTests.DeferAfterProjectLoad);
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ReplayUsesSavedSettingAndReportsOnlyExecutedStepsAsync(bool hasCad, bool replayStep2)
    {
        using var files = new ProjectLoadCharacterizationScope();
        var saver = hasCad ? await files.CreateSavedCadAsync("saved.json") : files.CreateViewModel();
        saver.AutoReplayStep2AfterProjectLoad = replayStep2;
        var path = files.PathFor("saved.json");
        saver.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);
        Assert.True(await saver.SaveProjectAsync());
        var vm = files.CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.True(result.IsLoaded);
        Assert.Equal(hasCad, result.Step1Replayed);
        Assert.Equal(hasCad && replayStep2, result.Step2Replayed);
        if (!hasCad) Assert.Contains("Step 1 auto replay skipped", vm.StatusText, StringComparison.Ordinal);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingCadSourceKeepsEditsOrUsesEmbeddedCopyAndPublishesWarningAsync(bool embed)
    {
        using var files = new ProjectLoadCharacterizationScope();
        var saver = await files.CreateSavedCadAsync("saved.json");
        await ProjectLoadCharacterizationScope.HideOnlyPadAsync(saver);
        saver.ConfirmEmbedDxfAsync = () => Task.FromResult(embed);
        Assert.True(await saver.SaveProjectAsync());
        var path = files.PathFor("saved.json");
        var saved = JsonProjectStore.Load(path);
        File.Delete(saved.LastDxfPath!);
        var vm = files.CreateViewModel();

        Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded);

        Assert.StartsWith("Warning: The DXF file was not found:", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains(embed ? "Using the DXF copy stored in the project." : "The saved DXF edits are kept", vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(saved.HiddenCadPadIds, FreeformHelperViewModelPrivateAccess.ProjectFile(vm).HiddenCadPadIds);
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(files.PathFor("again.json"));
        Assert.True(await vm.SaveProjectAsync());
        Assert.Equal(saved.HiddenCadPadIds, JsonProjectStore.Load(files.PathFor("again.json")).HiddenCadPadIds);
    }

    private static async Task<string?> P3WaitForPathAsync(SemaphoreSlim gate, string path)
    {
        await gate.WaitAsync();
        return path;
    }

    private static void P3AssertBefore(List<string> events, string earlier, string later)
    {
        Assert.Contains(earlier, events);
        Assert.Contains(later, events);
        Assert.True(events.IndexOf(earlier) < events.IndexOf(later), string.Join(", ", events));
    }

    private static void P3OverflowSnapshot(string path)
    {
        JsonNode document;
        using (var reader = new StreamReader(path))
        {
            document = JsonNode.Parse(reader.ReadToEnd())!;
        }

        document["uiSnapshot"]!["view"]!["cadLineWidth"] = 1e100;
        using var writer = new StreamWriter(path, append: false);
        writer.Write(document.ToJsonString());
    }

    private sealed class P3SaveLogScope : IDisposable
    {
        private readonly LoggingConfiguration? _previous = LogManager.Configuration;
        private readonly MemoryTarget _target = new()
        {
            Layout = "${message}"
        };

        public P3SaveLogScope()
        {
            var configuration = new LoggingConfiguration();
            configuration.AddRuleForAllLevels(_target);
            LogManager.Configuration = configuration;
        }

        public IList<string> Messages => _target.Logs;

        public void Dispose()
        {
            LogManager.Configuration = _previous;
            _target.Dispose();
        }
    }
}

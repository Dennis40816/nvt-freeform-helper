// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.ComponentModel;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the best-effort boundaries of the startup log level override and four view-model workflows do
/// when the guarded work fails: an ordinary failure is reported and swallowed, and the state around it is kept.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class RecoverableBoundaryTests
{
    [Fact]
    public void StartupLogLevelOverride_WhenLevelIsRequested_AppliesIt()
    {
        string? applied = null;

        Program.ApplyStartupLogLevelOverride(() => "Debug", level =>
        {
            applied = level;
            return level;
        });

        Assert.Equal("Debug", applied);
    }

    [Fact]
    public void StartupLogLevelOverride_WhenNoLevelIsRequested_AppliesNothing()
    {
        var applyCalls = 0;

        Program.ApplyStartupLogLevelOverride(() => " ", level =>
        {
            applyCalls++;
            return level;
        });

        Assert.Equal(0, applyCalls);
    }

    [Fact]
    public void StartupLogLevelOverride_WhenReadingTheLevelFails_ContinuesStartup()
    {
        var applyCalls = 0;

        Program.ApplyStartupLogLevelOverride(
            static () => throw new InvalidOperationException("settings unreadable"),
            level =>
            {
                applyCalls++;
                return level;
            });

        Assert.Equal(0, applyCalls);
    }

    [Fact]
    public void StartupLogLevelOverride_WhenApplyingTheLevelFails_ContinuesStartup()
    {
        Program.ApplyStartupLogLevelOverride(
            static () => "Debug",
            static _ => throw new InvalidOperationException("level rejected"));
    }

    [Fact]
    public void PersistAppGeneralSettings_WhenSaveFails_KeepsTheViewModelUsable()
    {
        var directory = Directory.CreateTempSubdirectory("freeformhelper-persist-");
        try
        {
            var store = new ScriptedSettingsStore(
                Path.Combine(directory.FullName, "app-settings.json"),
                static () => new IOException("disk full"));
            var vm = new FreeformHelperViewModel(store);
            store.SaveCalls = 0;

            vm.PersistAppGeneralSettingsNowForTests();

            Assert.Equal(1, store.SaveCalls);
            Assert.False(vm.AppGeneralPersistenceForTests.DeferAfterProjectLoad);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void PersistAppGeneralSettings_WhenSaveSucceeds_WritesTheDocument()
    {
        var directory = Directory.CreateTempSubdirectory("freeformhelper-persist-");
        try
        {
            var path = Path.Combine(directory.FullName, "app-settings.json");
            var store = new ScriptedSettingsStore(path, static () => null);
            var vm = new FreeformHelperViewModel(store);
            store.SaveCalls = 0;

            vm.PersistAppGeneralSettingsNowForTests();

            Assert.Equal(1, store.SaveCalls);
            Assert.True(File.Exists(path));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task LayerFilter_WhenApplyingTheResultFails_ReportsTheFailureAndKeepsRunning()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10), ("signal", 0, 0, 14, 10));
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            using var trap = FailOnce(vm, nameof(vm.CadPads), new InvalidOperationException("collection rejected"));

            vm.FilterCadPadsByLayerForTests();

            Assert.True(trap.Fired);
            Assert.Contains("Layer filter failed", vm.StatusText, StringComparison.Ordinal);
            Assert.Contains("collection rejected", vm.StatusText, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public async Task AutoDetectFreeforms_WhenCommittingTheResultFails_ReportsTheFailureAndKeepsRunning()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10), ("signal", 0, 0, 14, 10));
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            var host = new FailingCanvasHost(new InvalidOperationException("canvas rejected"));
            vm.CanvasHost = host;

            await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);

            Assert.True(host.Failed);
            Assert.Contains("Auto detect failed", vm.StatusText, StringComparison.Ordinal);
            Assert.Contains("canvas rejected", vm.StatusText, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public async Task DeferredRegularInspectorRefresh_WhenApplyingTheSnapshotFails_EndsWithoutFailureAndClearsPending()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10), ("signal", 0, 0, 14, 10));
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            var regularPadId = vm.RegularPads[0].RegularPadId;
            await SelectRegularPadAndSettleAsync(vm, regularPadId);
            // The rebuilt snapshot equals the shown one, and an equal value raises no change. Make the shown one differ.
            vm.CurrentPadInspectorSnapshot = vm.CurrentPadInspectorSnapshot! with { Kind = "stale" };
            using var trap = FailOnce(
                vm,
                nameof(vm.CurrentPadInspectorSnapshot),
                new InvalidOperationException("snapshot rejected"));

            await vm.RunDeferredRegularInspectorSnapshotRefreshForTestsAsync(regularPadId, new CancellationTokenSource());

            Assert.True(trap.Fired);
            Assert.False(vm.IsPadInspectorDeferredRefreshPending);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task SelectRegularPadAndSettleAsync(FreeformHelperViewModel vm, int regularPadId)
    {
        vm.ApplyCanvasSelection(Array.Empty<int>(), [regularPadId]);
        var deadline = Environment.TickCount64 + 10_000;
        while (vm.CurrentPadInspectorSnapshot?.Regular?.RegularPadId != regularPadId || vm.IsPadInspectorDeferredRefreshPending)
        {
            Assert.True(Environment.TickCount64 < deadline, $"The inspector did not settle (status: '{vm.StatusText}').");
            await Task.Delay(20);
        }
    }

    private static PropertyChangedTrap FailOnce(FreeformHelperViewModel vm, string propertyName, Exception failure) =>
        new(vm, propertyName, failure);

    private sealed class PropertyChangedTrap : IDisposable
    {
        private readonly FreeformHelperViewModel _vm;
        private readonly string _propertyName;
        private readonly Exception _failure;

        public PropertyChangedTrap(FreeformHelperViewModel vm, string propertyName, Exception failure)
        {
            _vm = vm;
            _propertyName = propertyName;
            _failure = failure;
            _vm.PropertyChanged += OnPropertyChanged;
        }

        public bool Fired { get; private set; }

        public void Dispose() => _vm.PropertyChanged -= OnPropertyChanged;

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (Fired || e.PropertyName != _propertyName)
            {
                return;
            }

            Fired = true;
            throw _failure;
        }
    }

    private sealed class FailingCanvasHost(Exception failure) : ICanvasHost
    {
        public bool Failed { get; private set; }

        public void Invalidate()
        {
            if (Failed)
            {
                return;
            }

            Failed = true;
            throw failure;
        }

        public void FitToContent()
        {
        }

        public void ResetView()
        {
        }

        public void ClearSelection()
        {
        }

        public void SetSelection(IReadOnlyCollection<int> cadPadIds, IReadOnlyCollection<int> regularPadIndices)
        {
        }

        public void FocusSelection()
        {
        }

        public void FocusSelectionWithMinZoom(double minZoom)
        {
        }

        public PadCanvasSelectionPerfSnapshot GetSelectionPerfSnapshot() => default;

        public PadCanvasVisibleDrawPerfSnapshot GetVisibleDrawPerfSnapshot() => default;
    }

    private sealed class ScriptedSettingsStore(string path, Func<Exception?> failure) : AppGeneralSettingsStore(path)
    {
        public int SaveCalls { get; set; }

        public override void Save(AppGeneralSettingsDocument document)
        {
            SaveCalls++;
            var error = failure();
            if (error is not null)
            {
                throw error;
            }

            base.Save(document);
        }
    }
}

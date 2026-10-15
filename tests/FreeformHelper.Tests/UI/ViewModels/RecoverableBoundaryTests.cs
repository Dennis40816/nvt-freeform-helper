// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
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
    private const int SettleTimeoutMilliseconds = 10_000;

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
    public void StartupLogLevelOverride_WhenReadingTheLevelRunsOutOfMemory_Propagates()
    {
        var applyCalls = 0;

        Assert.Throws<OutOfMemoryException>(() => Program.ApplyStartupLogLevelOverride(
            static () => throw CreateOutOfMemory(),
            level =>
            {
                applyCalls++;
                return level;
            }));

        Assert.Equal(0, applyCalls);
    }

    [Fact]
    public void StartupLogLevelOverride_WhenApplyingTheLevelRunsOutOfMemory_Propagates()
    {
        Assert.Throws<OutOfMemoryException>(() => Program.ApplyStartupLogLevelOverride(
            static () => "Debug",
            static _ => throw CreateOutOfMemory()));
    }

    [Fact]
    public void StartupLogLevelOverride_WhenReadingTheLevelFails_ContinuesStartup()
    {
        var readCalls = 0;
        var applyCalls = 0;

        Program.ApplyStartupLogLevelOverride(
            () =>
            {
                readCalls++;
                throw new InvalidOperationException("settings unreadable");
            },
            level =>
            {
                applyCalls++;
                return level;
            });

        Assert.Equal(1, readCalls);
        Assert.Equal(0, applyCalls);
    }

    [Fact]
    public void StartupLogLevelOverride_WhenApplyingTheLevelFails_ContinuesStartup()
    {
        var applyCalls = 0;

        Program.ApplyStartupLogLevelOverride(
            static () => "Debug",
            _ =>
            {
                applyCalls++;
                throw new InvalidOperationException("level rejected");
            });

        Assert.Equal(1, applyCalls);
    }

    [Fact]
    public void PersistAppGeneralSettings_WhenSaveFails_KeepsTheViewModelUsable()
    {
        var store = new ScriptedSettingsStore(static () => new IOException("disk full"));
        var vm = new FreeformHelperViewModel(store.Store);
        store.SaveCalls = 0;

        vm.PersistAppGeneralSettingsNowForTests();

        Assert.Equal(1, store.SaveCalls);
        Assert.Null(store.LastSaved);
    }

    [Fact]
    public void PersistAppGeneralSettings_WhenSaveRunsOutOfMemory_Propagates()
    {
        var store = new ScriptedSettingsStore(static () => CreateOutOfMemory());
        var vm = new FreeformHelperViewModel(store.Store);
        store.SaveCalls = 0;

        Assert.Throws<OutOfMemoryException>(vm.PersistAppGeneralSettingsNowForTests);

        Assert.Equal(1, store.SaveCalls);
    }

    [Fact]
    public void PersistAppGeneralSettings_WhenSaveSucceeds_HandsOverTheDocument()
    {
        var store = new ScriptedSettingsStore(static () => null);
        var vm = new FreeformHelperViewModel(store.Store);
        store.SaveCalls = 0;

        vm.PersistAppGeneralSettingsNowForTests();

        Assert.Equal(1, store.SaveCalls);
        Assert.NotNull(store.LastSaved);
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
    public async Task LayerFilter_WhenApplyingTheResultRunsOutOfMemory_Propagates()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10), ("signal", 0, 0, 14, 10));
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            using var trap = FailOnce(vm, nameof(vm.CadPads), CreateOutOfMemory());

            Assert.Throws<OutOfMemoryException>(vm.FilterCadPadsByLayerForTests);
            Assert.True(trap.Fired);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public async Task AutoDetectFreeforms_WhenCommittingTheResultRunsOutOfMemory_Propagates()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10), ("signal", 0, 0, 14, 10));
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            var host = new FailingCanvasHost(CreateOutOfMemory());
            vm.CanvasHost = host;

            await Assert.ThrowsAsync<OutOfMemoryException>(() => vm.AutoDetectFreeformsCommand.ExecuteAsync(null));

            Assert.True(host.Failed);
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
            var work = new RecordingBackgroundWorkRunner();
            vm.BackgroundWork.Runner = work;

            vm.QueueDeferredRegularInspectorSnapshotRefresh(regularPadId);
            Assert.True(vm.IsPadInspectorDeferredRefreshPending);
            await work.LastStartedAsync(FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName);

            Assert.True(trap.Fired);
            Assert.False(vm.IsPadInspectorDeferredRefreshPending);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public async Task DeferredRegularInspectorRefresh_WhenApplyingTheSnapshotRunsOutOfMemory_Propagates()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10), ("signal", 0, 0, 14, 10));
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            var regularPadId = vm.RegularPads[0].RegularPadId;
            await SelectRegularPadAndSettleAsync(vm, regularPadId);
            vm.CurrentPadInspectorSnapshot = vm.CurrentPadInspectorSnapshot! with { Kind = "stale" };
            using var trap = FailOnce(vm, nameof(vm.CurrentPadInspectorSnapshot), CreateOutOfMemory());
            var work = new RecordingBackgroundWorkRunner();
            vm.BackgroundWork.Runner = work;

            vm.QueueDeferredRegularInspectorSnapshotRefresh(regularPadId);
            Assert.True(vm.IsPadInspectorDeferredRefreshPending);
            await Assert.ThrowsAsync<OutOfMemoryException>(() =>
                work.LastStartedAsync(FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName));

            Assert.True(trap.Fired);
            Assert.False(vm.IsPadInspectorDeferredRefreshPending);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static OutOfMemoryException CreateOutOfMemory() =>
        (OutOfMemoryException)RuntimeHelpers.GetUninitializedObject(typeof(OutOfMemoryException));

    private static async Task SelectRegularPadAndSettleAsync(FreeformHelperViewModel vm, int regularPadId)
    {
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var selectionApplied = 0;

        bool IsSettled() =>
            vm.CurrentPadInspectorSnapshot?.Regular?.RegularPadId == regularPadId && !vm.IsPadInspectorDeferredRefreshPending;

        void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // The immediate snapshot is set before the deferred refresh is queued, so ignore changes until the
            // selection call has returned.
            if (Volatile.Read(ref selectionApplied) == 1 && IsSettled())
            {
                settled.TrySetResult();
            }
        }

        vm.PropertyChanged += OnPropertyChanged;
        try
        {
            vm.ApplyCanvasSelection(Array.Empty<int>(), [regularPadId]);
            Volatile.Write(ref selectionApplied, 1);
            if (!IsSettled())
            {
                try
                {
                    await settled.Task.WaitAsync(TimeSpan.FromMilliseconds(SettleTimeoutMilliseconds));
                }
                catch (TimeoutException)
                {
                    Assert.Fail($"The inspector did not settle (status: '{vm.StatusText}').");
                }
            }
        }
        finally
        {
            vm.PropertyChanged -= OnPropertyChanged;
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

    // Never touches the disk: the read finds no file, and the save only records what it was given.
    private sealed class ScriptedSettingsStore
    {
        private readonly Func<Exception?> _failure;

        public ScriptedSettingsStore(Func<Exception?> failure)
        {
            _failure = failure;
            Store = new AppGeneralSettingsStore(
                "recoverable-boundary-unused.json",
                static _ => null,
                Save);
        }

        public AppGeneralSettingsStore Store { get; }

        public int SaveCalls { get; set; }

        public AppGeneralSettingsDocument? LastSaved { get; private set; }

        private void Save(AppGeneralSettingsDocument document)
        {
            SaveCalls++;
            var error = _failure();
            if (error is not null)
            {
                throw error;
            }

            LastSaved = document;
        }
    }
}

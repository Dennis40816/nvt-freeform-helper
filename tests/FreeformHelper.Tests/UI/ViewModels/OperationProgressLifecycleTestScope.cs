// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Threading.Channels;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Owns lifecycle test files and isolates app settings in deferred persistence mode.
/// Exact notification counts pin the contract that properties notify only when their values change.
/// </summary>
internal sealed class OperationProgressLifecycleTestScope : IDisposable
{
    private readonly DirectoryInfo _directory = TestFiles.CreateTempDirectory();
    private readonly string _dxfPath = FreeformHelperViewModelTests.CreateTempDxfFile(("signal", 0, 0, 20, 10));

    public string ExportPath => Path.Combine(_directory.FullName, "notch.c");

    public async Task<FreeformHelperViewModel> CreateWithoutDxfAsync()
    {
        var projectPath = Path.Combine(_directory.FullName, "project.json");
        await new JsonProjectStore().SaveAsync(projectPath, new ProjectFile(), CancellationToken.None);
        var vm = new FreeformHelperViewModel(new AppGeneralSettingsStore(Path.Combine(_directory.FullName, "app-settings.json")))
        {
            PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(_dxfPath),
        };
        // Successful public project loading stops persistence and defers later writes until Save Project.
        await vm.LoadProjectCommand.ExecuteAsync(null);
        Assert.DoesNotContain("failed", vm.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(vm.RegularPads);
        Assert.Empty(vm.CadPads);
        return vm;
    }

    public async Task<FreeformHelperViewModel> CreateLoadedAsync()
    {
        var vm = await CreateWithoutDxfAsync();
        vm.ActiveAreaWidth = 20;
        vm.ActiveAreaHeight = 10;
        vm.GridPaddingPercent = 0;
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 2;
        cascade.YChannels = 1;
        await vm.RebuildGridCommand.ExecuteAsync(null);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        Assert.Single(vm.CadPads);
        Assert.Equal(2, vm.RegularPads.Count);
        return vm;
    }

    public async Task<FreeformHelperViewModel> CreateExportReadyAsync()
    {
        var vm = await CreateLoadedAsync();
        await vm.MatchCommand.ExecuteAsync(null);
        await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);
        vm.EnableV21 = false;
        vm.EnableV22 = true;
        vm.SelectedNotchExportFileTypeOption = Assert.Single(vm.NotchExportFileTypeOptions,
            option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv22);
        vm.OpenNotchExportSelectionAsync = null;
        return vm;
    }

    public void Dispose()
    {
        File.Delete(_dxfPath);
        _directory.Delete(recursive: true);
    }
}

internal sealed record ProgressOperation(FreeformHelperViewModel ViewModel, string Kind)
{
    public IAsyncRelayCommand Command => Kind switch
    {
        "Overlap" => ViewModel.CheckDxfQualityCommand,
        "Match" => ViewModel.MatchCommand,
        "Mapping" => ViewModel.AnalyzeIndexMappingCommand,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
    };

    public string BusyName => Kind switch
    {
        "Overlap" => "IsCheckingDxfOverlap",
        "Match" => "IsMatchingPads",
        "Mapping" => "IsAnalyzingIndexMapping",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
    };

    public string ProgressName => Kind switch
    {
        "Overlap" => "DxfOverlapProgress",
        "Match" => "MatchProgress",
        "Mapping" => "IndexMappingProgress",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
    };

    public bool Busy => Kind switch
    {
        "Overlap" => ViewModel.IsCheckingDxfOverlap,
        "Match" => ViewModel.IsMatchingPads,
        "Mapping" => ViewModel.IsAnalyzingIndexMapping,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
    };

    public double Progress => Kind switch
    {
        "Overlap" => ViewModel.DxfOverlapProgress,
        "Match" => ViewModel.MatchProgress,
        "Mapping" => ViewModel.IndexMappingProgress,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
    };

    public LifecycleNotifications Observe() => new(ViewModel, () => Busy, () => Progress, BusyName, ProgressName);
}

internal sealed record LifecycleChange(string Name, bool Busy, double Progress);

internal sealed class LifecycleNotifications : IDisposable
{
    private static readonly double[] _initialProgress = [0.0];
    private static readonly bool[] _busyScopeSequence = [true, false];
    private readonly FreeformHelperViewModel _vm;
    private readonly Func<bool> _busy;
    private readonly Func<double> _progress;
    private readonly string[] _names;
    // Notifications can originate on workers; the queue owns the mutable observations.
    private readonly ConcurrentQueue<LifecycleChange> _changes = new();

    public LifecycleNotifications(FreeformHelperViewModel vm, Func<bool> busy, Func<double> progress, params string[] names)
    {
        _vm = vm;
        _busy = busy;
        _progress = progress;
        _names = names;
        _vm.PropertyChanged += OnChange;
    }

    public LifecycleChange[] Changes => _changes.ToArray();

    private void OnChange(object? sender, PropertyChangedEventArgs change)
    {
        if (change.PropertyName is string name && _names.Contains(name, StringComparer.Ordinal))
        {
            _changes.Enqueue(new LifecycleChange(name, _busy(), _progress()));
        }
    }

    public void AssertSuccessful(ProgressOperation operation, bool repeated = false)
    {
        var changes = Changes;
        var idleIndex = Array.FindLastIndex(changes, change => change.Name == operation.BusyName);
        var active = changes[..(idleIndex + 1)];
        Assert.Equal(operation.BusyName, changes[0].Name);
        Assert.True(changes[0].Busy);
        if (!repeated) Assert.Equal(0.0, changes[0].Progress);
        Assert.Equal(operation.BusyName, active[^1].Name);
        Assert.False(active[^1].Busy);
        Assert.Equal(1.0, active[^1].Progress);
        Assert.All(active[1..^1], change =>
        {
            Assert.Equal(operation.ProgressName, change.Name);
            Assert.True(change.Busy);
            Assert.InRange(change.Progress, 0.0, 1.0);
        });
        // Dispatcher reports can already be queued when finally sets idle. Pin the active sequence;
        // any trailing notifications still use the old progress name and observe idle, never a new busy scope.
        Assert.All(changes[(idleIndex + 1)..], change =>
        {
            Assert.Equal(operation.ProgressName, change.Name);
            Assert.False(change.Busy);
            Assert.InRange(change.Progress, 0.0, 1.0);
        });
        var progress = active.Where(change => change.Name == operation.ProgressName).Select(change => change.Progress).ToArray();
        Assert.NotEmpty(progress);
        Assert.Equal(1.0, progress[^1]);
        if (repeated) Assert.Equal(0.0, progress[0]);
        var fromStart = _initialProgress.Concat(progress).ToArray();
        Assert.All(fromStart.Zip(fromStart.Skip(1)), pair => Assert.True(pair.Second >= pair.First));
        Assert.False(operation.Busy);
        Assert.Equal(1.0, operation.Progress);
    }

    public void AssertBusyScope()
    {
        var changes = Changes;
        var busyChanges = changes.Where(change => change.Name == "IsNotchExporting").ToArray();
        Assert.Equal(_busyScopeSequence, busyChanges.Select(change => change.Busy));
        // Export enters its busy scope before reporting and leaves it after the final progress value.
        Assert.Equal("IsNotchExporting", changes[0].Name);
        var idleIndex = Array.FindLastIndex(changes, change => change.Name == "IsNotchExporting");
        var lastProgressIndex = Array.FindLastIndex(changes, change => change.Name == "NotchExportProgress");
        Assert.True(idleIndex > lastProgressIndex);
    }

    public void Dispose() => _vm.PropertyChanged -= OnChange;
}

internal sealed class SavePathGate : IDisposable
{
    // Entry-source replacement and picker invocation are confined to the headless UI thread.
    // Completion sources and the channel own synchronization across continuations.
    private TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<string?> _result = Channel.CreateBounded<string?>(1);

    public Task Entered => _entered.Task;

    public async Task<Task> StartAndWaitForEntryAsync(Func<Task> executeCommand)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _entered = entered;
        var command = executeCommand();
        await Task.WhenAny(entered.Task, command).ConfigureAwait(false);
        if (command.IsCompleted) await command;
        Assert.True(entered.Task.IsCompletedSuccessfully, "The command completed without entering the expected gate.");
        return command;
    }

    public async Task<string?> PickAsync(string _, string __)
    {
        _entered.TrySetResult();
        return await _result.Reader.ReadAsync();
    }

    public void Release(string? path = null) => _result.Writer.TryWrite(path);

    public void Dispose()
    {
        Release();
        _result.Writer.TryComplete();
    }
}

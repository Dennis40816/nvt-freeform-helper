// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.ViewModels.Simulation;

/// <summary>Owns playback transitions and timer lifetime. UI thread only after view attachment.</summary>
internal sealed class SimulationPlaybackOwner
{
    internal readonly record struct Seed(
        int SelectedFrameSliderIndex, bool IsPlaybackRunning,
        bool IsPlaybackLoopEnabled, int PlaybackFramesPerSecond);

    private readonly Func<bool> _isCsvSource;
    private readonly Func<IReadOnlyList<NotchApplySimulationFrameOption>> _readFrames;
    private readonly Action _refreshSnapshot;
    private readonly Action _refreshCommands;
    private readonly Func<Action, TimeSpan, (Action Start, Action Stop, Action<TimeSpan> SetInterval)?> _createTimer;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;
    private (Action Start, Action Stop, Action<TimeSpan> SetInterval)? _timer;
    private bool _isSyncingFrameSelection;
    private int _selectedFrameSliderIndex;
    private bool _isPlaybackRunning;
    private bool _isPlaybackLoopEnabled;
    private int _playbackFramesPerSecond;

    internal SimulationPlaybackOwner(
        Seed seed, Func<bool> isCsvSource,
        Func<IReadOnlyList<NotchApplySimulationFrameOption>> readFrames,
        Action refreshSnapshot, Action refreshCommands,
        Func<Action, TimeSpan, (Action Start, Action Stop, Action<TimeSpan> SetInterval)?> createTimer,
        Action<string> propertyChanging, Action<string> propertyChanged)
    {
        _selectedFrameSliderIndex = seed.SelectedFrameSliderIndex;
        _isPlaybackRunning = seed.IsPlaybackRunning;
        _isPlaybackLoopEnabled = seed.IsPlaybackLoopEnabled;
        _playbackFramesPerSecond = seed.PlaybackFramesPerSecond;
        _isCsvSource = isCsvSource;
        _readFrames = readFrames;
        _refreshSnapshot = refreshSnapshot;
        _refreshCommands = refreshCommands;
        _createTimer = createTimer;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
        TogglePlaybackCommand = new RelayCommand(TogglePlayback, () => CanPlayFrames);
        PreviousFrameCommand = new RelayCommand(MoveToPreviousFrame, () => CanStepFrames);
        NextFrameCommand = new RelayCommand(MoveToNextFrame, () => CanStepFrames);
    }

    public IRelayCommand TogglePlaybackCommand { get; }
    public IRelayCommand PreviousFrameCommand { get; }
    public IRelayCommand NextFrameCommand { get; }
    public bool CanPlayFrames => _isCsvSource() && FrameOptions.Count > 1;
    public bool CanStepFrames => _isCsvSource() && FrameOptions.Count > 0;
    private IReadOnlyList<NotchApplySimulationFrameOption> FrameOptions => _readFrames();

    public int SelectedFrameSliderIndex
    {
        get => _selectedFrameSliderIndex;
        set => SetValue(ref _selectedFrameSliderIndex, value, nameof(SelectedFrameSliderIndex), OnSelectedFrameSliderIndexChanged);
    }

    public bool IsPlaybackRunning
    {
        get => _isPlaybackRunning;
        set => SetValue(ref _isPlaybackRunning, value, nameof(IsPlaybackRunning), _ => _refreshCommands());
    }

    public bool IsPlaybackLoopEnabled
    {
        get => _isPlaybackLoopEnabled;
        set => SetValue(ref _isPlaybackLoopEnabled, value, nameof(IsPlaybackLoopEnabled), _ => _propertyChanged(nameof(SimulationWorkspaceViewModel.PlaybackLoopText)));
    }

    public int PlaybackFramesPerSecond
    {
        get => _playbackFramesPerSecond;
        set => SetValue(ref _playbackFramesPerSecond, value, nameof(PlaybackFramesPerSecond), OnPlaybackFramesPerSecondChanged);
    }

    public void EnsureTimer()
    {
        if (_timer is not null)
        {
            return;
        }

        var framesPerSecond = PlaybackFramesPerSecond > 0 ? PlaybackFramesPerSecond : 6;
        _timer = _createTimer(AdvanceFrame, TimeSpan.FromMilliseconds(1000d / framesPerSecond));
    }

    public void ReconcileFrames()
    {
        _isSyncingFrameSelection = true;
        SelectedFrameSliderIndex = Math.Clamp(SelectedFrameSliderIndex, 0, Math.Max(FrameOptions.Count - 1, 0));
        _isSyncingFrameSelection = false;
        _propertyChanged(nameof(SimulationWorkspaceViewModel.SelectedFrameSliderMaximum));
        _propertyChanged(nameof(SimulationWorkspaceViewModel.FrameSummaryText));
    }

    private void TogglePlayback()
    {
        if (!CanPlayFrames)
        {
            return;
        }

        if (IsPlaybackRunning)
        {
            Stop();
            return;
        }

        IsPlaybackRunning = true;
        EnsureTimer();
        _timer?.Start();
        _refreshCommands();
    }

    private void MoveToPreviousFrame()
    {
        if (!CanStepFrames)
        {
            return;
        }

        Stop();
        var nextIndex = SelectedFrameSliderIndex - 1;
        if (nextIndex < 0)
        {
            nextIndex = IsPlaybackLoopEnabled ? FrameOptions.Count - 1 : 0;
        }

        SelectedFrameSliderIndex = nextIndex;
    }

    private void MoveToNextFrame()
    {
        if (!CanStepFrames)
        {
            return;
        }

        Stop();
        var nextIndex = SelectedFrameSliderIndex + 1;
        if (nextIndex >= FrameOptions.Count)
        {
            nextIndex = IsPlaybackLoopEnabled ? 0 : FrameOptions.Count - 1;
        }

        SelectedFrameSliderIndex = nextIndex;
    }

    public void Stop()
    {
        if (!IsPlaybackRunning)
        {
            return;
        }

        _timer?.Stop();
        IsPlaybackRunning = false;
        _refreshCommands();
    }

    public void AdvanceFrame()
    {
        if (!CanPlayFrames)
        {
            Stop();
            return;
        }

        var nextIndex = SelectedFrameSliderIndex + 1;
        if (nextIndex >= FrameOptions.Count)
        {
            if (!IsPlaybackLoopEnabled)
            {
                Stop();
                return;
            }

            nextIndex = 0;
        }

        SelectedFrameSliderIndex = nextIndex;
    }

    private void OnPlaybackFramesPerSecondChanged(int value)
    {
        var normalized = Math.Clamp(value, 1, 60);
        if (normalized != value)
        {
            PlaybackFramesPerSecond = normalized;
            return;
        }

        if (_timer is not null)
        {
            _timer.Value.SetInterval(TimeSpan.FromMilliseconds(1000d / normalized));
        }
    }

    private void OnSelectedFrameSliderIndexChanged(int value)
    {
        if (_isSyncingFrameSelection || !CanPlayFrames)
        {
            return;
        }

        var clamped = Math.Clamp(value, 0, Math.Max(FrameOptions.Count - 1, 0));
        if (clamped != value)
        {
            _isSyncingFrameSelection = true;
            SelectedFrameSliderIndex = clamped;
            _isSyncingFrameSelection = false;
            return;
        }

        _refreshSnapshot();
        _propertyChanged(nameof(SimulationWorkspaceViewModel.FrameSummaryText));
        _propertyChanged(nameof(SimulationWorkspaceViewModel.SelectedFrameDisplayText));
    }

    private void SetValue<T>(ref T field, T value, string name, Action<T>? changed = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        _propertyChanging(name);
        field = value;
        changed?.Invoke(value);
        _propertyChanged(name);
    }
}

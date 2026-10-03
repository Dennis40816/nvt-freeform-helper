using Avalonia.Threading;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SimulationWorkspaceViewModel
{
    private DispatcherTimer? EnsurePlaybackTimer()
    {
        if (_playbackTimer is not null)
        {
            return _playbackTimer;
        }

        if (!UiThread.IsCurrent(out _, out _))
        {
            return null;
        }

        var framesPerSecond = PlaybackFramesPerSecond > 0
            ? PlaybackFramesPerSecond
            : DefaultPlaybackFramesPerSecond;
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(1000d / framesPerSecond),
        };
        timer.Tick += (_, _) => AdvanceFrame();
        _playbackTimer = timer;
        return timer;
    }

    private void TogglePlayback()
    {
        if (!CanPlayFrames)
        {
            return;
        }

        if (IsPlaybackRunning)
        {
            StopPlayback();
            return;
        }

        IsPlaybackRunning = true;
        EnsurePlaybackTimer()?.Start();
        RefreshCommandState();
    }

    private void MoveToPreviousFrame()
    {
        if (!CanStepFrames)
        {
            return;
        }

        StopPlayback();
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

        StopPlayback();
        var nextIndex = SelectedFrameSliderIndex + 1;
        if (nextIndex >= FrameOptions.Count)
        {
            nextIndex = IsPlaybackLoopEnabled ? 0 : FrameOptions.Count - 1;
        }

        SelectedFrameSliderIndex = nextIndex;
    }

    private void StopPlayback()
    {
        if (!IsPlaybackRunning)
        {
            return;
        }

        _playbackTimer?.Stop();
        IsPlaybackRunning = false;
        RefreshCommandState();
    }

    private void AdvanceFrame()
    {
        if (!CanPlayFrames)
        {
            StopPlayback();
            return;
        }

        var nextIndex = SelectedFrameSliderIndex + 1;
        if (nextIndex >= FrameOptions.Count)
        {
            if (!IsPlaybackLoopEnabled)
            {
                StopPlayback();
                return;
            }

            nextIndex = 0;
        }

        SelectedFrameSliderIndex = nextIndex;
    }

    partial void OnIsPlaybackRunningChanged(bool value)
    {
        RefreshCommandState();
    }

    partial void OnPlaybackFramesPerSecondChanged(int value)
    {
        var normalized = Math.Clamp(value, 1, 60);
        if (normalized != value)
        {
            PlaybackFramesPerSecond = normalized;
            return;
        }

        if (_playbackTimer is not null)
        {
            _playbackTimer.Interval = TimeSpan.FromMilliseconds(1000d / normalized);
        }
    }

    partial void OnIsPlaybackLoopEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(PlaybackLoopText));
    }
}

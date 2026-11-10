using Avalonia.Threading;
using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SimulationWorkspaceViewModel
{
    private static (Action Start, Action Stop, Action<TimeSpan> SetInterval)? CreatePlaybackTimer(
        Action advanceFrame, TimeSpan interval)
    {
        if (!UiThread.IsCurrent(out _, out _))
        {
            return null;
        }

        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = interval,
        };
        timer.Tick += (_, _) => advanceFrame();
        return (timer.Start, timer.Stop, value => timer.Interval = value);
    }
}

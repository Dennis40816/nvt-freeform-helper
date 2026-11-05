using System.Runtime.CompilerServices;
using Avalonia.Threading;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Typed access to private state of <see cref="SimulationWorkspaceViewModel"/>.</summary>
internal static class SimulationWorkspaceViewModelPrivateAccess
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_playbackTimer")]
    public static extern ref DispatcherTimer? PlaybackTimer(SimulationWorkspaceViewModel viewModel);
}

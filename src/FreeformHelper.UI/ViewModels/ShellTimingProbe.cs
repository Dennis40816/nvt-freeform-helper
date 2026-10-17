namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Marks the moment of the shell where a test must hold the work back. Production code uses
/// <see cref="NullShellTimingProbe"/>; a test passes its own probe to the constructor of the shell.
/// </summary>
internal interface IShellTimingProbe
{
    /// <summary>Completes when a workspace build may continue past its first step.</summary>
    Task WorkspaceBuildPauseAsync();
}

/// <summary>
/// Default probe: does nothing and never holds the work back.
/// </summary>
internal sealed class NullShellTimingProbe : IShellTimingProbe
{
    public static NullShellTimingProbe Instance { get; } = new();

    private NullShellTimingProbe()
    {
    }

    public Task WorkspaceBuildPauseAsync() => Task.CompletedTask;
}

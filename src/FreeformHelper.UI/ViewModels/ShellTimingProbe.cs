namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Marks the moments of the shell where a test must run code or hold the work back. Production code uses
/// <see cref="NullShellTimingProbe"/>; a test passes its own probe to the constructor of the shell.
/// </summary>
internal interface IShellTimingProbe
{
    /// <summary>Runs inside the console text gate, right after the text buffer is cleared and before the log snapshot is read.</summary>
    void ConsoleSnapshotRead();

    /// <summary>Runs on a notification thread that finds the console text gate held, right before it waits for the gate.</summary>
    void ConsoleGateContended();

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

    public void ConsoleSnapshotRead()
    {
    }

    public void ConsoleGateContended()
    {
    }

    public Task WorkspaceBuildPauseAsync() => Task.CompletedTask;
}

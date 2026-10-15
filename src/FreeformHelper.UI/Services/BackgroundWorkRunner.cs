namespace FreeformHelper.UI.Services;

/// <summary>
/// Starts background work that nobody awaits. Production code uses <see cref="FireAndForgetBackgroundWorkRunner"/>;
/// tests replace it to observe the started task and its failure.
/// </summary>
internal interface IBackgroundWorkRunner
{
    /// <summary>Starts <paramref name="work"/>. <paramref name="name"/> tells the kinds of work apart.</summary>
    void Run(string name, Func<Task> work);
}

/// <summary>
/// The runner a view model starts its background work through. It is fixed per view model and forwards to
/// <see cref="Runner"/>, which is the fire-and-forget runner unless a test replaces it. Holding the switch here
/// keeps it out of the view model's own state.
/// </summary>
internal sealed class BackgroundWorkRunnerSlot : IBackgroundWorkRunner
{
    public IBackgroundWorkRunner Runner { get; set; } = FireAndForgetBackgroundWorkRunner.Instance;

    public void Run(string name, Func<Task> work) => Runner.Run(name, work);
}

/// <summary>
/// Default runner: starts the work and drops the task, as the call sites did before this seam existed.
/// A fault that nobody observes reaches <see cref="TaskScheduler.UnobservedTaskException"/>.
/// </summary>
internal sealed class FireAndForgetBackgroundWorkRunner : IBackgroundWorkRunner
{
    public static FireAndForgetBackgroundWorkRunner Instance { get; } = new();

    private FireAndForgetBackgroundWorkRunner()
    {
    }

    public void Run(string name, Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        _ = work();
    }
}

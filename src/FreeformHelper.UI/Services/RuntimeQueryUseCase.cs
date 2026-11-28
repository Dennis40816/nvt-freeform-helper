using FreeformHelper.UI.ViewModels;
using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

/// <summary>
/// The state that the runtime query command groups share: the shell view model and the notch query cache.
/// The command groups reach the view models through <see cref="Shell"/>.
/// </summary>
internal sealed class RuntimeQueryContext(ShellViewModel shell, RuntimeQueryNotchCacheService notchCache)
{
    public ShellViewModel Shell { get; } = shell ?? throw new ArgumentNullException(nameof(shell));

    public RuntimeQueryNotchCacheService NotchCache { get; } = notchCache ?? throw new ArgumentNullException(nameof(notchCache));
}

/// <summary>
/// The single entry point of the runtime query commands. It wires the command groups into one registry
/// and hands a request to the router built from it.
/// </summary>
internal sealed class RuntimeQueryUseCase
{
    private readonly RuntimeQueryCommandRouter _commandRouter;

    public RuntimeQueryUseCase(ShellViewModel shellViewModel)
    {
        ArgumentNullException.ThrowIfNull(shellViewModel);
        var context = new RuntimeQueryContext(shellViewModel, new RuntimeQueryNotchCacheService());
        var registry = new RuntimeQueryCommandRegistry();
        registry.AddSync("help", _ => RuntimeQueryHelp.Query());
        new RuntimeQueryStatusCommands(context).Register(registry);
        new RuntimeQueryPadCommands(context).Register(registry);
        new RuntimeQueryNotchCommands(context).Register(registry);
        new RuntimeQueryMultiOwnerCommands(context).Register(registry);
        new RuntimeQueryWorkflowCommands(context).Register(registry);
        new RuntimeQueryOutputCommands(context).Register(registry);
        _commandRouter = registry.BuildRouter();
    }

    public async Task<RuntimeQueryResponseEnvelope> ExecuteAsync(RuntimeQueryRequest? request)
    {
        return await _commandRouter.ExecuteAsync(request, RuntimeQueryProtocol.Version);
    }
}

using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Collects the runtime query command handlers by name and builds the router from them.
/// Each command group adds its own commands, so the use case no longer lists every handler itself.
/// </summary>
internal sealed class RuntimeQueryCommandRegistry
{
    private readonly Dictionary<string, Func<IReadOnlyDictionary<string, string>?, CancellationToken, Task<RuntimeQueryResponseEnvelope>>> _handlers =
        new(StringComparer.Ordinal);

    /// <summary>Adds a command whose handler completes synchronously.</summary>
    /// <param name="name">The command name, as the CLI sends it.</param>
    /// <param name="handler">The handler that maps the arguments to the response.</param>
    public void AddSync(string name, Func<IReadOnlyDictionary<string, string>?, RuntimeQueryResponseEnvelope> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        AddHandler(name, (args, _) => Task.FromResult(handler(args)));
    }

    /// <summary>Adds a command whose handler is asynchronous.</summary>
    /// <param name="name">The command name, as the CLI sends it.</param>
    /// <param name="handler">The handler that maps the arguments to the response.</param>
    public void Add(string name, Func<IReadOnlyDictionary<string, string>?, Task<RuntimeQueryResponseEnvelope>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        AddHandler(name, (args, _) => handler(args));
    }

    /// <summary>Builds the router over the handlers added so far.</summary>
    /// <returns>The router that dispatches a request to the handler with the same command name.</returns>
    public RuntimeQueryCommandRouter BuildRouter()
    {
        return new RuntimeQueryCommandRouter(_handlers);
    }

    private void AddHandler(
        string name,
        Func<IReadOnlyDictionary<string, string>?, CancellationToken, Task<RuntimeQueryResponseEnvelope>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_handlers.TryAdd(name, handler))
        {
            throw new InvalidOperationException($"The runtime query command '{name}' is already registered.");
        }
    }
}

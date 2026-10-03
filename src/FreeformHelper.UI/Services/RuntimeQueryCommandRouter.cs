namespace FreeformHelper.UI.Services;

internal sealed class RuntimeQueryCommandRouter
{
    private readonly IReadOnlyDictionary<string, Func<IReadOnlyDictionary<string, string>?, Task<RuntimeQueryResponseEnvelope>>> _handlers;

    public RuntimeQueryCommandRouter(
        IReadOnlyDictionary<string, Func<IReadOnlyDictionary<string, string>?, Task<RuntimeQueryResponseEnvelope>>> handlers)
    {
        _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
    }

    public Task<RuntimeQueryResponseEnvelope> RouteAsync(string? commandText, IReadOnlyDictionary<string, string>? args)
    {
        var command = commandText?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(command) || !_handlers.TryGetValue(command, out var handler))
        {
            return Task.FromResult(RuntimeQueryResponseEnvelope.Failure(
                code: "UNKNOWN_COMMAND",
                message: $"Unknown query command '{commandText}'."));
        }

        return handler(args);
    }
}

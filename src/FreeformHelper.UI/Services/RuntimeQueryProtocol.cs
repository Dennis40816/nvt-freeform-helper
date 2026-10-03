using System.Text.Json;

namespace FreeformHelper.UI.Services;

internal static class RuntimeQueryProtocol
{
    public const string PipeName = "freeformhelper.runtime.v1";
    public const string Version = "1";

    public static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}

internal sealed record RuntimeQueryRequest(
    string Version,
    string Command,
    IReadOnlyDictionary<string, string>? Args);

internal sealed record RuntimeQueryError(
    string Code,
    string Message);

internal sealed record RuntimeQueryResponseEnvelope(
    bool Ok,
    object? Data,
    RuntimeQueryError? Error)
{
    public static RuntimeQueryResponseEnvelope Success(object? data)
    {
        return new RuntimeQueryResponseEnvelope(
            Ok: true,
            Data: data,
            Error: null);
    }

    public static RuntimeQueryResponseEnvelope Failure(string code, string message)
    {
        return new RuntimeQueryResponseEnvelope(
            Ok: false,
            Data: null,
            Error: new RuntimeQueryError(code, message));
    }
}

using System.Runtime.CompilerServices;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Compile-time bound access to the private parser of the static <c>RuntimeQueryCommandLine</c> class.</summary>
internal static class RuntimeQueryCommandLinePrivateAccess
{
    // The owner is a static class, so it cannot be a parameter type; the attribute names it and the argument is always null.
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "TryParseCommandLine")]
    public static extern bool TryParseCommandLine(
        [UnsafeAccessorType("FreeformHelper.UI.Services.RuntimeQueryCommandLine, FreeformHelper.UI")] object? owner,
        string[] args,
        out string command,
        out Dictionary<string, string> commandArgs,
        out int timeoutMs,
        out bool prettyJson,
        out string? error);
}

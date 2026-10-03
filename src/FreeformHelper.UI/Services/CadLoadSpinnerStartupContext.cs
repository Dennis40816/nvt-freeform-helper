using System.Globalization;

namespace FreeformHelper.UI.Services;

internal sealed record CadLoadSpinnerWindowOptions(
    int ParentProcessId,
    string PipeName);

internal static class CadLoadSpinnerStartupContext
{
    private static CadLoadSpinnerWindowOptions? _current;

    public static bool IsSpinnerMode => _current is not null;

    public static CadLoadSpinnerWindowOptions Current =>
        _current ?? throw new InvalidOperationException("CAD load spinner startup context is not initialized.");

    public static bool TryInitializeFromArgs(string[] args)
    {
        if (!CadLoadSpinnerCommandLine.TryParse(args, out var options))
        {
            return false;
        }

        _current = options;
        return true;
    }
}

internal static class CadLoadSpinnerCommandLine
{
    private const string SpinnerArg = "--cad-load-spinner";
    private const string ParentPidArg = "--parent-pid";
    private const string PipeNameArg = "--pipe-name";

    public static bool TryParse(string[] args, out CadLoadSpinnerWindowOptions options)
    {
        options = default!;
        if (args is null || args.Length == 0 || !args.Contains(SpinnerArg, StringComparer.Ordinal))
        {
            return false;
        }

        if (!TryGetInt(args, ParentPidArg, out var parentProcessId) ||
            !TryGetString(args, PipeNameArg, out var pipeName))
        {
            return false;
        }

        options = new CadLoadSpinnerWindowOptions(
            ParentProcessId: parentProcessId,
            PipeName: pipeName);
        return true;
    }

    private static bool TryGetInt(string[] args, string key, out int value)
    {
        value = 0;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], key, StringComparison.Ordinal))
            {
                continue;
            }

            return int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        return false;
    }

    private static bool TryGetString(string[] args, string key, out string value)
    {
        value = string.Empty;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], key, StringComparison.Ordinal))
            {
                continue;
            }

            value = args[i + 1];
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }
}

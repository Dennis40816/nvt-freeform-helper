namespace FreeformHelper.Tests;

internal enum UiBaselineUpdateMode
{
    Check = 0,
    DryRun = 1,
    Apply = 2,
}

internal static class UiBaselineUpdateModeResolver
{
    internal const string EnvironmentVariableName = "FH_UI_BASELINE_MODE";

    public static UiBaselineUpdateMode Resolve()
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return UiBaselineUpdateMode.Check;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "dry-run" => UiBaselineUpdateMode.DryRun,
            "apply" => UiBaselineUpdateMode.Apply,
            _ => UiBaselineUpdateMode.Check,
        };
    }
}

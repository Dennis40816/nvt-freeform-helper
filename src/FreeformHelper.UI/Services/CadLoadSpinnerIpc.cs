using System.Text.Json;

namespace FreeformHelper.UI.Services;

internal static class CadLoadSpinnerIpcProtocol
{
    public const string ShowCommand = "show";
    public const string HideCommand = "hide";
    public const string ShutdownCommand = "shutdown";
    public const string PingCommand = "ping";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static string BuildPipeName(int parentProcessId)
    {
        return $"freeformhelper.cadloadspinner.{parentProcessId}";
    }
}

internal sealed record CadLoadSpinnerRequest(
    string Command,
    int OwnerLeft = 0,
    int OwnerTop = 0,
    int OwnerWidth = 0,
    int OwnerHeight = 0);

using System.Text.Json;

namespace FreeformHelper.UI.Services;

internal static class RuntimeQueryProtocol
{
    public const string PipeName = "freeformhelper.runtime.v1";
    public const string Version = "1";

    public static JsonSerializerOptions CompactJsonOptions => Nvt.Core.RuntimeQuery.RuntimeQueryProtocol.CompactJsonOptions;

    public static JsonSerializerOptions PrettyJsonOptions => Nvt.Core.RuntimeQuery.RuntimeQueryProtocol.PrettyJsonOptions;
}

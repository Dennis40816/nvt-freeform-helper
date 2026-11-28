namespace FreeformHelper.UI.Services;

/// <summary>
/// Default and maximum row counts of the runtime query commands that return lists.
/// </summary>
internal static class RuntimeQueryLimits
{
    internal const int DefaultTerminalTail = 120;
    internal const int MaxTerminalTail = 5000;
    internal const int DefaultTerminalLinkLimit = 200;
    internal const int MaxTerminalLinkLimit = 2000;
    internal const int DefaultNotchRegularLimit = 200;
    internal const int MaxNotchRegularLimit = 5000;
    internal const int DefaultNotchTargetLimit = 120;
    internal const int MaxNotchTargetLimit = 5000;
    internal const int DefaultNotchPolygonLimit = 64;
    internal const int MaxNotchPolygonLimit = 2000;
}

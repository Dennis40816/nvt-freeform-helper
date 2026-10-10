// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NLog.Common;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.Logging;

internal static class EmergencyLogSink
{
    // The canonical runner isolates arbitrary writer failures without another catch policy.
    private static readonly UiEventRunner _stderrRunner = new(
        static (operation, exception) => InternalLogger.Error(exception, "{0} failed.", operation),
        static (operation, exception) => InternalLogger.Error(exception, "{0} failed.", operation));

    public static void Report(string operation, Exception exception)
    {
        // InternalLogger isolates writer failures independently of application targets.
        InternalLogger.Error(exception, "UI event {0} failed.", operation);
        _stderrRunner.Run("EmergencyLogSink.WriteStandardError", _ =>
            Console.Error.WriteLineAsync($"UI event {operation} failed: {exception}".ReplaceLineEndings(" ")));
    }
}

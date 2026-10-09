// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NLog.Common;

namespace FreeformHelper.UI.Logging;

internal static class EmergencyLogSink
{
    public static void Report(string operation, Exception exception)
    {
        // InternalLogger isolates writer failures independently of application targets.
        InternalLogger.Error(exception, "UI event {0} failed.", operation);
    }
}

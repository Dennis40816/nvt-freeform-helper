// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NLog;

namespace FreeformHelper.UI.Services;

internal sealed class UiEventFailureReporter(UiOperationStatusReporter status)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public void Report(string operation, Exception exception)
    {
        Logger.Error(exception, "UI event {0} failed.", operation);
        status.SetError($"{operation} failed", exception);
    }
}

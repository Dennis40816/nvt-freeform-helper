// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NLog;

namespace FreeformHelper.UI.Services;

internal sealed class UiEventFailureReporter(UiOperationStatusReporter status)
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public void Report(string operation, Exception exception)
    {
        _logger.Error(exception, "UI event {0} failed.", operation);
        status.SetError($"{operation} failed", exception);
    }
}

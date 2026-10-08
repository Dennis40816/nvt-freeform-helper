// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

internal static class RuntimeQueryErrorMapper
{
    public static RuntimeQueryError Map(RuntimeQueryFailure failure, string? detail)
    {
        // Keep the frozen NFH serializer text when Core names its envelope types.
        if (failure is RuntimeQueryFailure.InvalidJson or RuntimeQueryFailure.ClientError)
        {
            detail = detail?.Replace("Nvt.Core.RuntimeQuery.", "FreeformHelper.UI.Services.", StringComparison.Ordinal);
        }

        return failure switch
        {
            RuntimeQueryFailure.RequestTimeout => new("IPC_REQUEST_TIMEOUT",
                "Runtime query client connected but did not send a request before the server read timeout."),
            RuntimeQueryFailure.EmptyRequest => new("INVALID_REQUEST", "Empty request payload."),
            RuntimeQueryFailure.InvalidJson => new("INVALID_JSON", detail!),
            RuntimeQueryFailure.EmptyResponse => new("EMPTY_RESPONSE", "Runtime query returned an empty response."),
            RuntimeQueryFailure.InvalidResponse => new("INVALID_RESPONSE", "Runtime query response could not be parsed."),
            RuntimeQueryFailure.ConnectionTimeout => new("INSTANCE_NOT_RUNNING",
                "No running FreeformHelper instance responded within timeout."),
            RuntimeQueryFailure.ClientTimeout => new("IPC_TIMEOUT", "Runtime query did not complete within timeout."),
            RuntimeQueryFailure.IoError => new("IPC_IO_ERROR", detail!),
            RuntimeQueryFailure.DispatcherUnavailable => new("IPC_ERROR", "The UI dispatcher is unavailable."),
            _ => new("IPC_ERROR", detail!)
        };
    }
}

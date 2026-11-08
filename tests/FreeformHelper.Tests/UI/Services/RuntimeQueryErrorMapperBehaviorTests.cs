// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.Services;
using Nvt.Core.RuntimeQuery;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class RuntimeQueryErrorMapperBehaviorTests
{
    [Theory]
    [InlineData(RuntimeQueryFailure.InvalidJson, "INVALID_JSON")]
    [InlineData(RuntimeQueryFailure.ClientError, "IPC_ERROR")]
    public void MapSerializerFailurePreservesApplicationEnvelopeNames(RuntimeQueryFailure failure, string expectedCode)
    {
        const string detail = "Cannot deserialize Nvt.Core.RuntimeQuery.RuntimeQueryRequest or " +
            "Nvt.Core.RuntimeQuery.RuntimeQueryResponseEnvelope at $.command.";

        var error = RuntimeQueryErrorMapper.Map(failure, detail);

        Assert.Equal(expectedCode, error.Code);
        Assert.Equal("Cannot deserialize FreeformHelper.UI.Services.RuntimeQueryRequest or " +
            "FreeformHelper.UI.Services.RuntimeQueryResponseEnvelope at $.command.", error.Message);
    }

    [Theory]
    [InlineData(RuntimeQueryFailure.IoError, "Pipe disconnected while reading Nvt.Core.RuntimeQuery.RuntimeQueryRequest.",
        "IPC_IO_ERROR", "Pipe disconnected while reading Nvt.Core.RuntimeQuery.RuntimeQueryRequest.")]
    [InlineData(RuntimeQueryFailure.HandlerError, "Failed to handle Nvt.Core.RuntimeQuery.RuntimeQueryRequest.",
        "IPC_ERROR", "Failed to handle Nvt.Core.RuntimeQuery.RuntimeQueryRequest.")]
    [InlineData(RuntimeQueryFailure.DispatcherUnavailable, null, "IPC_ERROR", "The UI dispatcher is unavailable.")]
    public void MapTransportOrDispatchFailureReturnsCallerDiagnostic(
        RuntimeQueryFailure failure, string? detail, string expectedCode, string expectedMessage)
    {
        var error = RuntimeQueryErrorMapper.Map(failure, detail);

        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(expectedMessage, error.Message);
    }
}

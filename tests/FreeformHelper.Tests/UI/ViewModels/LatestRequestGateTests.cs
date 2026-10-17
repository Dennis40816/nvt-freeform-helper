// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LatestRequestGateTests
{
    [Fact]
    public void Begin_StoresTheSourceAndCountsRequests()
    {
        var gate = new LatestRequestGate();
        Assert.False(gate.HasPendingRequest);
        Assert.Equal(0, gate.LatestRequestId);

        var (firstId, firstSource) = gate.Begin();
        var (secondId, secondSource) = gate.Begin();

        Assert.Equal(1, firstId);
        Assert.Equal(2, secondId);
        Assert.Equal(2, gate.LatestRequestId);
        Assert.True(gate.HasPendingRequest);
        Assert.NotSame(firstSource, secondSource);
    }

    [Fact]
    public void Cancel_CancelsAndDisposesTheStoredSourceAndKeepsTheCounter()
    {
        var gate = new LatestRequestGate();
        var (_, source) = gate.Begin();
        var token = source.Token;

        gate.Cancel();

        Assert.True(token.IsCancellationRequested);
        Assert.False(gate.HasPendingRequest);
        Assert.Equal(1, gate.LatestRequestId);
        Assert.Throws<ObjectDisposedException>(() => source.Token.WaitHandle);
        gate.Cancel();
        Assert.Equal(1, gate.LatestRequestId);
    }

    [Fact]
    public void Begin_AfterCancel_KeepsCounting()
    {
        var gate = new LatestRequestGate();
        gate.Begin();
        gate.Cancel();

        var (nextId, nextSource) = gate.Begin();

        Assert.Equal(2, nextId);
        Assert.Equal(2, gate.LatestRequestId);
        Assert.True(gate.HasPendingRequest);
        Assert.False(nextSource.IsCancellationRequested);
    }

    [Fact]
    public void Release_OnlyClearsAndDisposesTheStoredSource()
    {
        var gate = new LatestRequestGate();
        var (_, staleSource) = gate.Begin();
        var (_, currentSource) = gate.Begin();

        Assert.False(gate.Release(staleSource));
        Assert.True(gate.HasPendingRequest);
        Assert.NotNull(staleSource.Token.WaitHandle);
        Assert.True(gate.Release(currentSource));
        Assert.False(gate.HasPendingRequest);
        Assert.Throws<ObjectDisposedException>(() => currentSource.Token.WaitHandle);
        Assert.Equal(2, gate.LatestRequestId);
    }

    [Fact]
    public void Release_AfterCancel_ReturnsFalse()
    {
        var gate = new LatestRequestGate();
        var (_, source) = gate.Begin();
        gate.Cancel();

        Assert.False(gate.Release(source));
    }
}

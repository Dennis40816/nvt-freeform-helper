// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using FreeformHelper.UI.Services;
using Nvt.Core.RuntimeQuery;
using Xunit;
using RuntimeQueryProtocol = FreeformHelper.UI.Services.RuntimeQueryProtocol;

namespace FreeformHelper.Tests;

[Collection("RuntimeQueryIpcHost")]
public sealed class RuntimeQueryCoreBehaviorTests
{
    [Fact]
    public async Task PipeAccess_AllowsProcessUserDeniesNetworkLogonsOnSuccessiveInstances()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows pipe access rules require Windows.");
            return;
        }

        var pipeName = NewPipeName();
        await using var server = CreateServer(pipeName);
        using var identity = WindowsIdentity.GetCurrent();
        var networkSid = new SecurityIdentifier(WellKnownSidType.NetworkSid, null);
        server.Start();
        NamedPipeServerStream? previous = null;
        for (var instance = 0; instance < 2; instance++)
        {
            var pipe = await WaitForPipeAsync(server, previous);
            var security = pipe.GetAccessControl();
            Assert.True(security.AreAccessRulesProtected);
            Assert.Equal(identity.Owner, security.GetOwner(typeof(SecurityIdentifier)));
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToArray();
            Assert.Equal(2, rules.Length);
            var allow = rules[1];
            Assert.Equal(AccessControlType.Allow, allow.AccessControlType);
            Assert.Equal(identity.User, allow.IdentityReference);
            Assert.Equal(PipeAccessRights.FullControl, allow.PipeAccessRights);
            var deny = rules[0];
            Assert.Equal(AccessControlType.Deny, deny.AccessControlType);
            Assert.Equal(networkSid, deny.IdentityReference);
            Assert.Equal(PipeAccessRights.FullControl, deny.PipeAccessRights);

            var response = await SendRequestAsync(pipeName);
            Assert.True(response.Ok);
            previous = pipe;
        }
    }

    [Fact]
    public async Task DisposeAsync_WhenHandlerIgnoresCancellation_ClosesPipeAndStopsWaiting()
    {
        var pipeName = NewPipeName();
        var entered = NewSignal();
        var work = new TaskCompletionSource<RuntimeQueryResponseEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken handlerToken = default;
        await using var server = CreateServer(pipeName, (_, _, token) =>
        {
            handlerToken = token;
            entered.TrySetResult();
            return work.Task;
        });
        server.Start();
        await using var client = await ConnectAndWriteAsync(pipeName);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            var pipe = await WaitForPipeAsync(server);
            var handle = pipe.SafePipeHandle;
            await server.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

            Assert.True(handlerToken.IsCancellationRequested);
            Assert.True(handle.IsClosed);
            Assert.False(work.Task.IsCompleted);
        }
        finally
        {
            work.TrySetResult(RuntimeQueryResponseEnvelope.Success(null));
        }
    }

    [Fact]
    public async Task DisposeAsync_WhenHandlerBlocksSynchronously_ReportsBoundAndReleasesPipe()
    {
        var pipeName = NewPipeName();
        var entered = NewSignal();
        var returned = NewSignal();
        var diagnostics = new ConcurrentQueue<RuntimeQueryDiagnostic>();
        using var release = new ManualResetEventSlim();
        await using var server = CreateServer(pipeName, (_, _, _) =>
        {
            entered.TrySetResult();
            release.Wait(TimeSpan.FromSeconds(10), CancellationToken.None);
            returned.TrySetResult();
            return Task.FromResult(RuntimeQueryResponseEnvelope.Success(null));
        }, (diagnostic, _) => diagnostics.Enqueue(diagnostic));
        server.Start();
        await using var client = await ConnectAndWriteAsync(pipeName);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            var pipe = await WaitForPipeAsync(server);
            var handle = pipe.SafePipeHandle;
            await server.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

            Assert.False(returned.Task.IsCompleted);
            Assert.True(handle.IsClosed);
            Assert.Single(diagnostics, diagnostic => diagnostic == RuntimeQueryDiagnostic.ShutdownTimedOut);
            using var replacement = new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        }
        finally
        {
            release.Set();
            await returned.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task PipeCreationFailure_ReportsOnceThenStoppedBeforeDisposalAndKeepsFirstServerWorking()
    {
        var pipeName = NewPipeName();
        await using var first = CreateServer(pipeName);
        first.Start();
        _ = await WaitForPipeAsync(first);
        var stopped = NewSignal();
        var diagnostics = new ConcurrentQueue<(RuntimeQueryDiagnostic Diagnostic, Exception? Exception)>();
        await using var second = CreateServer(pipeName, diagnostic: (diagnostic, exception) =>
        {
            diagnostics.Enqueue((diagnostic, exception));
            if (diagnostic == RuntimeQueryDiagnostic.Stopped)
            {
                stopped.TrySetResult();
            }
        });

        second.Start();
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        var events = diagnostics.ToArray();
        var failure = Assert.Single(events, item => item.Diagnostic == RuntimeQueryDiagnostic.PipeCreationFailed);
        Assert.NotNull(failure.Exception);
        Assert.Single(events, item => item.Diagnostic == RuntimeQueryDiagnostic.Stopped);
        Assert.True(Array.FindIndex(events, item => item.Diagnostic == RuntimeQueryDiagnostic.PipeCreationFailed) <
                    Array.FindIndex(events, item => item.Diagnostic == RuntimeQueryDiagnostic.Stopped));
        Assert.True((await SendRequestAsync(pipeName)).Ok);
        await second.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.DoesNotContain(diagnostics, item => item.Diagnostic == RuntimeQueryDiagnostic.ShutdownFailed);
    }

    private static string NewPipeName() => $"nfh.runtimequery.core.tests.{Guid.NewGuid():N}";

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static RuntimeQueryIpcServer CreateServer(
        string pipeName,
        Func<RuntimeQueryRequest?, string, CancellationToken, Task<RuntimeQueryResponseEnvelope>>? handler = null,
        Action<RuntimeQueryDiagnostic, Exception?>? diagnostic = null)
    {
        return new RuntimeQueryIpcServer(pipeName, RuntimeQueryProtocol.Version,
            RuntimeQueryIpcHost.RequestReadTimeoutMs, RuntimeQueryIpcHost.ShutdownTimeoutMs,
            RuntimeQueryErrorMapper.Map, diagnostic,
            handler ?? ((_, _, _) => Task.FromResult(RuntimeQueryResponseEnvelope.Success(null))));
    }

    private static async Task<NamedPipeServerStream> WaitForPipeAsync(
        RuntimeQueryIpcServer server, NamedPipeServerStream? previous = null)
    {
        var activePipe = typeof(RuntimeQueryIpcServer).GetProperty("ActivePipe", BindingFlags.Instance | BindingFlags.NonPublic)!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        while (true)
        {
            if (activePipe.GetValue(server) is NamedPipeServerStream pipe && !ReferenceEquals(pipe, previous))
            {
                return pipe;
            }

            await Task.Delay(10, timeout.Token);
        }
    }

    private static async Task<NamedPipeClientStream> ConnectAndWriteAsync(string pipeName)
    {
        var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await client.ConnectAsync(3000, TestContext.Current.CancellationToken);
            using var writer = new StreamWriter(client, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync("{\"version\":\"1\",\"command\":\"status\",\"args\":null}")
                .WaitAsync(TestContext.Current.CancellationToken);
            return client;
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }

    private static Task<RuntimeQueryResponseEnvelope> SendRequestAsync(string pipeName)
    {
        return Task.Run(() => RuntimeQueryIpcClient.SendRequest(pipeName,
            new RuntimeQueryRequest(RuntimeQueryProtocol.Version, "status", null), 3000,
            RuntimeQueryErrorMapper.Map), TestContext.Current.CancellationToken);
    }
}

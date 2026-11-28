using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.Services;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the spinner host does when sending a request fails: a failure that sending can end in is ignored,
/// the visible state stays as it was. Other recoverable visibility failures are logged by the state sync,
/// and out-of-memory faults its task. Idle shutdown failures still reach the Dispose caller.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class CadLoadSpinnerProcessHostFailureTests
{
    // Long on purpose: it only turns a real hang into a failure.
    private static TimeSpan SafetyNetTimeout => TimeSpan.FromSeconds(30);

    private static OutOfMemoryException CreateOutOfMemory() =>
        (OutOfMemoryException)RuntimeHelpers.GetUninitializedObject(typeof(OutOfMemoryException));

    private static Exception SendFailure(string kind) => kind switch
    {
        "io" => new IOException("pipe broken"),
        "timeout" => new TimeoutException("no server"),
        "invalid-operation" => new InvalidOperationException("already connected"),
        "disposed" => new ObjectDisposedException("pipe"),
        "unauthorized" => new UnauthorizedAccessException("pipe belongs to another user"),
        "not-supported" => new NotSupportedException("no serialization info"),
        "argument" => new ArgumentOutOfRangeException(null, "timeout"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    // The shutdown tests call Dispose themselves because it is what they test. The second Dispose of `using` does nothing.
    private static CadLoadSpinnerProcessHost CreateHostFailingShutdown(Func<Exception> failure) =>
        new(request => request.Command == CadLoadSpinnerIpcProtocol.ShutdownCommand ? throw failure() : true);

    private static async Task AwaitStateSyncAsync(Task stateSync)
    {
        await stateSync.WaitAsync(SafetyNetTimeout);
    }

    [AvaloniaTheory]
    [InlineData("io")]
    [InlineData("timeout")]
    [InlineData("invalid-operation")]
    [InlineData("disposed")]
    [InlineData("unauthorized")]
    [InlineData("not-supported")]
    [InlineData("argument")]
    public async Task Show_WhenTheShowSendFailsWithAnExpectedException_StaysHidden(string kind)
    {
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var host = new CadLoadSpinnerProcessHost(request =>
        {
            if (request.Command == CadLoadSpinnerIpcProtocol.ShowCommand)
            {
                attempted.TrySetResult();
                throw SendFailure(kind);
            }
            return true;
        });

        host.Show(new Window());
        await attempted.Task.WaitAsync(SafetyNetTimeout, TestContext.Current.CancellationToken);

        var snapshot = host.GetVisibilityStateSnapshot();
        Assert.Equal("Visible", snapshot.Requested);
        Assert.Equal("Hidden", snapshot.Effective);
        host.Hide();
        await AwaitStateSyncAsync(host.LatestStateSyncTask);
    }

    [AvaloniaFact]
    public async Task Show_WhenTheShowSendRunsOutOfMemory_FaultsTheStateSync()
    {
        using var host = new CadLoadSpinnerProcessHost(request =>
            request.Command == CadLoadSpinnerIpcProtocol.ShowCommand ? throw CreateOutOfMemory() : true);

        host.Show(new Window());
        await Assert.ThrowsAsync<OutOfMemoryException>(async () =>
            await host.LatestStateSyncTask.WaitAsync(SafetyNetTimeout, TestContext.Current.CancellationToken));
    }

    [AvaloniaFact]
    public async Task Show_WhenTheShowSendFailsWithAnUndocumentedException_LogsTheFailure()
    {
        var previousConfiguration = LogManager.Configuration;
        using var target = new MemoryTarget { Layout = "${level}|${message}|${exception:format=Type,Message}" };
        var configuration = new LoggingConfiguration();
        configuration.AddRule(LogLevel.Warn, LogLevel.Warn, target, typeof(CadLoadSpinnerProcessHost).FullName!);
        try
        {
            LogManager.Configuration = configuration;
            using var host = new CadLoadSpinnerProcessHost(request =>
                request.Command == CadLoadSpinnerIpcProtocol.ShowCommand ? throw new FormatException("unexpected") : true);

            host.Show(new Window());
            await AwaitStateSyncAsync(host.LatestStateSyncTask);

            var message = Assert.Single(target.Logs);
            Assert.Contains("Failed to sync CAD load spinner visibility state.", message, StringComparison.Ordinal);
            Assert.Contains("FormatException", message, StringComparison.Ordinal);
            Assert.Contains("unexpected", message, StringComparison.Ordinal);
        }
        finally
        {
            LogManager.Configuration = previousConfiguration;
        }
    }

    [AvaloniaTheory]
    [InlineData("io")]
    [InlineData("timeout")]
    [InlineData("invalid-operation")]
    [InlineData("disposed")]
    [InlineData("unauthorized")]
    [InlineData("not-supported")]
    [InlineData("argument")]
    public async Task Hide_WhenTheHideSendFailsWithAnExpectedException_KeepsTheSpinnerVisible(string kind)
    {
        using var host = new CadLoadSpinnerProcessHost(request =>
            request.Command == CadLoadSpinnerIpcProtocol.HideCommand ? throw SendFailure(kind) : true);
        host.Show(new Window());
        await AwaitStateSyncAsync(host.LatestStateSyncTask);
        Assert.Equal("Visible", host.GetVisibilityStateSnapshot().Effective);

        host.Hide();
        await AwaitStateSyncAsync(host.LatestStateSyncTask);

        var snapshot = host.GetVisibilityStateSnapshot();
        Assert.Equal("Hidden", snapshot.Requested);
        Assert.Equal("Visible", snapshot.Effective);
    }

    [AvaloniaFact]
    public async Task Hide_WhenTheHideSendRunsOutOfMemory_FailsTheStateSync()
    {
        using var host = new CadLoadSpinnerProcessHost(request =>
            request.Command == CadLoadSpinnerIpcProtocol.HideCommand ? throw CreateOutOfMemory() : true);
        host.Show(new Window());

        host.Hide();
        OutOfMemoryException? thrown = null;
        try
        {
            await AwaitStateSyncAsync(host.LatestStateSyncTask);
        }
        catch (OutOfMemoryException ex)
        {
            thrown = ex;
        }

        Assert.NotNull(thrown);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("timeout")]
    [InlineData("invalid-operation")]
    [InlineData("disposed")]
    [InlineData("unauthorized")]
    [InlineData("not-supported")]
    [InlineData("argument")]
    public void Dispose_WhenTheShutdownSendFailsWithAnExpectedException_DoesNotThrow(string kind)
    {
        using var host = CreateHostFailingShutdown(() => SendFailure(kind));

        var exception = Record.Exception(host.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_WhenTheShutdownSendRunsOutOfMemory_Propagates()
    {
        using var host = CreateHostFailingShutdown(CreateOutOfMemory);

        Assert.Throws<OutOfMemoryException>(host.Dispose);
    }

    [Fact]
    public void Dispose_WhenTheShutdownSendFailsWithAnUndocumentedException_Propagates()
    {
        using var host = CreateHostFailingShutdown(() => new FormatException("unexpected"));

        Assert.Throws<FormatException>(host.Dispose);
    }
}

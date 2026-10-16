using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the spinner host does when sending a request fails: a failure that sending can end in is ignored,
/// the visible state stays as it was, and any other failure, out-of-memory included, reaches the caller.
/// </summary>
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
    public void Show_WhenTheShowSendFailsWithAnExpectedException_StaysHidden(string kind)
    {
        using var host = new CadLoadSpinnerProcessHost(request =>
            request.Command == CadLoadSpinnerIpcProtocol.ShowCommand ? throw SendFailure(kind) : true);

        host.Show(new Window());

        var snapshot = host.GetVisibilityStateSnapshot();
        Assert.Equal("Visible", snapshot.Requested);
        Assert.Equal("Hidden", snapshot.Effective);
    }

    [AvaloniaFact]
    public void Show_WhenTheShowSendRunsOutOfMemory_Propagates()
    {
        using var host = new CadLoadSpinnerProcessHost(request =>
            request.Command == CadLoadSpinnerIpcProtocol.ShowCommand ? throw CreateOutOfMemory() : true);

        Assert.Throws<OutOfMemoryException>(() => host.Show(new Window()));
    }

    [AvaloniaFact]
    public void Show_WhenTheShowSendFailsWithAnUndocumentedException_Propagates()
    {
        using var host = new CadLoadSpinnerProcessHost(request =>
            request.Command == CadLoadSpinnerIpcProtocol.ShowCommand ? throw new FormatException("unexpected") : true);

        Assert.Throws<FormatException>(() => host.Show(new Window()));
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
        Assert.Equal("Visible", host.GetVisibilityStateSnapshot().Effective);

        host.Hide();
        await AwaitStateSyncAsync(host.GetLatestStateSyncTask());

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
        var thrown = await Record.ExceptionAsync(() => AwaitStateSyncAsync(host.GetLatestStateSyncTask()));

        Assert.IsType<OutOfMemoryException>(thrown);
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

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadLoadSpinnerProcessHostTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(@"C:\tests\testhost.exe", false)]
    [InlineData(@"C:\tests\TESTHOST.x86.exe", false)]
    [InlineData(@"C:\FreeformHelper\FreeformHelper.UI.exe", true)]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe", true)]
    public void CanLaunchSpinnerProcessForPath_RejectsTestHostsOnly(
        string? processPath,
        bool expected)
    {
        Assert.Equal(expected, CadLoadSpinnerProcessHost.CanLaunchSpinnerProcessForPath(processPath));
    }

    [Fact]
    public void BuildSpinnerArgs_EncodesParentAndOwnerBounds()
    {
        var args = CadLoadSpinnerProcessHost.BuildSpinnerArgs(
            parentProcessId: 123,
            pipeName: "freeformhelper.cadloadspinner.123");

        Assert.Equal(
        [
            "--cad-load-spinner",
            "--parent-pid", "123",
            "--pipe-name", "freeformhelper.cadloadspinner.123",
        ], args);
    }

    [Fact]
    public void CadLoadSpinnerCommandLine_TryParse_RoundTripsSpinnerArgs()
    {
        var args = CadLoadSpinnerProcessHost.BuildSpinnerArgs(
            parentProcessId: 222,
            pipeName: "freeformhelper.cadloadspinner.222");

        var ok = CadLoadSpinnerCommandLine.TryParse(args, out var options);

        Assert.True(ok);
        Assert.Equal(222, options.ParentProcessId);
        Assert.Equal("freeformhelper.cadloadspinner.222", options.PipeName);
    }

    [Fact]
    public void VisibilitySnapshot_DefaultsToHiddenStates()
    {
        using var host = new CadLoadSpinnerProcessHost();

        var snapshot = host.GetVisibilityStateSnapshot();

        Assert.Equal("Hidden", snapshot.Requested);
        Assert.Equal("Hidden", snapshot.Effective);
    }

    [Fact]
    public void Dispose_ResetsRequestedAndEffectiveVisibilityToHidden()
    {
        using var host = new CadLoadSpinnerProcessHost();
        SetVisibilityState(host, "_requestedVisibility", "Visible");
        SetVisibilityState(host, "_effectiveVisibility", "Visible");

        host.Dispose();
        var snapshot = host.GetVisibilityStateSnapshot();

        Assert.Equal("Hidden", snapshot.Requested);
        Assert.Equal("Hidden", snapshot.Effective);
    }

    [Fact]
    public async Task Hide_WhenSpinnerIsNotVisible_CompletesStateSync()
    {
        using var host = new CadLoadSpinnerProcessHost();

        host.Hide();
        var stateSync = host.GetLatestStateSyncTask();
        var finished = await Task.WhenAny(stateSync, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        // A state sync that never ends holds a thread-pool thread in a busy loop until the host is disposed.
        Assert.Same(stateSync, finished);
    }

    [AvaloniaFact]
    public async Task Show_WhileHideSendIsInFlight_RestoresVisibleState()
    {
        var hideSendEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseHideSend = new ManualResetEventSlim(false);
        using var host = new CadLoadSpinnerProcessHost(request =>
        {
            if (request.Command == CadLoadSpinnerIpcProtocol.HideCommand)
            {
                hideSendEntered.TrySetResult();
                releaseHideSend.Wait();
            }

            return true;
        });
        var owner = new Window();

        host.Show(owner);
        Assert.Equal("Visible", host.GetVisibilityStateSnapshot().Effective);

        host.Hide();
        var stateSync = host.GetLatestStateSyncTask();
        try
        {
            await AwaitWithStateAsync(hideSendEntered.Task, "The hide message was not sent", host, stateSync);
            host.Show(owner);
        }
        finally
        {
            releaseHideSend.Set();
        }

        await AwaitWithStateAsync(stateSync, "The state sync did not finish", host, stateSync);
        var snapshot = host.GetVisibilityStateSnapshot();
        Assert.Equal("Visible", snapshot.Requested);
        Assert.Equal("Visible", snapshot.Effective);
    }

    // Long on purpose: it is only the safety net that turns a real hang into a failure with the state in the message.
    private static TimeSpan SafetyNetTimeout => TimeSpan.FromSeconds(30);

    private static async Task AwaitWithStateAsync(Task awaited, string what, CadLoadSpinnerProcessHost host, Task stateSync)
    {
        try
        {
            await awaited.WaitAsync(SafetyNetTimeout);
        }
        catch (TimeoutException)
        {
            var snapshot = host.GetVisibilityStateSnapshot();
            Assert.Fail(
                $"{what} within {SafetyNetTimeout.TotalSeconds:0} s. Requested={snapshot.Requested}, Effective={snapshot.Effective}, state sync status={stateSync.Status}.");
        }
    }

    private static void SetVisibilityState(CadLoadSpinnerProcessHost host, string fieldName, string stateName)
    {
        var field = typeof(CadLoadSpinnerProcessHost).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        var stateType = field!.FieldType;
        var value = Enum.Parse(stateType, stateName);
        field.SetValue(host, value);
    }
}

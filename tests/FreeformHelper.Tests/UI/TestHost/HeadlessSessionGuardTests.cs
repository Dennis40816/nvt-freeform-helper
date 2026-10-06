using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.ViewModels;
using Xunit;
using Xunit.v3;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class HeadlessSessionGuardTests
{
    [AvaloniaFact]
    public void Before_RunsForHeadlessTests()
    {
        Assert.Equal(
            $"{nameof(HeadlessSessionGuardTests)}.{nameof(Before_RunsForHeadlessTests)}",
            HeadlessSessionGuardAttribute.CurrentHeadlessTest);
    }

    [AvaloniaFact]
    public void After_ClosesTheWindowsOfAHeadlessTest()
    {
        var window = new Window { Content = new TextBlock { Text = "left open" } };
        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.Show();
        var thisTest = typeof(HeadlessSessionGuardTests).GetMethod(nameof(After_ClosesTheWindowsOfAHeadlessTest))!;

        new HeadlessSessionGuardAttribute().After(thisTest, (IXunitTest)TestContext.Current.Test!);

        Assert.True(closed);
    }

    [AvaloniaFact]
    public void After_MarksTheTestAsEnded_SoLaterWorkThatThrowsDoesNotEscape()
    {
        var thisTest = typeof(HeadlessSessionGuardTests).GetMethod(nameof(After_MarksTheTestAsEnded_SoLaterWorkThatThrowsDoesNotEscape))!;
        new HeadlessSessionGuardAttribute().After(thisTest, (IXunitTest)TestContext.Current.Test!);
        Dispatcher.UIThread.Post(static () => throw new InvalidOperationException("work after the hook"));

        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            HeadlessSessionGuardAttribute.SnapshotLeftoverWorkFailures(),
            static failure => failure.Exception.Message == "work after the hook");
    }

    [AvaloniaFact]
    public void Before_RejectsAContextThatAHeadlessTestAlreadyRanWith()
    {
        var thisTest = typeof(HeadlessSessionGuardTests).GetMethod(nameof(Before_RejectsAContextThatAHeadlessTestAlreadyRanWith))!;
        var guard = new HeadlessSessionGuardAttribute();

        // xUnit v3 runs the hook and test body with the same session context. The automatic
        // Before call has already recorded it, so another call must reject it.

        Assert.Throws<InvalidOperationException>(() => guard.Before(thisTest, (IXunitTest)TestContext.Current.Test!));
    }

    [AvaloniaFact]
    public void CloseOpenWindowsAndRunQueuedWork_WhenOneWindowFailsToClose_StillClosesTheOthersAndReportsIt()
    {
        var failing = new Window { Content = new TextBlock { Text = "fails to close" } };
        EventHandler<WindowClosingEventArgs> fail = static (_, _) => throw new InvalidOperationException("closing failed");
        failing.Closing += fail;
        var other = new Window { Content = new TextBlock { Text = "closes" } };
        var otherClosed = false;
        other.Closed += (_, _) => otherClosed = true;
        other.Show();
        // Windows close most-recent-first, so the failing window must be encountered first.
        failing.Show();
        var queuedWorkRan = false;
        Dispatcher.UIThread.Post(() => queuedWorkRan = true);

        try
        {
            var exception = Assert.Throws<AggregateException>(HeadlessSessionGuardAttribute.CloseOpenWindowsAndRunQueuedWork);

            Assert.True(otherClosed);
            Assert.True(queuedWorkRan);
            Assert.Contains(exception.InnerExceptions, static inner => inner.Message == "closing failed");
        }
        finally
        {
            failing.Closing -= fail;
            failing.Close();
        }
    }

    [AvaloniaFact]
    public void After_LeavesWindowsAloneForOtherTests()
    {
        var window = new Window { Content = new TextBlock { Text = "still needed" } };
        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.Show();
        var plainTest = typeof(HeadlessSessionGuardTests).GetMethod(nameof(ThrowIfContextWasUsedBefore_FailsTheSecondTestThatSeesTheSameContext))!;

        new HeadlessSessionGuardAttribute().After(plainTest, (IXunitTest)TestContext.Current.Test!);

        Assert.False(closed);
    }

    [Fact]
    public void ThrowIfContextWasUsedBefore_FailsTheSecondTestThatSeesTheSameContext()
    {
        var context = new SynchronizationContext();

        HeadlessSessionGuardAttribute.ThrowIfContextWasUsedBefore(context, previousHeadlessTest: null);
        var exception = Assert.Throws<InvalidOperationException>(
            () => HeadlessSessionGuardAttribute.ThrowIfContextWasUsedBefore(context, "SomeTests.Earlier"));

        Assert.Contains("SomeTests.Earlier", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThrowIfContextWasUsedBefore_AcceptsANewContextForEveryTest()
    {
        HeadlessSessionGuardAttribute.ThrowIfContextWasUsedBefore(new SynchronizationContext(), "SomeTests.Earlier");
        HeadlessSessionGuardAttribute.ThrowIfContextWasUsedBefore(new SynchronizationContext(), "SomeTests.Earlier");
    }

    [AvaloniaFact]
    public void CloseOpenWindowsAndRunQueuedWork_ClosesAMainWindowThatCancelledClose()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var window = new MainWindow();
        using var shell = new ShellViewModel();
        shell.FreeformHelper.HasUnsavedChanges = true;
        window.SetShellViewModel(shell);
        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var store = AppLogStore.Instance;
        store.MarkUiReady();
        store.Add(new AppLogEntry(DateTimeOffset.UtcNow, "INFO", "test", "before window closes"));
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("before window closes", shell.ConsoleText);

        // Unsaved changes make the window cancel the close and ask first.
        window.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.False(closed);
        var textBeforeClose = shell.ConsoleText;
        store.Add(new AppLogEntry(DateTimeOffset.UtcNow, "INFO", "test", "after cancelled close"));
        Dispatcher.UIThread.RunJobs();
        Assert.NotEqual(textBeforeClose, shell.ConsoleText);

        HeadlessSessionGuardAttribute.CloseOpenWindowsAndRunQueuedWork();

        Assert.True(closed);
        var textAfterClose = shell.ConsoleText;
        store.Add(new AppLogEntry(DateTimeOffset.UtcNow, "INFO", "test", "after window closes"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(textAfterClose, shell.ConsoleText);
    }

    [AvaloniaFact]
    public void LeftoverWork_ThatThrowsAfterTheTestEnded_DoesNotEscapeTheDispatcher()
    {
        const string test = nameof(LeftoverWork_ThatThrowsAfterTheTestEnded_DoesNotEscapeTheDispatcher);
        var watch = HeadlessSessionGuardAttribute.WatchForLeftoverWork(test);
        watch.TestEnded = true;
        Dispatcher.UIThread.Post(static () => throw new InvalidOperationException("leftover work"));

        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            HeadlessSessionGuardAttribute.SnapshotLeftoverWorkFailures(),
            failure => failure.Test == test && failure.Exception.Message == "leftover work");
    }

    [AvaloniaFact]
    public void Work_ThatThrowsWhileTheTestIsRunning_StillEscapesTheDispatcher()
    {
        HeadlessSessionGuardAttribute.WatchForLeftoverWork(nameof(Work_ThatThrowsWhileTheTestIsRunning_StillEscapesTheDispatcher));
        Dispatcher.UIThread.Post(static () => throw new InvalidOperationException("work of the running test"));

        var exception = Assert.Throws<InvalidOperationException>(static () => Dispatcher.UIThread.RunJobs());

        Assert.Equal("work of the running test", exception.Message);
    }

    [AvaloniaFact]
    public void CloseAtTestEnd_ClosesAWindowThatWasMeasuredButNeverShown()
    {
        var window = new Window { Content = new TextBlock { Text = "measured only" } };
        var closed = false;
        window.Closed += (_, _) => closed = true;
        HeadlessSessionGuardAttribute.CloseAtTestEnd(window);
        var size = new Size(320, 200);
        window.Measure(size);
        window.Arrange(new Rect(size));

        HeadlessSessionGuardAttribute.CloseOpenWindowsAndRunQueuedWork();

        Assert.True(closed);
    }
}

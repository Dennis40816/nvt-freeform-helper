using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.ViewModels;
using Xunit;

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

        new HeadlessSessionGuardAttribute().After(thisTest);

        Assert.True(closed);
    }

    [AvaloniaFact]
    public void After_MarksTheTestAsEnded_SoLaterWorkThatThrowsDoesNotEscape()
    {
        var thisTest = typeof(HeadlessSessionGuardTests).GetMethod(nameof(After_MarksTheTestAsEnded_SoLaterWorkThatThrowsDoesNotEscape))!;
        new HeadlessSessionGuardAttribute().After(thisTest);
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

        // The test body runs with a context of its own (xunit wraps the session's), so the first call
        // sees it for the first time and the second call sees it again.
        guard.Before(thisTest);

        Assert.Throws<InvalidOperationException>(() => guard.Before(thisTest));
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

        new HeadlessSessionGuardAttribute().After(plainTest);

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
        var shell = new ShellViewModel();
        shell.FreeformHelper.HasUnsavedChanges = true;
        window.SetShellViewModel(shell);
        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Unsaved changes make the window cancel the close and ask first.
        window.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.False(closed);

        HeadlessSessionGuardAttribute.CloseOpenWindowsAndRunQueuedWork();

        Assert.True(closed);
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

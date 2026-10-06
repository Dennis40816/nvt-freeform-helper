using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit.v3;

[assembly: FreeformHelper.Tests.HeadlessSessionGuard]

namespace FreeformHelper.Tests;

/// <summary>
/// Keeps the headless session usable from one test to the next.
/// </summary>
/// <remarks>
/// When a headless test ends, the session disposes the font manager and then runs the work still queued
/// on the dispatcher. A window that is still alive has a layout pass queued, which then throws. The session
/// swallows that exception and skips the rest of its teardown, so its thread keeps the synchronization
/// context of the finished test: every await in the following headless tests is posted to a dispatcher that
/// no longer runs, and the run stalls. <c>MainWindow</c> is such a window: it cancels <c>Close()</c> to ask
/// about unsaved work. So after each headless test the windows it left open are closed and the queued work
/// runs while the session is still intact; work that throws later is kept from breaking the teardown; and
/// before each headless test a leftover context fails the test at once instead of stalling it.
/// <para>
/// This relies on the session building a new application for every test (the default,
/// <c>AvaloniaTestIsolationLevel.PerTest</c>); with one application per assembly every test after the
/// first would be reported as having a leftover context.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class HeadlessSessionGuardAttribute : BeforeAfterTestAttribute
{
    private const int MaxClosePasses = 3;
    private static readonly object Marker = new();
    private static readonly object Gate = new();
    private static readonly List<Window> OpenWindows = [];
    private static readonly List<(string Test, Exception Exception)> LeftoverWorkFailures = [];
    private static readonly ConditionalWeakTable<SynchronizationContext, object> SeenContexts = [];
    private static bool s_tracksOpenedWindows;
    private static LeftoverWorkWatch? s_leftoverWork;
    private static string? s_previousHeadlessTest;

    internal static string? CurrentHeadlessTest { get; private set; }

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        ArgumentNullException.ThrowIfNull(methodUnderTest);
        if (!IsHeadlessTest(methodUnderTest))
        {
            return;
        }

        CurrentHeadlessTest = DescribeTest(methodUnderTest);
        TrackOpenedWindows();
        s_leftoverWork = WatchForLeftoverWork(CurrentHeadlessTest);

        var context = SynchronizationContext.Current;
        if (context is not null)
        {
            ThrowIfContextWasUsedBefore(context, s_previousHeadlessTest);
        }
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        ArgumentNullException.ThrowIfNull(methodUnderTest);
        if (!IsHeadlessTest(methodUnderTest))
        {
            return;
        }

        s_previousHeadlessTest = DescribeTest(methodUnderTest);
        CurrentHeadlessTest = null;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return;
        }

        try
        {
            CloseOpenWindowsAndRunQueuedWork();
        }
        finally
        {
            if (s_leftoverWork is { } leftoverWork)
            {
                leftoverWork.TestEnded = true;
            }
        }
    }

    /// <summary>
    /// Watches the dispatcher of the test that is starting. Work that other threads post to it runs on the
    /// session thread too. Once the test is over, or while the session is taking the application apart (the
    /// font manager is disposed first), such work can throw for no reason of its own, and one exception there
    /// makes the session skip the rest of its teardown. Those exceptions are marked as handled and reported
    /// on the error stream. An exception while the test is still running is left alone: it fails that test.
    /// </summary>
    internal static LeftoverWorkWatch WatchForLeftoverWork(string test)
    {
        var watch = new LeftoverWorkWatch(test);
        Dispatcher.UIThread.UnhandledException += (sender, e) =>
        {
            if (!watch.TestEnded && !IsSessionTeardown(sender))
            {
                return;
            }

            e.Handled = true;
            lock (Gate)
            {
                LeftoverWorkFailures.Add((watch.Test, e.Exception));
            }

            Console.Error.WriteLine(
                $"[HeadlessSessionGuard] work on the dispatcher of {watch.Test} threw after the test ended or during session teardown: {e.Exception}");
        };
        return watch;
    }

    // The session replaces the dispatcher implementation during teardown, so it no longer supports run loops.
    private static bool IsSessionTeardown(object? sender)
    {
        return sender is Dispatcher { SupportsRunLoops: false };
    }

    /// <summary>
    /// The exceptions thrown by leftover work in this test run, each with the test whose dispatcher it ran
    /// on. The work itself may have been started by an earlier test or by a test running in parallel.
    /// </summary>
    internal static IReadOnlyList<(string Test, Exception Exception)> SnapshotLeftoverWorkFailures()
    {
        lock (Gate)
        {
            return [.. LeftoverWorkFailures];
        }
    }

    /// <summary>
    /// Registers a window that the test never shows, so that it is closed when the test ends. A window that
    /// is only measured still attaches its content, which then keeps receiving work after the test.
    /// </summary>
    internal static void CloseAtTestEnd(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Track(window);
    }

    /// <summary>
    /// The session installs a new context for every test. One that an earlier test already ran with belongs
    /// to that test's dispatcher, which no longer runs. Once that happens the session stays broken, so every
    /// later headless test of the run fails here too; the first such failure names the test to look at.
    /// </summary>
    internal static void ThrowIfContextWasUsedBefore(SynchronizationContext context, string? previousHeadlessTest)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (SeenContexts.TryGetValue(context, out _))
        {
            throw new InvalidOperationException(
                "The headless session still has the synchronization context of an earlier test, so awaits in this test " +
                "would never resume. Work left behind by an earlier headless test threw while the session was torn down " +
                $"(last finished before this one: {previousHeadlessTest ?? "none"}). Every later headless test of this run " +
                "fails the same way; look at the first one.");
        }

        SeenContexts.Add(context, Marker);
    }

    /// <summary>
    /// Closes every window that is still open, the most recently opened first, and runs the queued work.
    /// </summary>
    internal static void CloseOpenWindowsAndRunQueuedWork()
    {
        List<Exception>? failures = null;
        try
        {
            // Queued work may open further windows, so close again until nothing is left.
            for (var pass = 0; pass < MaxClosePasses; pass++)
            {
                var windows = SnapshotOpenWindows();
                for (var index = windows.Length - 1; index >= 0; index--)
                {
                    var window = windows[index];
                    if (!IsTracked(window))
                    {
                        continue;
                    }

                    try
                    {
                        // A window may cancel Close() to ask a question; MainWindow does so for a project
                        // that has not been saved. Without its view model it has nothing to ask about.
                        window.DataContext = null;
                    }
                    catch (Exception exception)
                    {
                        (failures ??= []).Add(exception);
                    }

                    try
                    {
                        window.Close();
                    }
                    catch (Exception exception)
                    {
                        (failures ??= []).Add(exception);
                    }
                }

                Dispatcher.UIThread.RunJobs();
                if (SnapshotOpenWindows().Length == 0)
                {
                    break;
                }
            }
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        var stillOpen = SnapshotOpenWindows();
        lock (Gate)
        {
            OpenWindows.Clear();
        }

        foreach (var window in stillOpen)
        {
            window.Closed -= OnWindowClosed;
        }

        if (stillOpen.Length > 0)
        {
            var names = string.Join(", ", stillOpen.Select(static window => window.GetType().Name));
            (failures ??= []).Add(new InvalidOperationException(
                $"Windows refused to close at the end of a headless test: {names}. " +
                "A window left open breaks the headless session for the tests that follow."));
        }

        if (failures is not null)
        {
            throw new AggregateException("Closing the windows of a headless test failed.", failures);
        }
    }

    // Runs on the session thread, so the window types are initialised there and not on a test runner thread.
    private static void TrackOpenedWindows()
    {
        if (s_tracksOpenedWindows)
        {
            return;
        }

        s_tracksOpenedWindows = true;
        Window.WindowOpenedEvent.AddClassHandler(typeof(Window), static (sender, _) =>
        {
            if (sender is Window window)
            {
                Track(window);
            }
        });
    }

    private static void Track(Window window)
    {
        lock (Gate)
        {
            if (OpenWindows.Contains(window))
            {
                return;
            }

            OpenWindows.Add(window);
        }

        window.Closed += OnWindowClosed;
    }

    private static void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        window.Closed -= OnWindowClosed;
        lock (Gate)
        {
            OpenWindows.Remove(window);
        }
    }

    private static Window[] SnapshotOpenWindows()
    {
        lock (Gate)
        {
            return [.. OpenWindows];
        }
    }

    private static bool IsTracked(Window window)
    {
        lock (Gate)
        {
            return OpenWindows.Contains(window);
        }
    }

    private static bool IsHeadlessTest(MethodInfo method)
    {
        return method.IsDefined(typeof(AvaloniaFactAttribute), inherit: false)
            || method.IsDefined(typeof(AvaloniaTheoryAttribute), inherit: false);
    }

    private static string DescribeTest(MethodInfo method)
    {
        return $"{method.DeclaringType?.Name}.{method.Name}";
    }

    /// <summary>The state of one headless test as far as leftover work is concerned.</summary>
    internal sealed class LeftoverWorkWatch(string test)
    {
        public string Test { get; } = test;

        public bool TestEnded { get; set; }
    }
}

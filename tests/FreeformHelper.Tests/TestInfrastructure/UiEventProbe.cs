// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FreeformHelper.UI;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.Threading;

namespace FreeformHelper.Tests.TestInfrastructure;

internal sealed class UiEventProbe
{
    public List<(string Operation, Exception Exception)> Reports { get; } = [];
    public List<(string Operation, Exception Exception)> FallbackReports { get; } = [];
    public TaskCompletionSource<(string Operation, Exception Exception)> Reported { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public UiEventRunner Runner { get; }

    public UiEventProbe()
    {
        Runner = new UiEventRunner(Report, (operation, exception) => FallbackReports.Add((operation, exception)));
    }

    /// <summary>Raises the routed click on an existing button to run its code-behind handler.</summary>
    public static void Click(Control root, Func<Button, bool> predicate)
    {
        var button = root.GetVisualDescendants().OfType<Button>().Single(predicate);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private void Report(string operation, Exception exception)
    {
        Reports.Add((operation, exception));
        Reported.TrySetResult((operation, exception));
    }
}

internal sealed class ClosingWindowFixture : IDisposable
{
    private readonly ShellViewModel _shell;

    public FreeformHelperViewModel Helper { get; }
    public MainWindow Window { get; }
    public int ClosedCount { get; private set; }
    public TaskCompletionSource Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ClosingWindowFixture(Func<Window, Task<bool?>> confirmSave, UiEventRunner? runner = null)
    {
        Helper = new FreeformHelperViewModel(null, runner);
        _shell = new ShellViewModel(Helper) { IsDevPageAvailable = true };
        _shell.ShowHowToUseCommand.Execute(null);
        Window = new MainWindow(new MainWindowCloseSeams(confirmSave));
        Window.SetShellViewModel(_shell);
        Window.Closed += (_, _) =>
        {
            ClosedCount++;
            Closed.TrySetResult();
        };
        Window.Show();
        Helper.HasUnsavedChanges = true;
    }

    public void Dispose()
    {
        Window.DataContext = null;
        Window.Close();
        _shell.Dispose();
    }
}

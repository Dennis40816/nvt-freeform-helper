using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI;

/// <summary>
/// Represents the main application window of the FreeformHelper UI.
/// This class handles window-level events and interactions, particularly related to application shutdown.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly WindowNotificationManager _notificationManager;
    private readonly MainWindowCloseSeams _closeSeams;
    private bool _isClosePending;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow() : this(new MainWindowCloseSeams(ConfirmSaveOnCloseAsync))
    {
    }

    internal MainWindow(MainWindowCloseSeams closeSeams)
    {
        _closeSeams = closeSeams;
        StartupPerfTracker.Mark("window.initcomponent-start");
        InitializeComponent(); // Initializes the Avalonia UI components defined in MainWindow.axaml.
        StartupPerfTracker.Mark("window.initcomponent-complete");
        _notificationManager = new WindowNotificationManager(this)
        {
            Position = NotificationPosition.TopRight,
            MaxItems = 3
        };
        StartupPerfTracker.Mark("workspace.startup-overlay-hidden", "disabled");
        Closing += OnClosing; // Subscribes to the window's Closing event to handle unsaved changes.
    }

    public void SetShellViewModel(ViewModels.ShellViewModel shellViewModel)
    {
        DataContext = shellViewModel;
        Closed += (_, _) => shellViewModel.Dispose();
    }

    public void ShowTopToast(string message, NotificationType type = NotificationType.Information)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _notificationManager.Show(new Notification(
            title: "Freeform Helper",
            message: message,
            type: type,
            expiration: TimeSpan.FromSeconds(2.5)));
    }

    /// <summary>
    /// Event handler for the window's Closing event. This method asynchronously prompts the user
    /// to save changes if there are unsaved modifications to the project before the application exits.
    /// </summary>
    /// <param name="sender">The source of the event (this window).</param>
    /// <param name="e">Event arguments containing a <c>Cancel</c> property that can prevent closure.</param>
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isClosePending)
        {
            e.Cancel = true;
            return;
        }

        if (DataContext is not ShellViewModel shell || !shell.FreeformHelper.ShouldPromptSaveOnExit())
        {
            return;
        }

        e.Cancel = true;
        _isClosePending = true;
        shell.UiEvents.Run("Window.ClosingSave", _ => SaveBeforeClosingAsync(shell.FreeformHelper), CancellationToken.None);
    }

    private async Task SaveBeforeClosingAsync(FreeformHelperViewModel helper)
    {
        try
        {
            var result = await _closeSeams.ConfirmSaveAsync(this);
            if (result is null) return;
            if (result.Value)
            {
                var save = helper.TryStartSaveProjectCommand();
                if (save is null || !await save) return;
            }

            Closing -= OnClosing;
            Close();
        }
        finally
        {
            _isClosePending = false;
        }
    }

    private static Task<bool?> ConfirmSaveOnCloseAsync(Window owner)
    {
        var dialog = new Views.ConfirmDialog(
            "Unsaved project",
            "Project has not been saved. Save before exiting?",
            "Save and exit",
            "Exit without saving",
            emphasizeCancel: true);
        return dialog.ShowDialog<bool?>(owner);
    }
}

internal sealed record MainWindowCloseSeams(Func<Window, Task<bool?>> ConfirmSaveAsync);

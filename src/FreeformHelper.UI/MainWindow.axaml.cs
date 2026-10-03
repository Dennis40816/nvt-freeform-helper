using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI;

/// <summary>
/// Represents the main application window of the FreeformHelper UI.
/// This class handles window-level events and interactions, particularly related to application shutdown.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly WindowNotificationManager _notificationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        StartupPerfTracker.Mark("window.initcomponent-start");
        InitializeComponent(); // Initializes the Avalonia UI components defined in MainWindow.axaml.
        StartupPerfTracker.Mark("window.initcomponent-complete");
        _notificationManager = new WindowNotificationManager(this)
        {
            Position = NotificationPosition.TopRight,
            MaxItems = 3
        };
        StartupPerfTracker.Mark("workspace.startup-overlay-hidden", "disabled");
        Closing += OnClosingAsync; // Subscribes to the window's Closing event to handle unsaved changes.
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
    private async void OnClosingAsync(object? sender, WindowClosingEventArgs e)
    {
        // Attempt to cast the DataContext to ShellViewModel to access project state.
        if (DataContext is not ViewModels.ShellViewModel shell)
        {
            return; // If DataContext is not a ShellViewModel, or null, exit without further action.
        }

        var helper = shell.FreeformHelper; // Get the FreeformHelperViewModel instance.
        // Check if there are unsaved changes and if the user should be prompted.
        if (!helper.ShouldPromptSaveOnExit())
        {
            return; // No unsaved changes or no prompt needed, allow closing.
        }

        e.Cancel = true; // Temporarily cancel the closing operation to show the save dialog.

        // Create and show a confirmation dialog asking the user to save.
        var dialog = new Views.ConfirmDialog(
            "Unsaved project", // Title of the dialog.
            "Project has not been saved. Save before exiting?", // Message to the user.
            "Save and exit", // Text for the affirmative button.
            "Exit without saving", // Text for the negative button.
            emphasizeCancel: true); // Emphasize the "Exit without saving" option visually.

        // Show the dialog and await the user's response.
        var result = await dialog.ShowDialog<bool?>(this);

        // If the dialog was dismissed (e.g., by pressing Esc or clicking outside),
        // result will be null. In this case, keep the application open.
        if (result is null)
        {
            return;
        }

        // If the user chose to save and exit.
        if (result.Value)
        {
            var ok = await helper.SaveProjectAsync(); // Attempt to save the project.
            if (ok)
            {
                helper.HasUnsavedChanges = false; // Clear the unsaved changes flag.
                Closing -= OnClosingAsync; // Unsubscribe from the event to prevent re-prompting.
                Close(); // Close the window.
            }
            // If save failed, the window remains open and e.Cancel remains true.
        }
        else // If the user chose to exit without saving.
        {
            Closing -= OnClosingAsync; // Unsubscribe from the event.
            Close(); // Close the window.
        }
    }
}

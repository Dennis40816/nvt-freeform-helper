using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace FreeformHelper.UI.Controls;

public partial class WorkspaceHeader
{
    private void HeaderMenuButton_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not ToggleButton button) return;
        var range = button == RangeMenuButton;
        if (e.Key == Key.Escape && (range ? RangeMenuPopup : ViewMenuPopup).IsOpen)
        {
            ClosePopup(range);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Down) return;
        button.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
        FocusPopupInput(button == RangeMenuButton ? RangeMenuPopup : ViewMenuPopup);
        e.Handled = true;
    }

    private void HeaderMenuPopup_Opened(object? sender, EventArgs e)
    {
        if (sender is Popup popup && popup.PlacementTarget?.IsFocused == true)
        {
            Dispatcher.Post(() => FocusPopupInput(popup), Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    private static void FocusPopupInput(Popup popup)
    {
        popup.Child?.ApplyTemplate();
        popup.Child?.GetVisualDescendants().OfType<InputElement>()
            .FirstOrDefault(control => control is Avalonia.Controls.TextBox or Avalonia.Controls.ToggleSwitch &&
                control.Focusable && control.IsEffectivelyEnabled && control.IsEffectivelyVisible)
            ?.Focus(NavigationMethod.Tab);
    }

    private void HeaderMenuPopup_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        // Both popups can be open (Display stays pinned), so the sender says which one the key came from.
        ClosePopup(!ReferenceEquals(sender, ViewMenuPopupRoot));
        e.Handled = true;
    }

    private void ClosePopup(bool range)
    {
        if (!range) OnWindowDeactivated(this, EventArgs.Empty);
        (range ? RangeMenuPopup : ViewMenuPopup).SetCurrentValue(Popup.IsOpenProperty, false);
        (range ? RangeMenuButton : ViewMenuButton).Focus(NavigationMethod.Tab);
    }
}

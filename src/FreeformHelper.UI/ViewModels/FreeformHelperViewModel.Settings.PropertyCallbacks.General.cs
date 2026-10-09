using Avalonia.Media;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    partial void OnShowCadChanged(bool value) => SyncWorkspaceToggle("cadLayer", value);
    partial void OnShowRegularChanged(bool value) => SyncWorkspaceToggle("regularGrid", value);
    partial void OnHighlightUnmatchedChanged(bool value) => SyncWorkspaceToggle("highlightUnmatched", value);
    partial void OnHighlightFreeformChanged(bool value) => SyncWorkspaceToggle("highlightFreeform", value);
    partial void OnColorCadByAreaChanged(bool value) => SyncWorkspaceToggle("colorByArea", value);

    partial void OnCadLineOpacityChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        CanvasHost?.Invalidate();
    }

    partial void OnCadFillOpacityChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        CanvasHost?.Invalidate();
    }

    partial void OnRegularLineOpacityChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        CanvasHost?.Invalidate();
    }

    partial void OnRegularFillOpacityChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        CanvasHost?.Invalidate();
    }

    partial void OnCadLineColorHexChanged(string value)
    {
        // Attempt to parse hex string to Color and update property.
        if (Color.TryParse(value, out var c))
        {
            CadLineColor = c;
        }
    }

    partial void OnCadLineColorChanged(Color value)
    {
        using (_undoSuppression.Enter())
        {
            CadLineColorHex = ToRgbHex(value); // Update hex string.
        }
    }

    partial void OnRegularLineColorHexChanged(string value)
    {
        // Attempt to parse hex string to Color and update property.
        if (Color.TryParse(value, out var c))
        {
            RegularLineColor = c;
        }
    }

    partial void OnRegularLineColorChanged(Color value)
    {
        using (_undoSuppression.Enter())
        {
            RegularLineColorHex = ToRgbHex(value); // Update hex string.
        }
    }

    partial void OnRegularSelectedColorHexChanged(string value)
    {
        if (Color.TryParse(value, out var c))
        {
            RegularSelectedColor = c;
        }
    }

    partial void OnRegularSelectedColorChanged(Color value)
    {
        using (_undoSuppression.Enter())
        {
            RegularSelectedColorHex = ToRgbHex(value);
        }
    }

    partial void OnRegularSelectedFillOpacityChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        var clamped = Math.Clamp(value, 0.0m, 1.0m);
        if (clamped != value)
        {
            using (_undoSuppression.Enter())
            {
                RegularSelectedFillOpacity = clamped;
            }
        }
    }

    partial void OnHighlightStrokeWidthAdjustChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        var clamped = Math.Clamp(value, -2.0m, 4.0m);
        if (clamped != value)
        {
            using (_undoSuppression.Enter())
            {
                HighlightStrokeWidthAdjust = clamped;
            }
        }
    }

    partial void OnRecalcBoundsOnLayerFilterChanged(bool value)
    {
        if (_isLoadingSettings) return;
        ApplySettingsOrchestration(nameof(RecalcBoundsOnLayerFilter));
    }

    partial void OnApplyAppVisualPreferencesOnProjectLoadChanged(bool value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        SchedulePersistAppGeneralSettings();
    }
}

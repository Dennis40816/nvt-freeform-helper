using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Settings;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> manages the interaction
/// between the UI properties and the application's <see cref="ProjectSettings"/>.
/// It handles loading settings into the UI, applying UI changes back to settings,
/// and triggering related actions like grid rebuilds.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Rebuilds cascade per-IC settings from current channel and cascade counts.
    /// </summary>
    private void RebuildCascadeSettings()
    {
        // Unsubscribe from previous settings to prevent memory leaks or multiple subscriptions.
        foreach (var setting in CascadeIcSettings)
        {
            setting.PropertyChanged -= OnCascadeSettingChanged;
            setting.ChannelChanging -= OnCascadeSettingChanging;
        }

        var cascade = Math.Max(1, (int)Math.Round(CascadeNum));
        var baseX = Math.Max(1, (int)Math.Round(XChannels));
        var baseY = Math.Max(1, (int)Math.Round(YChannels));

        var layout = CascadeIcLayoutService.Build(
            cascade,
            baseX,
            baseY,
            _projectFile.Settings.Grid.PerIcXChannels,
            _projectFile.Settings.Grid.PerIcYChannels);
        var list = new ObservableCollection<CascadeIcSetting>();
        foreach (var row in layout.Rows)
        {
            var item = new CascadeIcSetting(row.IcIndex, row.XChannels, row.YChannels);
            item.PropertyChanged += OnCascadeSettingChanged; // Subscribe to changes.
            item.ChannelChanging += OnCascadeSettingChanging; // Subscribe to pending changes (for undo).
            list.Add(item);
        }

        CascadeIcSettings = list; // Update UI-bound collection.
        UpdateGlobalChannelsFromPerIc(list); // Sync global X/Y channels.
    }

    /// <summary>
    /// Event handler for changes in individual <see cref="CascadeIcSetting"/> properties.
    /// Updates the underlying project settings and triggers a grid rebuild.
    /// </summary>
    private void OnCascadeSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not CascadeIcSetting)
        {
            return;
        }

        // Collect current per-IC channel counts.
        var perIcX = CascadeIcSettings
            .Select(s => Math.Max(1, (int)Math.Round(s.XChannels)))
            .ToList();
        var perIcY = CascadeIcSettings
            .Select(s => Math.Max(1, (int)Math.Round(s.YChannels)))
            .ToList();

        // Update project settings.
        _projectFile.Settings.Grid.ReplacePerIcChannels(perIcX, perIcY);

        // Calculate new total X and max Y channels.
        var totalX = perIcX.Sum();
        var maxY = perIcY.Count == 0 ? 1 : perIcY.Max();

        // Update global X/Y channels if they differ, suppressing cascade rebuild during this update.
        if (XChannels != totalX)
        {
            _suppressCascadeRebuild = true;
            _suppressUndo = true; // Suppress undo for this programmatic change.
            XChannels = totalX;
            _suppressUndo = false;
            _suppressCascadeRebuild = false;
        }

        if (YChannels != maxY)
        {
            _suppressCascadeRebuild = true;
            _suppressUndo = true;
            YChannels = maxY;
            _suppressUndo = false;
            _suppressCascadeRebuild = false;
        }

        _ = TriggerGridRebuildAsync(requestFit: true); // Trigger grid rebuild.
    }

    /// <summary>
    /// Event handler for the <see cref="CascadeIcSetting.ChannelChanging"/> event.
    /// Used to track changes in cascade settings for undo/redo functionality.
    /// </summary>
    private void OnCascadeSettingChanging(object? sender, CascadeChannelChangingEventArgs e)
    {
        if (sender is not CascadeIcSetting setting)
        {
            return;
        }

        // Track the change as an undoable action.
        TrackUndo(e.OldValue, e.NewValue, value =>
        {
            _suppressUndo = true;
            if (e.PropertyName == nameof(CascadeIcSetting.XChannels))
            {
                setting.XChannels = value;
            }
            else if (e.PropertyName == nameof(CascadeIcSetting.YChannels))
            {
                setting.YChannels = value;
            }
            _suppressUndo = false;
        }, $"IC {setting.IcIndex} {e.PropertyName}");
    }

    /// <summary>
    /// Updates the global <see cref="XChannels"/> and <see cref="YChannels"/> properties
    /// based on the sum/max of individual IC channel settings.
    /// </summary>
    /// <param name="list">The list of <see cref="CascadeIcSetting"/>.</param>
    private void UpdateGlobalChannelsFromPerIc(ObservableCollection<CascadeIcSetting> list)
    {
        if (list.Count == 0)
        {
            return;
        }

        // Recalculate per-IC channel counts.
        var perIcX = list.Select(s => Math.Max(1, (int)Math.Round(s.XChannels))).ToList();
        var perIcY = list.Select(s => Math.Max(1, (int)Math.Round(s.YChannels))).ToList();

        // Update project settings.
        _projectFile.Settings.Grid.ReplacePerIcChannels(perIcX, perIcY);

        var totalX = perIcX.Sum();
        var maxY = perIcY.Max();

        // Update global X/Y channels if they differ, suppressing cascade rebuild during this update.
        if (XChannels != totalX || YChannels != maxY)
        {
            _suppressCascadeRebuild = true;
            _suppressUndo = true;
            XChannels = totalX;
            YChannels = maxY;
            _suppressUndo = false;
            _suppressCascadeRebuild = false;
        }
    }

    /// <summary>
    /// Clamps a decimal value to an integer within a specified min/max range.
    /// </summary>
    private static int ClampToInt(decimal value, int min, int max)
    {
        var v = (int)Math.Round(value);
        if (v < min) return min;
        if (v > max) return max;
        return v;
    }

    /// <summary>
    /// Converts an Avalonia <see cref="Color"/> to its RGB hexadecimal string representation.
    /// </summary>
    private static string ToRgbHex(Color c)
    {
        return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    /// <summary>
    /// Overrides the base <see cref="ObservableObject.OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs)"/> method to
    /// automatically mark the project as dirty (unsaved) if a persisted setting changes.
    /// </summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (_isLoadingSettings || e.PropertyName is null)
        {
            return; // Ignore property changes during settings loading or if property name is null.
        }

        if (e.PropertyName == nameof(HasUnsavedChanges))
        {
            return; // Don't mark as dirty if HasUnsavedChanges itself is changed.
        }

        // Check if the changed property is one of the persisted settings.
        if (DirtySettingNames.Value.Contains(e.PropertyName))
        {
            MarkUnsaved(); // Mark the project as unsaved.
            SchedulePersistAppGeneralSettings();
        }
    }

}

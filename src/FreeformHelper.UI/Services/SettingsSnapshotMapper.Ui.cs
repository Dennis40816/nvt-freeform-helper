// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Logging;

namespace FreeformHelper.UI.Services;

internal sealed partial class SettingsSnapshotMapper
{
    /// <summary>
    /// Applies persisted UI view settings from a <see cref="UiViewSnapshot"/>.
    /// </summary>
    /// <param name="snapshot">The snapshot containing view settings.</param>
    internal void ApplyViewSnapshot(UiViewSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        using (_host.EnterSettingsLoading())
        using (_host.EnterUndoSuppression())
        {

            _host.ShowCad = snapshot.ShowCad;
            _host.ShowRegular = snapshot.ShowRegular;
            _host.HighlightUnmatched = snapshot.HighlightUnmatched;
            _host.HighlightFreeform = snapshot.HighlightFreeform;
            _host.ColorCadByArea = snapshot.ColorCadByArea;

            _host.CadLineWidth = (decimal)snapshot.CadLineWidth;
            _host.CadFillOpacity = (decimal)Math.Clamp(snapshot.CadFillOpacity, 0.0, 1.0);
            _host.CadLineOpacity = snapshot.CadLineOpacity <= 0
                ? 1.0m
                : (decimal)Math.Clamp(snapshot.CadLineOpacity, 0.0, 1.0);
            if (!string.IsNullOrWhiteSpace(snapshot.CadLineColor))
            {
                _host.CadLineColorHex = snapshot.CadLineColor;
            }

            _host.RegularLineWidth = (decimal)snapshot.RegularLineWidth;
            _host.HighlightStrokeWidthAdjust = (decimal)Math.Clamp(snapshot.HighlightStrokeWidthAdjust, -2.0, 4.0);
            _host.RegularFillOpacity = (decimal)Math.Clamp(snapshot.RegularFillOpacity, 0.0, 1.0);
            _host.RegularLineOpacity = snapshot.RegularLineOpacity <= 0
                ? 1.0m
                : (decimal)Math.Clamp(snapshot.RegularLineOpacity, 0.0, 1.0);
            if (!string.IsNullOrWhiteSpace(snapshot.RegularLineColor))
            {
                _host.RegularLineColorHex = snapshot.RegularLineColor;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.RegularSelectedColor))
            {
                _host.RegularSelectedColorHex = snapshot.RegularSelectedColor;
            }
            if (snapshot.RegularSelectedFillOpacity > 0)
            {
                _host.RegularSelectedFillOpacity = (decimal)Math.Clamp(snapshot.RegularSelectedFillOpacity, 0.0, 1.0);
            }

            _host.AreaBucketTolerance = (decimal)snapshot.AreaBucketTolerance;
            _host.MaxAreaBuckets = snapshot.MaxAreaBuckets;
            _host.ShowDiffIndexOverlay = snapshot.ShowDiffIndexOverlay;
            _host.ShowNotchCanvasPreview = snapshot.ShowNotchCanvasPreview;
            _host.ShowNotchToRegularLabels = snapshot.ShowNotchToRegularLabels;
            _host.GlobalFontSizePercent = (decimal)Math.Clamp(snapshot.GlobalFontSizePercent, 80.0, 140.0);
            _host.NotchPreviewVisualizationStep = (decimal)Math.Clamp(snapshot.NotchPreviewVisualizationStep, 1.0, 3.0);
            _host.NotchPreviewAutoPlayIntervalMs = (decimal)Math.Clamp(snapshot.NotchPreviewAutoPlayIntervalMs, 200.0, 5000.0);
            _host.NotchPreviewAutoPlayEnabled = snapshot.NotchPreviewAutoPlayEnabled;
            _host.SelectNotchExportFileType(snapshot.NotchExportFileType);

            _host.ApplyDxfLayerImageExportSnapshot(snapshot);
            _host.CoordinatePixelWidth = (decimal)Math.Max(
                1.0,
                snapshot.CoordinatePixelWidth > 0.0
                    ? snapshot.CoordinatePixelWidth
                    : (double)_host.XChannels);
            _host.CoordinatePixelHeight = (decimal)Math.Max(
                1.0,
                snapshot.CoordinatePixelHeight > 0.0
                    ? snapshot.CoordinatePixelHeight
                    : (double)_host.YChannels);
            _host.CoordinatePreferredAaOutlineLayerName = snapshot.CoordinatePreferredAaOutlineLayerName?.Trim() ?? string.Empty;

        }
    }

    internal void ApplyImportSnapshot(UiImportSnapshot snapshot, byte[]? embeddedMask, string? embeddedMaskName)
    {
        if (snapshot is null)
        {
            return;
        }

        var allowRuntimeRefresh = !_host.IsLoadingSettings;
        using var settingsLoading = _host.EnterSettingsLoading();
        _host.ImportOnlyClosedPolylines = snapshot.OnlyClosedPolylines;
        _host.ImportBlockPolylines = snapshot.IncludeBlockPolylines;
        if (!string.IsNullOrWhiteSpace(snapshot.LogLevel))
        {
            var currentLevel = LoggingBootstrapper.GetCurrentMinimumLevelName();
            var normalizedLevel = snapshot.LogLevel.Trim();
            if (!string.Equals(currentLevel, normalizedLevel, StringComparison.OrdinalIgnoreCase))
            {
                var appliedLevel = LoggingBootstrapper.ApplyMinimumLevel(normalizedLevel);
                _host.SelectLogLevel(appliedLevel);
            }
        }

        _host.ApplyRegularVisibilityMaskSnapshot(
            sourcePath: snapshot.RegularVisibilityMaskSourcePath,
            isEnabled: snapshot.UseRegularVisibilityMask,
            allowRuntimeRefresh: allowRuntimeRefresh,
            embeddedMask: embeddedMask,
            embeddedMaskName: embeddedMaskName);
    }

}

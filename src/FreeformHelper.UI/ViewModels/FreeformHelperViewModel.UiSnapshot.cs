using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> handles the creation
/// of a <see cref="ProjectUiSnapshot"/> from the current ViewModel state.
/// This snapshot captures non-critical UI settings for persistence, allowing the application
/// to restore its visual and interactive state across sessions.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Builds a <see cref="ProjectUiSnapshot"/> from the current state of the ViewModel.
    /// This method consolidates various UI-related properties into a serializable snapshot object.
    /// </summary>
    /// <returns>A new <see cref="ProjectUiSnapshot"/> instance.</returns>
    private ProjectUiSnapshot BuildUiSnapshot()
    {
        var persistedMatching = _projectFile.UiSnapshot.Matching;
        var snapshot = new ProjectUiSnapshot
        {
            // --- Grid UI Snapshot ---
            Grid = new UiGridSnapshot
            {
                CascadeCount = (int)Math.Round(CascadeNum),
                // Capture per-IC channel settings.
                IcX = CascadeIcSettings.Select(s => Math.Max(1, (int)Math.Round(s.XChannels))).ToList(),
                IcY = CascadeIcSettings.Select(s => Math.Max(1, (int)Math.Round(s.YChannels))).ToList(),
                TotalX = (int)Math.Round(XChannels),
                TotalY = (int)Math.Round(YChannels),
                // Store display strings for selected options.
                ScanOrder = SelectedScanOrderOption.Display ?? SelectedScanOrder.ToString(),
                AlignmentMode = SelectedGridAlignmentOption.Display ?? GridAlignmentMode.ToString(),
                GridPaddingPercent = (double)GridPaddingPercent,
                ActiveAreaWidthMm = (double)ActiveAreaWidth,
                ActiveAreaHeightMm = (double)ActiveAreaHeight,
                PitchXmm = PitchSizeX,
                PitchYmm = PitchSizeY
            },
            // --- Matching UI Snapshot ---
            Matching = new UiMatchingSnapshot
            {
                MatchMode = persistedMatching.MatchMode,
                MatchThreshold = (double)MatchThreshold,
                FreeformAxisThreshold = (double)FreeformAxisThreshold,
                EnableAutoDetectXy = EnableAutoDetectXy,
                EnableFreeformEdgeSpecialization = EnableFreeformEdgeSpecialization,
                EnableCentroidFallback = persistedMatching.EnableCentroidFallback,
                NearestK = persistedMatching.NearestK
            },
            // --- View UI Snapshot ---
            View = new UiViewSnapshot
            {
                ShowCad = ShowCad,
                ShowRegular = ShowRegular,
                HighlightUnmatched = HighlightUnmatched,
                HighlightFreeform = HighlightFreeform,
                ColorCadByArea = ColorCadByArea,
                CadLineWidth = (double)CadLineWidth,
                CadLineColor = CadLineColorHex, // Store as hex string.
                CadFillOpacity = (double)CadFillOpacity,
                CadLineOpacity = (double)CadLineOpacity,
                RegularLineWidth = (double)RegularLineWidth,
                HighlightStrokeWidthAdjust = (double)HighlightStrokeWidthAdjust,
                RegularLineColor = RegularLineColorHex, // Store as hex string.
                RegularFillOpacity = (double)RegularFillOpacity,
                RegularLineOpacity = (double)RegularLineOpacity,
                RegularSelectedColor = RegularSelectedColorHex,
                RegularSelectedFillOpacity = (double)RegularSelectedFillOpacity,
                AreaBucketTolerance = (double)AreaBucketTolerance,
                MaxAreaBuckets = (int)Math.Round(MaxAreaBuckets),
                ShowDiffIndexOverlay = ShowDiffIndexOverlay,
                ShowNotchCanvasPreview = ShowNotchCanvasPreview,
                ShowNotchToRegularLabels = ShowNotchToRegularLabels,
                GlobalFontSizePercent = (double)GlobalFontSizePercent,
                NotchPreviewVisualizationStep = (double)NotchPreviewVisualizationStep,
                NotchPreviewAutoPlayEnabled = NotchPreviewAutoPlayEnabled,
                NotchPreviewAutoPlayIntervalMs = (double)NotchPreviewAutoPlayIntervalMs,
                NotchExportFileType = SelectedNotchExportFileTypeOption.Value.ToString(),
                DxfLayerImagePreferredLayer = _dxfLayerImageExportPreferredLayerName,
                DxfLayerImageWidthPixels = _dxfLayerImageExportWidthPixels,
                DxfLayerImageHeightPixels = _dxfLayerImageExportHeightPixels,
                DxfLayerImageLineWidthPixels = (double)_dxfLayerImageExportLineWidthPixels,
                DxfLayerImagePaddingXPixels = _dxfLayerImageExportPaddingXPixels,
                DxfLayerImagePaddingYPixels = _dxfLayerImageExportPaddingYPixels,
                DxfLayerImageFormat = _dxfLayerImageExportFormat.ToString(),
                DxfLayerImageUseDarkTheme = _dxfLayerImageExportUseDarkTheme,
                CoordinatePixelWidth = (double)CoordinatePixelWidth,
                CoordinatePixelHeight = (double)CoordinatePixelHeight,
                CoordinatePreferredAaOutlineLayerName = CoordinatePreferredAaOutlineLayerName,
                // Capture the selection state of each layer toggle.
                LayerSelections = LayerToggles.Select(t => new LayerSelectionSnapshot { Name = t.Name, IsSelected = t.IsSelected }).ToList()
            },
            // --- Notch UI Snapshot ---
            Notch = new UiNotchSnapshot
            {
                LenScale = (int)Math.Round(LenScale),
                NullValue = (int)Math.Round(NullValue),
                ThresholdQ7 = (int)Math.Round(NotchThresholdQ7),
                ThresholdPercentV22 = (double)NotchThresholdPercent,
                LinkThresholds = LinkNotchThresholds,
                EnableToFull = EnableToFull
            },
            // --- Import UI Snapshot ---
            Import = new UiImportSnapshot
            {
                OnlyClosedPolylines = ImportOnlyClosedPolylines,
                IncludeBlockPolylines = ImportBlockPolylines,
                LogLevel = SelectedLogLevelOption.Value,
                RegularVisibilityMaskSourcePath = _regularVisibilityMaskSourcePath,
                UseRegularVisibilityMask = IsRegularVisibilityMaskEnabled
            }
        };

        return snapshot;
    }
}

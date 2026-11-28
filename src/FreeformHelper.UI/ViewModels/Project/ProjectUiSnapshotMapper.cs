// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.ViewModels.Project;

/// <summary>Projects live UI inputs into fresh persistence snapshots without storing a snapshot.</summary>
internal sealed class ProjectUiSnapshotMapper(ProjectSnapshotReaders readers)
{
    private readonly ProjectSnapshotReaders _readers = readers;

    public ProjectUiSnapshot Build()
    {
        var persistedMatching = _readers.Matching.Persisted();
        var snapshot = new ProjectUiSnapshot
        {
            // --- Grid UI Snapshot ---
            Grid = BuildGrid(),
            // --- Matching UI Snapshot ---
            Matching = new UiMatchingSnapshot
            {
                MatchMode = persistedMatching.MatchMode,
                MatchThreshold = (double)_readers.Matching.Threshold(),
                FreeformAxisThreshold = (double)_readers.Matching.AxisThreshold(),
                EnableAutoDetectXy = _readers.Matching.AutoDetectXy(),
                EnableFreeformEdgeSpecialization = _readers.Matching.EdgeSpecialization(),
                EnableCentroidFallback = persistedMatching.EnableCentroidFallback,
                NearestK = persistedMatching.NearestK
            },
            // --- View UI Snapshot ---
            View = BuildView(),
            // --- Notch UI Snapshot ---
            Notch = new UiNotchSnapshot
            {
                LenScale = (int)Math.Round(_readers.Notch.LenScale()),
                NullValue = (int)Math.Round(_readers.Notch.NullValue()),
                ThresholdQ7 = (int)Math.Round(_readers.Notch.ThresholdQ7()),
                ThresholdPercentV22 = (double)_readers.Notch.ThresholdPercent(),
                LinkThresholds = _readers.Notch.LinkThresholds(),
                EnableToFull = _readers.Notch.EnableToFull()
            },
            // --- Import UI Snapshot ---
            Import = new UiImportSnapshot
            {
                OnlyClosedPolylines = _readers.Import.OnlyClosedPolylines(),
                IncludeBlockPolylines = _readers.Import.IncludeBlockPolylines(),
                LogLevel = _readers.Import.LogLevel(),
                RegularVisibilityMaskSourcePath = _readers.Import.MaskSourcePath(),
                UseRegularVisibilityMask = _readers.Import.UseMask()
            }
        };

        return snapshot;
    }

    public UiGridSnapshot BuildGrid() =>
        new UiGridSnapshot
        {
            CascadeCount = (int)Math.Round(_readers.Grid.CascadeCount()),
            // Capture per-IC channel settings.
            IcX = _readers.Grid.IcX().Select(value => Math.Max(1, (int)Math.Round(value))).ToList(),
            IcY = _readers.Grid.IcY().Select(value => Math.Max(1, (int)Math.Round(value))).ToList(),
            TotalX = (int)Math.Round(_readers.Grid.TotalX()),
            TotalY = (int)Math.Round(_readers.Grid.TotalY()),
            // Store display strings for selected options.
            ScanOrder = _readers.Grid.ScanOrderDisplay() ?? _readers.Grid.ScanOrderFallback(),
            AlignmentMode = _readers.Grid.AlignmentDisplay() ?? _readers.Grid.AlignmentFallback(),
            GridPaddingPercent = (double)_readers.Grid.PaddingPercent(),
            ActiveAreaWidthMm = (double)_readers.Grid.ActiveAreaWidth(),
            ActiveAreaHeightMm = (double)_readers.Grid.ActiveAreaHeight(),
            PitchXmm = _readers.Grid.PitchX(),
            PitchYmm = _readers.Grid.PitchY()
        };

    public UiViewSnapshot BuildView() =>
        new UiViewSnapshot
        {
            ShowCad = _readers.Display.ShowCad(),
            ShowRegular = _readers.Display.ShowRegular(),
            HighlightUnmatched = _readers.Display.HighlightUnmatched(),
            HighlightFreeform = _readers.Display.HighlightFreeform(),
            ColorCadByArea = _readers.Display.ColorCadByArea(),
            CadLineWidth = (double)_readers.Appearance.CadLineWidth(),
            CadLineColor = _readers.Appearance.CadLineColor(), // Store as hex string.
            CadFillOpacity = (double)_readers.Appearance.CadFillOpacity(),
            CadLineOpacity = (double)_readers.Appearance.CadLineOpacity(),
            RegularLineWidth = (double)_readers.Appearance.RegularLineWidth(),
            HighlightStrokeWidthAdjust = (double)_readers.Appearance.HighlightStrokeWidthAdjust(),
            RegularLineColor = _readers.Appearance.RegularLineColor(), // Store as hex string.
            RegularFillOpacity = (double)_readers.Appearance.RegularFillOpacity(),
            RegularLineOpacity = (double)_readers.Appearance.RegularLineOpacity(),
            RegularSelectedColor = _readers.Appearance.RegularSelectedColor(),
            RegularSelectedFillOpacity = (double)_readers.Appearance.RegularSelectedFillOpacity(),
            AreaBucketTolerance = (double)_readers.ViewOptions.AreaBucketTolerance(),
            MaxAreaBuckets = (int)Math.Round(_readers.ViewOptions.MaxAreaBuckets()),
            ShowDiffIndexOverlay = _readers.ViewOptions.ShowDiffIndexOverlay(),
            ShowNotchCanvasPreview = _readers.ViewOptions.ShowNotchCanvasPreview(),
            ShowNotchToRegularLabels = _readers.ViewOptions.ShowNotchToRegularLabels(),
            GlobalFontSizePercent = (double)_readers.ViewOptions.GlobalFontSizePercent(),
            NotchPreviewVisualizationStep = (double)_readers.ViewOptions.NotchPreviewVisualizationStep(),
            NotchPreviewAutoPlayEnabled = _readers.ViewOptions.NotchPreviewAutoPlayEnabled(),
            NotchPreviewAutoPlayIntervalMs = (double)_readers.ViewOptions.NotchPreviewAutoPlayIntervalMs(),
            NotchExportFileType = _readers.ViewOptions.NotchExportFileType().ToString(),
            DxfLayerImagePreferredLayer = _readers.ImagePreferences().LayerName,
            DxfLayerImageWidthPixels = _readers.ImagePreferences().WidthPixels,
            DxfLayerImageHeightPixels = _readers.ImagePreferences().HeightPixels,
            DxfLayerImageLineWidthPixels = (double)_readers.ImagePreferences().LineWidthPixels,
            DxfLayerImagePaddingXPixels = _readers.ImagePreferences().PaddingXPixels,
            DxfLayerImagePaddingYPixels = _readers.ImagePreferences().PaddingYPixels,
            DxfLayerImageFormat = _readers.ImagePreferences().Format.ToString(),
            DxfLayerImageUseDarkTheme = _readers.ImagePreferences().UseDarkTheme,
            CoordinatePixelWidth = (double)_readers.ViewOptions.CoordinatePixelWidth(),
            CoordinatePixelHeight = (double)_readers.ViewOptions.CoordinatePixelHeight(),
            CoordinatePreferredAaOutlineLayerName = _readers.ViewOptions.CoordinatePreferredAaOutlineLayerName(),
            // Capture the selection state of each layer toggle.
            LayerSelections = _readers.Layers().Select(t => new LayerSelectionSnapshot { Name = t.Name, IsSelected = t.IsSelected }).ToList()
        };
}

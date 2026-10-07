using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public SettingsWindowViewModel CreateSettingsWindowViewModel()
    {
        return new SettingsWindowViewModel(this);
    }

    internal void ApplySettingsWindowDraft(SettingsWindowViewModel draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!IsProjectEditingEnabled) return;

        var resolvedScanOrder = ScanOrderOptions.FirstOrDefault(o => o.Value == draft.SelectedScanOrderOption.Value);
        if (string.IsNullOrWhiteSpace(resolvedScanOrder.Display))
        {
            resolvedScanOrder = SelectedScanOrderOption;
        }

        var resolvedAlignment = GridAlignmentOptions.FirstOrDefault(o => o.Value == draft.SelectedGridAlignmentOption.Value);
        if (string.IsNullOrWhiteSpace(resolvedAlignment.Display))
        {
            resolvedAlignment = SelectedGridAlignmentOption;
        }

        var resolvedRegularSourceMode = RegularSourceModeOptions.FirstOrDefault(o =>
            o.Value == draft.SelectedRegularSourceModeOption.Value);
        if (string.IsNullOrWhiteSpace(resolvedRegularSourceMode.Display))
        {
            resolvedRegularSourceMode = SelectedRegularSourceModeOption;
        }

        var resolvedRegularSourceLayer = RegularSourceLayerOptions.FirstOrDefault(o =>
            string.Equals(o.Name, draft.SelectedRegularSourceLayerOption.Name, StringComparison.Ordinal))
            ?? SelectedRegularSourceLayerOption;
        if (resolvedRegularSourceLayer is null || string.IsNullOrWhiteSpace(resolvedRegularSourceLayer.Display))
        {
            resolvedRegularSourceLayer = RegularSourceLayerOptions.Count > 0 ? RegularSourceLayerOptions[0] : null;
        }
        resolvedRegularSourceLayer ??= new RegularSourceLayerOption(null, "Select layer");

        var resolvedLogLevel = LogLevelOptions.FirstOrDefault(o =>
            string.Equals(o.Value, draft.SelectedLogLevelOption.Value, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(resolvedLogLevel.Display))
        {
            resolvedLogLevel = SelectedLogLevelOption;
        }
        if (string.IsNullOrWhiteSpace(resolvedLogLevel.Display))
        {
            resolvedLogLevel = LogLevelOptions.Count > 0 ? LogLevelOptions[0] : default;
        }

        var resolvedCadOutputFwDiffAutoMode = CadOutputFwDiffAutoModeOptions.FirstOrDefault(o =>
            o.Value == draft.SelectedCadOutputFwDiffAutoModeOption.Value);
        if (string.IsNullOrWhiteSpace(resolvedCadOutputFwDiffAutoMode.Display))
        {
            resolvedCadOutputFwDiffAutoMode = SelectedCadOutputFwDiffAutoModeOption;
        }

        var resolvedNotchExportFileType = NotchExportFileTypeOptions.FirstOrDefault(o =>
            o.Value == draft.SelectedNotchExportFileTypeOption.Value);
        if (string.IsNullOrWhiteSpace(resolvedNotchExportFileType.Display))
        {
            resolvedNotchExportFileType = SelectedNotchExportFileTypeOption;
        }

        var draftPerIcXChannels = draft.PerIcXChannels.ToList();
        var draftPerIcYChannels = draft.PerIcYChannels.ToList();
        var hasCascadeDetailChanges = !CascadeIcLayoutService.HasSameChannels(
            CascadeIcSettings.Select(static row => Math.Max(1, (int)Math.Round(row.XChannels))),
            CascadeIcSettings.Select(static row => Math.Max(1, (int)Math.Round(row.YChannels))),
            draftPerIcXChannels,
            draftPerIcYChannels);

        var changedProperties = new HashSet<string>(StringComparer.Ordinal);
        AddChangedProperty(nameof(XChannels), XChannels != draft.XChannels);
        AddChangedProperty(nameof(YChannels), YChannels != draft.YChannels);
        AddChangedProperty(nameof(CascadeNum), CascadeNum != draft.CascadeNum);
        AddChangedProperty(nameof(CascadeIcSettings), hasCascadeDetailChanges);
        AddChangedProperty(nameof(CoordinatePixelWidth), CoordinatePixelWidth != draft.CoordinatePixelWidth);
        AddChangedProperty(nameof(CoordinatePixelHeight), CoordinatePixelHeight != draft.CoordinatePixelHeight);
        AddChangedProperty(nameof(GridPaddingPercent), GridPaddingPercent != draft.GridPaddingPercent);
        AddChangedProperty(nameof(ActiveAreaWidth), ActiveAreaWidth != draft.ActiveAreaWidth);
        AddChangedProperty(nameof(ActiveAreaHeight), ActiveAreaHeight != draft.ActiveAreaHeight);
        AddChangedProperty(nameof(PanelBiasX), PanelBiasX != draft.PanelBiasX);
        AddChangedProperty(nameof(PanelBiasY), PanelBiasY != draft.PanelBiasY);
        AddChangedProperty(nameof(SelectedScanOrder), SelectedScanOrder != resolvedScanOrder.Value);
        AddChangedProperty(nameof(ImportOnlyClosedPolylines), ImportOnlyClosedPolylines != draft.ImportOnlyClosedPolylines);
        AddChangedProperty(nameof(ImportBlockPolylines), ImportBlockPolylines != draft.ImportBlockPolylines);
        AddChangedProperty(nameof(RecalcBoundsOnLayerFilter), RecalcBoundsOnLayerFilter != draft.RecalcBoundsOnLayerFilter);
        AddChangedProperty(nameof(GridAlignmentMode), GridAlignmentMode != resolvedAlignment.Value);
        AddChangedProperty(nameof(RegularSourceMode), RegularSourceMode != resolvedRegularSourceMode.Value);
        AddChangedProperty(nameof(SelectedRegularSourceLayerOption), !Equals(resolvedRegularSourceLayer, SelectedRegularSourceLayerOption));
        AddChangedProperty(nameof(SelectedLogLevelOption), !resolvedLogLevel.Equals(SelectedLogLevelOption));
        AddChangedProperty(nameof(MatchThreshold), MatchThreshold != draft.MatchThreshold);
        AddChangedProperty(nameof(FreeformAxisThreshold), FreeformAxisThreshold != draft.FreeformAxisThreshold);
        AddChangedProperty(nameof(EnableAutoDetectXy), EnableAutoDetectXy != draft.EnableAutoDetectXy);
        AddChangedProperty(nameof(EnableFreeformEdgeSpecialization), EnableFreeformEdgeSpecialization != draft.EnableFreeformEdgeSpecialization);
        AddChangedProperty(nameof(AutoReplayStep2AfterProjectLoad), AutoReplayStep2AfterProjectLoad != draft.AutoReplayStep2AfterProjectLoad);
        AddChangedProperty(nameof(ApplyAppVisualPreferencesOnProjectLoad), ApplyAppVisualPreferencesOnProjectLoad != draft.ApplyAppVisualPreferencesOnProjectLoad);
        AddChangedProperty(nameof(CadOutputFwDiffIndexStart), CadOutputFwDiffIndexStart != draft.CadOutputFwDiffIndexStart);
        AddChangedProperty(nameof(CadOutputFwDiffIndexAnchorCadId), CadOutputFwDiffIndexAnchorCadId != draft.CadOutputFwDiffIndexAnchorCadId);
        AddChangedProperty(nameof(ShowDiffIndexOverlay), ShowDiffIndexOverlay != draft.ShowDiffIndexOverlay);
        AddChangedProperty(nameof(CadOutputFwDiffAutoMode), CadOutputFwDiffAutoMode != resolvedCadOutputFwDiffAutoMode.Value);
        AddChangedProperty(
            nameof(SelectedNotchCompensationModelOption),
            !SelectedNotchCompensationModelOption.Equals(draft.SelectedNotchCompensationModelOption));
        AddChangedProperty(nameof(EnableToRegular), EnableToRegular != draft.EnableToRegular);
        AddChangedProperty(nameof(EnableToFull), EnableToFull != draft.EnableToFull);
        AddChangedProperty(nameof(EnableToFullRuleEngine), EnableToFullRuleEngine != draft.EnableToFullRuleEngine);
        AddChangedProperty(nameof(EnableToFullRuleTrace), EnableToFullRuleTrace != draft.EnableToFullRuleTrace);
        AddChangedProperty(nameof(EnableBoundaryVirtualAreaCap), EnableBoundaryVirtualAreaCap != draft.EnableBoundaryVirtualAreaCap);
        AddChangedProperty(nameof(BoundaryVirtualAreaCapPercent), BoundaryVirtualAreaCapPercent != draft.BoundaryVirtualAreaCapPercent);
        AddChangedProperty(nameof(EnableTargetCoverageGuard), EnableTargetCoverageGuard != draft.EnableTargetCoverageGuard);
        AddChangedProperty(nameof(TargetCoverageCapPercent), TargetCoverageCapPercent != draft.TargetCoverageCapPercent);
        AddChangedProperty(nameof(ShowNotchCanvasPreview), ShowNotchCanvasPreview != draft.ShowNotchCanvasPreview);
        AddChangedProperty(nameof(ShowNotchToRegularLabels), ShowNotchToRegularLabels != draft.ShowNotchToRegularLabels);
        AddChangedProperty(nameof(NotchPreviewVisualizationStep), NotchPreviewVisualizationStep != draft.NotchPreviewVisualizationStep);
        AddChangedProperty(nameof(ToFullStrictOverlapPercent), ToFullStrictOverlapPercent != draft.ToFullStrictOverlapPercent);
        AddChangedProperty(nameof(NotchPreviewAutoPlayEnabled), NotchPreviewAutoPlayEnabled != draft.NotchPreviewAutoPlayEnabled);
        AddChangedProperty(nameof(NotchPreviewAutoPlayIntervalMs), NotchPreviewAutoPlayIntervalMs != draft.NotchPreviewAutoPlayIntervalMs);
        AddChangedProperty(nameof(EnableV21), EnableV21 != draft.EnableV21);
        AddChangedProperty(nameof(EnableV22), EnableV22 != draft.EnableV22);
        AddChangedProperty(nameof(SelectedNotchExportFileTypeOption), !resolvedNotchExportFileType.Equals(SelectedNotchExportFileTypeOption));
        AddChangedProperty(nameof(LenScale), LenScale != draft.LenScale);
        AddChangedProperty(nameof(NullValue), NullValue != draft.NullValue);
        AddChangedProperty(nameof(NotchThresholdQ7), NotchThresholdQ7 != draft.NotchThresholdQ7);
        AddChangedProperty(nameof(NotchThresholdPercent), NotchThresholdPercent != draft.NotchThresholdPercent);
        AddChangedProperty(nameof(LinkNotchThresholds), LinkNotchThresholds != draft.LinkNotchThresholds);
        AddChangedProperty(nameof(SelectedNotchExportProfileOption), !SelectedNotchExportProfileOption.Equals(draft.SelectedNotchExportProfileOption));
        AddChangedProperty(nameof(MappingWeightIou), MappingWeightIou != draft.MappingWeightIou);
        AddChangedProperty(nameof(MappingWeightCentroidDistance), MappingWeightCentroidDistance != draft.MappingWeightCentroidDistance);
        AddChangedProperty(nameof(MappingWeightAreaRatio), MappingWeightAreaRatio != draft.MappingWeightAreaRatio);
        AddChangedProperty(nameof(MappingLowConfidenceThreshold), MappingLowConfidenceThreshold != draft.MappingLowConfidenceThreshold);
        AddChangedProperty(nameof(MappingAmbiguousMargin), MappingAmbiguousMargin != draft.MappingAmbiguousMargin);
        AddChangedProperty(nameof(MappingCandidateNumber), MappingCandidateNumber != draft.MappingCandidateNumber);
        AddChangedProperty(nameof(UseLocalSizing), UseLocalSizing != draft.UseLocalSizing);
        AddChangedProperty(nameof(GlobalFontSizePercent), GlobalFontSizePercent != draft.GlobalFontSizePercent);
        AddChangedProperty(nameof(HighlightStrokeWidthAdjust), HighlightStrokeWidthAdjust != draft.HighlightStrokeWidthAdjust);
        var hasChanges = changedProperties.Count > 0;

        if (!hasChanges)
        {
            return;
        }

        _isLoadingSettings = true;
        try
        {
            XChannels = draft.XChannels;
            YChannels = draft.YChannels;
            CascadeNum = draft.CascadeNum;
            _projectFile.Settings.Grid.ReplacePerIcChannels(draftPerIcXChannels, draftPerIcYChannels);
            CoordinatePixelWidth = draft.CoordinatePixelWidth;
            CoordinatePixelHeight = draft.CoordinatePixelHeight;
            GridPaddingPercent = draft.GridPaddingPercent;
            ActiveAreaWidth = draft.ActiveAreaWidth;
            ActiveAreaHeight = draft.ActiveAreaHeight;
            PanelBiasX = draft.PanelBiasX;
            PanelBiasY = draft.PanelBiasY;
            SelectedScanOrder = resolvedScanOrder.Value;
            SelectedScanOrderOption = resolvedScanOrder;
            ImportOnlyClosedPolylines = draft.ImportOnlyClosedPolylines;
            ImportBlockPolylines = draft.ImportBlockPolylines;
            RecalcBoundsOnLayerFilter = draft.RecalcBoundsOnLayerFilter;
            GridAlignmentMode = resolvedAlignment.Value;
            SelectedGridAlignmentOption = resolvedAlignment;
            RegularSourceMode = resolvedRegularSourceMode.Value;
            SelectedRegularSourceModeOption = resolvedRegularSourceMode;
            SelectedRegularSourceLayerOption = resolvedRegularSourceLayer;
            SelectedLogLevelOption = resolvedLogLevel;
            MatchThreshold = draft.MatchThreshold;
            FreeformAxisThreshold = draft.FreeformAxisThreshold;
            EnableAutoDetectXy = draft.EnableAutoDetectXy;
            EnableFreeformEdgeSpecialization = draft.EnableFreeformEdgeSpecialization;
            AutoReplayStep2AfterProjectLoad = draft.AutoReplayStep2AfterProjectLoad;
            ApplyAppVisualPreferencesOnProjectLoad = draft.ApplyAppVisualPreferencesOnProjectLoad;
            CadOutputFwDiffIndexStart = draft.CadOutputFwDiffIndexStart;
            CadOutputFwDiffIndexAnchorCadId = draft.CadOutputFwDiffIndexAnchorCadId;
            ShowDiffIndexOverlay = draft.ShowDiffIndexOverlay;
            CadOutputFwDiffAutoMode = resolvedCadOutputFwDiffAutoMode.Value;
            SelectedCadOutputFwDiffAutoModeOption = resolvedCadOutputFwDiffAutoMode;
            SelectedNotchCompensationModelOption = draft.SelectedNotchCompensationModelOption;
            EnableToRegular = draft.EnableToRegular;
            EnableToFull = draft.EnableToFull;
            EnableToFullRuleEngine = draft.EnableToFullRuleEngine;
            EnableToFullRuleTrace = draft.EnableToFullRuleTrace;
            EnableBoundaryVirtualAreaCap = draft.EnableBoundaryVirtualAreaCap;
            BoundaryVirtualAreaCapPercent = draft.BoundaryVirtualAreaCapPercent;
            EnableTargetCoverageGuard = draft.EnableTargetCoverageGuard;
            TargetCoverageCapPercent = draft.TargetCoverageCapPercent;
            ShowNotchCanvasPreview = draft.ShowNotchCanvasPreview;
            ShowNotchToRegularLabels = draft.ShowNotchToRegularLabels;
            NotchPreviewVisualizationStep = draft.NotchPreviewVisualizationStep;
            ToFullStrictOverlapPercent = draft.ToFullStrictOverlapPercent;
            NotchPreviewAutoPlayEnabled = draft.NotchPreviewAutoPlayEnabled;
            NotchPreviewAutoPlayIntervalMs = draft.NotchPreviewAutoPlayIntervalMs;
            EnableV21 = draft.EnableV21;
            EnableV22 = draft.EnableV22;
            SelectedNotchExportFileTypeOption = resolvedNotchExportFileType;
            LenScale = draft.LenScale;
            NullValue = draft.NullValue;
            NotchThresholdQ7 = draft.NotchThresholdQ7;
            NotchThresholdPercent = draft.NotchThresholdPercent;
            LinkNotchThresholds = draft.LinkNotchThresholds;
            SelectedNotchExportProfileOption = draft.SelectedNotchExportProfileOption;
            MappingWeightIou = draft.MappingWeightIou;
            MappingWeightCentroidDistance = draft.MappingWeightCentroidDistance;
            MappingWeightAreaRatio = draft.MappingWeightAreaRatio;
            MappingLowConfidenceThreshold = draft.MappingLowConfidenceThreshold;
            MappingAmbiguousMargin = draft.MappingAmbiguousMargin;
            MappingCandidateNumber = draft.MappingCandidateNumber;
            MappingCandidatePaddingCells = IndexMappingSettings.PaddingFromCandidateNumber((int)Math.Round(draft.MappingCandidateNumber));
            UseLocalSizing = draft.UseLocalSizing;
            GlobalFontSizePercent = draft.GlobalFontSizePercent;
            HighlightStrokeWidthAdjust = Math.Clamp(draft.HighlightStrokeWidthAdjust, -2m, 4m);
        }
        finally
        {
            _isLoadingSettings = false;
        }
        if (changedProperties.Contains(nameof(SelectedLogLevelOption)))
        {
            var appliedLevelName = LoggingBootstrapper.ApplyMinimumLevel(resolvedLogLevel.Value);
            var normalized = LogLevelOptions.FirstOrDefault(o =>
                string.Equals(o.Value, appliedLevelName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(normalized.Display) &&
                !normalized.Equals(SelectedLogLevelOption))
            {
                _isLoadingSettings = true;
                try
                {
                    SelectedLogLevelOption = normalized;
                }
                finally
                {
                    _isLoadingSettings = false;
                }
            }
        }
        if (changedProperties.Contains(nameof(CadOutputFwDiffIndexAnchorCadId)) &&
            !TryCommitCadOutputFwDiffIndexAnchorCadIdFromCurrentValue())
        {
            changedProperties.Remove(nameof(CadOutputFwDiffIndexAnchorCadId));
        }

        ApplySettingsWindowOrchestration(changedProperties);

        return;

        void AddChangedProperty(string propertyName, bool condition)
        {
            if (condition)
            {
                changedProperties.Add(propertyName);
            }
        }
    }

    internal void ResetAllSettingsToDefaults()
    {
        var defaultsSettings = new ProjectSettings();
        var defaultsUi = new ProjectUiSnapshot();

        _projectFile.Settings = defaultsSettings;
        _projectFile.UiSnapshot = defaultsUi;
        _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc.Clear();
        _projectFile.CadOutputFwDiffIndexAnchorCadPadId = null;

        _suppressAppGeneralPersistence = true;
        try
        {
            ApplyAppVisualPreferencesOnProjectLoad = true;
            LoadSettingsToUi(defaultsSettings);
            ApplyViewSnapshot(defaultsUi.View);
            ApplyImportSnapshot(defaultsUi.Import);
            RebuildCascadeSettings();
        }
        finally
        {
            _suppressAppGeneralPersistence = false;
        }

        InvalidateNotchCompensationCache();
        InvalidateNotchValidationCache();
        HasUnsavedChanges = true;
        SetStatus("Settings reset to defaults.");
        _ = TriggerGridRebuildAsync(requestFit: true);

        PersistAppGeneralSettingsNow();
    }
}

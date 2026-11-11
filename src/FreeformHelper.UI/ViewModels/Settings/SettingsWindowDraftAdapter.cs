// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.UI.Contracts.Settings;
using FreeformHelper.UI.Logging;
using static FreeformHelper.UI.ViewModels.FreeformHelperViewModel;

namespace FreeformHelper.UI.ViewModels.Settings;

/// <summary>Resolves and adapts a single draft Apply; it does not retain workspace state.</summary>
internal sealed class SettingsWindowDraftAdapter : ISettingsDraftApplyOperation
{
    private readonly ISettingsWindowDraftHost _host;
    private readonly SettingsWindowViewModel _draft;
    private readonly SettingsWindowDraftOptions _resolved;

    internal SettingsWindowDraftAdapter(ISettingsWindowDraftHost host, SettingsWindowViewModel draft)
    {
        _host = host;
        _draft = draft;
        _resolved = ResolveSettingsWindowDraftOptions(draft);
        PerIcXChannels = draft.PerIcXChannels.ToList();
        PerIcYChannels = draft.PerIcYChannels.ToList();
    }

    public IReadOnlyList<int> PerIcXChannels { get; }
    public IReadOnlyList<int> PerIcYChannels { get; }

    public HashSet<string> CollectChanges(bool hasCascadeDetailChanges) =>
        CollectSettingsWindowDraftChanges(_draft, _resolved, hasCascadeDetailChanges);

    public void ApplyValues(HashSet<string> changed) =>
        ApplySettingsWindowDraftValues(_draft, _resolved, PerIcXChannels, PerIcYChannels, changed);

    public void ApplyNotchValues(HashSet<string> changed) => ApplySettingsWindowDraftNotchValues(_draft, _resolved, changed);

    public void ApplyLogLevel()
    {
        var appliedLevelName = LoggingBootstrapper.ApplyMinimumLevel(_resolved.LogLevel.Value);
        var normalized = _host.LogLevelOptions.FirstOrDefault(o =>
            string.Equals(o.Value, appliedLevelName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(normalized.Display) &&
            !normalized.Equals(_host.SelectedLogLevelOption))
        {
            using (_host.EnterSettingsLoading())
            {
                _host.SelectedLogLevelOption = normalized;
            }
        }
    }

    private SettingsWindowDraftOptions ResolveSettingsWindowDraftOptions(SettingsWindowViewModel draft)
    {
        var resolvedScanOrder = _host.ScanOrderOptions.FirstOrDefault(o => o.Value == draft.SelectedScanOrderOption.Value);
        if (string.IsNullOrWhiteSpace(resolvedScanOrder.Display))
        {
            resolvedScanOrder = _host.SelectedScanOrderOption;
        }

        var resolvedAlignment = _host.GridAlignmentOptions.FirstOrDefault(o => o.Value == draft.SelectedGridAlignmentOption.Value);
        if (string.IsNullOrWhiteSpace(resolvedAlignment.Display))
        {
            resolvedAlignment = _host.SelectedGridAlignmentOption;
        }

        var resolvedRegularSourceMode = _host.RegularSourceModeOptions.FirstOrDefault(o =>
            o.Value == draft.SelectedRegularSourceModeOption.Value);
        if (string.IsNullOrWhiteSpace(resolvedRegularSourceMode.Display))
        {
            resolvedRegularSourceMode = _host.SelectedRegularSourceModeOption;
        }

        var resolvedRegularSourceLayer = _host.RegularSourceLayerOptions.FirstOrDefault(o =>
            string.Equals(o.Name, draft.SelectedRegularSourceLayerOption.Name, StringComparison.Ordinal))
            ?? _host.SelectedRegularSourceLayerOption;
        if (resolvedRegularSourceLayer is null || string.IsNullOrWhiteSpace(resolvedRegularSourceLayer.Display))
        {
            resolvedRegularSourceLayer = _host.RegularSourceLayerOptions.Count > 0 ? _host.RegularSourceLayerOptions[0] : null;
        }
        resolvedRegularSourceLayer ??= new RegularSourceLayerOption(null, "Select layer");

        var resolvedLogLevel = _host.LogLevelOptions.FirstOrDefault(o =>
            string.Equals(o.Value, draft.SelectedLogLevelOption.Value, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(resolvedLogLevel.Display))
        {
            resolvedLogLevel = _host.SelectedLogLevelOption;
        }
        if (string.IsNullOrWhiteSpace(resolvedLogLevel.Display))
        {
            resolvedLogLevel = _host.LogLevelOptions.Count > 0 ? _host.LogLevelOptions[0] : default;
        }

        var resolvedCadOutputFwDiffAutoMode = _host.CadOutputFwDiffAutoModeOptions.FirstOrDefault(o =>
            o.Value == draft.SelectedCadOutputFwDiffAutoModeOption.Value);
        if (string.IsNullOrWhiteSpace(resolvedCadOutputFwDiffAutoMode.Display))
        {
            resolvedCadOutputFwDiffAutoMode = _host.SelectedCadOutputFwDiffAutoModeOption;
        }

        var resolvedNotchExportFileType = _host.NotchExportFileTypeOptions.FirstOrDefault(o =>
            o.Value == draft.SelectedNotchExportFileTypeOption.Value);
        if (string.IsNullOrWhiteSpace(resolvedNotchExportFileType.Display))
        {
            resolvedNotchExportFileType = _host.SelectedNotchExportFileTypeOption;
        }

        return new SettingsWindowDraftOptions(
            resolvedScanOrder,
            resolvedAlignment,
            resolvedRegularSourceMode,
            resolvedRegularSourceLayer,
            resolvedLogLevel,
            resolvedCadOutputFwDiffAutoMode,
            resolvedNotchExportFileType);
    }

    private HashSet<string> CollectSettingsWindowDraftChanges(
        SettingsWindowViewModel draft, SettingsWindowDraftOptions resolved, bool hasCascadeDetailChanges)
    {
        var changedProperties = new HashSet<string>(StringComparer.Ordinal);
        AddChangedProperty(nameof(_host.XChannels), _host.XChannels != draft.XChannels);
        AddChangedProperty(nameof(_host.YChannels), _host.YChannels != draft.YChannels);
        AddChangedProperty(nameof(_host.CascadeNum), _host.CascadeNum != draft.CascadeNum);
        AddChangedProperty("CascadeIcSettings", hasCascadeDetailChanges);
        AddChangedProperty(nameof(_host.CoordinatePixelWidth), _host.CoordinatePixelWidth != draft.CoordinatePixelWidth);
        AddChangedProperty(nameof(_host.CoordinatePixelHeight), _host.CoordinatePixelHeight != draft.CoordinatePixelHeight);
        AddChangedProperty(nameof(_host.GridPaddingPercent), _host.GridPaddingPercent != draft.GridPaddingPercent);
        AddChangedProperty(nameof(_host.ActiveAreaWidth), _host.ActiveAreaWidth != draft.ActiveAreaWidth);
        AddChangedProperty(nameof(_host.ActiveAreaHeight), _host.ActiveAreaHeight != draft.ActiveAreaHeight);
        AddChangedProperty(nameof(_host.PanelBiasX), _host.PanelBiasX != draft.PanelBiasX);
        AddChangedProperty(nameof(_host.PanelBiasY), _host.PanelBiasY != draft.PanelBiasY);
        AddChangedProperty(nameof(_host.SelectedScanOrder), _host.SelectedScanOrder != resolved.ScanOrder.Value);
        AddChangedProperty(nameof(_host.ImportOnlyClosedPolylines), _host.ImportOnlyClosedPolylines != draft.ImportOnlyClosedPolylines);
        AddChangedProperty(nameof(_host.ImportBlockPolylines), _host.ImportBlockPolylines != draft.ImportBlockPolylines);
        AddChangedProperty(nameof(_host.RecalcBoundsOnLayerFilter), _host.RecalcBoundsOnLayerFilter != draft.RecalcBoundsOnLayerFilter);
        AddChangedProperty(nameof(_host.GridAlignmentMode), _host.GridAlignmentMode != resolved.Alignment.Value);
        AddChangedProperty(nameof(_host.RegularSourceMode), _host.RegularSourceMode != resolved.RegularSourceMode.Value);
        AddChangedProperty(nameof(_host.SelectedRegularSourceLayerOption), !Equals(resolved.RegularSourceLayer, _host.SelectedRegularSourceLayerOption));
        AddChangedProperty(nameof(_host.SelectedLogLevelOption), !resolved.LogLevel.Equals(_host.SelectedLogLevelOption));
        AddChangedProperty(nameof(_host.MatchThreshold), _host.MatchThreshold != draft.MatchThreshold);
        AddChangedProperty(nameof(_host.FreeformAxisThreshold), _host.FreeformAxisThreshold != draft.FreeformAxisThreshold);
        AddChangedProperty(nameof(_host.EnableAutoDetectXy), _host.EnableAutoDetectXy != draft.EnableAutoDetectXy);
        AddChangedProperty(nameof(_host.EnableFreeformEdgeSpecialization), _host.EnableFreeformEdgeSpecialization != draft.EnableFreeformEdgeSpecialization);
        AddChangedProperty(nameof(_host.AutoReplayStep2AfterProjectLoad), _host.AutoReplayStep2AfterProjectLoad != draft.AutoReplayStep2AfterProjectLoad);
        AddChangedProperty(nameof(_host.ApplyAppVisualPreferencesOnProjectLoad), _host.ApplyAppVisualPreferencesOnProjectLoad != draft.ApplyAppVisualPreferencesOnProjectLoad);
        AddChangedProperty(nameof(_host.CadOutputFwDiffIndexStart), _host.CadOutputFwDiffIndexStart != draft.CadOutputFwDiffIndexStart);
        AddChangedProperty(nameof(_host.CadOutputFwDiffIndexAnchorCadId), _host.CadOutputFwDiffIndexAnchorCadId != draft.CadOutputFwDiffIndexAnchorCadId);
        AddChangedProperty(nameof(_host.ShowDiffIndexOverlay), _host.ShowDiffIndexOverlay != draft.ShowDiffIndexOverlay);
        AddChangedProperty(nameof(_host.CadOutputFwDiffAutoMode), _host.CadOutputFwDiffAutoMode != resolved.CadOutputFwDiffAutoMode.Value);
        AddChangedProperty(
            nameof(_host.SelectedNotchCompensationModelOption),
            !_host.SelectedNotchCompensationModelOption.Equals(draft.SelectedNotchCompensationModelOption));
        AddChangedProperty(nameof(_host.EnableToRegular), _host.EnableToRegular != draft.EnableToRegular);
        AddChangedProperty(nameof(_host.EnableToFull), _host.EnableToFull != draft.EnableToFull);
        AddChangedProperty(nameof(_host.EnableToFullRuleEngine), _host.EnableToFullRuleEngine != draft.EnableToFullRuleEngine);
        AddChangedProperty(nameof(_host.EnableToFullRuleTrace), _host.EnableToFullRuleTrace != draft.EnableToFullRuleTrace);
        AddChangedProperty(nameof(_host.EnableBoundaryVirtualAreaCap), _host.EnableBoundaryVirtualAreaCap != draft.EnableBoundaryVirtualAreaCap);
        AddChangedProperty(nameof(_host.BoundaryVirtualAreaCapPercent), _host.BoundaryVirtualAreaCapPercent != draft.BoundaryVirtualAreaCapPercent);
        AddChangedProperty(nameof(_host.EnableTargetCoverageGuard), _host.EnableTargetCoverageGuard != draft.EnableTargetCoverageGuard);
        AddChangedProperty(nameof(_host.TargetCoverageCapPercent), _host.TargetCoverageCapPercent != draft.TargetCoverageCapPercent);
        AddChangedProperty(nameof(_host.ShowNotchCanvasPreview), _host.ShowNotchCanvasPreview != draft.ShowNotchCanvasPreview);
        AddChangedProperty(nameof(_host.ShowNotchToRegularLabels), _host.ShowNotchToRegularLabels != draft.ShowNotchToRegularLabels);
        AddChangedProperty(nameof(_host.NotchPreviewVisualizationStep), _host.NotchPreviewVisualizationStep != draft.NotchPreviewVisualizationStep);
        AddChangedProperty(nameof(_host.ToFullStrictOverlapPercent), _host.ToFullStrictOverlapPercent != draft.ToFullStrictOverlapPercent);
        AddChangedProperty(nameof(_host.NotchPreviewAutoPlayEnabled), _host.NotchPreviewAutoPlayEnabled != draft.NotchPreviewAutoPlayEnabled);
        AddChangedProperty(nameof(_host.NotchPreviewAutoPlayIntervalMs), _host.NotchPreviewAutoPlayIntervalMs != draft.NotchPreviewAutoPlayIntervalMs);
        AddChangedProperty(nameof(_host.EnableV21), _host.EnableV21 != draft.EnableV21);
        AddChangedProperty(nameof(_host.EnableV22), _host.EnableV22 != draft.EnableV22);
        AddChangedProperty(nameof(_host.SelectedNotchExportFileTypeOption), !resolved.NotchExportFileType.Equals(_host.SelectedNotchExportFileTypeOption));
        AddChangedProperty(nameof(_host.LenScale), _host.LenScale != draft.LenScale);
        AddChangedProperty(nameof(_host.NullValue), _host.NullValue != draft.NullValue);
        AddChangedProperty(nameof(_host.NotchThresholdQ7), _host.NotchThresholdQ7 != draft.NotchThresholdQ7);
        AddChangedProperty(nameof(_host.NotchThresholdPercent), _host.NotchThresholdPercent != draft.NotchThresholdPercent);
        AddChangedProperty(nameof(_host.LinkNotchThresholds), _host.LinkNotchThresholds != draft.LinkNotchThresholds);
        AddChangedProperty(nameof(_host.SelectedNotchExportProfileOption), !_host.SelectedNotchExportProfileOption.Equals(draft.SelectedNotchExportProfileOption));
        AddChangedProperty(nameof(_host.MappingWeightIou), _host.MappingWeightIou != draft.MappingWeightIou);
        AddChangedProperty(nameof(_host.MappingWeightCentroidDistance), _host.MappingWeightCentroidDistance != draft.MappingWeightCentroidDistance);
        AddChangedProperty(nameof(_host.MappingWeightAreaRatio), _host.MappingWeightAreaRatio != draft.MappingWeightAreaRatio);
        AddChangedProperty(nameof(_host.MappingLowConfidenceThreshold), _host.MappingLowConfidenceThreshold != draft.MappingLowConfidenceThreshold);
        AddChangedProperty(nameof(_host.MappingAmbiguousMargin), _host.MappingAmbiguousMargin != draft.MappingAmbiguousMargin);
        AddChangedProperty(nameof(_host.MappingCandidateNumber), _host.MappingCandidateNumber != draft.MappingCandidateNumber);
        AddChangedProperty(nameof(_host.UseLocalSizing), _host.UseLocalSizing != draft.UseLocalSizing);
        AddChangedProperty(nameof(_host.GlobalFontSizePercent), _host.GlobalFontSizePercent != draft.GlobalFontSizePercent);
        AddChangedProperty(nameof(_host.HighlightStrokeWidthAdjust), _host.HighlightStrokeWidthAdjust != draft.HighlightStrokeWidthAdjust);
        return changedProperties;

        void AddChangedProperty(string propertyName, bool condition)
        {
            if (condition && draft.IsEdited(propertyName))
            {
                changedProperties.Add(propertyName);
            }
        }
    }

    private void ApplySettingsWindowDraftValues(
        SettingsWindowViewModel draft,
        SettingsWindowDraftOptions resolved,
        IReadOnlyList<int> draftPerIcXChannels,
        IReadOnlyList<int> draftPerIcYChannels,
        HashSet<string> changed)
    {
        if (changed.Contains(nameof(_host.XChannels))) _host.XChannels = draft.XChannels;
        if (changed.Contains(nameof(_host.YChannels))) _host.YChannels = draft.YChannels;
        if (changed.Contains(nameof(_host.CascadeNum))) _host.CascadeNum = draft.CascadeNum;
        if (changed.Contains("CascadeIcSettings"))
        {
            _host.ReplacePerIcChannels(draftPerIcXChannels, draftPerIcYChannels);
        }
        if (changed.Contains(nameof(_host.CoordinatePixelWidth))) _host.CoordinatePixelWidth = draft.CoordinatePixelWidth;
        if (changed.Contains(nameof(_host.CoordinatePixelHeight))) _host.CoordinatePixelHeight = draft.CoordinatePixelHeight;
        if (changed.Contains(nameof(_host.GridPaddingPercent))) _host.GridPaddingPercent = draft.GridPaddingPercent;
        if (changed.Contains(nameof(_host.ActiveAreaWidth))) _host.ActiveAreaWidth = draft.ActiveAreaWidth;
        if (changed.Contains(nameof(_host.ActiveAreaHeight))) _host.ActiveAreaHeight = draft.ActiveAreaHeight;
        if (changed.Contains(nameof(_host.PanelBiasX))) _host.PanelBiasX = draft.PanelBiasX;
        if (changed.Contains(nameof(_host.PanelBiasY))) _host.PanelBiasY = draft.PanelBiasY;
        if (changed.Contains(nameof(_host.SelectedScanOrder))) _host.SelectedScanOrder = resolved.ScanOrder.Value;
        if (changed.Contains(nameof(_host.SelectedScanOrder))) _host.SelectedScanOrderOption = resolved.ScanOrder;
        if (changed.Contains(nameof(_host.ImportOnlyClosedPolylines))) _host.ImportOnlyClosedPolylines = draft.ImportOnlyClosedPolylines;
        if (changed.Contains(nameof(_host.ImportBlockPolylines))) _host.ImportBlockPolylines = draft.ImportBlockPolylines;
        if (changed.Contains(nameof(_host.RecalcBoundsOnLayerFilter))) _host.RecalcBoundsOnLayerFilter = draft.RecalcBoundsOnLayerFilter;
        if (changed.Contains(nameof(_host.GridAlignmentMode))) _host.GridAlignmentMode = resolved.Alignment.Value;
        if (changed.Contains(nameof(_host.GridAlignmentMode))) _host.SelectedGridAlignmentOption = resolved.Alignment;
        if (changed.Contains(nameof(_host.RegularSourceMode))) _host.RegularSourceMode = resolved.RegularSourceMode.Value;
        if (changed.Contains(nameof(_host.RegularSourceMode))) _host.SelectedRegularSourceModeOption = resolved.RegularSourceMode;
        if (changed.Contains(nameof(_host.SelectedRegularSourceLayerOption))) _host.SelectedRegularSourceLayerOption = resolved.RegularSourceLayer;
        if (changed.Contains(nameof(_host.SelectedLogLevelOption))) _host.SelectedLogLevelOption = resolved.LogLevel;
        if (changed.Contains(nameof(_host.MatchThreshold))) _host.MatchThreshold = draft.MatchThreshold;
        if (changed.Contains(nameof(_host.FreeformAxisThreshold))) _host.FreeformAxisThreshold = draft.FreeformAxisThreshold;
        if (changed.Contains(nameof(_host.EnableAutoDetectXy))) _host.EnableAutoDetectXy = draft.EnableAutoDetectXy;
        if (changed.Contains(nameof(_host.EnableFreeformEdgeSpecialization)))
        {
            _host.EnableFreeformEdgeSpecialization = draft.EnableFreeformEdgeSpecialization;
        }
        if (changed.Contains(nameof(_host.AutoReplayStep2AfterProjectLoad)))
        {
            _host.AutoReplayStep2AfterProjectLoad = draft.AutoReplayStep2AfterProjectLoad;
        }
        if (changed.Contains(nameof(_host.ApplyAppVisualPreferencesOnProjectLoad)))
        {
            _host.ApplyAppVisualPreferencesOnProjectLoad = draft.ApplyAppVisualPreferencesOnProjectLoad;
        }
        if (changed.Contains(nameof(_host.CadOutputFwDiffIndexStart))) _host.CadOutputFwDiffIndexStart = draft.CadOutputFwDiffIndexStart;
        if (changed.Contains(nameof(_host.CadOutputFwDiffIndexAnchorCadId)))
        {
            _host.CadOutputFwDiffIndexAnchorCadId = draft.CadOutputFwDiffIndexAnchorCadId;
        }
        if (changed.Contains(nameof(_host.ShowDiffIndexOverlay))) _host.ShowDiffIndexOverlay = draft.ShowDiffIndexOverlay;
        if (changed.Contains(nameof(_host.CadOutputFwDiffAutoMode))) _host.CadOutputFwDiffAutoMode = resolved.CadOutputFwDiffAutoMode.Value;
        if (changed.Contains(nameof(_host.CadOutputFwDiffAutoMode))) _host.SelectedCadOutputFwDiffAutoModeOption = resolved.CadOutputFwDiffAutoMode;
    }

    private void ApplySettingsWindowDraftNotchValues(
        SettingsWindowViewModel draft, SettingsWindowDraftOptions resolved, HashSet<string> changed)
    {
        if (changed.Contains(nameof(_host.SelectedNotchCompensationModelOption)))
        {
            _host.SelectedNotchCompensationModelOption = draft.SelectedNotchCompensationModelOption;
        }
        if (changed.Contains(nameof(_host.EnableToRegular))) _host.EnableToRegular = draft.EnableToRegular;
        if (changed.Contains(nameof(_host.EnableToFull))) _host.EnableToFull = draft.EnableToFull;
        if (changed.Contains(nameof(_host.EnableToFullRuleEngine))) _host.EnableToFullRuleEngine = draft.EnableToFullRuleEngine;
        if (changed.Contains(nameof(_host.EnableToFullRuleTrace))) _host.EnableToFullRuleTrace = draft.EnableToFullRuleTrace;
        if (changed.Contains(nameof(_host.EnableBoundaryVirtualAreaCap))) _host.EnableBoundaryVirtualAreaCap = draft.EnableBoundaryVirtualAreaCap;
        if (changed.Contains(nameof(_host.BoundaryVirtualAreaCapPercent))) _host.BoundaryVirtualAreaCapPercent = draft.BoundaryVirtualAreaCapPercent;
        if (changed.Contains(nameof(_host.EnableTargetCoverageGuard))) _host.EnableTargetCoverageGuard = draft.EnableTargetCoverageGuard;
        if (changed.Contains(nameof(_host.TargetCoverageCapPercent))) _host.TargetCoverageCapPercent = draft.TargetCoverageCapPercent;
        if (changed.Contains(nameof(_host.ShowNotchCanvasPreview))) _host.ShowNotchCanvasPreview = draft.ShowNotchCanvasPreview;
        if (changed.Contains(nameof(_host.ShowNotchToRegularLabels))) _host.ShowNotchToRegularLabels = draft.ShowNotchToRegularLabels;
        if (changed.Contains(nameof(_host.NotchPreviewVisualizationStep))) _host.NotchPreviewVisualizationStep = draft.NotchPreviewVisualizationStep;
        if (changed.Contains(nameof(_host.ToFullStrictOverlapPercent))) _host.ToFullStrictOverlapPercent = draft.ToFullStrictOverlapPercent;
        if (changed.Contains(nameof(_host.NotchPreviewAutoPlayEnabled))) _host.NotchPreviewAutoPlayEnabled = draft.NotchPreviewAutoPlayEnabled;
        if (changed.Contains(nameof(_host.NotchPreviewAutoPlayIntervalMs))) _host.NotchPreviewAutoPlayIntervalMs = draft.NotchPreviewAutoPlayIntervalMs;
        if (changed.Contains(nameof(_host.EnableV21))) _host.EnableV21 = draft.EnableV21;
        if (changed.Contains(nameof(_host.EnableV22))) _host.EnableV22 = draft.EnableV22;
        if (changed.Contains(nameof(_host.SelectedNotchExportFileTypeOption))) _host.SelectedNotchExportFileTypeOption = resolved.NotchExportFileType;
        if (changed.Contains(nameof(_host.LenScale))) _host.LenScale = draft.LenScale;
        if (changed.Contains(nameof(_host.NullValue))) _host.NullValue = draft.NullValue;
        if (changed.Contains(nameof(_host.NotchThresholdQ7))) _host.NotchThresholdQ7 = draft.NotchThresholdQ7;
        if (changed.Contains(nameof(_host.NotchThresholdPercent))) _host.NotchThresholdPercent = draft.NotchThresholdPercent;
        if (changed.Contains(nameof(_host.LinkNotchThresholds))) _host.LinkNotchThresholds = draft.LinkNotchThresholds;
        if (changed.Contains(nameof(_host.SelectedNotchExportProfileOption)))
        {
            _host.SelectedNotchExportProfileOption = draft.SelectedNotchExportProfileOption;
        }
        if (changed.Contains(nameof(_host.MappingWeightIou))) _host.MappingWeightIou = draft.MappingWeightIou;
        if (changed.Contains(nameof(_host.MappingWeightCentroidDistance))) _host.MappingWeightCentroidDistance = draft.MappingWeightCentroidDistance;
        if (changed.Contains(nameof(_host.MappingWeightAreaRatio))) _host.MappingWeightAreaRatio = draft.MappingWeightAreaRatio;
        if (changed.Contains(nameof(_host.MappingLowConfidenceThreshold))) _host.MappingLowConfidenceThreshold = draft.MappingLowConfidenceThreshold;
        if (changed.Contains(nameof(_host.MappingAmbiguousMargin))) _host.MappingAmbiguousMargin = draft.MappingAmbiguousMargin;
        if (changed.Contains(nameof(_host.MappingCandidateNumber)))
        {
            _host.MappingCandidateNumber = draft.MappingCandidateNumber;
            _host.MappingCandidatePaddingCells = IndexMappingSettings.PaddingFromCandidateNumber((int)Math.Round(_host.MappingCandidateNumber));
        }
        if (changed.Contains(nameof(_host.UseLocalSizing))) _host.UseLocalSizing = draft.UseLocalSizing;
        if (changed.Contains(nameof(_host.GlobalFontSizePercent))) _host.GlobalFontSizePercent = draft.GlobalFontSizePercent;
        if (changed.Contains(nameof(_host.HighlightStrokeWidthAdjust)))
        {
            _host.HighlightStrokeWidthAdjust = Math.Clamp(draft.HighlightStrokeWidthAdjust, -2m, 4m);
        }
    }

    private readonly record struct SettingsWindowDraftOptions(
        ScanOrderOption ScanOrder,
        GridAlignmentOption Alignment,
        RegularSourceModeOption RegularSourceMode,
        RegularSourceLayerOption RegularSourceLayer,
        LogLevelOption LogLevel,
        CadOutputFwDiffAutoModeOption CadOutputFwDiffAutoMode,
        NotchExportFileTypeOption NotchExportFileType);

}

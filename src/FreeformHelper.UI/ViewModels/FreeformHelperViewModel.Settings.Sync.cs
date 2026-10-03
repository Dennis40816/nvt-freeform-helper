using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void LoadSettingsToUi(ProjectSettings s)
    {
        _isLoadingSettings = true; // Set flag to prevent UI updates from marking project dirty.
        EnsureChannelDefaults(s.Grid); // Ensure channel counts are valid.
        EnsureActiveAreaDefaults(s.Grid); // Ensure active area dimensions are valid.
        ManualSizingService.EnsureSizingLists(s.Grid); // Ensure sizing lists are correctly initialized.

        // --- Grid Settings ---
        XChannels = s.Grid.XChannels;
        YChannels = s.Grid.YChannels;
        CascadeNum = s.Grid.CascadeNum;
        SelectedScanOrder = s.Grid.ScanOrder;
        SelectedScanOrderOption = ScanOrderOptions.FirstOrDefault(o => o.Value == s.Grid.ScanOrder);
        GridAlignmentMode = s.Grid.AlignmentMode;
        SelectedGridAlignmentOption = GridAlignmentOptions.FirstOrDefault(o => o.Value == s.Grid.AlignmentMode);
        RegularSourceMode = s.Grid.RegularSourceMode;
        SelectedRegularSourceModeOption = RegularSourceModeOptions.FirstOrDefault(o => o.Value == s.Grid.RegularSourceMode);

        // Fallback for potentially invalid or uninitialized enum values.
        if (string.IsNullOrWhiteSpace(SelectedGridAlignmentOption.Display))
        {
            var fallback = GridAlignmentOptions.FirstOrDefault(o => o.Value == GridAlignmentMode.FromCadBounds);
            if (!string.IsNullOrWhiteSpace(fallback.Display))
            {
                GridAlignmentMode = fallback.Value;
                SelectedGridAlignmentOption = fallback;
            }
        }
        if (string.IsNullOrWhiteSpace(SelectedRegularSourceModeOption.Display))
        {
            var fallback = RegularSourceModeOptions.FirstOrDefault(o => o.Value == RegularSourceMode.GeneratedGrid);
            if (!string.IsNullOrWhiteSpace(fallback.Display))
            {
                RegularSourceMode = fallback.Value;
                SelectedRegularSourceModeOption = fallback;
            }
        }
        // Notify UI of changes to dependent properties.
        OnPropertyChanged(nameof(IsPanelAlignment));
        OnPropertyChanged(nameof(IsDxfLayerRegularSource));

        GridPaddingPercent = (decimal)(s.Grid.BoundsPaddingRatio * 100.0);
        ActiveAreaWidth = (decimal)s.Grid.ActiveAreaWidth;
        ActiveAreaHeight = (decimal)s.Grid.ActiveAreaHeight;
        PanelBiasX = (decimal)s.Grid.PanelBiasX;
        PanelBiasY = (decimal)s.Grid.PanelBiasY;
        RecalcBoundsOnLayerFilter = s.Grid.RecalcBoundsOnLayerFilter;
        _pendingBoundLayerName = s.Grid.BoundLayerName;
        _pendingRegularSourceLayerName = s.Grid.RegularSourceLayerName;

        // --- Matching Settings ---
        MatchThreshold = (decimal)s.Matching.MatchThreshold;
        FreeformAxisThreshold = (decimal)s.Matching.FreeformAxisThreshold;
        EnableAutoDetectXy = s.Matching.EnableAutoDetectXy;
        EnableFreeformEdgeSpecialization = s.Matching.EnableFreeformEdgeSpecialization;
        AutoReplayStep2AfterProjectLoad = s.View.AutoReplayStep2AfterProjectLoad;

        // --- Index Mapping Settings ---
        MappingWeightIou = (decimal)s.IndexMapping.WeightIou;
        MappingWeightCentroidDistance = (decimal)s.IndexMapping.WeightCentroidDistance;
        MappingWeightAreaRatio = (decimal)s.IndexMapping.WeightAreaRatio;
        MappingLowConfidenceThreshold = (decimal)s.IndexMapping.LowConfidenceThreshold;
        MappingAmbiguousMargin = (decimal)s.IndexMapping.AmbiguousMargin;
        MappingCandidatePaddingCells = s.IndexMapping.CandidatePaddingCells;
        MappingCandidateNumber = s.IndexMapping.GetEffectiveCandidateNumber();
        CadOutputFwDiffIndexStart = s.IndexMapping.CadOutputFwDiffStartIndex;
        CadOutputFwDiffAutoMode = s.IndexMapping.CadOutputFwDiffAutoMode;
        var dxfModeOption = CadOutputFwDiffAutoModeOptions.FirstOrDefault(option => option.Value == s.IndexMapping.CadOutputFwDiffAutoMode);
        if (string.IsNullOrWhiteSpace(dxfModeOption.Display))
        {
            dxfModeOption = CadOutputFwDiffAutoModeOptions.Count > 0 ? CadOutputFwDiffAutoModeOptions[0] : default;
        }

        SelectedCadOutputFwDiffAutoModeOption = dxfModeOption;
        var anchorFallback = _projectFile.CadOutputFwDiffIndexAnchorCadPadId ?? -1;
        var anchorCadId = anchorFallback;
        foreach (var value in _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc.Values)
        {
            anchorCadId = value;
            break;
        }

        CadOutputFwDiffIndexAnchorCadId = anchorCadId;

        // --- Notch Settings ---
        EnableV21 = s.Notch.EnabledVersions.Contains(NotchAlgorithmVersion.V21);
        EnableV22 = s.Notch.EnabledVersions.Contains(NotchAlgorithmVersion.V22);
        if (!EnableV21 && !EnableV22)
        {
            EnableV21 = true;
            EnableV22 = true;
        }
        OnPropertyChanged(nameof(IsLenScaleVisible));
        LenScale = s.Notch.LenScale;
        NullValue = s.Notch.NullValue;
        _suppressNotchThresholdSync = true;
        NotchThresholdQ7 = s.Notch.ThresholdQ7;
        NotchThresholdPercent = (decimal)Math.Clamp(s.Notch.ThresholdPercentV22, 0.0, 100.0);
        LinkNotchThresholds = s.Notch.LinkVersionThresholds;
        if (LinkNotchThresholds)
        {
            NotchThresholdPercent = Math.Round(NotchThresholdQ7 * 100m / 128m, 2);
        }
        _suppressNotchThresholdSync = false;
        var compensationModelOption = NotchCompensationModelOptions.FirstOrDefault(
            option => option.Value == s.Notch.CompensationModel);
        if (string.IsNullOrWhiteSpace(compensationModelOption.Display))
        {
            compensationModelOption = NotchCompensationModelOptions.Count > 0
                ? NotchCompensationModelOptions[0]
                : default;
        }

        SelectedNotchCompensationModelOption = compensationModelOption;
        ApplyNotchCompensationModeToLegacySwitches(
            SelectedNotchCompensationModelOption.Value,
            s.Notch.EnableToRegular,
            s.Notch.EnableToFull);
        ToFullStrictOverlapPercent = (decimal)s.Notch.MultiOwnerStrictOverlapPercent;
        EnableToFullRuleEngine = s.Notch.EnableToFullRuleEngine;
        EnableToFullRuleTrace = s.Notch.EnableToFullRuleTrace;
        EnableBoundaryVirtualAreaCap = s.Notch.EnableBoundaryVirtualAreaCap;
        BoundaryVirtualAreaCapPercent = (decimal)(NotchV22CompensationService.NormalizeBoundaryVirtualAreaCapRatio(
            s.Notch.BoundaryVirtualAreaCapRatio) * 100.0);
        EnableTargetCoverageGuard = s.Notch.EnableTargetCoverageGuard;
        TargetCoverageCapPercent = s.Notch.TargetCoverageCapPercent;
        OnPropertyChanged(nameof(NotchToFullRuleEngineSummary));
        var notchExportProfileOption = NotchExportProfileOptions.FirstOrDefault(option => option.Value == s.Notch.ExportProfile);
        if (string.IsNullOrWhiteSpace(notchExportProfileOption.Display))
        {
            notchExportProfileOption = NotchExportProfileOptions.Count > 0 ? NotchExportProfileOptions[0] : default;
        }

        SelectedNotchExportProfileOption = notchExportProfileOption;

        // --- View Settings ---
        CadFillOpacity = (decimal)Math.Clamp(s.View.CadFillOpacity, 0.0, 1.0);
        CadLineOpacity = NormalizeLineOpacity(s.View.CadLineOpacity);
        RegularFillOpacity = (decimal)Math.Clamp(s.View.RegularFillOpacity, 0.0, 1.0);
        RegularLineOpacity = NormalizeLineOpacity(s.View.RegularLineOpacity);
        AreaBucketTolerance = (decimal)s.View.AreaBucketTolerance;
        MaxAreaBuckets = s.View.MaxAreaBuckets;

        // --- Manual Sizing Scope ---
        _suppressScopeSync = true; // Temporarily suppress scope synchronization.
        IsWidthRowLocal = s.Grid.WidthScope == WidthAdjustmentScope.RowLocal;
        IsHeightColumnLocal = s.Grid.HeightScope == HeightAdjustmentScope.ColumnLocal;
        UseLocalSizing = IsWidthRowLocal && IsHeightColumnLocal; // Sync UseLocalSizing based on individual scopes.
        _suppressScopeSync = false;

        // --- Manual Sizing Inputs (reset on load) ---
        PendingColumnWidth = 0;
        PendingRowHeight = 0;
        ManualRowsRange = string.Empty;
        ManualColsRange = string.Empty;
        ManualRangeStatus = "No manual range.";
        ManualRangeError = string.Empty;
        HasManualRangeError = false;

        RebuildCascadeSettings(); // Rebuild cascade-related UI elements.

        _isLoadingSettings = false; // Reset loading flag after cascade rebuild (prevents stray rebuild triggers).
        HasUnsavedChanges = false; // Project is clean after loading.
    }

    /// <summary>
    /// Ensures that <see cref="GridSettings.ActiveAreaWidth"/> and <see cref="GridSettings.ActiveAreaHeight"/>
    /// have valid default values if they are zero or negative.
    /// </summary>
    /// <param name="grid">The <see cref="GridSettings"/> to validate.</param>
    private static void EnsureActiveAreaDefaults(GridSettings grid)
    {
        var defaults = new GridSettings(); // Use default constructor to get default values.
        if (grid.ActiveAreaWidth <= 0)
        {
            grid.ActiveAreaWidth = defaults.ActiveAreaWidth;
        }
        if (grid.ActiveAreaHeight <= 0)
        {
            grid.ActiveAreaHeight = defaults.ActiveAreaHeight;
        }
    }

    /// <summary>
    /// Ensures that <see cref="GridSettings.XChannels"/> and <see cref="GridSettings.YChannels"/>
    /// have valid default values if they are zero or negative.
    /// </summary>
    /// <param name="grid">The <see cref="GridSettings"/> to validate.</param>
    private static void EnsureChannelDefaults(GridSettings grid)
    {
        var defaults = new GridSettings();
        if (grid.XChannels <= 0)
        {
            grid.XChannels = defaults.XChannels;
        }
        if (grid.YChannels <= 0)
        {
            grid.YChannels = defaults.YChannels;
        }
    }

    /// <summary>
    /// Applies the current values from the ViewModel's UI properties back to the
    /// provided <see cref="ProjectSettings"/> object.
    /// </summary>
    /// <param name="s">The <see cref="ProjectSettings"/> object to update.</param>
    private void ApplyUiToSettings(ProjectSettings s)
    {
        ApplyUiToSettings(s, markUnsaved: true);
    }

    private void ApplyUiToSettings(ProjectSettings s, bool markUnsaved)
    {
        // --- Grid Settings ---
        var requestedXChannels = ClampToInt(XChannels, 1, int.MaxValue);
        var requestedYChannels = ClampToInt(YChannels, 1, int.MaxValue);
        s.Grid.MaxChannelsX = Math.Max(s.Grid.MaxChannelsX, requestedXChannels);
        s.Grid.MaxChannelsY = Math.Max(s.Grid.MaxChannelsY, requestedYChannels);
        s.Grid.XChannels = requestedXChannels;
        s.Grid.YChannels = requestedYChannels;
        s.Grid.CascadeNum = ClampToInt(CascadeNum, 1, Math.Max(1, s.Grid.XChannels));
        s.Grid.ScanOrder = SelectedScanOrder;
        s.Grid.AlignmentMode = GridAlignmentMode;
        s.Grid.RegularSourceMode = RegularSourceMode;
        s.Grid.BoundsPaddingRatio = Math.Clamp((double)GridPaddingPercent / 100.0, 0.0, 0.5); // Convert percentage to ratio.
        s.Grid.ActiveAreaWidth = Math.Max(0.0, (double)ActiveAreaWidth);
        s.Grid.ActiveAreaHeight = Math.Max(0.0, (double)ActiveAreaHeight);
        s.Grid.PanelBiasX = (double)PanelBiasX;
        s.Grid.PanelBiasY = (double)PanelBiasY;
        s.Grid.RecalcBoundsOnLayerFilter = RecalcBoundsOnLayerFilter;
        s.Grid.BoundLayerName = SelectedBoundLayerOption?.Name;
        s.Grid.RegularSourceLayerName = SelectedRegularSourceLayerOption?.Name;
        // Map cascade IC settings to lists for ProjectSettings.
        s.Grid.ReplacePerIcChannels(
            CascadeIcSettings.Select(s => Math.Max(1, (int)Math.Round(s.XChannels))).ToList(),
            CascadeIcSettings.Select(s => Math.Max(1, (int)Math.Round(s.YChannels))).ToList());

        // --- Matching Settings ---
        s.Matching.MatchThreshold = Math.Clamp((double)MatchThreshold, 0.0, 1.0);
        s.Matching.FreeformAxisThreshold = Math.Clamp((double)FreeformAxisThreshold, 0.0, 1.0);
        s.Matching.EnableAutoDetectXy = EnableAutoDetectXy;
        s.Matching.EnableFreeformEdgeSpecialization = EnableFreeformEdgeSpecialization;

        // --- Index Mapping Settings ---
        s.IndexMapping.WeightIou = Math.Max(0.0, (double)MappingWeightIou);
        s.IndexMapping.WeightCentroidDistance = Math.Max(0.0, (double)MappingWeightCentroidDistance);
        s.IndexMapping.WeightAreaRatio = Math.Max(0.0, (double)MappingWeightAreaRatio);
        s.IndexMapping.LowConfidenceThreshold = Math.Clamp((double)MappingLowConfidenceThreshold, 0.0, 1.0);
        s.IndexMapping.AmbiguousMargin = Math.Clamp((double)MappingAmbiguousMargin, 0.0, 1.0);
        s.IndexMapping.CandidateNumber = ClampToInt(MappingCandidateNumber, 1, 400);
        s.IndexMapping.CandidatePaddingCells = IndexMappingSettings.PaddingFromCandidateNumber(s.IndexMapping.CandidateNumber);
        s.IndexMapping.CadOutputFwDiffStartIndex = ClampToInt(CadOutputFwDiffIndexStart, 0, 1_000_000);
        s.IndexMapping.CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode;

        // --- Notch Settings ---
        s.Notch.LenScale = ClampToInt(LenScale, 1, int.MaxValue);
        s.Notch.NullValue = ClampToInt(NullValue, 0, int.MaxValue);
        s.Notch.ThresholdQ7 = ClampToInt(NotchThresholdQ7, 0, 128);
        s.Notch.ThresholdPercentV22 = Math.Clamp((double)NotchThresholdPercent, 0.0, 100.0);
        s.Notch.LinkVersionThresholds = LinkNotchThresholds;
        s.Notch.CompensationModel = SelectedNotchCompensationModelOption.Value;
        var (enableToRegular, enableToFull) = s.Notch.ResolveStep3Switches();
        s.Notch.EnableToRegular = enableToRegular;
        s.Notch.EnableToFull = enableToFull;
        s.Notch.MultiOwnerStrictOverlapPercent = Math.Clamp((double)ToFullStrictOverlapPercent, 0.0, 100.0);
        s.Notch.EnableToFullRuleEngine = EnableToFullRuleEngine;
        s.Notch.EnableToFullRuleTrace = EnableToFullRuleTrace;
        s.Notch.EnableBoundaryVirtualAreaCap = EnableBoundaryVirtualAreaCap;
        s.Notch.BoundaryVirtualAreaCapRatio = NotchV22CompensationService.NormalizeBoundaryVirtualAreaCapRatio(
            (double)BoundaryVirtualAreaCapPercent / 100.0);
        s.Notch.EnableTargetCoverageGuard = EnableTargetCoverageGuard;
        s.Notch.TargetCoverageCapPercent = ClampToInt(TargetCoverageCapPercent, 0, 255);
        s.Notch.ExportProfile = SelectedNotchExportProfileOption.Value;
        // Update enabled notch algorithm versions via NotchSettings API.
        var enabledVersions = new List<NotchAlgorithmVersion>(capacity: 2);
        if (EnableV21)
        {
            enabledVersions.Add(NotchAlgorithmVersion.V21);
        }

        if (EnableV22)
        {
            enabledVersions.Add(NotchAlgorithmVersion.V22);
        }

        s.Notch.ReplaceEnabledVersions(enabledVersions);

        // --- View Settings ---
        s.View.CadFillOpacity = Math.Clamp((double)CadFillOpacity, 0.0, 1.0);
        s.View.CadLineOpacity = Math.Clamp((double)CadLineOpacity, 0.0, 1.0);
        s.View.RegularFillOpacity = Math.Clamp((double)RegularFillOpacity, 0.0, 1.0);
        s.View.RegularLineOpacity = Math.Clamp((double)RegularLineOpacity, 0.0, 1.0);
        s.View.AutoReplayStep2AfterProjectLoad = AutoReplayStep2AfterProjectLoad;
        s.View.AreaBucketTolerance = Math.Max(0.0, (double)AreaBucketTolerance);
        s.View.MaxAreaBuckets = ClampToInt(MaxAreaBuckets, 1, 1024);

        // --- Manual Sizing Scope ---
        s.Grid.WidthScope = IsWidthRowLocal ? WidthAdjustmentScope.RowLocal : WidthAdjustmentScope.ColumnGlobal;
        s.Grid.HeightScope = IsHeightColumnLocal ? HeightAdjustmentScope.ColumnLocal : HeightAdjustmentScope.RowGlobal;

        ManualSizingService.EnsureSizingLists(s.Grid); // Ensure sizing lists in settings are consistent.
        if (markUnsaved)
        {
            MarkUnsaved(); // Mark project as unsaved due to settings change.
        }
    }
}

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void LoadSettingsToUi(ProjectSettings s)
    {
        using (_settingsSuppression.EnterSettingsLoading())
        {
            LoadGridSettingsToUi(s.Grid);

            // --- Matching Settings ---
            LoadMatchingAndIndexMappingSettingsToUi(s);

            // --- Notch Settings ---
            LoadNotchSettingsToUi(s.Notch);

            // --- View Settings ---
            CadFillOpacity = (decimal)Math.Clamp(s.View.CadFillOpacity, 0.0, 1.0);
            CadLineOpacity = NormalizeLineOpacity(s.View.CadLineOpacity);
            RegularFillOpacity = (decimal)Math.Clamp(s.View.RegularFillOpacity, 0.0, 1.0);
            RegularLineOpacity = NormalizeLineOpacity(s.View.RegularLineOpacity);
            AreaBucketTolerance = (decimal)s.View.AreaBucketTolerance;
            MaxAreaBuckets = s.View.MaxAreaBuckets;
            // --- Manual Sizing Scope ---
            using (_settingsSuppression.EnterScopeSync())
            {
                IsWidthRowLocal = s.Grid.WidthScope == WidthAdjustmentScope.RowLocal;
                IsHeightColumnLocal = s.Grid.HeightScope == HeightAdjustmentScope.ColumnLocal;
                UseLocalSizing = IsWidthRowLocal && IsHeightColumnLocal; // Sync UseLocalSizing based on individual scopes.
            }

            // --- Manual Sizing Inputs (reset on load) ---
            PendingColumnWidth = 0;
            PendingRowHeight = 0;
            ManualRowsRange = string.Empty;
            ManualColsRange = string.Empty;
            ManualRangeStatus = "No manual range.";
            ManualRangeError = string.Empty;

            RebuildCascadeSettings(); // Rebuild cascade-related UI elements.
        }
        HasUnsavedChanges = false; // Project is clean after loading.
    }

    private void LoadGridSettingsToUi(GridSettings grid)
    {
        EnsureChannelDefaults(grid); // Ensure channel counts are valid.
        EnsureActiveAreaDefaults(grid); // Ensure active area dimensions are valid.
        ManualSizingService.EnsureSizingLists(grid); // Ensure sizing lists are correctly initialized.
        // --- Grid Settings ---
        XChannels = grid.XChannels;
        YChannels = grid.YChannels;
        CascadeNum = grid.CascadeNum;
        SelectedScanOrder = grid.ScanOrder;
        GridAlignmentMode = grid.AlignmentMode;
        RegularSourceMode = grid.RegularSourceMode;

        // Fallback for potentially invalid or uninitialized enum values.
        if (string.IsNullOrWhiteSpace(SelectedGridAlignmentOption.Display))
        {
            var fallback = GridAlignmentOptions.FirstOrDefault(o => o.Value == GridAlignmentMode.FromCadBounds);
            if (!string.IsNullOrWhiteSpace(fallback.Display))
            {
                GridAlignmentMode = fallback.Value;
            }
        }
        if (string.IsNullOrWhiteSpace(SelectedRegularSourceModeOption.Display))
        {
            var fallback = RegularSourceModeOptions.FirstOrDefault(o => o.Value == RegularSourceMode.GeneratedGrid);
            if (!string.IsNullOrWhiteSpace(fallback.Display))
            {
                RegularSourceMode = fallback.Value;
            }
        }
        // Notify UI of changes to dependent properties.
        OnPropertyChanged(nameof(IsPanelAlignment));
        OnPropertyChanged(nameof(IsDxfLayerRegularSource));

        GridPaddingPercent = (decimal)(grid.BoundsPaddingRatio * 100.0);
        ActiveAreaWidth = (decimal)grid.ActiveAreaWidth;
        ActiveAreaHeight = (decimal)grid.ActiveAreaHeight;
        PanelBiasX = (decimal)grid.PanelBiasX;
        PanelBiasY = (decimal)grid.PanelBiasY;
        RecalcBoundsOnLayerFilter = grid.RecalcBoundsOnLayerFilter;
        _pendingLayerSelection.BoundLayerName = grid.BoundLayerName;
        _pendingLayerSelection.RegularSourceLayerName = grid.RegularSourceLayerName;
    }

    private void LoadMatchingAndIndexMappingSettingsToUi(ProjectSettings s)
    {
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
        var anchorFallback = _projectFile.CadOutputFwDiffIndexAnchorCadPadId ?? -1;
        var anchorCadId = anchorFallback;
        foreach (var value in _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc.Values)
        {
            anchorCadId = value;
            break;
        }

        CadOutputFwDiffIndexAnchorCadId = anchorCadId;
    }

    private void LoadNotchSettingsToUi(NotchSettings notch)
    {
        EnableV21 = notch.EnabledVersions.Contains(NotchAlgorithmVersion.V21);
        EnableV22 = notch.EnabledVersions.Contains(NotchAlgorithmVersion.V22);
        if (!EnableV21 && !EnableV22)
        {
            EnableV21 = true;
            EnableV22 = true;
        }
        LenScale = notch.LenScale;
        NullValue = notch.NullValue;
        using (_settingsSuppression.EnterNotchThresholdSync())
        {
            NotchThresholdQ7 = notch.ThresholdQ7;
            NotchThresholdPercent = (decimal)Math.Clamp(notch.ThresholdPercentV22, 0.0, 100.0);
            LinkNotchThresholds = notch.LinkVersionThresholds;
            if (LinkNotchThresholds)
            {
                NotchThresholdPercent = Math.Round(NotchThresholdQ7 * 100m / 128m, 2);
            }
        }
        var compensationModelOption = NotchCompensationModelOptions.FirstOrDefault(
            option => option.Value == notch.CompensationModel);
        if (string.IsNullOrWhiteSpace(compensationModelOption.Display))
        {
            compensationModelOption = NotchCompensationModelOptions.Count > 0
                ? NotchCompensationModelOptions[0]
                : default;
        }

        SelectedNotchCompensationModelOption = compensationModelOption;
        ApplyNotchCompensationModeToLegacySwitches(
            SelectedNotchCompensationModelOption.Value,
            notch.EnableToRegular,
            notch.EnableToFull);
        ToFullStrictOverlapPercent = (decimal)notch.MultiOwnerStrictOverlapPercent;
        EnableToFullRuleEngine = notch.EnableToFullRuleEngine;
        EnableToFullRuleTrace = notch.EnableToFullRuleTrace;
        EnableBoundaryVirtualAreaCap = notch.EnableBoundaryVirtualAreaCap;
        BoundaryVirtualAreaCapPercent = (decimal)(NotchV22CompensationService.NormalizeBoundaryVirtualAreaCapRatio(
            notch.BoundaryVirtualAreaCapRatio) * 100.0);
        EnableTargetCoverageGuard = notch.EnableTargetCoverageGuard;
        TargetCoverageCapPercent = notch.TargetCoverageCapPercent;
        OnPropertyChanged(nameof(NotchToFullRuleEngineSummary));
        var notchExportProfileOption = NotchExportProfileOptions.FirstOrDefault(option => option.Value == notch.ExportProfile);
        if (string.IsNullOrWhiteSpace(notchExportProfileOption.Display))
        {
            notchExportProfileOption = NotchExportProfileOptions.Count > 0 ? NotchExportProfileOptions[0] : default;
        }

        SelectedNotchExportProfileOption = notchExportProfileOption;
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
        ApplyUiToGridSettings(s.Grid);

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
        ApplyUiToNotchSettings(s.Notch);

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

    private void ApplyUiToGridSettings(GridSettings grid)
    {
        var requestedXChannels = ClampToInt(XChannels, 1, int.MaxValue);
        var requestedYChannels = ClampToInt(YChannels, 1, int.MaxValue);
        grid.MaxChannelsX = Math.Max(grid.MaxChannelsX, requestedXChannels);
        grid.MaxChannelsY = Math.Max(grid.MaxChannelsY, requestedYChannels);
        grid.XChannels = requestedXChannels;
        grid.YChannels = requestedYChannels;
        grid.CascadeNum = ClampToInt(CascadeNum, 1, Math.Max(1, grid.XChannels));
        grid.ScanOrder = SelectedScanOrder;
        grid.AlignmentMode = GridAlignmentMode;
        grid.RegularSourceMode = RegularSourceMode;
        grid.BoundsPaddingRatio = Math.Clamp((double)GridPaddingPercent / 100.0, 0.0, 0.5); // Convert percentage to ratio.
        grid.ActiveAreaWidth = Math.Max(0.0, (double)ActiveAreaWidth);
        grid.ActiveAreaHeight = Math.Max(0.0, (double)ActiveAreaHeight);
        grid.PanelBiasX = (double)PanelBiasX;
        grid.PanelBiasY = (double)PanelBiasY;
        grid.RecalcBoundsOnLayerFilter = RecalcBoundsOnLayerFilter;
        grid.BoundLayerName = SelectedBoundLayerOption?.Name;
        grid.RegularSourceLayerName = SelectedRegularSourceLayerOption?.Name;
        // Map cascade IC settings to lists for ProjectSettings.
        grid.ReplacePerIcChannels(
            CascadeIcSettings.Select(s => Math.Max(1, (int)Math.Round(s.XChannels))).ToList(),
            CascadeIcSettings.Select(s => Math.Max(1, (int)Math.Round(s.YChannels))).ToList());
    }

    private void ApplyUiToNotchSettings(NotchSettings notch)
    {
        notch.LenScale = ClampToInt(LenScale, 1, int.MaxValue);
        notch.NullValue = ClampToInt(NullValue, 0, int.MaxValue);
        notch.ThresholdQ7 = ClampToInt(NotchThresholdQ7, 0, 128);
        notch.ThresholdPercentV22 = Math.Clamp((double)NotchThresholdPercent, 0.0, 100.0);
        notch.LinkVersionThresholds = LinkNotchThresholds;
        notch.CompensationModel = SelectedNotchCompensationModelOption.Value;
        notch.EnableToRegular = EnableToRegular;
        notch.EnableToFull = EnableToFull;
        var (enableToRegular, enableToFull) = notch.ResolveStep3Switches();
        notch.EnableToRegular = enableToRegular;
        notch.EnableToFull = enableToFull;
        notch.MultiOwnerStrictOverlapPercent = Math.Clamp((double)ToFullStrictOverlapPercent, 0.0, 100.0);
        notch.EnableToFullRuleEngine = EnableToFullRuleEngine;
        notch.EnableToFullRuleTrace = EnableToFullRuleTrace;
        notch.EnableBoundaryVirtualAreaCap = EnableBoundaryVirtualAreaCap;
        notch.BoundaryVirtualAreaCapRatio = NotchV22CompensationService.NormalizeBoundaryVirtualAreaCapRatio(
            (double)BoundaryVirtualAreaCapPercent / 100.0);
        notch.EnableTargetCoverageGuard = EnableTargetCoverageGuard;
        notch.TargetCoverageCapPercent = ClampToInt(TargetCoverageCapPercent, 0, 255);
        notch.ExportProfile = SelectedNotchExportProfileOption.Value;
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

        notch.ReplaceEnabledVersions(enabledVersions);
    }
}

// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.Contracts.Settings;

namespace FreeformHelper.UI.Services;

/// <summary>Maps live values through existing setters and scopes without retaining project snapshots.</summary>
internal sealed partial class SettingsSnapshotMapper(ISettingsSnapshotHost host)
{
    private readonly ISettingsSnapshotHost _host = host;

    internal void LoadSettingsToUi(ProjectSettings s)
    {
        using (_host.EnterSettingsLoading())
        {
            LoadGridSettingsToUi(s.Grid);

            // --- Matching Settings ---
            LoadMatchingAndIndexMappingSettingsToUi(s);

            // --- Notch Settings ---
            LoadNotchSettingsToUi(s.Notch);

            // --- View Settings ---
            _host.CadFillOpacity = (decimal)Math.Clamp(s.View.CadFillOpacity, 0.0, 1.0);
            _host.CadLineOpacity = _host.NormalizeLineOpacity(s.View.CadLineOpacity);
            _host.RegularFillOpacity = (decimal)Math.Clamp(s.View.RegularFillOpacity, 0.0, 1.0);
            _host.RegularLineOpacity = _host.NormalizeLineOpacity(s.View.RegularLineOpacity);
            _host.AreaBucketTolerance = (decimal)s.View.AreaBucketTolerance;
            _host.MaxAreaBuckets = s.View.MaxAreaBuckets;
            // --- Manual Sizing Scope ---
            using (_host.EnterScopeSync())
            {
                _host.IsWidthRowLocal = s.Grid.WidthScope == WidthAdjustmentScope.RowLocal;
                _host.IsHeightColumnLocal = s.Grid.HeightScope == HeightAdjustmentScope.ColumnLocal;
                _host.UseLocalSizing = _host.IsWidthRowLocal && _host.IsHeightColumnLocal; // Sync the combined mode from the individual scopes.
            }

            // --- Manual Sizing Inputs (reset on load) ---
            _host.PendingColumnWidth = 0;
            _host.PendingRowHeight = 0;
            _host.ManualRowsRange = string.Empty;
            _host.ManualColsRange = string.Empty;
            _host.ManualRangeStatus = "No manual range.";
            _host.ManualRangeError = string.Empty;

            _host.RebuildCascadeSettings(); // Rebuild cascade-related UI elements.
        }
        _host.HasUnsavedChanges = false; // Project is clean after loading.
    }

    private void LoadGridSettingsToUi(GridSettings grid)
    {
        EnsureChannelDefaults(grid); // Ensure channel counts are valid.
        EnsureActiveAreaDefaults(grid); // Ensure active area dimensions are valid.
        ManualSizingService.EnsureSizingLists(grid); // Ensure sizing lists are correctly initialized.
        // --- Grid Settings ---
        _host.XChannels = grid.XChannels;
        _host.YChannels = grid.YChannels;
        _host.CascadeNum = grid.CascadeNum;
        _host.SelectedScanOrder = grid.ScanOrder;
        _host.GridAlignmentMode = grid.AlignmentMode;
        _host.RegularSourceMode = grid.RegularSourceMode;

        _host.EnsureGridAlignmentOption();
        _host.EnsureRegularSourceModeOption();
        // Notify UI of changes to dependent properties.
        _host.NotifyPropertyChanged("IsPanelAlignment");
        _host.NotifyPropertyChanged("IsDxfLayerRegularSource");

        _host.GridPaddingPercent = (decimal)(grid.BoundsPaddingRatio * 100.0);
        _host.ActiveAreaWidth = (decimal)grid.ActiveAreaWidth;
        _host.ActiveAreaHeight = (decimal)grid.ActiveAreaHeight;
        _host.PanelBiasX = (decimal)grid.PanelBiasX;
        _host.PanelBiasY = (decimal)grid.PanelBiasY;
        _host.RecalcBoundsOnLayerFilter = grid.RecalcBoundsOnLayerFilter;
        _host.PendingBoundLayerName = grid.BoundLayerName;
        _host.PendingRegularSourceLayerName = grid.RegularSourceLayerName;
    }

    private void LoadMatchingAndIndexMappingSettingsToUi(ProjectSettings s)
    {
        _host.MatchThreshold = (decimal)s.Matching.MatchThreshold;
        _host.FreeformAxisThreshold = (decimal)s.Matching.FreeformAxisThreshold;
        _host.EnableAutoDetectXy = s.Matching.EnableAutoDetectXy;
        _host.EnableFreeformEdgeSpecialization = s.Matching.EnableFreeformEdgeSpecialization;
        _host.AutoReplayStep2AfterProjectLoad = s.View.AutoReplayStep2AfterProjectLoad;

        // --- Index Mapping Settings ---
        _host.MappingWeightIou = (decimal)s.IndexMapping.WeightIou;
        _host.MappingWeightCentroidDistance = (decimal)s.IndexMapping.WeightCentroidDistance;
        _host.MappingWeightAreaRatio = (decimal)s.IndexMapping.WeightAreaRatio;
        _host.MappingLowConfidenceThreshold = (decimal)s.IndexMapping.LowConfidenceThreshold;
        _host.MappingAmbiguousMargin = (decimal)s.IndexMapping.AmbiguousMargin;
        _host.MappingCandidatePaddingCells = s.IndexMapping.CandidatePaddingCells;
        _host.MappingCandidateNumber = s.IndexMapping.GetEffectiveCandidateNumber();
        _host.CadOutputFwDiffIndexStart = s.IndexMapping.CadOutputFwDiffStartIndex;
        _host.CadOutputFwDiffAutoMode = s.IndexMapping.CadOutputFwDiffAutoMode;
        var anchorFallback = _host.LegacyFwDiffAnchor ?? -1;
        var anchorCadId = anchorFallback;
        foreach (var value in _host.FwDiffAnchorValues)
        {
            anchorCadId = value;
            break;
        }

        _host.CadOutputFwDiffIndexAnchorCadId = anchorCadId;
    }

    private void LoadNotchSettingsToUi(NotchSettings notch)
    {
        _host.EnableV21 = notch.EnabledVersions.Contains(NotchAlgorithmVersion.V21);
        _host.EnableV22 = notch.EnabledVersions.Contains(NotchAlgorithmVersion.V22);
        if (!_host.EnableV21 && !_host.EnableV22)
        {
            _host.EnableV21 = true;
            _host.EnableV22 = true;
        }
        _host.LenScale = notch.LenScale;
        _host.NullValue = notch.NullValue;
        using (_host.EnterNotchThresholdSync())
        {
            _host.NotchThresholdQ7 = notch.ThresholdQ7;
            _host.NotchThresholdPercent = (decimal)Math.Clamp(notch.ThresholdPercentV22, 0.0, 100.0);
            _host.LinkNotchThresholds = notch.LinkVersionThresholds;
            if (_host.LinkNotchThresholds)
            {
                _host.NotchThresholdPercent = Math.Round(_host.NotchThresholdQ7 * 100m / 128m, 2);
            }
        }
        _host.SelectNotchCompensationModel(notch.CompensationModel);
        _host.ApplyNotchCompensationModeToLegacySwitches(
            _host.SelectedCompensationModel,
            notch.EnableToRegular,
            notch.EnableToFull);
        _host.ToFullStrictOverlapPercent = (decimal)notch.MultiOwnerStrictOverlapPercent;
        _host.EnableToFullRuleEngine = notch.EnableToFullRuleEngine;
        _host.EnableToFullRuleTrace = notch.EnableToFullRuleTrace;
        _host.EnableBoundaryVirtualAreaCap = notch.EnableBoundaryVirtualAreaCap;
        _host.BoundaryVirtualAreaCapPercent = (decimal)(NotchV22CompensationService.NormalizeBoundaryVirtualAreaCapRatio(
            notch.BoundaryVirtualAreaCapRatio) * 100.0);
        _host.EnableTargetCoverageGuard = notch.EnableTargetCoverageGuard;
        _host.TargetCoverageCapPercent = notch.TargetCoverageCapPercent;
        _host.NotifyPropertyChanged("NotchToFullRuleEngineSummary");
        _host.SelectNotchExportProfile(notch.ExportProfile);
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

    internal void ApplyUiToSettings(ProjectSettings s, bool markUnsaved)
    {
        // --- Grid Settings ---
        ApplyUiToGridSettings(s.Grid);

        // --- Matching Settings ---
        s.Matching.MatchThreshold = Math.Clamp((double)_host.MatchThreshold, 0.0, 1.0);
        s.Matching.FreeformAxisThreshold = Math.Clamp((double)_host.FreeformAxisThreshold, 0.0, 1.0);
        s.Matching.EnableAutoDetectXy = _host.EnableAutoDetectXy;
        s.Matching.EnableFreeformEdgeSpecialization = _host.EnableFreeformEdgeSpecialization;

        // --- Index Mapping Settings ---
        s.IndexMapping.WeightIou = Math.Max(0.0, (double)_host.MappingWeightIou);
        s.IndexMapping.WeightCentroidDistance = Math.Max(0.0, (double)_host.MappingWeightCentroidDistance);
        s.IndexMapping.WeightAreaRatio = Math.Max(0.0, (double)_host.MappingWeightAreaRatio);
        s.IndexMapping.LowConfidenceThreshold = Math.Clamp((double)_host.MappingLowConfidenceThreshold, 0.0, 1.0);
        s.IndexMapping.AmbiguousMargin = Math.Clamp((double)_host.MappingAmbiguousMargin, 0.0, 1.0);
        s.IndexMapping.CandidateNumber = _host.ClampToInt(_host.MappingCandidateNumber, 1, 400);
        s.IndexMapping.CandidatePaddingCells = IndexMappingSettings.PaddingFromCandidateNumber(s.IndexMapping.CandidateNumber);
        s.IndexMapping.CadOutputFwDiffStartIndex = _host.ClampToInt(_host.CadOutputFwDiffIndexStart, 0, 1_000_000);
        s.IndexMapping.CadOutputFwDiffAutoMode = _host.CadOutputFwDiffAutoMode;

        // --- Notch Settings ---
        ApplyUiToNotchSettings(s.Notch);

        // --- View Settings ---
        s.View.CadFillOpacity = Math.Clamp((double)_host.CadFillOpacity, 0.0, 1.0);
        s.View.CadLineOpacity = Math.Clamp((double)_host.CadLineOpacity, 0.0, 1.0);
        s.View.RegularFillOpacity = Math.Clamp((double)_host.RegularFillOpacity, 0.0, 1.0);
        s.View.RegularLineOpacity = Math.Clamp((double)_host.RegularLineOpacity, 0.0, 1.0);
        s.View.AutoReplayStep2AfterProjectLoad = _host.AutoReplayStep2AfterProjectLoad;
        s.View.AreaBucketTolerance = Math.Max(0.0, (double)_host.AreaBucketTolerance);
        s.View.MaxAreaBuckets = _host.ClampToInt(_host.MaxAreaBuckets, 1, 1024);

        // --- Manual Sizing Scope ---
        s.Grid.WidthScope = _host.IsWidthRowLocal ? WidthAdjustmentScope.RowLocal : WidthAdjustmentScope.ColumnGlobal;
        s.Grid.HeightScope = _host.IsHeightColumnLocal ? HeightAdjustmentScope.ColumnLocal : HeightAdjustmentScope.RowGlobal;

        ManualSizingService.EnsureSizingLists(s.Grid); // Ensure sizing lists in settings are consistent.
        if (markUnsaved)
        {
            _host.MarkUnsaved(); // Mark project as unsaved due to settings change.
        }
    }

    private void ApplyUiToGridSettings(GridSettings grid)
    {
        var requestedXChannels = _host.ClampToInt(_host.XChannels, 1, int.MaxValue);
        var requestedYChannels = _host.ClampToInt(_host.YChannels, 1, int.MaxValue);
        grid.MaxChannelsX = Math.Max(grid.MaxChannelsX, requestedXChannels);
        grid.MaxChannelsY = Math.Max(grid.MaxChannelsY, requestedYChannels);
        grid.XChannels = requestedXChannels;
        grid.YChannels = requestedYChannels;
        grid.CascadeNum = _host.ClampToInt(_host.CascadeNum, 1, Math.Max(1, grid.XChannels));
        grid.ScanOrder = _host.SelectedScanOrder;
        grid.AlignmentMode = _host.GridAlignmentMode;
        grid.RegularSourceMode = _host.RegularSourceMode;
        grid.BoundsPaddingRatio = Math.Clamp((double)_host.GridPaddingPercent / 100.0, 0.0, 0.5); // Convert percentage to ratio.
        grid.ActiveAreaWidth = Math.Max(0.0, (double)_host.ActiveAreaWidth);
        grid.ActiveAreaHeight = Math.Max(0.0, (double)_host.ActiveAreaHeight);
        grid.PanelBiasX = (double)_host.PanelBiasX;
        grid.PanelBiasY = (double)_host.PanelBiasY;
        grid.RecalcBoundsOnLayerFilter = _host.RecalcBoundsOnLayerFilter;
        grid.BoundLayerName = _host.SelectedBoundLayerName;
        grid.RegularSourceLayerName = _host.SelectedRegularSourceLayerName;
        // Map cascade IC settings to lists for ProjectSettings.
        grid.ReplacePerIcChannels(
            _host.PerIcXChannels.Select(value => Math.Max(1, (int)Math.Round(value))).ToList(),
            _host.PerIcYChannels.Select(value => Math.Max(1, (int)Math.Round(value))).ToList());
    }

    private void ApplyUiToNotchSettings(NotchSettings notch)
    {
        notch.LenScale = _host.ClampToInt(_host.LenScale, 1, int.MaxValue);
        notch.NullValue = _host.ClampToInt(_host.NullValue, 0, int.MaxValue);
        notch.ThresholdQ7 = _host.ClampToInt(_host.NotchThresholdQ7, 0, 128);
        notch.ThresholdPercentV22 = Math.Clamp((double)_host.NotchThresholdPercent, 0.0, 100.0);
        notch.LinkVersionThresholds = _host.LinkNotchThresholds;
        notch.CompensationModel = _host.SelectedCompensationModel;
        notch.EnableToRegular = _host.EnableToRegular;
        notch.EnableToFull = _host.EnableToFull;
        var (enableToRegular, enableToFull) = notch.ResolveStep3Switches();
        notch.EnableToRegular = enableToRegular;
        notch.EnableToFull = enableToFull;
        notch.MultiOwnerStrictOverlapPercent = Math.Clamp((double)_host.ToFullStrictOverlapPercent, 0.0, 100.0);
        notch.EnableToFullRuleEngine = _host.EnableToFullRuleEngine;
        notch.EnableToFullRuleTrace = _host.EnableToFullRuleTrace;
        notch.EnableBoundaryVirtualAreaCap = _host.EnableBoundaryVirtualAreaCap;
        notch.BoundaryVirtualAreaCapRatio = NotchV22CompensationService.NormalizeBoundaryVirtualAreaCapRatio(
            (double)_host.BoundaryVirtualAreaCapPercent / 100.0);
        notch.EnableTargetCoverageGuard = _host.EnableTargetCoverageGuard;
        notch.TargetCoverageCapPercent = _host.ClampToInt(_host.TargetCoverageCapPercent, 0, 255);
        notch.ExportProfile = _host.SelectedExportProfile;
        // Update enabled notch algorithm versions via NotchSettings API.
        var enabledVersions = new List<NotchAlgorithmVersion>(capacity: 2);
        if (_host.EnableV21)
        {
            enabledVersions.Add(NotchAlgorithmVersion.V21);
        }

        if (_host.EnableV22)
        {
            enabledVersions.Add(NotchAlgorithmVersion.V22);
        }

        notch.ReplaceEnabledVersions(enabledVersions);
    }
}

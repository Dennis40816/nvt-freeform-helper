using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Settings;
using FreeformHelper.UI.Services;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SettingsWindowViewModel : ObservableObject
{
    private readonly FreeformHelperViewModel _owner;
    internal UiEventRunner UiEvents => _owner.UiEvents;
    private bool _suppressNotchThresholdSync;
    private bool _suppressCompensationModelSync;
    private readonly bool _showInternalLegacyNotchFields;

    public SettingsWindowViewModel(FreeformHelperViewModel owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _showInternalLegacyNotchFields = false;

        ScanOrderOptions = owner.ScanOrderOptions.ToList();
        var selectedScanOrder = ScanOrderOptions.FirstOrDefault(o => o.Value == owner.SelectedScanOrderOption.Value);
        if (string.IsNullOrWhiteSpace(selectedScanOrder.Display))
        {
            selectedScanOrder = GetFirstOrDefault(ScanOrderOptions);
        }

        GridAlignmentOptions = owner.GridAlignmentOptions.ToList();
        var selectedAlignment = GridAlignmentOptions.FirstOrDefault(o => o.Value == owner.SelectedGridAlignmentOption.Value);
        if (string.IsNullOrWhiteSpace(selectedAlignment.Display))
        {
            selectedAlignment = GetFirstOrDefault(GridAlignmentOptions);
        }

        RegularSourceModeOptions = owner.RegularSourceModeOptions.ToList();
        var selectedRegularSourceMode = RegularSourceModeOptions.FirstOrDefault(o =>
            o.Value == owner.SelectedRegularSourceModeOption.Value);
        if (string.IsNullOrWhiteSpace(selectedRegularSourceMode.Display))
        {
            selectedRegularSourceMode = GetFirstOrDefault(RegularSourceModeOptions);
        }

        RegularSourceLayerOptions = owner.RegularSourceLayerOptions.ToList();
        var selectedRegularSourceLayer = RegularSourceLayerOptions.FirstOrDefault(o =>
            string.Equals(o.Name, owner.SelectedRegularSourceLayerOption.Name, StringComparison.Ordinal))
            ?? GetFirstOrDefault(RegularSourceLayerOptions)
            ?? new FreeformHelperViewModel.RegularSourceLayerOption(null, "Select layer");

        LogLevelOptions = owner.LogLevelOptions.ToList();
        var selectedLogLevel = LogLevelOptions.FirstOrDefault(o =>
            string.Equals(o.Value, owner.SelectedLogLevelOption.Value, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(selectedLogLevel.Display))
        {
            selectedLogLevel = GetFirstOrDefault(LogLevelOptions);
        }

        CadOutputFwDiffAutoModeOptions = owner.CadOutputFwDiffAutoModeOptions.ToList();
        var selectedCadOutputFwDiffAutoMode = CadOutputFwDiffAutoModeOptions.FirstOrDefault(o =>
            o.Value == owner.SelectedCadOutputFwDiffAutoModeOption.Value);
        if (string.IsNullOrWhiteSpace(selectedCadOutputFwDiffAutoMode.Display))
        {
            selectedCadOutputFwDiffAutoMode = GetFirstOrDefault(CadOutputFwDiffAutoModeOptions);
        }

        NotchExportFileTypeOptions = owner.NotchExportFileTypeOptions.ToList();
        var selectedNotchExportFileType = NotchExportFileTypeOptions.FirstOrDefault(o =>
            o.Value == owner.SelectedNotchExportFileTypeOption.Value);
        if (string.IsNullOrWhiteSpace(selectedNotchExportFileType.Display))
        {
            selectedNotchExportFileType = GetFirstOrDefault(NotchExportFileTypeOptions);
        }

        NotchExportProfileOptions = owner.NotchExportProfileOptions.ToList();
        var selectedNotchExportProfile = NotchExportProfileOptions.FirstOrDefault(o =>
            o.Value == owner.SelectedNotchExportProfileOption.Value);
        if (string.IsNullOrWhiteSpace(selectedNotchExportProfile.Display))
        {
            selectedNotchExportProfile = GetFirstOrDefault(NotchExportProfileOptions);
        }

        NotchCompensationModelOptions = owner.NotchCompensationModelOptions.ToList();
        var selectedNotchCompensationModel = NotchCompensationModelOptions.FirstOrDefault(o =>
            o.Value == owner.SelectedNotchCompensationModelOption.Value);
        if (string.IsNullOrWhiteSpace(selectedNotchCompensationModel.Display))
        {
            selectedNotchCompensationModel = GetFirstOrDefault(NotchCompensationModelOptions);
        }

        _selectedScanOrderOption = selectedScanOrder;
        _selectedGridAlignmentOption = selectedAlignment;
        _selectedRegularSourceModeOption = selectedRegularSourceMode;
        _selectedRegularSourceLayerOption = selectedRegularSourceLayer;
        _selectedLogLevelOption = selectedLogLevel;
        _selectedCadOutputFwDiffAutoModeOption = selectedCadOutputFwDiffAutoMode;
        _selectedNotchExportFileTypeOption = selectedNotchExportFileType;
        _selectedNotchExportProfileOption = selectedNotchExportProfile;
        _selectedNotchCompensationModelOption = selectedNotchCompensationModel;

        _xChannels = owner.XChannels;
        _yChannels = owner.YChannels;
        _cascadeNum = owner.CascadeNum;
        _coordinatePixelWidth = owner.CoordinatePixelWidth;
        _coordinatePixelHeight = owner.CoordinatePixelHeight;
        _gridPaddingPercent = owner.GridPaddingPercent;
        _activeAreaWidth = owner.ActiveAreaWidth;
        _activeAreaHeight = owner.ActiveAreaHeight;
        _panelBiasX = owner.PanelBiasX;
        _panelBiasY = owner.PanelBiasY;
        _importOnlyClosedPolylines = owner.ImportOnlyClosedPolylines;
        _importBlockPolylines = owner.ImportBlockPolylines;
        _recalcBoundsOnLayerFilter = owner.RecalcBoundsOnLayerFilter;
        _dxfRegularSourceHint = owner.DxfRegularSourceHint;
        _matchThreshold = owner.MatchThreshold;
        _freeformAxisThreshold = owner.FreeformAxisThreshold;
        _enableAutoDetectXy = owner.EnableAutoDetectXy;
        _enableFreeformEdgeSpecialization = owner.EnableFreeformEdgeSpecialization;
        _autoReplayStep2AfterProjectLoad = owner.AutoReplayStep2AfterProjectLoad;
        _applyAppVisualPreferencesOnProjectLoad = owner.ApplyAppVisualPreferencesOnProjectLoad;
        _enableToRegular = owner.EnableToRegular;
        _enableToFull = owner.EnableToFull;
        _enableToFullRuleEngine = owner.EnableToFullRuleEngine;
        _enableToFullRuleTrace = owner.EnableToFullRuleTrace;
        _enableBoundaryVirtualAreaCap = owner.EnableBoundaryVirtualAreaCap;
        _boundaryVirtualAreaCapPercent = owner.BoundaryVirtualAreaCapPercent;
        _enableTargetCoverageGuard = owner.EnableTargetCoverageGuard;
        _targetCoverageCapPercent = owner.TargetCoverageCapPercent;
        _showNotchCanvasPreview = owner.ShowNotchCanvasPreview;
        _showNotchToRegularLabels = owner.ShowNotchToRegularLabels;
        _toFullStrictOverlapPercent = owner.ToFullStrictOverlapPercent;
        _notchPreviewVisualizationStep = owner.NotchPreviewVisualizationStep;
        _notchPreviewAutoPlayEnabled = owner.NotchPreviewAutoPlayEnabled;
        _notchPreviewAutoPlayIntervalMs = owner.NotchPreviewAutoPlayIntervalMs;
        _showDiffIndexOverlay = owner.ShowDiffIndexOverlay;
        _cadOutputFwDiffIndexStart = owner.CadOutputFwDiffIndexStart;
        _cadOutputFwDiffIndexAnchorCadId = owner.CadOutputFwDiffIndexAnchorCadId;
        _enableV21 = owner.EnableV21;
        _enableV22 = owner.EnableV22;
        _lenScale = owner.LenScale;
        _nullValue = owner.NullValue;
        _mappingWeightIou = owner.MappingWeightIou;
        _mappingWeightCentroidDistance = owner.MappingWeightCentroidDistance;
        _mappingWeightAreaRatio = owner.MappingWeightAreaRatio;
        _mappingLowConfidenceThreshold = owner.MappingLowConfidenceThreshold;
        _mappingAmbiguousMargin = owner.MappingAmbiguousMargin;
        _mappingCandidateNumber = owner.MappingCandidateNumber;
        _useLocalSizing = owner.UseLocalSizing;
        _globalFontSizePercent = owner.GlobalFontSizePercent;
        _highlightStrokeWidthAdjust = owner.HighlightStrokeWidthAdjust;
        _suppressNotchThresholdSync = true;
        _notchThresholdQ7 = owner.NotchThresholdQ7;
        _notchThresholdPercent = owner.NotchThresholdPercent;
        _linkNotchThresholds = owner.LinkNotchThresholds;
        _suppressNotchThresholdSync = false;

        InitializeCascadeIcSettings(owner);
        RefreshLayerCategoryRows();
        SaveCommand = new RelayCommand(OnSave, () => _owner.IsProjectEditingEnabled);
        CancelCommand = new RelayCommand(OnCancel);
    }

    public event Action? RequestClose;

    public IReadOnlyList<FreeformHelperViewModel.ScanOrderOption> ScanOrderOptions { get; }
    public IReadOnlyList<FreeformHelperViewModel.GridAlignmentOption> GridAlignmentOptions { get; }
    public IReadOnlyList<FreeformHelperViewModel.RegularSourceModeOption> RegularSourceModeOptions { get; }
    public IReadOnlyList<FreeformHelperViewModel.RegularSourceLayerOption> RegularSourceLayerOptions { get; }
    public IReadOnlyList<FreeformHelperViewModel.LogLevelOption> LogLevelOptions { get; }
    public IReadOnlyList<FreeformHelperViewModel.CadOutputFwDiffAutoModeOption> CadOutputFwDiffAutoModeOptions { get; }
    public IReadOnlyList<FreeformHelperViewModel.NotchExportFileTypeOption> NotchExportFileTypeOptions { get; }
    public IReadOnlyList<FreeformHelperViewModel.NotchExportProfileOption> NotchExportProfileOptions { get; }
    public IReadOnlyList<FreeformHelperViewModel.NotchCompensationModelOption> NotchCompensationModelOptions { get; }

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }

    [ObservableProperty] private SettingsWindowSection _selectedSection = SettingsWindowSection.General;
    [ObservableProperty] private decimal _xChannels;
    [ObservableProperty] private decimal _yChannels;
    [ObservableProperty] private decimal _cascadeNum;
    [ObservableProperty] private decimal _coordinatePixelWidth;
    [ObservableProperty] private decimal _coordinatePixelHeight;
    [ObservableProperty] private decimal _gridPaddingPercent;
    [ObservableProperty] private decimal _activeAreaWidth;
    [ObservableProperty] private decimal _activeAreaHeight;
    [ObservableProperty] private decimal _panelBiasX;
    [ObservableProperty] private decimal _panelBiasY;
    [ObservableProperty] private bool _importOnlyClosedPolylines;
    [ObservableProperty] private bool _importBlockPolylines;
    [ObservableProperty] private bool _recalcBoundsOnLayerFilter;
    [ObservableProperty] private FreeformHelperViewModel.ScanOrderOption _selectedScanOrderOption;
    [ObservableProperty] private decimal _matchThreshold;
    [ObservableProperty] private decimal _freeformAxisThreshold;
    [ObservableProperty] private bool _enableAutoDetectXy;
    [ObservableProperty] private bool _enableFreeformEdgeSpecialization;
    [ObservableProperty] private bool _autoReplayStep2AfterProjectLoad;
    [ObservableProperty] private bool _applyAppVisualPreferencesOnProjectLoad;
    [ObservableProperty] private FreeformHelperViewModel.RegularSourceModeOption _selectedRegularSourceModeOption;
    [ObservableProperty] private FreeformHelperViewModel.RegularSourceLayerOption _selectedRegularSourceLayerOption;
    [ObservableProperty] private string _dxfRegularSourceHint = string.Empty;
    [ObservableProperty] private bool _showDiffIndexOverlay;
    [ObservableProperty] private decimal _cadOutputFwDiffIndexStart;
    [ObservableProperty] private decimal _cadOutputFwDiffIndexAnchorCadId;
    [ObservableProperty] private FreeformHelperViewModel.CadOutputFwDiffAutoModeOption _selectedCadOutputFwDiffAutoModeOption;
    [ObservableProperty] private bool _enableToRegular;
    [ObservableProperty] private bool _enableToFull;
    [ObservableProperty] private bool _enableToFullRuleEngine;
    [ObservableProperty] private bool _enableToFullRuleTrace;
    [ObservableProperty] private bool _enableBoundaryVirtualAreaCap;
    [ObservableProperty] private decimal _boundaryVirtualAreaCapPercent;
    [ObservableProperty] private bool _enableTargetCoverageGuard;
    [ObservableProperty] private decimal _targetCoverageCapPercent;
    [ObservableProperty] private FreeformHelperViewModel.NotchCompensationModelOption _selectedNotchCompensationModelOption;
    [ObservableProperty] private bool _showNotchCanvasPreview;
    [ObservableProperty] private bool _showNotchToRegularLabels;
    [ObservableProperty] private decimal _notchPreviewVisualizationStep;
    [ObservableProperty] private decimal _toFullStrictOverlapPercent;
    [ObservableProperty] private bool _notchPreviewAutoPlayEnabled;
    [ObservableProperty] private decimal _notchPreviewAutoPlayIntervalMs;
    [ObservableProperty] private FreeformHelperViewModel.GridAlignmentOption _selectedGridAlignmentOption;
    [ObservableProperty] private FreeformHelperViewModel.LogLevelOption _selectedLogLevelOption;
    [ObservableProperty] private FreeformHelperViewModel.NotchExportFileTypeOption _selectedNotchExportFileTypeOption;
    [ObservableProperty] private FreeformHelperViewModel.NotchExportProfileOption _selectedNotchExportProfileOption;
    [ObservableProperty] private decimal _mappingWeightIou;
    [ObservableProperty] private decimal _mappingWeightCentroidDistance;
    [ObservableProperty] private decimal _mappingWeightAreaRatio;
    [ObservableProperty] private decimal _mappingLowConfidenceThreshold;
    [ObservableProperty] private decimal _mappingAmbiguousMargin;
    [ObservableProperty] private decimal _mappingCandidateNumber;
    [ObservableProperty] private bool _useLocalSizing;
    [ObservableProperty] private decimal _globalFontSizePercent;
    [ObservableProperty] private decimal _highlightStrokeWidthAdjust;
    [ObservableProperty] private bool _enableV21;
    [ObservableProperty] private bool _enableV22;
    [ObservableProperty] private decimal _lenScale;
    [ObservableProperty] private decimal _nullValue;
    [ObservableProperty] private decimal _notchThresholdQ7;
    [ObservableProperty] private decimal _notchThresholdPercent;
    [ObservableProperty] private bool _linkNotchThresholds;
    [ObservableProperty] private ObservableCollection<LayerCategoryRow> _layerCategoryRows = new();

    public bool HasLayerCategoryRows => LayerCategoryRows.Count > 0;
    public bool IsLenScaleVisible => EnableV21 && _showInternalLegacyNotchFields;
    public bool IsPanelAlignment => SelectedGridAlignmentOption.Value == FreeformHelper.Application.Settings.GridAlignmentMode.FromPanelAa;
    public bool IsDxfLayerRegularSource => SelectedRegularSourceModeOption.Value == FreeformHelper.Application.Settings.RegularSourceMode.FromDxfLayer;
    public bool IsGeneralSectionSelected
    {
        get => SelectedSection == SettingsWindowSection.General;
        set
        {
            if (value)
            {
                SelectedSection = SettingsWindowSection.General;
            }
        }
    }

    public bool IsStep1SectionSelected
    {
        get => SelectedSection == SettingsWindowSection.Step1GeometryMatch;
        set
        {
            if (value)
            {
                SelectedSection = SettingsWindowSection.Step1GeometryMatch;
            }
        }
    }

    public bool IsStep2SectionSelected
    {
        get => SelectedSection == SettingsWindowSection.Step2FreeformTagging;
        set
        {
            if (value)
            {
                SelectedSection = SettingsWindowSection.Step2FreeformTagging;
            }
        }
    }

    public bool IsStep3SectionSelected
    {
        get => SelectedSection == SettingsWindowSection.Step3NotchCompensation;
        set
        {
            if (value)
            {
                SelectedSection = SettingsWindowSection.Step3NotchCompensation;
            }
        }
    }

    public bool IsStep4SectionSelected
    {
        get => SelectedSection == SettingsWindowSection.Step4IndexDiagnostics;
        set
        {
            if (value)
            {
                SelectedSection = SettingsWindowSection.Step4IndexDiagnostics;
            }
        }
    }

    public bool IsStep5SectionSelected
    {
        get => SelectedSection == SettingsWindowSection.Step5NotchExport;
        set
        {
            if (value)
            {
                SelectedSection = SettingsWindowSection.Step5NotchExport;
            }
        }
    }

    public string ToFullRuleEngineSummary => EnableToFullRuleEngine
        ? (EnableToFullRuleTrace
            ? "To Full rule engine: ON (trace ON)"
            : "To Full rule engine: ON (trace OFF)")
        : "To Full rule engine: OFF (legacy gate path)";

    public string SettingsSelectedSectionTitle => SelectedSection switch
    {
        SettingsWindowSection.General => "General",
        SettingsWindowSection.Step1GeometryMatch => "Step 1 - Geometry match",
        SettingsWindowSection.Step2FreeformTagging => "Step 2 - Freeform tagging",
        SettingsWindowSection.Step3NotchCompensation => "Step 3 - Notch compensation",
        SettingsWindowSection.Step4IndexDiagnostics => "Step 4 - Index diagnostics",
        SettingsWindowSection.Step5NotchExport => "Step 5 - Notch export",
        _ => "Settings",
    };

    public string SettingsSelectedSectionDescription => SelectedSection switch
    {
        SettingsWindowSection.General => "Project layout, DXF import, display, and generated regular grid settings.",
        SettingsWindowSection.Step1GeometryMatch => "Overlap diagnostics and unmatched highlighting.",
        SettingsWindowSection.Step2FreeformTagging => "Freeform classification and project-load replay behavior.",
        SettingsWindowSection.Step3NotchCompensation => "Compensation model, allocation semantics, and preview diagnostics.",
        SettingsWindowSection.Step4IndexDiagnostics => "FW output diff visibility and mapping confidence thresholds.",
        SettingsWindowSection.Step5NotchExport => "Export versions, file type, gate thresholds, and handoff safety.",
        _ => "Edit project settings.",
    };

    public string NotchCompensationModelSummary => string.IsNullOrWhiteSpace(SelectedNotchCompensationModelOption.Description)
        ? string.Empty
        : SelectedNotchCompensationModelOption.Description;

    public string NotchEffectiveModelSummary =>
        $"{SelectedNotchCompensationModelOption.Display} · ToRegular {(EnableToRegular ? "ON" : "OFF")} · ToFull {(EnableToFull ? "ON" : "OFF")}";

    public string NotchAllocationModelSummary =>
        SelectedNotchCompensationModelOption.Value switch
        {
            NotchCompensationModel.CurrentGain => "Allocation model: Stage3 effective-area gain; ToFull-expanded area splits the combined source signal.",
            NotchCompensationModel.ConservativeNoGain => "Allocation model: SourceArea-dominant; ToFull is support/cap, not direct full-area weight.",
            NotchCompensationModel.Disabled => "Allocation model: disabled baseline.",
            _ => string.Empty,
        };

    public string NotchAllocationModelShortText =>
        SelectedNotchCompensationModelOption.Value switch
        {
            NotchCompensationModel.CurrentGain => "Stage3 gain allocation",
            NotchCompensationModel.ConservativeNoGain => "Source-area dominant",
            NotchCompensationModel.Disabled => "Disabled baseline",
            _ => string.Empty,
        };

    public string NotchToRegularSemanticSummary => EnableToRegular
        ? "ToRegular: undo NF area flattening and restore area-proportional signal before redistribution."
        : "ToRegular: disabled for baseline comparison.";

    public string NotchToRegularShortText => EnableToRegular
        ? "ON - restore area signal"
        : "OFF - baseline";

    public string NotchToFullSemanticSummary => EnableToFull
        ? "ToFull: boundary support/cap/allowance only; target amount remains area-proportional."
        : "ToFull: disabled for baseline comparison.";

    public string NotchToFullShortText => EnableToFull
        ? "ON - support/cap only"
        : "OFF - no boundary support";

    public string NotchBoundaryVirtualAreaCapSummary => EnableBoundaryVirtualAreaCap
        ? $"Boundary cap: virtual ToFull area <= inside overlap x {BoundaryVirtualAreaCapPercent:0.#}%."
        : "Boundary cap: OFF; Stage3 uses raw ToFull reachable area.";

    public string NotchBoundaryVirtualAreaCapShortText => EnableBoundaryVirtualAreaCap
        ? $"Boundary cap {BoundaryVirtualAreaCapPercent:0.#}%"
        : "Boundary cap OFF";

    public string NotchTargetCoverageGuardSummary =>
        SimulationSafetyTextProjector.BuildNotchTargetCoverageGuardSummary(
            EnableTargetCoverageGuard,
            TargetCoverageCapPercent);

    public string NotchTargetCoverageGuardShortText => EnableTargetCoverageGuard
        ? $"Target guard {TargetCoverageCapPercent:0.#}%"
        : "Target guard OFF";

    public string NotchTargetCoverageCapHelpText =>
        SimulationSafetyTextProjector.BuildNotchTargetCoverageCapHelpText(
            TargetCoverageCapPercent,
            SimulationSafetyTextProjector.DefaultEmsAfterCapText);

    public string NotchEmsSafetyPolicySummary =>
        !string.IsNullOrWhiteSpace(SelectedNotchCompensationModelOption.Display)
            ? SimulationSafetyTextProjector.BuildNotchEmsSafetyPolicySummary(
                SimulationSafetyTextProjector.DefaultEmsAfterCapText)
            : string.Empty;

    public string NotchEmsSafetyShortText =>
        !string.IsNullOrWhiteSpace(SelectedNotchCompensationModelOption.Display)
            ? SimulationSafetyTextProjector.BuildNotchEmsSafetyShortText(
                SimulationSafetyTextProjector.DefaultEmsAfterCapText)
            : string.Empty;

    public string NotchExportSafetyPolicySummary =>
        !string.IsNullOrWhiteSpace(SelectedNotchExportFileTypeOption.Display)
            ? SimulationSafetyTextProjector.BuildNotchExportSafetyPolicySummary(
                SimulationSafetyTextProjector.DefaultEmsAfterCapText)
            : string.Empty;

    partial void OnEnableV21Changed(bool value)
    {
        OnPropertyChanged(nameof(IsLenScaleVisible));
    }

    partial void OnSelectedGridAlignmentOptionChanged(FreeformHelperViewModel.GridAlignmentOption value)
    {
        OnPropertyChanged(nameof(IsPanelAlignment));
    }

    partial void OnSelectedRegularSourceModeOptionChanged(FreeformHelperViewModel.RegularSourceModeOption value)
    {
        OnPropertyChanged(nameof(IsDxfLayerRegularSource));
    }

    partial void OnSelectedSectionChanged(SettingsWindowSection value)
    {
        OnPropertyChanged(nameof(IsGeneralSectionSelected));
        OnPropertyChanged(nameof(IsStep1SectionSelected));
        OnPropertyChanged(nameof(IsStep2SectionSelected));
        OnPropertyChanged(nameof(IsStep3SectionSelected));
        OnPropertyChanged(nameof(IsStep4SectionSelected));
        OnPropertyChanged(nameof(IsStep5SectionSelected));
        OnPropertyChanged(nameof(SettingsSelectedSectionTitle));
        OnPropertyChanged(nameof(SettingsSelectedSectionDescription));
    }

    partial void OnEnableToFullRuleEngineChanged(bool value)
    {
        OnPropertyChanged(nameof(ToFullRuleEngineSummary));
    }

    partial void OnEnableToFullRuleTraceChanged(bool value)
    {
        OnPropertyChanged(nameof(ToFullRuleEngineSummary));
    }

    partial void OnEnableBoundaryVirtualAreaCapChanged(bool value)
    {
        NotifyNotchModelSummaryChanged();
    }

    partial void OnBoundaryVirtualAreaCapPercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 1000m);
        if (clamped != value)
        {
            BoundaryVirtualAreaCapPercent = clamped;
            return;
        }

        NotifyNotchModelSummaryChanged();
    }

    partial void OnEnableTargetCoverageGuardChanged(bool value)
    {
        NotifyNotchModelSummaryChanged();
    }

    partial void OnTargetCoverageCapPercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 255m);
        if (clamped != value)
        {
            TargetCoverageCapPercent = clamped;
            return;
        }

        NotifyNotchModelSummaryChanged();
    }

    partial void OnEnableToRegularChanged(bool value)
    {
        NotifyNotchModelSummaryChanged();
    }

    partial void OnEnableToFullChanged(bool value)
    {
        NotifyNotchModelSummaryChanged();
    }

    partial void OnSelectedNotchCompensationModelOptionChanged(FreeformHelperViewModel.NotchCompensationModelOption value)
    {
        if (_suppressCompensationModelSync)
        {
            return;
        }

        _suppressCompensationModelSync = true;
        try
        {
            var (enableToRegular, enableToFull) = value.Value == NotchCompensationModel.Disabled
                ? (false, false)
                : (true, true);
            EnableToRegular = enableToRegular;
            EnableToFull = enableToFull;
        }
        finally
        {
            _suppressCompensationModelSync = false;
        }

        OnPropertyChanged(nameof(NotchCompensationModelSummary));
        NotifyNotchModelSummaryChanged();
    }

    partial void OnSelectedNotchExportFileTypeOptionChanged(FreeformHelperViewModel.NotchExportFileTypeOption value)
    {
        OnPropertyChanged(nameof(NotchExportSafetyPolicySummary));
    }

    private void NotifyNotchModelSummaryChanged()
    {
        OnPropertyChanged(nameof(NotchEffectiveModelSummary));
        OnPropertyChanged(nameof(NotchAllocationModelSummary));
        OnPropertyChanged(nameof(NotchAllocationModelShortText));
        OnPropertyChanged(nameof(NotchToRegularSemanticSummary));
        OnPropertyChanged(nameof(NotchToRegularShortText));
        OnPropertyChanged(nameof(NotchToFullSemanticSummary));
        OnPropertyChanged(nameof(NotchToFullShortText));
        OnPropertyChanged(nameof(NotchBoundaryVirtualAreaCapSummary));
        OnPropertyChanged(nameof(NotchBoundaryVirtualAreaCapShortText));
        OnPropertyChanged(nameof(NotchTargetCoverageGuardSummary));
        OnPropertyChanged(nameof(NotchTargetCoverageGuardShortText));
        OnPropertyChanged(nameof(NotchTargetCoverageCapHelpText));
        OnPropertyChanged(nameof(NotchEmsSafetyPolicySummary));
        OnPropertyChanged(nameof(NotchEmsSafetyShortText));
    }

    partial void OnCoordinatePixelWidthChanged(decimal value)
    {
        var clamped = Math.Max(1m, Math.Round(value, MidpointRounding.AwayFromZero));
        if (clamped != value)
        {
            CoordinatePixelWidth = clamped;
        }
    }

    partial void OnCoordinatePixelHeightChanged(decimal value)
    {
        var clamped = Math.Max(1m, Math.Round(value, MidpointRounding.AwayFromZero));
        if (clamped != value)
        {
            CoordinatePixelHeight = clamped;
        }
    }

    partial void OnHighlightStrokeWidthAdjustChanged(decimal value)
    {
        var clamped = Math.Clamp(value, -2m, 4m);
        if (clamped != value)
        {
            HighlightStrokeWidthAdjust = clamped;
        }
    }

    partial void OnNotchThresholdQ7Changed(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 128m);
        if (clamped != value)
        {
            NotchThresholdQ7 = clamped;
            return;
        }

        if (_suppressNotchThresholdSync || !LinkNotchThresholds)
        {
            return;
        }

        _suppressNotchThresholdSync = true;
        NotchThresholdPercent = Math.Round(clamped * 100m / 128m, 2);
        _suppressNotchThresholdSync = false;
    }

    partial void OnNotchThresholdPercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 100m);
        if (clamped != value)
        {
            NotchThresholdPercent = clamped;
            return;
        }

        if (_suppressNotchThresholdSync || !LinkNotchThresholds)
        {
            return;
        }

        _suppressNotchThresholdSync = true;
        var q7 = Math.Clamp((int)Math.Round(clamped * 128m / 100m, MidpointRounding.AwayFromZero), 0, 128);
        NotchThresholdQ7 = q7;
        _suppressNotchThresholdSync = false;
    }

    partial void OnLinkNotchThresholdsChanged(bool value)
    {
        if (!value || _suppressNotchThresholdSync)
        {
            return;
        }

        _suppressNotchThresholdSync = true;
        NotchThresholdPercent = Math.Round(NotchThresholdQ7 * 100m / 128m, 2);
        _suppressNotchThresholdSync = false;
    }

    private void RefreshLayerCategoryRows()
    {
        LayerCategoryRows = new ObservableCollection<LayerCategoryRow>(
            _owner.GetDxfLayerCategoryStates()
                .Select(state => new LayerCategoryRow(state, ApplyLayerCategorySelection)));
        OnPropertyChanged(nameof(HasLayerCategoryRows));
    }

    private void ApplyLayerCategorySelection(FreeformHelperViewModel.DxfLayerCategory category, bool isSelected)
    {
        _owner.ApplyLayerCategorySelection(category, isSelected);
        RefreshLayerCategoryRows();
    }

    private void OnSave()
    {
        _owner.ApplySettingsWindowDraft(this);
        RequestClose?.Invoke();
    }

    private void OnCancel()
    {
        RequestClose?.Invoke();
    }

    private static T GetFirstOrDefault<T>(IReadOnlyList<T> list)
    {
        return list.Count > 0 ? list[0] : default!;
    }

    public void ResetAllSettingsToDefaults()
    {
        _owner.ResetAllSettingsToDefaults();
        RequestClose?.Invoke();
    }

    public sealed partial class LayerCategoryRow : ObservableObject
    {
        private readonly Action<FreeformHelperViewModel.DxfLayerCategory, bool> _applySelection;

        public LayerCategoryRow(
            FreeformHelperViewModel.DxfLayerCategoryState state,
            Action<FreeformHelperViewModel.DxfLayerCategory, bool> applySelection)
        {
            Category = state.Category;
            _display = state.Display;
            _description = state.Description;
            _totalCount = state.TotalCount;
            _selectedCount = state.SelectedCount;
            _applySelection = applySelection;
            EnableCommand = new RelayCommand(() => _applySelection(Category, true));
            DisableCommand = new RelayCommand(() => _applySelection(Category, false));
        }

        public FreeformHelperViewModel.DxfLayerCategory Category { get; }
        public IRelayCommand EnableCommand { get; }
        public IRelayCommand DisableCommand { get; }

        [ObservableProperty] private string _display;
        [ObservableProperty] private string _description;
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _selectedCount;

        public string Summary => $"{SelectedCount}/{TotalCount} selected";
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels.Settings;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SettingsWindowViewModel : ObservableObject
{
    private readonly FreeformHelperViewModel _owner;
    internal UiEventRunner UiEvents => _owner.UiEvents;
    private readonly HashSet<string> _editedProperties = new(StringComparer.Ordinal);
    private readonly SettingsNotchDraftOwner _notch;
    private readonly SettingsMappingExportDraftOwner _mappingExport;
    private readonly SettingsWindowOptionSet _options;
    private readonly SettingsGeneralDraftOwner _general;
    private readonly SettingsGridDraftOwner _grid;

    public SettingsWindowViewModel(FreeformHelperViewModel owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _options = new SettingsWindowOptionSet(owner);

        _general = CreateGeneralDraftOwner();
        _notch = CreateNotchDraftOwner();
        _mappingExport = CreateMappingExportDraftOwner();
        _grid = CreateGridDraftOwner(new ActiveAreaPixelsDraft());
        _general.RefreshLayerCategoryRows();
        SaveCommand = new RelayCommand(OnSave, () => _owner.IsProjectEditingEnabled);
        CancelCommand = new RelayCommand(OnCancel);
    }

    private SettingsGeneralDraftOwner CreateGeneralDraftOwner()
    {
        var seed = new SettingsGeneralDraftOwner.Seed(
            ImportOnlyClosedPolylines: _owner.ImportOnlyClosedPolylines,
            ImportBlockPolylines: _owner.ImportBlockPolylines,
            RecalcBoundsOnLayerFilter: _owner.RecalcBoundsOnLayerFilter,
            MatchThreshold: _owner.MatchThreshold,
            FreeformAxisThreshold: _owner.FreeformAxisThreshold,
            EnableAutoDetectXy: _owner.EnableAutoDetectXy,
            EnableFreeformEdgeSpecialization: _owner.EnableFreeformEdgeSpecialization,
            AutoReplayStep2AfterProjectLoad: _owner.AutoReplayStep2AfterProjectLoad,
            ApplyAppVisualPreferencesOnProjectLoad: _owner.ApplyAppVisualPreferencesOnProjectLoad,
            SelectedLogLevelOption: _options.SelectedLogLevel,
            UseLocalSizing: _owner.UseLocalSizing,
            GlobalFontSizePercent: _owner.GlobalFontSizePercent,
            HighlightStrokeWidthAdjust: _owner.HighlightStrokeWidthAdjust,
            LayerCategoryRows: new());
        return new SettingsGeneralDraftOwner(
            seed, () => ActiveAreaPixels.ProjectIdentity,
            _owner.GetDxfLayerCategoryStates, _owner.ApplyLayerCategorySelection,
            _options.LogLevel,
            OnPropertyChanging, OnPropertyChanged);
    }

    private SettingsGridDraftOwner CreateGridDraftOwner(ActiveAreaPixelsDraft activeAreaPixels)
    {
        var seed = new SettingsGridDraftOwner.Seed(
            XChannels: _owner.XChannels,
            YChannels: _owner.YChannels,
            CascadeNum: _owner.CascadeNum,
            CoordinatePixelWidth: _owner.CoordinatePixelWidth,
            CoordinatePixelHeight: _owner.CoordinatePixelHeight,
            GridPaddingPercent: _owner.GridPaddingPercent,
            ActiveAreaWidth: _owner.ActiveAreaWidth,
            ActiveAreaHeight: _owner.ActiveAreaHeight,
            PanelBiasX: _owner.PanelBiasX,
            PanelBiasY: _owner.PanelBiasY,
            SelectedScanOrderOption: _options.SelectedScanOrder,
            SelectedRegularSourceModeOption: _options.SelectedRegularSourceMode,
            SelectedRegularSourceLayerOption: _options.SelectedRegularSourceLayer,
            DxfRegularSourceHint: _owner.DxfRegularSourceHint,
            SelectedGridAlignmentOption: _options.SelectedGridAlignment,
            CascadeIcSettings: new());
        return new SettingsGridDraftOwner(
            seed, () => ActiveAreaPixels.ProjectIdentity,
            activeAreaPixels,
            _owner.CascadeIcSettings.Select(static row => Math.Max(1, (int)Math.Round(row.XChannels))),
            _owner.CascadeIcSettings.Select(static row => Math.Max(1, (int)Math.Round(row.YChannels))),
            BuildCascadeDraftLayout, propertyName => _editedProperties.Add(propertyName),
            _options.ScanOrder, _options.GridAlignment, _options.RegularSourceMode, _options.RegularSourceLayer,
            OnPropertyChanging, OnPropertyChanged);
    }

    private SettingsNotchDraftOwner CreateNotchDraftOwner()
    {
        var seed = new SettingsNotchDraftOwner.Seed(
            EnableToRegular: _owner.EnableToRegular,
            EnableToFull: _owner.EnableToFull,
            EnableToFullRuleEngine: _owner.EnableToFullRuleEngine,
            EnableToFullRuleTrace: _owner.EnableToFullRuleTrace,
            EnableBoundaryVirtualAreaCap: _owner.EnableBoundaryVirtualAreaCap,
            BoundaryVirtualAreaCapPercent: _owner.BoundaryVirtualAreaCapPercent,
            EnableTargetCoverageGuard: _owner.EnableTargetCoverageGuard,
            TargetCoverageCapPercent: _owner.TargetCoverageCapPercent,
            SelectedNotchCompensationModelOption: _options.SelectedNotchCompensationModel,
            ShowNotchCanvasPreview: _owner.ShowNotchCanvasPreview,
            ShowNotchToRegularLabels: _owner.ShowNotchToRegularLabels,
            NotchPreviewVisualizationStep: _owner.NotchPreviewVisualizationStep,
            ToFullStrictOverlapPercent: _owner.ToFullStrictOverlapPercent,
            NotchPreviewAutoPlayEnabled: _owner.NotchPreviewAutoPlayEnabled,
            NotchPreviewAutoPlayIntervalMs: _owner.NotchPreviewAutoPlayIntervalMs,
            NotchThresholdQ7: _owner.NotchThresholdQ7,
            NotchThresholdPercent: _owner.NotchThresholdPercent,
            LinkNotchThresholds: _owner.LinkNotchThresholds);
        return new SettingsNotchDraftOwner(
            seed, () => ActiveAreaPixels.ProjectIdentity,
            _options.NotchCompensationModel,
            SimulationSafetyTextProjector.BuildNotchTargetCoverageGuardSummary,
            percent => SimulationSafetyTextProjector.BuildNotchTargetCoverageCapHelpText(
                percent, SimulationSafetyTextProjector.DefaultEmsAfterCapText),
            () => SimulationSafetyTextProjector.BuildNotchEmsSafetyPolicySummary(
                SimulationSafetyTextProjector.DefaultEmsAfterCapText),
            () => SimulationSafetyTextProjector.BuildNotchEmsSafetyShortText(
                SimulationSafetyTextProjector.DefaultEmsAfterCapText),
            OnPropertyChanging, OnPropertyChanged);
    }

    private SettingsMappingExportDraftOwner CreateMappingExportDraftOwner()
    {
        var seed = new SettingsMappingExportDraftOwner.Seed(
            ShowDiffIndexOverlay: _owner.ShowDiffIndexOverlay,
            CadOutputFwDiffIndexStart: _owner.CadOutputFwDiffIndexStart,
            CadOutputFwDiffIndexAnchorCadId: _owner.CadOutputFwDiffIndexAnchorCadId,
            SelectedCadOutputFwDiffAutoModeOption: _options.SelectedCadOutputFwDiffAutoMode,
            SelectedNotchExportFileTypeOption: _options.SelectedNotchExportFileType,
            SelectedNotchExportProfileOption: _options.SelectedNotchExportProfile,
            MappingWeightIou: _owner.MappingWeightIou,
            MappingWeightCentroidDistance: _owner.MappingWeightCentroidDistance,
            MappingWeightAreaRatio: _owner.MappingWeightAreaRatio,
            MappingLowConfidenceThreshold: _owner.MappingLowConfidenceThreshold,
            MappingAmbiguousMargin: _owner.MappingAmbiguousMargin,
            MappingCandidateNumber: _owner.MappingCandidateNumber,
            EnableV21: _owner.EnableV21,
            EnableV22: _owner.EnableV22,
            LenScale: _owner.LenScale,
            NullValue: _owner.NullValue);
        return new SettingsMappingExportDraftOwner(
            seed, () => ActiveAreaPixels.ProjectIdentity,
            _options.CadOutputFwDiffAutoMode, _options.NotchExportFileType, _options.NotchExportProfile,
            () => SimulationSafetyTextProjector.BuildNotchExportSafetyPolicySummary(
                SimulationSafetyTextProjector.DefaultEmsAfterCapText),
            OnPropertyChanging, OnPropertyChanged);
    }

    public event Action? RequestClose;

    public IReadOnlyList<ScanOrderOption> ScanOrderOptions => _grid.ScanOrderOptions;
    public IReadOnlyList<GridAlignmentOption> GridAlignmentOptions => _grid.GridAlignmentOptions;
    public IReadOnlyList<RegularSourceModeOption> RegularSourceModeOptions => _grid.RegularSourceModeOptions;
    public IReadOnlyList<RegularSourceLayerOption> RegularSourceLayerOptions => _grid.RegularSourceLayerOptions;
    public IReadOnlyList<LogLevelOption> LogLevelOptions => _general.LogLevelOptions;
    public IReadOnlyList<CadOutputFwDiffAutoModeOption> CadOutputFwDiffAutoModeOptions => _mappingExport.CadOutputFwDiffAutoModeOptions;
    public IReadOnlyList<NotchExportFileTypeOption> NotchExportFileTypeOptions => _mappingExport.NotchExportFileTypeOptions;
    public IReadOnlyList<NotchExportProfileOption> NotchExportProfileOptions => _mappingExport.NotchExportProfileOptions;
    public IReadOnlyList<NotchCompensationModelOption> NotchCompensationModelOptions => _notch.NotchCompensationModelOptions;

    public ActiveAreaPixelsDraft ActiveAreaPixels => _grid.ActiveAreaPixels;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }

    [ObservableProperty] private SettingsWindowSection _selectedSection = SettingsWindowSection.General;
    public decimal XChannels { get => _grid.XChannels; set => _grid.XChannels = value; }
    public decimal YChannels { get => _grid.YChannels; set => _grid.YChannels = value; }
    public decimal CascadeNum { get => _grid.CascadeNum; set => _grid.CascadeNum = value; }
    public decimal CoordinatePixelWidth { get => _grid.CoordinatePixelWidth; set => _grid.CoordinatePixelWidth = value; }
    public decimal CoordinatePixelHeight { get => _grid.CoordinatePixelHeight; set => _grid.CoordinatePixelHeight = value; }
    public decimal GridPaddingPercent { get => _grid.GridPaddingPercent; set => _grid.GridPaddingPercent = value; }
    public decimal ActiveAreaWidth { get => _grid.ActiveAreaWidth; set => _grid.ActiveAreaWidth = value; }
    public decimal ActiveAreaHeight { get => _grid.ActiveAreaHeight; set => _grid.ActiveAreaHeight = value; }
    public decimal PanelBiasX { get => _grid.PanelBiasX; set => _grid.PanelBiasX = value; }
    public decimal PanelBiasY { get => _grid.PanelBiasY; set => _grid.PanelBiasY = value; }
    public bool ImportOnlyClosedPolylines { get => _general.ImportOnlyClosedPolylines; set => _general.ImportOnlyClosedPolylines = value; }
    public bool ImportBlockPolylines { get => _general.ImportBlockPolylines; set => _general.ImportBlockPolylines = value; }
    public bool RecalcBoundsOnLayerFilter { get => _general.RecalcBoundsOnLayerFilter; set => _general.RecalcBoundsOnLayerFilter = value; }
    public ScanOrderOption SelectedScanOrderOption { get => _grid.SelectedScanOrderOption; set => _grid.SelectedScanOrderOption = value; }
    public decimal MatchThreshold { get => _general.MatchThreshold; set => _general.MatchThreshold = value; }
    public decimal FreeformAxisThreshold { get => _general.FreeformAxisThreshold; set => _general.FreeformAxisThreshold = value; }
    public bool EnableAutoDetectXy { get => _general.EnableAutoDetectXy; set => _general.EnableAutoDetectXy = value; }
    public bool EnableFreeformEdgeSpecialization { get => _general.EnableFreeformEdgeSpecialization; set => _general.EnableFreeformEdgeSpecialization = value; }
    public bool AutoReplayStep2AfterProjectLoad { get => _general.AutoReplayStep2AfterProjectLoad; set => _general.AutoReplayStep2AfterProjectLoad = value; }
    public bool ApplyAppVisualPreferencesOnProjectLoad { get => _general.ApplyAppVisualPreferencesOnProjectLoad; set => _general.ApplyAppVisualPreferencesOnProjectLoad = value; }
    public RegularSourceModeOption SelectedRegularSourceModeOption { get => _grid.SelectedRegularSourceModeOption; set => _grid.SelectedRegularSourceModeOption = value; }
    public RegularSourceLayerOption SelectedRegularSourceLayerOption { get => _grid.SelectedRegularSourceLayerOption; set => _grid.SelectedRegularSourceLayerOption = value; }
    public string DxfRegularSourceHint { get => _grid.DxfRegularSourceHint; set => _grid.DxfRegularSourceHint = value; }
    public bool ShowDiffIndexOverlay { get => _mappingExport.ShowDiffIndexOverlay; set => _mappingExport.ShowDiffIndexOverlay = value; }
    public decimal CadOutputFwDiffIndexStart { get => _mappingExport.CadOutputFwDiffIndexStart; set => _mappingExport.CadOutputFwDiffIndexStart = value; }
    public decimal CadOutputFwDiffIndexAnchorCadId { get => _mappingExport.CadOutputFwDiffIndexAnchorCadId; set => _mappingExport.CadOutputFwDiffIndexAnchorCadId = value; }
    public CadOutputFwDiffAutoModeOption SelectedCadOutputFwDiffAutoModeOption { get => _mappingExport.SelectedCadOutputFwDiffAutoModeOption; set => _mappingExport.SelectedCadOutputFwDiffAutoModeOption = value; }
    public bool EnableToRegular { get => _notch.EnableToRegular; set => _notch.EnableToRegular = value; }
    public bool EnableToFull { get => _notch.EnableToFull; set => _notch.EnableToFull = value; }
    public bool EnableToFullRuleEngine { get => _notch.EnableToFullRuleEngine; set => _notch.EnableToFullRuleEngine = value; }
    public bool EnableToFullRuleTrace { get => _notch.EnableToFullRuleTrace; set => _notch.EnableToFullRuleTrace = value; }
    public bool EnableBoundaryVirtualAreaCap { get => _notch.EnableBoundaryVirtualAreaCap; set => _notch.EnableBoundaryVirtualAreaCap = value; }
    public decimal BoundaryVirtualAreaCapPercent { get => _notch.BoundaryVirtualAreaCapPercent; set => _notch.BoundaryVirtualAreaCapPercent = value; }
    public bool EnableTargetCoverageGuard { get => _notch.EnableTargetCoverageGuard; set => _notch.EnableTargetCoverageGuard = value; }
    public decimal TargetCoverageCapPercent { get => _notch.TargetCoverageCapPercent; set => _notch.TargetCoverageCapPercent = value; }
    public NotchCompensationModelOption SelectedNotchCompensationModelOption { get => _notch.SelectedNotchCompensationModelOption; set => _notch.SelectedNotchCompensationModelOption = value; }
    public bool ShowNotchCanvasPreview { get => _notch.ShowNotchCanvasPreview; set => _notch.ShowNotchCanvasPreview = value; }
    public bool ShowNotchToRegularLabels { get => _notch.ShowNotchToRegularLabels; set => _notch.ShowNotchToRegularLabels = value; }
    public decimal NotchPreviewVisualizationStep { get => _notch.NotchPreviewVisualizationStep; set => _notch.NotchPreviewVisualizationStep = value; }
    public decimal ToFullStrictOverlapPercent { get => _notch.ToFullStrictOverlapPercent; set => _notch.ToFullStrictOverlapPercent = value; }
    public bool NotchPreviewAutoPlayEnabled { get => _notch.NotchPreviewAutoPlayEnabled; set => _notch.NotchPreviewAutoPlayEnabled = value; }
    public decimal NotchPreviewAutoPlayIntervalMs { get => _notch.NotchPreviewAutoPlayIntervalMs; set => _notch.NotchPreviewAutoPlayIntervalMs = value; }
    public GridAlignmentOption SelectedGridAlignmentOption { get => _grid.SelectedGridAlignmentOption; set => _grid.SelectedGridAlignmentOption = value; }
    public LogLevelOption SelectedLogLevelOption { get => _general.SelectedLogLevelOption; set => _general.SelectedLogLevelOption = value; }
    public NotchExportFileTypeOption SelectedNotchExportFileTypeOption { get => _mappingExport.SelectedNotchExportFileTypeOption; set => _mappingExport.SelectedNotchExportFileTypeOption = value; }
    public NotchExportProfileOption SelectedNotchExportProfileOption { get => _mappingExport.SelectedNotchExportProfileOption; set => _mappingExport.SelectedNotchExportProfileOption = value; }
    public decimal MappingWeightIou { get => _mappingExport.MappingWeightIou; set => _mappingExport.MappingWeightIou = value; }
    public decimal MappingWeightCentroidDistance { get => _mappingExport.MappingWeightCentroidDistance; set => _mappingExport.MappingWeightCentroidDistance = value; }
    public decimal MappingWeightAreaRatio { get => _mappingExport.MappingWeightAreaRatio; set => _mappingExport.MappingWeightAreaRatio = value; }
    public decimal MappingLowConfidenceThreshold { get => _mappingExport.MappingLowConfidenceThreshold; set => _mappingExport.MappingLowConfidenceThreshold = value; }
    public decimal MappingAmbiguousMargin { get => _mappingExport.MappingAmbiguousMargin; set => _mappingExport.MappingAmbiguousMargin = value; }
    public decimal MappingCandidateNumber { get => _mappingExport.MappingCandidateNumber; set => _mappingExport.MappingCandidateNumber = value; }
    public bool UseLocalSizing { get => _general.UseLocalSizing; set => _general.UseLocalSizing = value; }
    public decimal GlobalFontSizePercent { get => _general.GlobalFontSizePercent; set => _general.GlobalFontSizePercent = value; }
    public decimal HighlightStrokeWidthAdjust { get => _general.HighlightStrokeWidthAdjust; set => _general.HighlightStrokeWidthAdjust = value; }
    public bool EnableV21 { get => _mappingExport.EnableV21; set => _mappingExport.EnableV21 = value; }
    public bool EnableV22 { get => _mappingExport.EnableV22; set => _mappingExport.EnableV22 = value; }
    public decimal LenScale { get => _mappingExport.LenScale; set => _mappingExport.LenScale = value; }
    public decimal NullValue { get => _mappingExport.NullValue; set => _mappingExport.NullValue = value; }
    public decimal NotchThresholdQ7 { get => _notch.NotchThresholdQ7; set => _notch.NotchThresholdQ7 = value; }
    public decimal NotchThresholdPercent { get => _notch.NotchThresholdPercent; set => _notch.NotchThresholdPercent = value; }
    public bool LinkNotchThresholds { get => _notch.LinkNotchThresholds; set => _notch.LinkNotchThresholds = value; }
    public ObservableCollection<LayerCategoryRow> LayerCategoryRows { get => _general.LayerCategoryRows; set => _general.LayerCategoryRows = value; }

    public bool HasLayerCategoryRows => _general.HasLayerCategoryRows;
    public bool IsPanelAlignment => _grid.IsPanelAlignment;
    public bool IsDxfLayerRegularSource => _grid.IsDxfLayerRegularSource;
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

    public string ToFullRuleEngineSummary => _notch.ToFullRuleEngineSummary;

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

    public string NotchCompensationModelSummary => _notch.NotchCompensationModelSummary;

    public string NotchEffectiveModelSummary => _notch.NotchEffectiveModelSummary;

    public string NotchAllocationModelSummary => _notch.NotchAllocationModelSummary;

    public string NotchAllocationModelShortText => _notch.NotchAllocationModelShortText;

    public string NotchToRegularSemanticSummary => _notch.NotchToRegularSemanticSummary;

    public string NotchToRegularShortText => _notch.NotchToRegularShortText;

    public string NotchToFullSemanticSummary => _notch.NotchToFullSemanticSummary;

    public string NotchToFullShortText => _notch.NotchToFullShortText;

    public string NotchBoundaryVirtualAreaCapSummary => _notch.NotchBoundaryVirtualAreaCapSummary;

    public string NotchBoundaryVirtualAreaCapShortText => _notch.NotchBoundaryVirtualAreaCapShortText;

    public string NotchTargetCoverageGuardSummary => _notch.NotchTargetCoverageGuardSummary;

    public string NotchTargetCoverageGuardShortText => _notch.NotchTargetCoverageGuardShortText;

    public string NotchTargetCoverageCapHelpText => _notch.NotchTargetCoverageCapHelpText;

    public string NotchEmsSafetyPolicySummary => _notch.NotchEmsSafetyPolicySummary;

    public string NotchEmsSafetyShortText => _notch.NotchEmsSafetyShortText;

    public string NotchExportSafetyPolicySummary => _mappingExport.NotchExportSafetyPolicySummary;

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

    /// <summary>
    /// Records every property this window changed, so Save can write back only those and leave
    /// the values that the workflow panels changed while the window stayed open.
    /// </summary>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is { } propertyName)
        {
            _editedProperties.Add(propertyName);
        }
    }

    /// <summary>
    /// Forgets the changes made while the draft was built. The window calls this once the draft is ready,
    /// so only the edits of the user remain.
    /// </summary>
    internal void MarkOpened() => _editedProperties.Clear();

    /// <summary>
    /// Tells whether the user changed a setting in this window since it opened.
    /// </summary>
    /// <param name="ownerProperty">The name of the setting on <see cref="FreeformHelperViewModel"/>.</param>
    internal bool IsEdited(string ownerProperty) => ownerProperty switch
    {
        nameof(FreeformHelperViewModel.SelectedScanOrder) => _editedProperties.Contains(nameof(SelectedScanOrderOption)),
        nameof(FreeformHelperViewModel.GridAlignmentMode) => _editedProperties.Contains(nameof(SelectedGridAlignmentOption)),
        nameof(FreeformHelperViewModel.RegularSourceMode) => _editedProperties.Contains(nameof(SelectedRegularSourceModeOption)),
        nameof(FreeformHelperViewModel.CadOutputFwDiffAutoMode) =>
            _editedProperties.Contains(nameof(SelectedCadOutputFwDiffAutoModeOption)),
        nameof(FreeformHelperViewModel.CascadeIcSettings) =>
            _editedProperties.Overlaps([nameof(CascadeIcSettings), nameof(XChannels), nameof(YChannels), nameof(CascadeNum)]),
        _ => _editedProperties.Contains(ownerProperty),
    };

    private void OnSave()
    {
        // The window is not modal. A draft opened before another project was loaded must not overwrite that project.
        if (!_owner.TryAcceptSettingsWindowDraft(ActiveAreaPixels.ProjectIdentity))
        {
            RequestClose?.Invoke();
            return;
        }

        _owner.ApplySettingsWindowDraft(this);
        _owner.ApplyActiveAreaPixelsDraft(ActiveAreaPixels);
        RequestClose?.Invoke();
    }

    private void OnCancel()
    {
        RequestClose?.Invoke();
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
            DxfLayerCategoryState state,
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

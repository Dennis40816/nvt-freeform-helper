// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels.Settings;

/// <summary>Owns the settings mapping and export draft and its synchronous transitions. UI thread only.</summary>
internal sealed class SettingsMappingExportDraftOwner
{
    private readonly Func<object?> _readDraftProjectIdentity;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;
    private readonly Func<string> _buildExportSafetySummary;

    private bool _showDiffIndexOverlay;
    private decimal _cadOutputFwDiffIndexStart;
    private decimal _cadOutputFwDiffIndexAnchorCadId;
    private FreeformHelperViewModel.CadOutputFwDiffAutoModeOption _selectedCadOutputFwDiffAutoModeOption;
    private FreeformHelperViewModel.NotchExportFileTypeOption _selectedNotchExportFileTypeOption;
    private FreeformHelperViewModel.NotchExportProfileOption _selectedNotchExportProfileOption;
    private decimal _mappingWeightIou;
    private decimal _mappingWeightCentroidDistance;
    private decimal _mappingWeightAreaRatio;
    private decimal _mappingLowConfidenceThreshold;
    private decimal _mappingAmbiguousMargin;
    private decimal _mappingCandidateNumber;
    private bool _enableV21;
    private bool _enableV22;
    private decimal _lenScale;
    private decimal _nullValue;

    internal readonly record struct Seed(
        bool ShowDiffIndexOverlay,
        decimal CadOutputFwDiffIndexStart,
        decimal CadOutputFwDiffIndexAnchorCadId,
        FreeformHelperViewModel.CadOutputFwDiffAutoModeOption SelectedCadOutputFwDiffAutoModeOption,
        FreeformHelperViewModel.NotchExportFileTypeOption SelectedNotchExportFileTypeOption,
        FreeformHelperViewModel.NotchExportProfileOption SelectedNotchExportProfileOption,
        decimal MappingWeightIou,
        decimal MappingWeightCentroidDistance,
        decimal MappingWeightAreaRatio,
        decimal MappingLowConfidenceThreshold,
        decimal MappingAmbiguousMargin,
        decimal MappingCandidateNumber,
        bool EnableV21,
        bool EnableV22,
        decimal LenScale,
        decimal NullValue);

    public SettingsMappingExportDraftOwner(
        Seed seed,
        Func<object?> readDraftProjectIdentity,
        IReadOnlyList<FreeformHelperViewModel.CadOutputFwDiffAutoModeOption> cadOutputFwDiffAutoModeOptions,
        IReadOnlyList<FreeformHelperViewModel.NotchExportFileTypeOption> notchExportFileTypeOptions,
        IReadOnlyList<FreeformHelperViewModel.NotchExportProfileOption> notchExportProfileOptions,
        Func<string> buildExportSafetySummary,
        Action<string> propertyChanging,
        Action<string> propertyChanged)
    {
        _readDraftProjectIdentity = readDraftProjectIdentity;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
        _buildExportSafetySummary = buildExportSafetySummary;
        CadOutputFwDiffAutoModeOptions = cadOutputFwDiffAutoModeOptions;
        NotchExportFileTypeOptions = notchExportFileTypeOptions;
        NotchExportProfileOptions = notchExportProfileOptions;

        _showDiffIndexOverlay = seed.ShowDiffIndexOverlay;
        _cadOutputFwDiffIndexStart = seed.CadOutputFwDiffIndexStart;
        _cadOutputFwDiffIndexAnchorCadId = seed.CadOutputFwDiffIndexAnchorCadId;
        _selectedCadOutputFwDiffAutoModeOption = seed.SelectedCadOutputFwDiffAutoModeOption;
        _selectedNotchExportFileTypeOption = seed.SelectedNotchExportFileTypeOption;
        _selectedNotchExportProfileOption = seed.SelectedNotchExportProfileOption;
        _mappingWeightIou = seed.MappingWeightIou;
        _mappingWeightCentroidDistance = seed.MappingWeightCentroidDistance;
        _mappingWeightAreaRatio = seed.MappingWeightAreaRatio;
        _mappingLowConfidenceThreshold = seed.MappingLowConfidenceThreshold;
        _mappingAmbiguousMargin = seed.MappingAmbiguousMargin;
        _mappingCandidateNumber = seed.MappingCandidateNumber;
        _enableV21 = seed.EnableV21;
        _enableV22 = seed.EnableV22;
        _lenScale = seed.LenScale;
        _nullValue = seed.NullValue;
    }

    public object? ProjectIdentity => _readDraftProjectIdentity();

    public IReadOnlyList<FreeformHelperViewModel.CadOutputFwDiffAutoModeOption> CadOutputFwDiffAutoModeOptions { get; }
    public IReadOnlyList<FreeformHelperViewModel.NotchExportFileTypeOption> NotchExportFileTypeOptions { get; }
    public IReadOnlyList<FreeformHelperViewModel.NotchExportProfileOption> NotchExportProfileOptions { get; }

    public bool ShowDiffIndexOverlay
    {
        get => _showDiffIndexOverlay;
        set => SetValue(ref _showDiffIndexOverlay, value, nameof(ShowDiffIndexOverlay));
    }

    public decimal CadOutputFwDiffIndexStart
    {
        get => _cadOutputFwDiffIndexStart;
        set => SetValue(ref _cadOutputFwDiffIndexStart, value, nameof(CadOutputFwDiffIndexStart));
    }

    public decimal CadOutputFwDiffIndexAnchorCadId
    {
        get => _cadOutputFwDiffIndexAnchorCadId;
        set => SetValue(ref _cadOutputFwDiffIndexAnchorCadId, value, nameof(CadOutputFwDiffIndexAnchorCadId));
    }

    public FreeformHelperViewModel.CadOutputFwDiffAutoModeOption SelectedCadOutputFwDiffAutoModeOption
    {
        get => _selectedCadOutputFwDiffAutoModeOption;
        set => SetValue(ref _selectedCadOutputFwDiffAutoModeOption, value, nameof(SelectedCadOutputFwDiffAutoModeOption));
    }

    public FreeformHelperViewModel.NotchExportFileTypeOption SelectedNotchExportFileTypeOption
    {
        get => _selectedNotchExportFileTypeOption;
        set => SetValue(ref _selectedNotchExportFileTypeOption, value, nameof(SelectedNotchExportFileTypeOption), _ => _propertyChanged(nameof(NotchExportSafetyPolicySummary)));
    }

    public FreeformHelperViewModel.NotchExportProfileOption SelectedNotchExportProfileOption
    {
        get => _selectedNotchExportProfileOption;
        set => SetValue(ref _selectedNotchExportProfileOption, value, nameof(SelectedNotchExportProfileOption));
    }

    public decimal MappingWeightIou
    {
        get => _mappingWeightIou;
        set => SetValue(ref _mappingWeightIou, value, nameof(MappingWeightIou));
    }

    public decimal MappingWeightCentroidDistance
    {
        get => _mappingWeightCentroidDistance;
        set => SetValue(ref _mappingWeightCentroidDistance, value, nameof(MappingWeightCentroidDistance));
    }

    public decimal MappingWeightAreaRatio
    {
        get => _mappingWeightAreaRatio;
        set => SetValue(ref _mappingWeightAreaRatio, value, nameof(MappingWeightAreaRatio));
    }

    public decimal MappingLowConfidenceThreshold
    {
        get => _mappingLowConfidenceThreshold;
        set => SetValue(ref _mappingLowConfidenceThreshold, value, nameof(MappingLowConfidenceThreshold));
    }

    public decimal MappingAmbiguousMargin
    {
        get => _mappingAmbiguousMargin;
        set => SetValue(ref _mappingAmbiguousMargin, value, nameof(MappingAmbiguousMargin));
    }

    public decimal MappingCandidateNumber
    {
        get => _mappingCandidateNumber;
        set => SetValue(ref _mappingCandidateNumber, value, nameof(MappingCandidateNumber));
    }

    public bool EnableV21
    {
        get => _enableV21;
        set => SetValue(ref _enableV21, value, nameof(EnableV21));
    }

    public bool EnableV22
    {
        get => _enableV22;
        set => SetValue(ref _enableV22, value, nameof(EnableV22));
    }

    public decimal LenScale
    {
        get => _lenScale;
        set => SetValue(ref _lenScale, value, nameof(LenScale));
    }

    public decimal NullValue
    {
        get => _nullValue;
        set => SetValue(ref _nullValue, value, nameof(NullValue));
    }

    public string NotchExportSafetyPolicySummary =>
        !string.IsNullOrWhiteSpace(SelectedNotchExportFileTypeOption.Display)
            ? _buildExportSafetySummary()
            : string.Empty;

    private void SetValue<T>(ref T field, T value, string propertyName, Action<T>? changed = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        _propertyChanging(propertyName);
        field = value;
        changed?.Invoke(value);
        _propertyChanged(propertyName);
    }
}

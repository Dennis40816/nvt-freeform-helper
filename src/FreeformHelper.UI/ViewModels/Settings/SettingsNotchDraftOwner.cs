// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.ViewModels.Settings;

/// <summary>Owns the settings notch draft and its synchronous transitions. UI thread only.</summary>
internal sealed class SettingsNotchDraftOwner
{
    private readonly Func<object?> _readDraftProjectIdentity;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;
    private readonly UndoSuppression _thresholdSync = new();
    private readonly UndoSuppression _compensationModelSync = new();
    private readonly Func<bool, decimal, string> _buildTargetCoverageGuardSummary;
    private readonly Func<decimal, string> _buildTargetCoverageCapHelpText;
    private readonly Func<string> _buildEmsSafetyPolicySummary;
    private readonly Func<string> _buildEmsSafetyShortText;

    private bool _enableToRegular;
    private bool _enableToFull;
    private bool _enableToFullRuleEngine;
    private bool _enableToFullRuleTrace;
    private bool _enableBoundaryVirtualAreaCap;
    private decimal _boundaryVirtualAreaCapPercent;
    private bool _enableTargetCoverageGuard;
    private decimal _targetCoverageCapPercent;
    private NotchCompensationModelOption _selectedNotchCompensationModelOption;
    private bool _showNotchCanvasPreview;
    private bool _showNotchToRegularLabels;
    private decimal _notchPreviewVisualizationStep;
    private decimal _toFullStrictOverlapPercent;
    private bool _notchPreviewAutoPlayEnabled;
    private decimal _notchPreviewAutoPlayIntervalMs;
    private decimal _notchThresholdQ7;
    private decimal _notchThresholdPercent;
    private bool _linkNotchThresholds;

    internal readonly record struct Seed(
        bool EnableToRegular,
        bool EnableToFull,
        bool EnableToFullRuleEngine,
        bool EnableToFullRuleTrace,
        bool EnableBoundaryVirtualAreaCap,
        decimal BoundaryVirtualAreaCapPercent,
        bool EnableTargetCoverageGuard,
        decimal TargetCoverageCapPercent,
        NotchCompensationModelOption SelectedNotchCompensationModelOption,
        bool ShowNotchCanvasPreview,
        bool ShowNotchToRegularLabels,
        decimal NotchPreviewVisualizationStep,
        decimal ToFullStrictOverlapPercent,
        bool NotchPreviewAutoPlayEnabled,
        decimal NotchPreviewAutoPlayIntervalMs,
        decimal NotchThresholdQ7,
        decimal NotchThresholdPercent,
        bool LinkNotchThresholds);

    public SettingsNotchDraftOwner(
        Seed seed,
        Func<object?> readDraftProjectIdentity,
        IReadOnlyList<NotchCompensationModelOption> notchCompensationModelOptions,
        Func<bool, decimal, string> buildTargetCoverageGuardSummary,
        Func<decimal, string> buildTargetCoverageCapHelpText,
        Func<string> buildEmsSafetyPolicySummary,
        Func<string> buildEmsSafetyShortText,
        Action<string> propertyChanging,
        Action<string> propertyChanged)
    {
        _readDraftProjectIdentity = readDraftProjectIdentity;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
        _buildTargetCoverageGuardSummary = buildTargetCoverageGuardSummary;
        _buildTargetCoverageCapHelpText = buildTargetCoverageCapHelpText;
        _buildEmsSafetyPolicySummary = buildEmsSafetyPolicySummary;
        _buildEmsSafetyShortText = buildEmsSafetyShortText;
        NotchCompensationModelOptions = notchCompensationModelOptions;

        _enableToRegular = seed.EnableToRegular;
        _enableToFull = seed.EnableToFull;
        _enableToFullRuleEngine = seed.EnableToFullRuleEngine;
        _enableToFullRuleTrace = seed.EnableToFullRuleTrace;
        _enableBoundaryVirtualAreaCap = seed.EnableBoundaryVirtualAreaCap;
        _boundaryVirtualAreaCapPercent = seed.BoundaryVirtualAreaCapPercent;
        _enableTargetCoverageGuard = seed.EnableTargetCoverageGuard;
        _targetCoverageCapPercent = seed.TargetCoverageCapPercent;
        _selectedNotchCompensationModelOption = seed.SelectedNotchCompensationModelOption;
        _showNotchCanvasPreview = seed.ShowNotchCanvasPreview;
        _showNotchToRegularLabels = seed.ShowNotchToRegularLabels;
        _notchPreviewVisualizationStep = seed.NotchPreviewVisualizationStep;
        _toFullStrictOverlapPercent = seed.ToFullStrictOverlapPercent;
        _notchPreviewAutoPlayEnabled = seed.NotchPreviewAutoPlayEnabled;
        _notchPreviewAutoPlayIntervalMs = seed.NotchPreviewAutoPlayIntervalMs;
        _notchThresholdQ7 = seed.NotchThresholdQ7;
        _notchThresholdPercent = seed.NotchThresholdPercent;
        _linkNotchThresholds = seed.LinkNotchThresholds;
    }

    public object? ProjectIdentity => _readDraftProjectIdentity();

    public IReadOnlyList<NotchCompensationModelOption> NotchCompensationModelOptions { get; }

    public bool EnableToRegular
    {
        get => _enableToRegular;
        set => SetValue(ref _enableToRegular, value, nameof(EnableToRegular), _ => NotifyNotchModelSummaryChanged());
    }

    public bool EnableToFull
    {
        get => _enableToFull;
        set => SetValue(ref _enableToFull, value, nameof(EnableToFull), _ => NotifyNotchModelSummaryChanged());
    }

    public bool EnableToFullRuleEngine
    {
        get => _enableToFullRuleEngine;
        set => SetValue(ref _enableToFullRuleEngine, value, nameof(EnableToFullRuleEngine), _ => _propertyChanged(nameof(ToFullRuleEngineSummary)));
    }

    public bool EnableToFullRuleTrace
    {
        get => _enableToFullRuleTrace;
        set => SetValue(ref _enableToFullRuleTrace, value, nameof(EnableToFullRuleTrace), _ => _propertyChanged(nameof(ToFullRuleEngineSummary)));
    }

    public bool EnableBoundaryVirtualAreaCap
    {
        get => _enableBoundaryVirtualAreaCap;
        set => SetValue(ref _enableBoundaryVirtualAreaCap, value, nameof(EnableBoundaryVirtualAreaCap), _ => NotifyNotchModelSummaryChanged());
    }

    public decimal BoundaryVirtualAreaCapPercent
    {
        get => _boundaryVirtualAreaCapPercent;
        set => SetValue(ref _boundaryVirtualAreaCapPercent, value, nameof(BoundaryVirtualAreaCapPercent), OnBoundaryVirtualAreaCapPercentChanged);
    }

    public bool EnableTargetCoverageGuard
    {
        get => _enableTargetCoverageGuard;
        set => SetValue(ref _enableTargetCoverageGuard, value, nameof(EnableTargetCoverageGuard), _ => NotifyNotchModelSummaryChanged());
    }

    public decimal TargetCoverageCapPercent
    {
        get => _targetCoverageCapPercent;
        set => SetValue(ref _targetCoverageCapPercent, value, nameof(TargetCoverageCapPercent), OnTargetCoverageCapPercentChanged);
    }

    public NotchCompensationModelOption SelectedNotchCompensationModelOption
    {
        get => _selectedNotchCompensationModelOption;
        set => SetValue(ref _selectedNotchCompensationModelOption, value, nameof(SelectedNotchCompensationModelOption), OnSelectedNotchCompensationModelOptionChanged);
    }

    public bool ShowNotchCanvasPreview
    {
        get => _showNotchCanvasPreview;
        set => SetValue(ref _showNotchCanvasPreview, value, nameof(ShowNotchCanvasPreview));
    }

    public bool ShowNotchToRegularLabels
    {
        get => _showNotchToRegularLabels;
        set => SetValue(ref _showNotchToRegularLabels, value, nameof(ShowNotchToRegularLabels));
    }

    public decimal NotchPreviewVisualizationStep
    {
        get => _notchPreviewVisualizationStep;
        set => SetValue(ref _notchPreviewVisualizationStep, value, nameof(NotchPreviewVisualizationStep));
    }

    public decimal ToFullStrictOverlapPercent
    {
        get => _toFullStrictOverlapPercent;
        set => SetValue(ref _toFullStrictOverlapPercent, value, nameof(ToFullStrictOverlapPercent));
    }

    public bool NotchPreviewAutoPlayEnabled
    {
        get => _notchPreviewAutoPlayEnabled;
        set => SetValue(ref _notchPreviewAutoPlayEnabled, value, nameof(NotchPreviewAutoPlayEnabled));
    }

    public decimal NotchPreviewAutoPlayIntervalMs
    {
        get => _notchPreviewAutoPlayIntervalMs;
        set => SetValue(ref _notchPreviewAutoPlayIntervalMs, value, nameof(NotchPreviewAutoPlayIntervalMs));
    }

    public decimal NotchThresholdQ7
    {
        get => _notchThresholdQ7;
        set => SetValue(ref _notchThresholdQ7, value, nameof(NotchThresholdQ7), OnNotchThresholdQ7Changed);
    }

    public decimal NotchThresholdPercent
    {
        get => _notchThresholdPercent;
        set => SetValue(ref _notchThresholdPercent, value, nameof(NotchThresholdPercent), OnNotchThresholdPercentChanged);
    }

    public bool LinkNotchThresholds
    {
        get => _linkNotchThresholds;
        set => SetValue(ref _linkNotchThresholds, value, nameof(LinkNotchThresholds), OnLinkNotchThresholdsChanged);
    }

    public string ToFullRuleEngineSummary => EnableToFullRuleEngine
        ? (EnableToFullRuleTrace
            ? "To Full rule engine: ON (trace ON)"
            : "To Full rule engine: ON (trace OFF)")
        : "To Full rule engine: OFF (legacy gate path)";

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
        _buildTargetCoverageGuardSummary(
            EnableTargetCoverageGuard,
            TargetCoverageCapPercent);

    public string NotchTargetCoverageGuardShortText => EnableTargetCoverageGuard
        ? $"Target guard {TargetCoverageCapPercent:0.#}%"
        : "Target guard OFF";

    public string NotchTargetCoverageCapHelpText => _buildTargetCoverageCapHelpText(TargetCoverageCapPercent);

    public string NotchEmsSafetyPolicySummary =>
        !string.IsNullOrWhiteSpace(SelectedNotchCompensationModelOption.Display)
            ? _buildEmsSafetyPolicySummary()
            : string.Empty;

    public string NotchEmsSafetyShortText =>
        !string.IsNullOrWhiteSpace(SelectedNotchCompensationModelOption.Display)
            ? _buildEmsSafetyShortText()
            : string.Empty;

    private void OnBoundaryVirtualAreaCapPercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 1000m);
        if (clamped != value)
        {
            BoundaryVirtualAreaCapPercent = clamped;
            return;
        }

        NotifyNotchModelSummaryChanged();
    }

    private void OnTargetCoverageCapPercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 255m);
        if (clamped != value)
        {
            TargetCoverageCapPercent = clamped;
            return;
        }

        NotifyNotchModelSummaryChanged();
    }

    private void OnSelectedNotchCompensationModelOptionChanged(NotchCompensationModelOption value)
    {
        if (_compensationModelSync.IsActive)
        {
            return;
        }

        using (_compensationModelSync.Enter())
        {
            var (enableToRegular, enableToFull) = value.Value == NotchCompensationModel.Disabled
                ? (false, false)
                : (true, true);
            EnableToRegular = enableToRegular;
            EnableToFull = enableToFull;
        }

        _propertyChanged(nameof(NotchCompensationModelSummary));
        NotifyNotchModelSummaryChanged();
    }

    private void OnNotchThresholdQ7Changed(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 128m);
        if (clamped != value)
        {
            NotchThresholdQ7 = clamped;
            return;
        }

        if (_thresholdSync.IsActive || !LinkNotchThresholds)
        {
            return;
        }

        // Release only after the nested setter succeeds, matching the original callback.
        var sync = _thresholdSync.Enter();
        NotchThresholdPercent = Math.Round(clamped * 100m / 128m, 2);
        sync.Dispose();
    }

    private void OnNotchThresholdPercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 100m);
        if (clamped != value)
        {
            NotchThresholdPercent = clamped;
            return;
        }

        if (_thresholdSync.IsActive || !LinkNotchThresholds)
        {
            return;
        }

        // Release only after the nested setter succeeds, matching the original callback.
        var sync = _thresholdSync.Enter();
        var q7 = Math.Clamp((int)Math.Round(clamped * 128m / 100m, MidpointRounding.AwayFromZero), 0, 128);
        NotchThresholdQ7 = q7;
        sync.Dispose();
    }

    private void OnLinkNotchThresholdsChanged(bool value)
    {
        if (!value || _thresholdSync.IsActive)
        {
            return;
        }

        // Release only after the nested setter succeeds, matching the original callback.
        var sync = _thresholdSync.Enter();
        NotchThresholdPercent = Math.Round(NotchThresholdQ7 * 100m / 128m, 2);
        sync.Dispose();
    }

    private void NotifyNotchModelSummaryChanged()
    {
        _propertyChanged(nameof(NotchEffectiveModelSummary));
        _propertyChanged(nameof(NotchAllocationModelSummary));
        _propertyChanged(nameof(NotchAllocationModelShortText));
        _propertyChanged(nameof(NotchToRegularSemanticSummary));
        _propertyChanged(nameof(NotchToRegularShortText));
        _propertyChanged(nameof(NotchToFullSemanticSummary));
        _propertyChanged(nameof(NotchToFullShortText));
        _propertyChanged(nameof(NotchBoundaryVirtualAreaCapSummary));
        _propertyChanged(nameof(NotchBoundaryVirtualAreaCapShortText));
        _propertyChanged(nameof(NotchTargetCoverageGuardSummary));
        _propertyChanged(nameof(NotchTargetCoverageGuardShortText));
        _propertyChanged(nameof(NotchTargetCoverageCapHelpText));
        _propertyChanged(nameof(NotchEmsSafetyPolicySummary));
        _propertyChanged(nameof(NotchEmsSafetyShortText));
    }

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

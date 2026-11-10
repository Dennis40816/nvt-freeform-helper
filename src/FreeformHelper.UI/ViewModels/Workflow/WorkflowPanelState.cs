// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns console and workflow-panel expansion and their synchronous transitions on the UI thread.</summary>
internal sealed class WorkflowPanelState
{
    private readonly Action<WorkspaceWorkStep> _selectWorkStep;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;

    // UI thread only. Writes remain sequential so reentrant observers see every intermediate state.
    private bool _isConsoleExpanded = true;
    private bool _isStep1Expanded = true;
    private bool _isStep2Expanded;
    private bool _isStep3Expanded;
    private bool _isStep4Expanded;
    private bool _isStep5Expanded;
    private bool _isStep6Expanded;

    internal WorkflowPanelState(
        Action<WorkspaceWorkStep> selectWorkStep,
        Action<string> propertyChanging,
        Action<string> propertyChanged)
    {
        ArgumentNullException.ThrowIfNull(selectWorkStep);
        ArgumentNullException.ThrowIfNull(propertyChanging);
        ArgumentNullException.ThrowIfNull(propertyChanged);
        _selectWorkStep = selectWorkStep;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
    }

    public bool IsConsoleExpanded
    {
        get => _isConsoleExpanded;
        set => SetValue(ref _isConsoleExpanded, value, nameof(IsConsoleExpanded));
    }

    public bool IsStep1Expanded
    {
        get => _isStep1Expanded;
        set => SetValue(ref _isStep1Expanded, value, nameof(IsStep1Expanded), WorkspaceWorkStep.Step1Match);
    }

    public bool IsStep2Expanded
    {
        get => _isStep2Expanded;
        set => SetValue(ref _isStep2Expanded, value, nameof(IsStep2Expanded), WorkspaceWorkStep.Step2Freeform);
    }

    public bool IsStep3Expanded
    {
        get => _isStep3Expanded;
        set => SetValue(ref _isStep3Expanded, value, nameof(IsStep3Expanded), WorkspaceWorkStep.Step3NotchPreview);
    }

    public bool IsStep4Expanded
    {
        get => _isStep4Expanded;
        set => SetValue(ref _isStep4Expanded, value, nameof(IsStep4Expanded));
    }

    public bool IsStep5Expanded
    {
        get => _isStep5Expanded;
        set => SetValue(ref _isStep5Expanded, value, nameof(IsStep5Expanded), WorkspaceWorkStep.Step5Export);
    }

    public bool IsStep6Expanded
    {
        get => _isStep6Expanded;
        set => SetValue(ref _isStep6Expanded, value, nameof(IsStep6Expanded));
    }

    internal void ApplyExpansion(
        bool isStep1Expanded,
        bool isStep2Expanded,
        bool isStep3Expanded,
        bool isStep4Expanded,
        bool isStep5Expanded,
        bool isStep6Expanded)
    {
        IsStep1Expanded = isStep1Expanded;
        IsStep2Expanded = isStep2Expanded;
        IsStep3Expanded = isStep3Expanded;
        IsStep4Expanded = isStep4Expanded;
        IsStep5Expanded = isStep5Expanded;
        IsStep6Expanded = isStep6Expanded;
    }

    private void SetValue(ref bool field, bool value, string propertyName, WorkspaceWorkStep? workStep = null)
    {
        if (EqualityComparer<bool>.Default.Equals(field, value))
        {
            return;
        }

        _propertyChanging(propertyName);
        field = value;
        if (value && workStep is { } step)
        {
            _selectWorkStep(step);
        }
        _propertyChanged(propertyName);
    }
}

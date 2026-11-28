// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns FW assignment inputs and their synchronous setter transitions for one workspace.</summary>
internal sealed class FwAssignmentSettingsViewModel
{
    private readonly IFwAssignmentSettingsHost _host;

    // UI thread only after construction; project load replaces values through the same setters.
    private FwDiffAssignmentSettings _fwDiffAssignment = FwDiffAssignmentSettings.Initial;

    internal FwAssignmentSettingsViewModel(IFwAssignmentSettingsHost host)
    {
        _host = host;
    }

    private void OnCadOutputFwDiffIndexStartChanged(decimal value)
    {
        if (_host.IsLoadingSettings) return;
        if (value < 0 || value > 1_000_000)
        {
            using (_host.UndoSuppression.Enter())
            {
                CadOutputFwDiffIndexStart = Math.Clamp(value, 0, 1_000_000);
            }
            return;
        }

        _host.ApplySettingsOrchestration(nameof(CadOutputFwDiffIndexStart));
    }

    private void OnCadOutputFwDiffAutoModeChanged()
    {
        if (_host.Suppression.IsCadOutputFwDiffAutoModeSelectionSyncActive) return;

        using (_host.Suppression.EnterCadOutputFwDiffAutoModeSelectionSync())
        {
            _host.NotifySelectedAutoModeChanged();
        }

        if (_host.IsLoadingSettings) return;
        _host.ApplySettingsOrchestration(nameof(CadOutputFwDiffAutoMode));
    }

    private void OnCadOutputFwDiffIndexAnchorCadIdChanged(decimal value)
    {
        if (_host.IsLoadingSettings || _host.Suppression.IsDxfIndexAnchorChangeActive) return;
        if (value < -1 || value > 2_000_000_000)
        {
            using (_host.UndoSuppression.Enter())
            {
                CadOutputFwDiffIndexAnchorCadId = Math.Clamp(value, -1, 2_000_000_000);
            }
            return;
        }

        if (!_host.TryCommitAnchorFromCurrentValue())
        {
            return;
        }

        _host.ApplySettingsOrchestration(nameof(CadOutputFwDiffIndexAnchorCadId));
    }

    /// <summary>
    /// Gets or sets a value indicating whether to display CAD Output FW Diff overlay hints on canvas.
    /// </summary>
    public bool ShowDiffIndexOverlay
    {
        get => _fwDiffAssignment.ShowDiffIndexOverlay;
        set
        {
            if (_fwDiffAssignment.ShowDiffIndexOverlay == value)
            {
                return;
            }

            _host.OnShowDiffIndexOverlayChanging(value);
            _host.NotifyPropertyChanging(nameof(ShowDiffIndexOverlay));
            _fwDiffAssignment = _fwDiffAssignment with { ShowDiffIndexOverlay = value };
            _host.NotifyPropertyChanged(nameof(ShowDiffIndexOverlay));
        }
    }

    /// <summary>
    /// Gets or sets the summary text for current CAD Output FW Diff assignment policy.
    /// </summary>
    public string CadOutputFwDiffAssignmentSummary
    {
        get => _fwDiffAssignment.CadOutputFwDiffAssignmentSummary;
        set
        {
            if (_fwDiffAssignment.CadOutputFwDiffAssignmentSummary == value)
            {
                return;
            }

            _host.NotifyPropertyChanging(nameof(CadOutputFwDiffAssignmentSummary));
            _fwDiffAssignment = _fwDiffAssignment with { CadOutputFwDiffAssignmentSummary = value };
            _host.NotifyPropertyChanged(nameof(CadOutputFwDiffAssignmentSummary));
        }
    }

    /// <summary>
    /// Gets or sets the CAD id used as diff idx start anchor.
    /// Use -1 to indicate "first CAD in scan order".
    /// </summary>
    public decimal CadOutputFwDiffIndexAnchorCadId
    {
        get => _fwDiffAssignment.CadOutputFwDiffIndexAnchorCadId;
        set
        {
            if (_fwDiffAssignment.CadOutputFwDiffIndexAnchorCadId == value)
            {
                return;
            }

            _host.OnCadOutputFwDiffIndexAnchorCadIdChanging(value);
            _host.NotifyPropertyChanging(nameof(CadOutputFwDiffIndexAnchorCadId));
            _fwDiffAssignment = _fwDiffAssignment with { CadOutputFwDiffIndexAnchorCadId = value };
            OnCadOutputFwDiffIndexAnchorCadIdChanged(value);
            _host.NotifyPropertyChanged(nameof(CadOutputFwDiffIndexAnchorCadId));
        }
    }

    /// <summary>
    /// Gets or sets the CAD auto CAD Output FW Diff assignment mode.
    /// </summary>
    public CadOutputFwDiffAutoMode CadOutputFwDiffAutoMode
    {
        get => _fwDiffAssignment.CadOutputFwDiffAutoMode;
        set
        {
            if (_fwDiffAssignment.CadOutputFwDiffAutoMode == value)
            {
                return;
            }

            _host.OnCadOutputFwDiffAutoModeChanging(value);
            _host.NotifyPropertyChanging(nameof(CadOutputFwDiffAutoMode));
            _fwDiffAssignment = _fwDiffAssignment with { CadOutputFwDiffAutoMode = value };
            OnCadOutputFwDiffAutoModeChanged();
            _host.NotifyPropertyChanged(nameof(CadOutputFwDiffAutoMode));
        }
    }

    /// <summary>
    /// Gets or sets the starting expected diff idx value for CAD scan-order indexing.
    /// </summary>
    public decimal CadOutputFwDiffIndexStart
    {
        get => _fwDiffAssignment.CadOutputFwDiffIndexStart;
        set
        {
            if (_fwDiffAssignment.CadOutputFwDiffIndexStart == value)
            {
                return;
            }

            _host.OnCadOutputFwDiffIndexStartChanging(value);
            _host.NotifyPropertyChanging(nameof(CadOutputFwDiffIndexStart));
            _fwDiffAssignment = _fwDiffAssignment with { CadOutputFwDiffIndexStart = value };
            OnCadOutputFwDiffIndexStartChanged(value);
            _host.NotifyPropertyChanged(nameof(CadOutputFwDiffIndexStart));
        }
    }
}

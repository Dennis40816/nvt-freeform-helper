using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    partial void OnCadOutputFwDiffIndexStartChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        if (value < 0 || value > 1_000_000)
        {
            using (_undoSuppression.Enter())
            {
                CadOutputFwDiffIndexStart = Math.Clamp(value, 0, 1_000_000);
            }
            return;
        }

        ApplySettingsOrchestration(nameof(CadOutputFwDiffIndexStart));
    }

    partial void OnCadOutputFwDiffAutoModeChanged(CadOutputFwDiffAutoMode value)
    {
        if (_settingsSuppression.IsCadOutputFwDiffAutoModeSelectionSyncActive) return;

        using (_settingsSuppression.EnterCadOutputFwDiffAutoModeSelectionSync())
        {
            OnPropertyChanged(nameof(SelectedCadOutputFwDiffAutoModeOption));
        }

        if (_isLoadingSettings) return;
        ApplySettingsOrchestration(nameof(CadOutputFwDiffAutoMode));
    }

    partial void OnCadOutputFwDiffIndexAnchorCadIdChanged(decimal value)
    {
        if (_isLoadingSettings || _settingsSuppression.IsDxfIndexAnchorChangeActive) return;
        if (value < -1 || value > 2_000_000_000)
        {
            using (_undoSuppression.Enter())
            {
                CadOutputFwDiffIndexAnchorCadId = Math.Clamp(value, -1, 2_000_000_000);
            }
            return;
        }

        if (!TryCommitCadOutputFwDiffIndexAnchorCadIdFromCurrentValue())
        {
            return;
        }

        ApplySettingsOrchestration(nameof(CadOutputFwDiffIndexAnchorCadId));
    }

    // The CAD Output FW Diff assignment settings are stored once. The properties below forward to the
    // record and raise the same notifications and callbacks as the stored properties they replaced.
    private FwDiffAssignmentSettings _fwDiffAssignment = FwDiffAssignmentSettings.Initial;

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

            OnShowDiffIndexOverlayChanging(value);
            OnPropertyChanging();
            _fwDiffAssignment = _fwDiffAssignment with { ShowDiffIndexOverlay = value };
            OnPropertyChanged();
        }
    }

    partial void OnShowDiffIndexOverlayChanging(bool value);

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

            OnPropertyChanging();
            _fwDiffAssignment = _fwDiffAssignment with { CadOutputFwDiffAssignmentSummary = value };
            OnPropertyChanged();
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

            OnCadOutputFwDiffIndexAnchorCadIdChanging(value);
            OnPropertyChanging();
            _fwDiffAssignment = _fwDiffAssignment with { CadOutputFwDiffIndexAnchorCadId = value };
            OnCadOutputFwDiffIndexAnchorCadIdChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnCadOutputFwDiffIndexAnchorCadIdChanging(decimal value);

    partial void OnCadOutputFwDiffIndexAnchorCadIdChanged(decimal value);

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

            OnCadOutputFwDiffAutoModeChanging(value);
            OnPropertyChanging();
            _fwDiffAssignment = _fwDiffAssignment with { CadOutputFwDiffAutoMode = value };
            OnCadOutputFwDiffAutoModeChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnCadOutputFwDiffAutoModeChanging(CadOutputFwDiffAutoMode value);

    partial void OnCadOutputFwDiffAutoModeChanged(CadOutputFwDiffAutoMode value);

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

            OnCadOutputFwDiffIndexStartChanging(value);
            OnPropertyChanging();
            _fwDiffAssignment = _fwDiffAssignment with { CadOutputFwDiffIndexStart = value };
            OnCadOutputFwDiffIndexStartChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnCadOutputFwDiffIndexStartChanging(decimal value);

    partial void OnCadOutputFwDiffIndexStartChanged(decimal value);
}

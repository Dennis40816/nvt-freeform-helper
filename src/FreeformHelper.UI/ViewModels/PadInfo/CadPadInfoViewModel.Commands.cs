using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class CadPadInfoViewModel : ObservableObject, IPadInfoChangeTracking
{
    /// <summary>
    /// Applies the pending custom value changes via the injected action.
    /// </summary>
    private void ApplyChanges()
    {
        if (_customValueDirty)
        {
            _applyCustomValue?.Invoke(_padIds, CustomValue); // Invoke action to apply changes.
            _customValueInitial = CustomValue; // Update initial value to current.
            _customValueDirty = false; // Clear dirty flag.
            OnPropertyChanged(nameof(HasPendingChanges));
        }

        _closePadInfo?.Invoke(); // Close the pad info popup.
    }

    /// <summary>
    /// Discards any pending custom value changes, reverting to the initial state.
    /// </summary>
    private void DiscardChanges()
    {
        if (!_customValueDirty)
        {
            return; // No pending changes to discard.
        }

        _isLoading = true; // Suppress dirty tracking during revert.
        CustomValue = _customValueInitial; // Revert to initial value.
        _isLoading = false;

        _customValueDirty = false; // Clear dirty flag.
        OnPropertyChanged(nameof(HasPendingChanges));
    }

    private void FocusMatch()
    {
        if (_singleCadPadId is not int cadPadId || _focusCadMatches is null)
        {
            return;
        }

        _focusCadMatches(cadPadId);
    }

    private void HighlightMatches()
    {
        if (_singleCadPadId is not int cadPadId || _highlightCadMatches is null)
        {
            return;
        }

        _highlightCadMatches(cadPadId);
    }

    private void LocateMatch()
    {
        if (_focusCadMatches is not null)
        {
            FocusMatch();
            return;
        }

        HighlightMatches();
    }

    private void HighlightAllNotchOwners()
    {
        if (_highlightCadOwnerPads is null || _allNotchOwnerCadIds.Count == 0)
        {
            return;
        }

        _highlightCadOwnerPads(_allNotchOwnerCadIds);
    }

    private void ApplyCadOutputFwDiffOverride()
    {
        if (_singleCadPadId is not int cadPadId || _setCadOutputFwDiffOverride is null)
        {
            return;
        }

        var rounded = (int)Math.Round(CadOutputFwDiffOverride);
        var clamped = Math.Clamp(rounded, 0, 1_000_000);
        _setCadOutputFwDiffOverride(cadPadId, clamped);
        CadOutputFwDiffOverride = clamped;
        DxfIndexText = clamped.ToString(CultureInfo.InvariantCulture);
        HasCadOutputFwDiffOverride = true;
    }

    private void ClearCadOutputFwDiffOverride()
    {
        if (_singleCadPadId is not int cadPadId || _clearCadOutputFwDiffOverride is null)
        {
            return;
        }

        _clearCadOutputFwDiffOverride(cadPadId);
        var currentVisibleIndex = _getDxfIndex(cadPadId);
        DxfIndexText = currentVisibleIndex?.ToString(CultureInfo.InvariantCulture) ?? "-";
        HasCadOutputFwDiffOverride = false;
    }

    private void SetDxfIndexAnchor()
    {
        if (_singleCadPadId is not int cadPadId || _setDxfIndexAnchor is null)
        {
            return;
        }

        _setDxfIndexAnchor(cadPadId);
        IsDxfIndexAnchor = _getIsDxfIndexAnchor?.Invoke(cadPadId) ?? true;
        var currentVisibleIndex = _getDxfIndex(cadPadId);
        DxfIndexText = currentVisibleIndex?.ToString(CultureInfo.InvariantCulture) ?? "-";
    }

    private void ClearDxfIndexAnchor()
    {
        if (_singleCadPadId is not int cadPadId || _clearDxfIndexAnchor is null)
        {
            return;
        }

        _clearDxfIndexAnchor(cadPadId);
        IsDxfIndexAnchor = _getIsDxfIndexAnchor?.Invoke(cadPadId) ?? false;
        var currentVisibleIndex = _getDxfIndex(cadPadId);
        DxfIndexText = currentVisibleIndex?.ToString(CultureInfo.InvariantCulture) ?? "-";
    }
}

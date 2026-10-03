using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class RegularPadInfoViewModel : ObservableObject, IPadInfoChangeTracking
{
    partial void OnShowGeometryDetailsChanged(bool value)
    {
        OnPropertyChanged(nameof(GeometryDetailsToggleText));
    }

    private void ApplyFreeform(FreeformType type)
    {
        _setFreeform?.Invoke(_padIndices, type);
        _closePadInfo?.Invoke();
    }

    private void NavigateToMatch()
    {
        if (_focusRegularMatches is null || !_regularPadIdForMatchActions.HasValue)
        {
            return;
        }

        _focusRegularMatches(_regularPadIdForMatchActions.Value);
    }

    private void HighlightMatches()
    {
        if (_highlightRegularMatches is null || !_regularPadIdForMatchActions.HasValue)
        {
            return;
        }

        _highlightRegularMatches(_regularPadIdForMatchActions.Value);
    }

    private void LocateMatch()
    {
        if (_focusRegularMatches is not null)
        {
            NavigateToMatch();
            return;
        }

        HighlightMatches();
    }

    private void HighlightAllOwners()
    {
        if (_highlightCadOwnerPads is null || _allOwnerCadIds.Count == 0)
        {
            return;
        }

        _highlightCadOwnerPads(_allOwnerCadIds);
    }

    private static string BuildMatchText(Func<int, int?>? getDxfIndex, List<int> cadIds)
    {
        if (cadIds.Count == 0)
        {
            return "Unmatched";
        }

        var dxfIndices = cadIds
            .Select(cadId => getDxfIndex?.Invoke(cadId))
            .Where(index => index.HasValue)
            .Select(index => index!.Value)
            .Distinct()
            .OrderBy(index => index)
            .ToList();

        var cadListText = PadInfoTextFormatter.CompactList(cadIds);
        if (dxfIndices.Count > 0)
        {
            var dxfListText = PadInfoTextFormatter.CompactList(dxfIndices);
            return $"CAD idx {dxfListText} (CAD id {cadListText})";
        }

        return $"CAD id {cadListText}";
    }

    private static string BuildRegularMatchDetailsText(
        Func<int, int?>? getDxfIndex,
        List<int> cadIds,
        List<PadMatchLink> links)
    {
        if (cadIds.Count == 0)
        {
            return "Unmatched";
        }

        if (links.Count == 0)
        {
            return $"CAD ids: {string.Join(", ", cadIds)}";
        }

        var lines = new List<string>(links.Count);
        foreach (var link in links)
        {
            var mappedIndex = getDxfIndex?.Invoke(link.CadPadId);
            var indexText = mappedIndex.HasValue
                ? $"idx {mappedIndex.Value}"
                : $"id {link.CadPadId}";
            lines.Add($"CAD {indexText}: {(link.CadCoverage * 100):0.#}%");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Partial method invoked when <see cref="PadWidth" /> property changes.
    /// Tracks whether the pad width has diverged from its initial state.
    /// </summary>
    partial void OnPadWidthChanged(decimal value)
    {
        if (_isLoading)
        {
            return;
        }

        _padWidthDirty = value != _padWidthInitial;
        OnPropertyChanged(nameof(HasPendingChanges));
    }

    /// <summary>
    /// Partial method invoked when <see cref="PadHeight" /> property changes.
    /// Tracks whether the pad height has diverged from its initial state.
    /// </summary>
    partial void OnPadHeightChanged(decimal value)
    {
        if (_isLoading)
        {
            return;
        }

        _padHeightDirty = value != _padHeightInitial;
        OnPropertyChanged(nameof(HasPendingChanges));
    }

    /// <summary>
    /// Applies the pending pad size changes via the injected action.
    /// </summary>
    private void ApplyChanges()
    {
        if (_padWidthDirty || _padHeightDirty)
        {
            // Only send changed dimensions.
            var width = _padWidthDirty ? PadWidth : (decimal?)null;
            var height = _padHeightDirty ? PadHeight : (decimal?)null;
            _applyPadSize?.Invoke(_rows, _cols, width, height);

            // Reset dirty flags and initial values for applied changes.
            if (_padWidthDirty)
            {
                _padWidthInitial = PadWidth;
                _padWidthDirty = false;
            }
            if (_padHeightDirty)
            {
                _padHeightInitial = PadHeight;
                _padHeightDirty = false;
            }
        }

        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(ApplyChangesCommand));
        OnPropertyChanged(nameof(DiscardChangesCommand));
        _closePadInfo?.Invoke();
    }

    /// <summary>
    /// Discards any pending pad size changes, reverting to the initial state.
    /// </summary>
    private void DiscardChanges()
    {
        if (!_padWidthDirty && !_padHeightDirty)
        {
            return; // No pending changes to discard.
        }

        _isLoading = true; // Suppress dirty tracking during revert.
        if (_padWidthDirty) PadWidth = _padWidthInitial;
        if (_padHeightDirty) PadHeight = _padHeightInitial;
        _isLoading = false;

        _padWidthDirty = false;
        _padHeightDirty = false;
        OnPropertyChanged(nameof(HasPendingChanges));
    }
}

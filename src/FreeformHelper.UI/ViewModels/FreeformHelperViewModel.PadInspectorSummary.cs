using System.Collections.ObjectModel;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private PadInspectorUiState? _padInspectorUi; // Created on first read so that the snapshot-change callback can point at this instance.

    /// <summary>Gets the pad inspector snapshot and refresh flag that the inspector views bind to.</summary>
    public PadInspectorUiState PadInspectorUi => _padInspectorUi ??= new PadInspectorUiState(OnPadInspectorSnapshotChanged);

    private void OnPadInspectorSnapshotChanged(PadInspectorSnapshot? value)
    {
        var display = value is null ? null : PadInspectorDisplayProjector.BuildPadInspectorDisplay(
            value,
            TryGetCadDisplayIndex,
            BuildCadCompensationLines);
        PadInspectorDisplay = display ?? PadInspectorDisplay.CreateEmpty();
        IsPadInspectorRawTraceExpanded = false;
    }

    partial void OnPadInspectorDisplayChanged(PadInspectorDisplay? oldValue, PadInspectorDisplay newValue)
    {
        ArgumentNullException.ThrowIfNull(oldValue);
        if (oldValue.HasSummary != newValue.HasSummary)
        {
            OnPropertyChanged(nameof(HasPadInspectorSummary));
            OnPropertyChanged(nameof(HasNoPadInspectorSummary));
        }

        RaiseWhenChanged(oldValue.CompactHeaderText, newValue.CompactHeaderText, nameof(PadInspectorCompactHeaderText));
        RaiseWhenChanged(oldValue.PrimaryText, newValue.PrimaryText, nameof(PadInspectorPrimaryText));
        RaiseWhenChanged(oldValue.SecondaryText, newValue.SecondaryText, nameof(PadInspectorSecondaryText));
        RaiseWhenChanged(oldValue.MatchText, newValue.MatchText, nameof(PadInspectorMatchText));
        RaiseWhenChanged(oldValue.CompensationLines, newValue.CompensationLines, nameof(PadInspectorCompensationLines));
        RaiseWhenChanged(oldValue.RuleTraceLines, newValue.RuleTraceLines, nameof(PadInspectorRuleTraceLines));
        RaiseWhenChanged(oldValue.RuleTraceSections, newValue.RuleTraceSections, nameof(PadInspectorRuleTraceSections));
        RaiseWhenChanged(oldValue.HasRuleTrace, newValue.HasRuleTrace, nameof(HasPadInspectorRuleTrace));
    }

    private void RaiseWhenChanged<T>(T oldValue, T newValue, string propertyName)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            OnPropertyChanged(propertyName);
        }
    }

    private static ObservableCollection<PadInspectorDisplayLine> BuildCadCompensationLines(CadPadInspectorSnapshot cad)
    {
        PadInspectorCompensationValues? values = null;
        if (cad.Notch is not null)
        {
            var notchDisplay = NotchDisplayProjector.Build(cad.Notch);
            values = new PadInspectorCompensationValues(
                IsToFullEnabled: cad.Notch.IsToFullEnabled,
                ToFullValueText: notchDisplay.ToFullValueText,
                ToFullReasonFullText: notchDisplay.ToFullReasonFullText,
                ToRegularValueText: notchDisplay.ToRegularValueText,
                CombinedValueText: notchDisplay.CombinedValueText,
                HasCombinedOverflowRisk: notchDisplay.HasCombinedOverflowRisk);
        }

        return PadInspectorCompensationLineProjector.Build(cad.NotchRowSummary, values);
    }
}

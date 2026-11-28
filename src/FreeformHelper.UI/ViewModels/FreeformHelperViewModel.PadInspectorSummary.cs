using System.Collections.ObjectModel;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    /// <summary>Gets the pad inspector snapshot and refresh flag that the inspector views bind to.</summary>
    public PadInspectorUiState PadInspectorUi => Inspector.PadInspectorUi;

    private PadInspectorDisplay? BuildPadInspectorDisplay(PadInspectorSnapshot snapshot) =>
        PadInspectorDisplayProjector.BuildPadInspectorDisplay(snapshot, TryGetCadDisplayIndex, BuildCadCompensationLines);

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

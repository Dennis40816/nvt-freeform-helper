using System.Collections.ObjectModel;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Projects resolved compensation values into ordered pad inspector display lines.
/// </summary>
internal static class PadInspectorCompensationLineProjector
{
    internal static ObservableCollection<PadInspectorDisplayLine> Build(
        string notchRowSummary,
        PadInspectorCompensationValues? values)
    {
        var lines = new List<PadInspectorDisplayLine>();
        if (values is null)
        {
            lines.Add(new("Notch 2.2", "Unavailable", IsEmphasized: true, Tone: PadInspectorLineTone.Warning));
            lines.Add(new("Notch rows", notchRowSummary));
            return new ObservableCollection<PadInspectorDisplayLine>(lines);
        }

        var toFullTone = values.IsToFullEnabled ? PadInspectorLineTone.Key : PadInspectorLineTone.Warning;
        var reasonTone = values.IsToFullEnabled ? PadInspectorLineTone.Key : PadInspectorLineTone.Warning;

        lines.Add(new(
            "To Full",
            values.ToFullValueText,
            IsEmphasized: true,
            Tone: toFullTone));
        if (!string.IsNullOrWhiteSpace(values.ToFullReasonFullText))
        {
            lines.Add(new("Reason", values.ToFullReasonFullText, IsEmphasized: true, Tone: reasonTone));
        }

        if (PadInspectorTraceSectionProjector.TryExtractNotchFreeformDirection(notchRowSummary, out var freeformDirection))
        {
            lines.Add(new(
                "Freeform",
                freeformDirection.ToString(),
                IsEmphasized: true,
                Tone: PadInspectorLineTone.Key,
                FreeformDirection: PadInspectorTraceSectionProjector.ToFreeformDirection(freeformDirection)));
        }

        lines.Add(new(
            "To Regular",
            values.ToRegularValueText));
        lines.Add(new(
            "Combined",
            values.CombinedValueText,
            IsEmphasized: values.HasCombinedOverflowRisk,
            Tone: values.HasCombinedOverflowRisk ? PadInspectorLineTone.Warning : PadInspectorLineTone.Normal));
        lines.Add(new("Notch rows", notchRowSummary));

        return new ObservableCollection<PadInspectorDisplayLine>(lines);
    }
}

internal sealed record PadInspectorCompensationValues(
    bool IsToFullEnabled,
    string ToFullValueText,
    string ToFullReasonFullText,
    string ToRegularValueText,
    string CombinedValueText,
    bool HasCombinedOverflowRisk);

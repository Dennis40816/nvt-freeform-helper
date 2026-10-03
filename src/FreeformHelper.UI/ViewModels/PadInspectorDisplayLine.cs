namespace FreeformHelper.UI.ViewModels;

public enum PadInspectorLineTone
{
    Normal,
    Key,
    Warning,
}

public enum PadInspectorFreeformDirection
{
    None,
    XWay,
    YWay,
    XYWay,
}

public sealed record PadInspectorDisplayLine(
    string Label,
    string Value,
    bool IsEmphasized = false,
    PadInspectorLineTone Tone = PadInspectorLineTone.Normal,
    PadInspectorFreeformDirection FreeformDirection = PadInspectorFreeformDirection.None)
{
    public bool HasLabel => !string.IsNullOrWhiteSpace(Label);
    public bool HasNoLabel => !HasLabel;
    public string ValueFontWeight =>
        IsEmphasized || Tone is PadInspectorLineTone.Key or PadInspectorLineTone.Warning
            ? "SemiBold"
            : "Normal";
    public bool IsToneKey => Tone == PadInspectorLineTone.Key;
    public bool IsToneWarning => Tone == PadInspectorLineTone.Warning;
    public bool HasFreeformDirectionMarker => FreeformDirection != PadInspectorFreeformDirection.None;
    public bool ShowFreeformXSlash =>
        FreeformDirection is PadInspectorFreeformDirection.XWay or PadInspectorFreeformDirection.XYWay;
    public bool ShowFreeformYSlash =>
        FreeformDirection is PadInspectorFreeformDirection.YWay or PadInspectorFreeformDirection.XYWay;
    public string DisplayText => HasLabel ? $"{Label}: {Value}" : Value;
}

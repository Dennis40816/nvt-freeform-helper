namespace FreeformHelper.UI.ViewModels;

public sealed record PadInspectorRuleTraceSection(
    string Title,
    IReadOnlyList<PadInspectorDisplayLine> Lines)
{
    public bool IsRawSection => string.Equals(Title, "Debug raw", System.StringComparison.Ordinal);
    public bool IsNonRawSection => !IsRawSection;
}

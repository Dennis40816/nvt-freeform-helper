namespace FreeformHelper.UI.Logging;

/// <summary>
/// Represents a clickable link span inside console text.
/// </summary>
public sealed record ConsoleLinkSpan(
    int StartOffset,
    int Length,
    string Target,
    bool IsUrl,
    int Line = 0,
    int Column = 0)
{
    public int EndOffset => StartOffset + Length;
}


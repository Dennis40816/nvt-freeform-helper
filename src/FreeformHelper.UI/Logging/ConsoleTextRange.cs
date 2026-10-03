namespace FreeformHelper.UI.Logging;

/// <summary>
/// Represents a text range in console-rendered text.
/// </summary>
public readonly record struct ConsoleTextRange(int StartOffset, int Length)
{
    public int EndOffset => StartOffset + Length;
}


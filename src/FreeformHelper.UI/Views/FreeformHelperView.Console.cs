using System.Collections.Specialized;
using Avalonia.Input;
using AvaloniaEdit.Rendering;
using FreeformHelper.UI.Logging;
using NLog;

namespace FreeformHelper.UI.Views;

/// <summary>
/// Console state container for <see cref="FreeformHelperView"/>.
/// </summary>
public sealed partial class FreeformHelperView
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private INotifyCollectionChanged? _logEntries;
    private ConsoleBaseTextColorizer? _consoleBaseTextColorizer;
    private ConsoleLinkColorizer? _consoleLinkColorizer;
    private ConsoleSearchHighlightColorizer? _consoleSearchHighlightColorizer;
    private IReadOnlyList<ConsoleLinkSpan> _consoleLinks = Array.Empty<ConsoleLinkSpan>();
    private IReadOnlyList<ConsoleTextRange> _consoleSearchRanges = Array.Empty<ConsoleTextRange>();
    private string _consoleSourceText = string.Empty;
    private string? _consoleRepoRoot;
    private ConsoleLinkSpan? _consoleHoveredLink;
    private ConsoleLinkSpan? _consolePressedLink;
    private bool _consoleAutoFollow = true;
    private TextView? _consoleTextView;
    private static readonly Cursor ConsoleTextCursor = new(StandardCursorType.Ibeam);
    private static readonly Cursor ConsoleLinkCursor = new(StandardCursorType.Hand);
}

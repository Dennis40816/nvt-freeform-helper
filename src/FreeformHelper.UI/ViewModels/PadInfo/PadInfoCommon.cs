using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.ViewModels;

internal static class PadInfoUnits
{
    internal const string SquareMillimeter = "mm²";
}

internal static class PadInspectorRuleTraceFormatter
{
    internal static string Format(PadInspectorRuleTraceEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Detail))
        {
            return $"{entry.Rule}  {entry.Outcome}";
        }

        return $"{entry.Rule}  {entry.Outcome}{Environment.NewLine}{entry.Detail}";
    }
}

public sealed class NotchOwnerLinkViewModel
{
    public NotchOwnerLinkViewModel(string text, IRelayCommand highlightCommand)
    {
        Text = text;
        HighlightCommand = highlightCommand;
    }

    public string Text { get; }

    public IRelayCommand HighlightCommand { get; }
}

public sealed class PadInfoRelationCardViewModel
{
    public PadInfoRelationCardViewModel(
        string titleText,
        string roleText,
        string metaText,
        string areaText,
        string ratioText,
        string rawText,
        IRelayCommand? highlightCommand)
    {
        TitleText = titleText;
        RoleText = roleText;
        MetaText = metaText;
        AreaText = string.IsNullOrWhiteSpace(areaText) ? "-" : areaText;
        RatioText = string.IsNullOrWhiteSpace(ratioText) ? "-" : ratioText;
        RawText = rawText;
        HighlightCommand = highlightCommand;
    }

    public string TitleText { get; }

    public string RoleText { get; }

    public string MetaText { get; }

    public string AreaText { get; }

    public string RatioText { get; }

    public string RawText { get; }

    public IRelayCommand? HighlightCommand { get; }

    public bool HasHighlightCommand => HighlightCommand is not null;
}

/// <summary>
/// Defines an interface for ViewModels that track pending changes,
/// typically used for user-editable information that can be applied or discarded.
/// </summary>
public interface IPadInfoChangeTracking
{
    /// <summary>
    /// Gets a value indicating whether there are any pending changes that need to be applied.
    /// </summary>
    bool HasPendingChanges { get; }
    /// <summary>
    /// Gets the command to apply pending changes.
    /// </summary>
    ICommand ApplyChangesCommand { get; }
    /// <summary>
    /// Gets the command to discard pending changes.
    /// </summary>
    ICommand DiscardChangesCommand { get; }
}

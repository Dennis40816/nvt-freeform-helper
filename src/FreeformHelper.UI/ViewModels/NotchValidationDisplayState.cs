using System.Collections.ObjectModel;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// The Step 6 validation quick trace display: the summary text and the four row collections.
/// The view model stores one value and replaces it member by member.
/// </summary>
internal sealed record NotchValidationDisplayState(
    string SummaryText,
    ObservableCollection<NotchValidationDisplayItem> Items,
    ObservableCollection<NotchValidationDisplayItem> DirectItems,
    ObservableCollection<NotchValidationDisplayItem> IncomingItems,
    ObservableCollection<NotchValidationDisplayItem> OutgoingItems)
{
    /// <summary>
    /// The summary text before the first analyze or after a clear.
    /// </summary>
    public const string HintSummaryText = "Validation: select REG, then run Analyze.";

    /// <summary>
    /// Creates the display that the view model shows before the first analyze. The collections are
    /// new instances on every call because they are mutable.
    /// </summary>
    public static NotchValidationDisplayState CreateInitial()
    {
        return new NotchValidationDisplayState(
            HintSummaryText,
            new ObservableCollection<NotchValidationDisplayItem>(),
            new ObservableCollection<NotchValidationDisplayItem>(),
            new ObservableCollection<NotchValidationDisplayItem>(),
            new ObservableCollection<NotchValidationDisplayItem>());
    }
}

using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed class PadInfoTargetAllocationViewModel
{
    public PadInfoTargetAllocationViewModel(
        string roleText,
        string targetText,
        string diffText,
        string areaText,
        string ratioText,
        string rawText,
        IReadOnlyList<int> regularPadIds,
        Action<IReadOnlyCollection<int>>? highlightRegularPads)
    {
        RoleText = roleText;
        TargetText = targetText;
        DiffText = diffText;
        AreaText = string.IsNullOrWhiteSpace(areaText) ? "-" : areaText;
        RatioText = string.IsNullOrWhiteSpace(ratioText) ? "-" : ratioText;
        RawText = rawText;
        RegularPadIds = regularPadIds;
        HighlightCommand = highlightRegularPads is null || regularPadIds.Count == 0
            ? null
            : new RelayCommand(() => highlightRegularPads(regularPadIds));
    }

    public string RoleText { get; }

    public string TargetText { get; }

    public string TitleText => TargetText;

    public string DiffText { get; }

    public string MetaText => DiffText;

    public string AreaText { get; }

    public string RatioText { get; }

    public string RawText { get; }

    public IReadOnlyList<int> RegularPadIds { get; }

    public ICommand? HighlightCommand { get; }

    public bool HasHighlightCommand => HighlightCommand is not null;

    internal static PadInfoTargetAllocationViewModel FromDisplayItem(
        NotchDisplayTargetItem item,
        Action<IReadOnlyCollection<int>>? highlightRegularPads)
    {
        return new PadInfoTargetAllocationViewModel(
            item.RoleText,
            item.TargetText,
            item.DiffText,
            item.AreaText,
            item.RatioText,
            item.RawText,
            item.RegularPadIds,
            highlightRegularPads);
    }
}

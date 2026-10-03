using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class CadPadInfoViewModel : ObservableObject, IPadInfoChangeTracking
{
    private static void BuildNotchOwnerLinks(
        IReadOnlyList<NotchToFullDiagnosticEntry> diagnosticEntries,
        int targetCadPadId,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads,
        List<NotchOwnerLinkViewModel> ownerLinks,
        List<int> allOwnerCadIds)
    {
        if (highlightCadOwnerPads is null || diagnosticEntries.Count == 0)
        {
            return;
        }

        var ownerUsageCount = new Dictionary<int, int>();
        foreach (var parsed in diagnosticEntries)
        {
            foreach (var ownerCadId in parsed.OwnerCadPadIds)
            {
                if (ownerCadId == targetCadPadId)
                {
                    continue;
                }

                if (ownerUsageCount.TryGetValue(ownerCadId, out var count))
                {
                    ownerUsageCount[ownerCadId] = count + 1;
                }
                else
                {
                    ownerUsageCount[ownerCadId] = 1;
                }
            }
        }

        if (ownerUsageCount.Count == 0)
        {
            return;
        }

        foreach (var ownerCadId in ownerUsageCount.Keys.OrderBy(static id => id))
        {
            allOwnerCadIds.Add(ownerCadId);
        }

        const int maxOwnerLinks = 20;
        foreach (var pair in ownerUsageCount
                     .OrderByDescending(static pair => pair.Value)
                     .ThenBy(static pair => pair.Key)
                     .Take(maxOwnerLinks))
        {
            var ownerCadId = pair.Key;
            var countText = pair.Value <= 1
                ? "1 reg"
                : $"{pair.Value} regs";
            ownerLinks.Add(new NotchOwnerLinkViewModel(
                $"CAD {ownerCadId} ({countText})",
                new RelayCommand(() => highlightCadOwnerPads(new[] { ownerCadId }))));
        }
    }
}

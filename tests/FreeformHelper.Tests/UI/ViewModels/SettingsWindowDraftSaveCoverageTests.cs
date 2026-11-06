// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Reflection;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Save writes back only the settings the window edited. The draft tells the owner which settings these are
/// by property name, so a rename on one side would silently stop Save from writing that setting. This theory
/// edits every on/off and number setting of the draft one at a time and checks that Save writes it.
/// Three kinds of draft property are left out. The section navigation flags of the window are not settings.
/// The anchor CAD id is accepted only when a CAD pad with that id exists. The X and Y channel totals follow
/// the per-IC rows, which Save writes as one cascade layout.
/// </summary>
public sealed class SettingsWindowDraftSaveCoverageTests
{
    private static readonly string[] _notEditedAlone =
    [
        "CadOutputFwDiffIndexAnchorCadId",
        nameof(SettingsWindowViewModel.XChannels),
        nameof(SettingsWindowViewModel.YChannels),
    ];

    public static IEnumerable<object[]> EditableDraftSettings() =>
        typeof(SettingsWindowViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static property => property.PropertyType == typeof(bool) || property.PropertyType == typeof(decimal))
            .Where(static property => property.GetMethod is { IsPublic: true } && property.SetMethod is { IsPublic: true })
            .Where(static property => !property.Name.EndsWith("SectionSelected", StringComparison.Ordinal))
            .Where(static property => !_notEditedAlone.Contains(property.Name))
            .Select(static property => new object[] { property.Name });

    [Theory]
    [MemberData(nameof(EditableDraftSettings))]
    public void Save_WritesTheSettingTheWindowEdited(string name)
    {
        var draftProperty = typeof(SettingsWindowViewModel).GetProperty(name)!;
        var ownerProperty = typeof(FreeformHelperViewModel).GetProperty(name);
        Assert.True(ownerProperty is not null, $"FreeformHelperViewModel has no public setting named {name}.");

        // The window and the owner may limit a number to different ranges. Try a step down, then a step up.
        var outcomes = new List<string>();
        foreach (var step in new[] { -1m, 1m })
        {
            var vm = new FreeformHelperViewModel();
            var draft = vm.CreateSettingsWindowViewModel();
            var opening = draftProperty.GetValue(draft)!;
            draftProperty.SetValue(draft, opening is bool flag ? !flag : (decimal)opening + step);
            var edited = draftProperty.GetValue(draft)!;
            if (Equals(edited, opening))
            {
                outcomes.Add($"step {step}: the draft kept {opening}");
                continue;
            }

            draft.SaveCommand.Execute(null);
            var saved = ownerProperty.GetValue(vm);
            if (Equals(edited, saved))
            {
                return;
            }

            outcomes.Add($"step {step}: draft {edited}, owner {saved}");
        }

        Assert.Fail($"Save did not write {name}. {string.Join("; ", outcomes)}");
    }
}

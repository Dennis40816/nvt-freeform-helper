// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsWindowSectionBehaviorTests
{
    [Theory]
    [InlineData(SettingsWindowSection.General, "General",
        "Project layout, DXF import, display, and generated regular grid settings.")]
    [InlineData(SettingsWindowSection.Step1GeometryMatch, "Step 1 - Geometry match",
        "Overlap diagnostics and unmatched highlighting.")]
    [InlineData(SettingsWindowSection.Step2FreeformTagging, "Step 2 - Freeform tagging",
        "Freeform classification and project-load replay behavior.")]
    [InlineData(SettingsWindowSection.Step3NotchCompensation, "Step 3 - Notch compensation",
        "Compensation model, allocation semantics, and preview diagnostics.")]
    [InlineData(SettingsWindowSection.Step4IndexDiagnostics, "Step 4 - Index diagnostics",
        "FW output diff visibility and mapping confidence thresholds.")]
    [InlineData(SettingsWindowSection.Step5NotchExport, "Step 5 - Notch export",
        "Export versions, file type, gate thresholds, and handoff safety.")]
    public void SectionToggle_SelectsItsPageAndIgnoresDeselection(
        SettingsWindowSection section, string title, string description)
    {
        var draft = new FreeformHelperViewModel().CreateSettingsWindowViewModel();
        draft.SelectedSection = section == SettingsWindowSection.General
            ? SettingsWindowSection.Step5NotchExport
            : SettingsWindowSection.General;
        var changes = new List<string?>();
        draft.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        SelectSection(draft, section, true);

        Assert.Equal(section, draft.SelectedSection);
        Assert.Equal(title, draft.SettingsSelectedSectionTitle);
        Assert.Equal(description, draft.SettingsSelectedSectionDescription);
        Assert.Equal(
            new[]
            {
                section == SettingsWindowSection.General,
                section == SettingsWindowSection.Step1GeometryMatch,
                section == SettingsWindowSection.Step2FreeformTagging,
                section == SettingsWindowSection.Step3NotchCompensation,
                section == SettingsWindowSection.Step4IndexDiagnostics,
                section == SettingsWindowSection.Step5NotchExport,
            },
            new[]
            {
                draft.IsGeneralSectionSelected, draft.IsStep1SectionSelected, draft.IsStep2SectionSelected,
                draft.IsStep3SectionSelected, draft.IsStep4SectionSelected, draft.IsStep5SectionSelected,
            });
        Assert.Contains(nameof(draft.SettingsSelectedSectionTitle), changes);
        Assert.Contains(nameof(draft.SettingsSelectedSectionDescription), changes);
        changes.Clear();

        SelectSection(draft, section, false);

        Assert.Equal(section, draft.SelectedSection);
        Assert.Equal(title, draft.SettingsSelectedSectionTitle);
        Assert.Empty(changes);
    }

    private static void SelectSection(SettingsWindowViewModel draft, SettingsWindowSection section, bool selected)
    {
        switch (section)
        {
            case SettingsWindowSection.General: draft.IsGeneralSectionSelected = selected; break;
            case SettingsWindowSection.Step1GeometryMatch: draft.IsStep1SectionSelected = selected; break;
            case SettingsWindowSection.Step2FreeformTagging: draft.IsStep2SectionSelected = selected; break;
            case SettingsWindowSection.Step3NotchCompensation: draft.IsStep3SectionSelected = selected; break;
            case SettingsWindowSection.Step4IndexDiagnostics: draft.IsStep4SectionSelected = selected; break;
            case SettingsWindowSection.Step5NotchExport: draft.IsStep5SectionSelected = selected; break;
            default: throw new ArgumentOutOfRangeException(nameof(section));
        }
    }
}

using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SettingsChangePolicyContractTests
{
    [Fact]
    public void PublicEnums_RetainPublishedNamesAndValues()
    {
        Assert.Equal(
            new[]
            {
                (nameof(SettingsChangeKind.Step2Detection), 0),
                (nameof(SettingsChangeKind.Step3Notch), 1),
                (nameof(SettingsChangeKind.Step3NotchWithOverlay), 2),
                (nameof(SettingsChangeKind.Step4Indexing), 3),
                (nameof(SettingsChangeKind.GridRebuild), 4),
                (nameof(SettingsChangeKind.GridRebuildWithFit), 5),
                (nameof(SettingsChangeKind.RecalcBoundsOnLayerFilter), 6),
                (nameof(SettingsChangeKind.LayoutRefresh), 7),
                (nameof(SettingsChangeKind.SettingsWindowProjectApply), 8),
                (nameof(SettingsChangeKind.SettingsWindowAppGeneralApply), 9),
            },
            Enum.GetValues<SettingsChangeKind>().Select(static value => (value.ToString(), (int)value)));

        Assert.Equal(
            new[]
            {
                (nameof(SettingsChangeEffects.None), 0),
                (nameof(SettingsChangeEffects.MarkUnsaved), 1 << 0),
                (nameof(SettingsChangeEffects.TriggerGridRebuild), 1 << 1),
                (nameof(SettingsChangeEffects.TriggerGridRebuildWithFit), 1 << 2),
                (nameof(SettingsChangeEffects.InvalidateStep2), 1 << 3),
                (nameof(SettingsChangeEffects.InvalidateStep3), 1 << 4),
                (nameof(SettingsChangeEffects.InvalidateStep4), 1 << 5),
                (nameof(SettingsChangeEffects.InvalidateNotchCompensationCache), 1 << 6),
                (nameof(SettingsChangeEffects.RefreshNotchCanvasPreview), 1 << 7),
                (nameof(SettingsChangeEffects.NotifyNotchOverlayVisibility), 1 << 8),
                (nameof(SettingsChangeEffects.UpdateCadOutputFwDiffIndexing), 1 << 9),
                (nameof(SettingsChangeEffects.RebuildCascadeSettings), 1 << 10),
                (nameof(SettingsChangeEffects.RefreshDxfRegularSourceHint), 1 << 11),
                (nameof(SettingsChangeEffects.PersistAppGeneralNow), 1 << 12),
            },
            Enum.GetValues<SettingsChangeEffects>().Select(static value => (value.ToString(), (int)value)));
    }
}

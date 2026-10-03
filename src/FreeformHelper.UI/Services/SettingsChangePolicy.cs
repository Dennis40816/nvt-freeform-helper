namespace FreeformHelper.UI.Services;

[Flags]
public enum SettingsChangeEffects
{
    None = 0,
    MarkUnsaved = 1 << 0,
    TriggerGridRebuild = 1 << 1,
    TriggerGridRebuildWithFit = 1 << 2,
    InvalidateStep2 = 1 << 3,
    InvalidateStep3 = 1 << 4,
    InvalidateStep4 = 1 << 5,
    InvalidateNotchCompensationCache = 1 << 6,
    RefreshNotchCanvasPreview = 1 << 7,
    NotifyNotchOverlayVisibility = 1 << 8,
    UpdateCadOutputFwDiffIndexing = 1 << 9,
    RebuildCascadeSettings = 1 << 10,
    RefreshDxfRegularSourceHint = 1 << 11,
    PersistAppGeneralNow = 1 << 12,
}

public enum SettingsChangeKind
{
    Step2Detection,
    Step3Notch,
    Step3NotchWithOverlay,
    Step4Indexing,
    GridRebuild,
    GridRebuildWithFit,
    RecalcBoundsOnLayerFilter,
    LayoutRefresh,
    SettingsWindowProjectApply,
    SettingsWindowAppGeneralApply,
}

/// <summary>
/// Maps settings-change categories to normalized side-effects.
/// </summary>
public sealed class SettingsChangePolicy
{
    public static SettingsChangeEffects Resolve(SettingsChangeKind kind)
    {
        return kind switch
        {
            SettingsChangeKind.Step2Detection =>
                SettingsChangeEffects.InvalidateStep2 |
                SettingsChangeEffects.MarkUnsaved,

            SettingsChangeKind.Step3Notch =>
                SettingsChangeEffects.InvalidateNotchCompensationCache |
                SettingsChangeEffects.InvalidateStep3 |
                SettingsChangeEffects.RefreshNotchCanvasPreview,

            SettingsChangeKind.Step3NotchWithOverlay =>
                Resolve(SettingsChangeKind.Step3Notch) |
                SettingsChangeEffects.NotifyNotchOverlayVisibility,

            SettingsChangeKind.Step4Indexing =>
                SettingsChangeEffects.UpdateCadOutputFwDiffIndexing |
                SettingsChangeEffects.InvalidateStep4,

            SettingsChangeKind.GridRebuild =>
                SettingsChangeEffects.TriggerGridRebuild,

            SettingsChangeKind.GridRebuildWithFit =>
                SettingsChangeEffects.TriggerGridRebuildWithFit,

            SettingsChangeKind.RecalcBoundsOnLayerFilter =>
                SettingsChangeEffects.MarkUnsaved |
                SettingsChangeEffects.TriggerGridRebuildWithFit,

            SettingsChangeKind.LayoutRefresh =>
                SettingsChangeEffects.RebuildCascadeSettings |
                SettingsChangeEffects.RefreshDxfRegularSourceHint,

            SettingsChangeKind.SettingsWindowProjectApply =>
                SettingsChangeEffects.MarkUnsaved |
                SettingsChangeEffects.PersistAppGeneralNow,

            SettingsChangeKind.SettingsWindowAppGeneralApply =>
                SettingsChangeEffects.PersistAppGeneralNow,

            _ => SettingsChangeEffects.None,
        };
    }
}

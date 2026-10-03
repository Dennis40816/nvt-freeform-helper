using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void ApplySettingsChange(SettingsChangeKind kind, bool showStatus = false, SettingsChangeEffects suppress = SettingsChangeEffects.None)
    {
        var effects = SettingsChangePolicy.Resolve(kind) & ~suppress;
        ApplySettingsChangeEffects(effects, showStatus);
    }

    private void ApplySettingsChangeEffects(
        SettingsChangeEffects effects,
        bool showStatus = false,
        bool invalidateNotchFinalProjection = false)
    {
        if (effects == SettingsChangeEffects.None && !invalidateNotchFinalProjection)
        {
            return;
        }

        if (effects.HasFlag(SettingsChangeEffects.RebuildCascadeSettings))
        {
            RebuildCascadeSettings();
        }

        if (effects.HasFlag(SettingsChangeEffects.RefreshDxfRegularSourceHint))
        {
            RefreshDxfRegularSourceHint();
        }

        if (effects.HasFlag(SettingsChangeEffects.UpdateCadOutputFwDiffIndexing))
        {
            UpdateCadOutputFwDiffIndexing(CadPads.ToList());
            NotifySimulationWorkspaceSourceChanged();
        }

        if (effects.HasFlag(SettingsChangeEffects.InvalidateNotchCompensationCache))
        {
            InvalidateNotchCompensationCache();
        }

        if (invalidateNotchFinalProjection)
        {
            ClearStep5ProjectedResultCore();
            if (!effects.HasFlag(SettingsChangeEffects.InvalidateNotchCompensationCache))
            {
                NotifySimulationWorkspaceSourceChanged();
            }
        }

        if (effects.HasFlag(SettingsChangeEffects.InvalidateStep2))
        {
            InvalidateDownstreamFromStep2(showStatus: showStatus);
        }

        if (effects.HasFlag(SettingsChangeEffects.InvalidateStep3))
        {
            InvalidateDownstreamFromStep3(showStatus: showStatus);
        }

        if (effects.HasFlag(SettingsChangeEffects.InvalidateStep4))
        {
            InvalidateDownstreamFromStep4(showStatus: showStatus);
        }

        if (effects.HasFlag(SettingsChangeEffects.NotifyNotchOverlayVisibility))
        {
            NotifyNotchOverlayVisibilityChanged();
        }

        if (effects.HasFlag(SettingsChangeEffects.RefreshNotchCanvasPreview))
        {
            RefreshNotchCanvasPreviewAfterStep3SettingsChanged();
        }

        if (effects.HasFlag(SettingsChangeEffects.MarkUnsaved))
        {
            MarkUnsaved();
        }

        if (effects.HasFlag(SettingsChangeEffects.PersistAppGeneralNow))
        {
            PersistAppGeneralSettingsNow();
        }

        if (effects.HasFlag(SettingsChangeEffects.TriggerGridRebuildWithFit))
        {
            _ = TriggerGridRebuildAsync(requestFit: true);
            return;
        }

        if (effects.HasFlag(SettingsChangeEffects.TriggerGridRebuild))
        {
            _ = TriggerGridRebuildAsync(requestFit: false);
        }
    }
}

// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.Contracts.Settings;

namespace FreeformHelper.UI.Services;

/// <summary>Resolves one apply plan and executes its effects in the established order.</summary>
internal sealed class SettingsEffectCoordinator(ISettingsEffectHost host)
{
    private readonly ISettingsEffectHost _host = host;

    internal void ApplySettingsOrchestration(string changedPropertyName)
    {
        ApplySettingsOrchestration([changedPropertyName], includeSettingsWindowPersistence: false);
    }

    internal void ApplySettingsWindowOrchestration(IReadOnlyCollection<string> changedPropertyNames)
    {
        ApplySettingsOrchestration(changedPropertyNames, includeSettingsWindowPersistence: true);
    }

    private void ApplySettingsOrchestration(
        IReadOnlyCollection<string> changedPropertyNames,
        bool includeSettingsWindowPersistence)
    {
        ArgumentNullException.ThrowIfNull(changedPropertyNames);
        if (changedPropertyNames.Count == 0)
        {
            return;
        }

        var plan = SettingsApplyPlanBuilder.Resolve(
            changedPropertyNames,
            new SettingsChangeContext(
                _host.IsPanelAlignment,
                _host.IsDxfLayerRegularSource));

        ExecuteSettingsOrchestrationActions(plan.Actions);

        var effects = plan.Effects;
        if (includeSettingsWindowPersistence)
        {
            effects |= ResolveSettingsWindowPersistenceEffects(changedPropertyNames);
        }

        ApplySettingsChangeEffects(effects, invalidateNotchFinalProjection: plan.InvalidateNotchFinalProjection);
    }

    private void ExecuteSettingsOrchestrationActions(SettingsOrchestrationActions actions)
    {
        if (actions.HasFlag(SettingsOrchestrationActions.NotifyIsPanelAlignment))
        {
            _host.NotifyPropertyChanged(nameof(_host.IsPanelAlignment));
        }

        if (actions.HasFlag(SettingsOrchestrationActions.NotifyIsDxfLayerRegularSource))
        {
            _host.NotifyPropertyChanged(nameof(_host.IsDxfLayerRegularSource));
        }

        if (actions.HasFlag(SettingsOrchestrationActions.ApplyRegularSourceLayerAutoHidePolicy))
        {
            _host.ApplyRegularSourceLayerAutoHidePolicy();
        }

    }

    private static SettingsChangeEffects ResolveSettingsWindowPersistenceEffects(
        IReadOnlyCollection<string> changedPropertyNames)
    {
        var hasProjectOrSnapshotChange = changedPropertyNames.Any(static propertyName =>
            !string.Equals(
                propertyName,
                "ApplyAppVisualPreferencesOnProjectLoad",
                StringComparison.Ordinal));

        return hasProjectOrSnapshotChange
            ? SettingsChangePolicy.Resolve(SettingsChangeKind.SettingsWindowProjectApply)
            : SettingsChangePolicy.Resolve(SettingsChangeKind.SettingsWindowAppGeneralApply);
    }

    internal bool TryCommitAnchorFromCurrentValue()
    {
        if (_host.CadOutputFwDiffIndexAnchorCadId < 0)
        {
            _host.ClearFwDiffAnchors();
            _host.SetStatus("CAD Output FW Diff anchor cleared for all ICs.");
            return true;
        }

        var cadId = _host.ClampToInt(_host.CadOutputFwDiffIndexAnchorCadId, 0, 2_000_000_000);
        if (!_host.TryGetCadIcIndex(cadId, out var icIndex))
        {
            using (_host.EnterDxfIndexAnchorChange())
            {
                using (_host.EnterUndoSuppression())
                {
                    _host.CadOutputFwDiffIndexAnchorCadId = -1;
                }
            }

            _host.SetStatus($"CAD Output FW Diff anchor ignored: CAD {cadId} is not visible.");
            return false;
        }

        _host.SaveFwDiffAnchor(icIndex, cadId);
        _host.SetStatus($"CAD Output FW Diff anchor saved: IC {icIndex + 1} -> CAD {cadId}.");
        return true;
    }

    internal void ApplySettingsChangeEffects(
        SettingsChangeEffects effects,
        bool showStatus = false,
        bool invalidateNotchFinalProjection = false)
    {
        if (effects == SettingsChangeEffects.None && !invalidateNotchFinalProjection)
        {
            return;
        }

        ApplySettingsSourceRefreshEffects(effects, invalidateNotchFinalProjection);

        ApplySettingsDownstreamEffects(effects, showStatus);

        if (effects.HasFlag(SettingsChangeEffects.TriggerGridRebuildWithFit))
        {
            _ = _host.TriggerGridRebuildAsync(requestFit: true);
            return;
        }

        if (effects.HasFlag(SettingsChangeEffects.TriggerGridRebuild))
        {
            _ = _host.TriggerGridRebuildAsync(requestFit: false);
        }
    }

    private void ApplySettingsSourceRefreshEffects(SettingsChangeEffects effects, bool invalidateNotchFinalProjection)
    {
        if (effects.HasFlag(SettingsChangeEffects.RebuildCascadeSettings))
        {
            _host.RebuildCascadeSettings();
        }

        if (effects.HasFlag(SettingsChangeEffects.RefreshDxfRegularSourceHint))
        {
            _host.RefreshDxfRegularSourceHint();
        }

        if (effects.HasFlag(SettingsChangeEffects.UpdateCadOutputFwDiffIndexing))
        {
            _host.UpdateCadOutputFwDiffIndexing();
            _host.NotifySimulationWorkspaceSourceChanged();
        }

        if (effects.HasFlag(SettingsChangeEffects.InvalidateNotchCompensationCache))
        {
            _host.InvalidateNotchCompensationCache();
        }

        if (invalidateNotchFinalProjection)
        {
            _host.ClearStep5ProjectedResultCore();
            if (!effects.HasFlag(SettingsChangeEffects.InvalidateNotchCompensationCache))
            {
                _host.NotifySimulationWorkspaceSourceChanged();
            }
        }
    }

    private void ApplySettingsDownstreamEffects(SettingsChangeEffects effects, bool showStatus)
    {
        if (effects.HasFlag(SettingsChangeEffects.InvalidateStep2))
        {
            _host.InvalidateDownstreamFromStep2(showStatus: showStatus);
        }

        if (effects.HasFlag(SettingsChangeEffects.InvalidateStep3))
        {
            _host.InvalidateDownstreamFromStep3(showStatus: showStatus);
        }

        if (effects.HasFlag(SettingsChangeEffects.InvalidateStep4))
        {
            _host.InvalidateDownstreamFromStep4(showStatus: showStatus);
        }

        if (effects.HasFlag(SettingsChangeEffects.NotifyNotchOverlayVisibility))
        {
            _host.NotifyNotchOverlayVisibilityChanged();
        }

        if (effects.HasFlag(SettingsChangeEffects.RefreshNotchCanvasPreview))
        {
            _host.RefreshNotchCanvasPreviewAfterStep3SettingsChanged();
        }

        if (effects.HasFlag(SettingsChangeEffects.MarkUnsaved))
        {
            _host.MarkUnsaved();
        }

        if (effects.HasFlag(SettingsChangeEffects.PersistAppGeneralNow))
        {
            _host.PersistAppGeneralSettingsNow();
        }
    }
}

using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void ApplySettingsOrchestration(string changedPropertyName)
    {
        ApplySettingsOrchestration([changedPropertyName], includeSettingsWindowPersistence: false);
    }

    private void ApplySettingsWindowOrchestration(IReadOnlyCollection<string> changedPropertyNames)
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
                IsPanelAlignment,
                IsDxfLayerRegularSource));

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
            OnPropertyChanged(nameof(IsPanelAlignment));
        }

        if (actions.HasFlag(SettingsOrchestrationActions.NotifyIsDxfLayerRegularSource))
        {
            OnPropertyChanged(nameof(IsDxfLayerRegularSource));
        }

        if (actions.HasFlag(SettingsOrchestrationActions.ApplyRegularSourceLayerAutoHidePolicy))
        {
            ApplyRegularSourceLayerAutoHidePolicy();
        }

    }

    private static SettingsChangeEffects ResolveSettingsWindowPersistenceEffects(
        IReadOnlyCollection<string> changedPropertyNames)
    {
        var hasProjectOrSnapshotChange = changedPropertyNames.Any(static propertyName =>
            !string.Equals(
                propertyName,
                nameof(ApplyAppVisualPreferencesOnProjectLoad),
                StringComparison.Ordinal));

        return hasProjectOrSnapshotChange
            ? SettingsChangePolicy.Resolve(SettingsChangeKind.SettingsWindowProjectApply)
            : SettingsChangePolicy.Resolve(SettingsChangeKind.SettingsWindowAppGeneralApply);
    }

    private bool TryCommitCadOutputFwDiffIndexAnchorCadIdFromCurrentValue()
    {
        if (CadOutputFwDiffIndexAnchorCadId < 0)
        {
            _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc.Clear();
            _projectFile.CadOutputFwDiffIndexAnchorCadPadId = null;
            SetStatus("CAD Output FW Diff anchor cleared for all ICs.");
            return true;
        }

        var cadId = ClampToInt(CadOutputFwDiffIndexAnchorCadId, 0, 2_000_000_000);
        if (!_cadIcIndexByCadId.TryGetValue(cadId, out var icIndex))
        {
            _suppressDxfIndexAnchorChange = true;
            try
            {
                using (_undoSuppression.Enter())
                {
                    CadOutputFwDiffIndexAnchorCadId = -1;
                }
            }
            finally
            {
                _suppressDxfIndexAnchorChange = false;
            }

            SetStatus($"CAD Output FW Diff anchor ignored: CAD {cadId} is not visible.");
            return false;
        }

        _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc[icIndex] = cadId;
        _projectFile.CadOutputFwDiffIndexAnchorCadPadId = null;
        SetStatus($"CAD Output FW Diff anchor saved: IC {icIndex + 1} -> CAD {cadId}.");
        return true;
    }
}

using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

[Flags]
internal enum SettingsOrchestrationActions
{
    None = 0,
    NotifyIsPanelAlignment = 1 << 0,
    NotifyIsDxfLayerRegularSource = 1 << 1,
    ApplyRegularSourceLayerAutoHidePolicy = 1 << 2,
}

internal readonly record struct SettingsChangeContext(
    bool IsPanelAlignment,
    bool IsDxfLayerRegularSource);

internal readonly record struct SettingsApplyPlan(
    SettingsChangeEffects Effects,
    bool InvalidateNotchFinalProjection,
    SettingsOrchestrationActions Actions);

internal static class SettingsApplyPlanBuilder
{
    private const SettingsChangeEffects NonBehaviorEffects =
        SettingsChangeEffects.MarkUnsaved |
        SettingsChangeEffects.PersistAppGeneralNow;

    public static SettingsApplyPlan Resolve(IEnumerable<string> changedPropertyNames, SettingsChangeContext context)
    {
        ArgumentNullException.ThrowIfNull(changedPropertyNames);

        var effects = SettingsChangeEffects.None;
        var invalidateNotchFinalProjection = false;
        var actions = SettingsOrchestrationActions.None;
        foreach (var propertyName in changedPropertyNames.Distinct(StringComparer.Ordinal))
        {
            switch (propertyName)
            {
                case nameof(FreeformHelperViewModel.ActiveAreaWidth):
                case nameof(FreeformHelperViewModel.ActiveAreaHeight):
                case nameof(FreeformHelperViewModel.GridPaddingPercent):
                    effects |= Behavioral(SettingsChangeKind.GridRebuildWithFit);
                    break;

                case nameof(FreeformHelperViewModel.PanelBiasX):
                case nameof(FreeformHelperViewModel.PanelBiasY):
                    if (context.IsPanelAlignment)
                    {
                        effects |= Behavioral(SettingsChangeKind.GridRebuildWithFit);
                    }
                    break;

                case nameof(FreeformHelperViewModel.SelectedScanOrder):
                    effects |= SettingsChangeEffects.UpdateCadOutputFwDiffIndexing |
                               SettingsChangeEffects.TriggerGridRebuild;
                    break;

                case nameof(FreeformHelperViewModel.GridAlignmentMode):
                    effects |= Behavioral(SettingsChangeKind.GridRebuildWithFit);
                    actions |= SettingsOrchestrationActions.NotifyIsPanelAlignment;
                    break;

                case nameof(FreeformHelperViewModel.RegularSourceMode):
                    effects |= SettingsChangeEffects.RefreshDxfRegularSourceHint |
                               SettingsChangeEffects.TriggerGridRebuildWithFit;
                    actions |= SettingsOrchestrationActions.NotifyIsDxfLayerRegularSource |
                               SettingsOrchestrationActions.ApplyRegularSourceLayerAutoHidePolicy;
                    break;

                case nameof(FreeformHelperViewModel.SelectedRegularSourceLayerOption):
                    effects |= SettingsChangeEffects.RefreshDxfRegularSourceHint;
                    if (context.IsDxfLayerRegularSource)
                    {
                        effects |= SettingsChangeEffects.TriggerGridRebuildWithFit;
                    }

                    actions |= SettingsOrchestrationActions.ApplyRegularSourceLayerAutoHidePolicy;
                    break;

                case nameof(FreeformHelperViewModel.XChannels):
                case nameof(FreeformHelperViewModel.YChannels):
                case nameof(FreeformHelperViewModel.CascadeNum):
                case nameof(FreeformHelperViewModel.CascadeIcSettings):
                    effects |= Behavioral(SettingsChangeKind.LayoutRefresh) |
                               Behavioral(SettingsChangeKind.GridRebuildWithFit);
                    break;

                case nameof(FreeformHelperViewModel.FreeformAxisThreshold):
                case nameof(FreeformHelperViewModel.EnableAutoDetectXy):
                case nameof(FreeformHelperViewModel.EnableFreeformEdgeSpecialization):
                    effects |= Behavioral(SettingsChangeKind.Step2Detection);
                    break;

                case nameof(FreeformHelperViewModel.EnableToRegular):
                case nameof(FreeformHelperViewModel.EnableToFullRuleEngine):
                case nameof(FreeformHelperViewModel.EnableToFullRuleTrace):
                case nameof(FreeformHelperViewModel.ToFullStrictOverlapPercent):
                case nameof(FreeformHelperViewModel.EnableBoundaryVirtualAreaCap):
                case nameof(FreeformHelperViewModel.BoundaryVirtualAreaCapPercent):
                    effects |= Behavioral(SettingsChangeKind.Step3Notch);
                    break;

                case nameof(FreeformHelperViewModel.EnableTargetCoverageGuard):
                case nameof(FreeformHelperViewModel.TargetCoverageCapPercent):
                    invalidateNotchFinalProjection = true;
                    break;

                case nameof(FreeformHelperViewModel.EnableToFull):
                case nameof(FreeformHelperViewModel.SelectedNotchCompensationModelOption):
                    effects |= Behavioral(SettingsChangeKind.Step3NotchWithOverlay);
                    break;

                case nameof(FreeformHelperViewModel.CadOutputFwDiffIndexStart):
                case nameof(FreeformHelperViewModel.CadOutputFwDiffAutoMode):
                case nameof(FreeformHelperViewModel.CadOutputFwDiffIndexAnchorCadId):
                    effects |= Behavioral(SettingsChangeKind.Step4Indexing);
                    break;

                case nameof(FreeformHelperViewModel.RecalcBoundsOnLayerFilter):
                    effects |= Behavioral(SettingsChangeKind.RecalcBoundsOnLayerFilter);
                    break;
            }
        }

        return new SettingsApplyPlan(effects, invalidateNotchFinalProjection, actions);
    }

    private static SettingsChangeEffects Behavioral(SettingsChangeKind kind)
    {
        return SettingsChangePolicy.Resolve(kind) & ~NonBehaviorEffects;
    }
}

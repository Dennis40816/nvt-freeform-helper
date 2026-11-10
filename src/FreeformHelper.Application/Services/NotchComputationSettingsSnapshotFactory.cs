// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;

namespace FreeformHelper.Application.Services;

public static class NotchComputationSettingsSnapshotFactory
{
    public static ProjectSettings Create(ProjectSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = source.Notch.ComputationMode,
                CompensationModel = source.Notch.CompensationModel,
                EnabledVersions = new HashSet<NotchAlgorithmVersion>(source.Notch.EnabledVersions),
                LenScale = source.Notch.LenScale,
                NullValue = source.Notch.NullValue,
                ThresholdQ7 = source.Notch.ThresholdQ7,
                ThresholdPercentV22 = source.Notch.ThresholdPercentV22,
                LinkVersionThresholds = source.Notch.LinkVersionThresholds,
                EnableToRegular = source.Notch.EnableToRegular,
                EnableToFull = source.Notch.EnableToFull,
                MultiOwnerStrictOverlapPercent = source.Notch.MultiOwnerStrictOverlapPercent,
                EnableToFullRuleEngine = source.Notch.EnableToFullRuleEngine,
                EnableToFullRuleTrace = source.Notch.EnableToFullRuleTrace,
                EnableBoundaryVirtualAreaCap = source.Notch.EnableBoundaryVirtualAreaCap,
                BoundaryVirtualAreaCapRatio = source.Notch.BoundaryVirtualAreaCapRatio,
                EnableTargetCoverageGuard = source.Notch.EnableTargetCoverageGuard,
                TargetCoverageCapPercent = source.Notch.TargetCoverageCapPercent,
                ExportProfile = source.Notch.ExportProfile
            }
        };
    }
}

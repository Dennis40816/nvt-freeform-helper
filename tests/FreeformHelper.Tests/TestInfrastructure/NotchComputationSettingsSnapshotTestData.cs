// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using Xunit;

namespace FreeformHelper.Tests.TestInfrastructure;

internal static class NotchComputationSettingsSnapshotTestData
{
    public static ProjectSettings CreateSource()
    {
        var source = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                CompensationModel = NotchCompensationModel.ConservativeNoGain,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V21 },
                LenScale = 23,
                NullValue = 43210,
                ThresholdQ7 = 37,
                ThresholdPercentV22 = 42.5,
                LinkVersionThresholds = false,
                EnableToRegular = false,
                EnableToFull = false,
                MultiOwnerStrictOverlapPercent = 2.5,
                EnableToFullRuleEngine = false,
                EnableToFullRuleTrace = true,
                EnableBoundaryVirtualAreaCap = false,
                BoundaryVirtualAreaCapRatio = 1.75,
                EnableTargetCoverageGuard = false,
                TargetCoverageCapPercent = 145,
                ExportProfile = NotchExportProfile.Debug
            }
        };
        source.Grid.XChannels = 64;
        source.Grid.YChannels = 40;
        source.Grid.PerIcXChannels.Add(64);
        source.Matching.MatchThreshold = 0.7;
        source.Matching.NearestK = 9;
        source.View.CadFillOpacity = 0.45;
        source.View.AutoReplayStep2AfterProjectLoad = false;
        source.IndexMapping.CandidateNumber = 16;
        source.IndexMapping.WeightIou = 0.8;
        source.ActiveAreaPixels.X = 1920;
        source.ActiveAreaPixels.Y = 1080;
        return source;
    }

    public static void AssertCopiedNotchSettings(NotchSettings expected, NotchSettings actual)
    {
        Assert.Equal(expected.ComputationMode, actual.ComputationMode);
        Assert.Equal(expected.CompensationModel, actual.CompensationModel);
        Assert.True(expected.EnabledVersions.SetEquals(actual.EnabledVersions));
        Assert.Equal(expected.LenScale, actual.LenScale);
        Assert.Equal(expected.NullValue, actual.NullValue);
        Assert.Equal(expected.ThresholdQ7, actual.ThresholdQ7);
        Assert.Equal(expected.ThresholdPercentV22, actual.ThresholdPercentV22);
        Assert.Equal(expected.LinkVersionThresholds, actual.LinkVersionThresholds);
        Assert.Equal(expected.EnableToRegular, actual.EnableToRegular);
        Assert.Equal(expected.EnableToFull, actual.EnableToFull);
        Assert.Equal(expected.MultiOwnerStrictOverlapPercent, actual.MultiOwnerStrictOverlapPercent);
        Assert.Equal(expected.EnableToFullRuleEngine, actual.EnableToFullRuleEngine);
        Assert.Equal(expected.EnableToFullRuleTrace, actual.EnableToFullRuleTrace);
        Assert.Equal(expected.EnableBoundaryVirtualAreaCap, actual.EnableBoundaryVirtualAreaCap);
        Assert.Equal(expected.BoundaryVirtualAreaCapRatio, actual.BoundaryVirtualAreaCapRatio);
        Assert.Equal(expected.EnableTargetCoverageGuard, actual.EnableTargetCoverageGuard);
        Assert.Equal(expected.TargetCoverageCapPercent, actual.TargetCoverageCapPercent);
        Assert.Equal(expected.ExportProfile, actual.ExportProfile);
    }
}

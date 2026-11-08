// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ProjectSettingsValidationBehaviorTests
{
    [Fact]
    public void ValidateOrThrow_OutOfRangeInput_IdentifiesTheRejectedSetting()
    {
        (string Property, double Value, string Message)[] cases =
        [
            (nameof(MatchingSettings.MatchThreshold), -0.01, "MatchThreshold must be in [0,1]."),
            (nameof(MatchingSettings.FreeformAxisThreshold), -0.01, "FreeformAxisThreshold must be in [0,1]."),
            (nameof(MatchingSettings.FreeformAxisThreshold), 1.01, "FreeformAxisThreshold must be in [0,1]."),
            (nameof(ViewSettings.CadLineOpacity), -0.01, "CadLineOpacity must be in [0,1]."),
            (nameof(ViewSettings.CadLineOpacity), 1.01, "CadLineOpacity must be in [0,1]."),
            (nameof(ViewSettings.RegularLineOpacity), -0.01, "RegularLineOpacity must be in [0,1]."),
            (nameof(ViewSettings.RegularLineOpacity), 1.01, "RegularLineOpacity must be in [0,1]."),
            (nameof(ViewSettings.AreaBucketTolerance), -0.001, "AreaBucketTolerance must be non-negative."),
            (nameof(NotchSettings.LenScale), 0, "LenScale must be positive."),
            (nameof(NotchSettings.ThresholdPercentV22), -0.1, "Notch ThresholdPercentV22 must be in [0,100]."),
            (nameof(NotchSettings.ThresholdPercentV22), 100.1, "Notch ThresholdPercentV22 must be in [0,100]."),
            (nameof(NotchSettings.MultiOwnerStrictOverlapPercent), -0.1, "Notch MultiOwnerStrictOverlapPercent must be in [0,100]."),
            (nameof(NotchSettings.MultiOwnerStrictOverlapPercent), 100.1, "Notch MultiOwnerStrictOverlapPercent must be in [0,100]."),
        ];

        foreach (var item in cases)
        {
            var settings = CreateInvalidSettings(item.Property, item.Value);

            var error = Assert.Throws<InvalidOperationException>(settings.ValidateOrThrow);

            Assert.Equal(item.Message, error.Message);
        }
    }

    [Fact]
    public void ValidateOrThrow_UnknownNotchOption_IdentifiesTheRejectedOption()
    {
        (string Property, string Message)[] cases =
        [
            (nameof(NotchSettings.ComputationMode), "Notch ComputationMode is invalid."),
            (nameof(NotchSettings.CompensationModel), "Notch CompensationModel is invalid."),
            (nameof(NotchSettings.ExportProfile), "Notch ExportProfile is invalid."),
        ];

        foreach (var item in cases)
        {
            var settings = new ProjectSettings
            {
                Notch = item.Property switch
                {
                    nameof(NotchSettings.ComputationMode) => new() { ComputationMode = (NotchComputationMode)99 },
                    nameof(NotchSettings.CompensationModel) => new() { CompensationModel = (NotchCompensationModel)99 },
                    nameof(NotchSettings.ExportProfile) => new() { ExportProfile = (NotchExportProfile)99 },
                    _ => throw new InvalidOperationException("Unknown Notch setting in the test data."),
                },
            };

            var error = Assert.Throws<InvalidOperationException>(settings.ValidateOrThrow);

            Assert.Equal(item.Message, error.Message);
        }
    }

    [Fact]
    public void ValidateOrThrow_InclusiveLimits_AcceptsZeroAndFullRatiosAndPercentages()
    {
        foreach (var useUpperLimits in new[] { false, true })
        {
            var settings = new ProjectSettings
            {
                Matching = new MatchingSettings
                {
                    MatchThreshold = useUpperLimits ? 1 : 0,
                    FreeformAxisThreshold = useUpperLimits ? 1 : 0,
                },
                View = new ViewSettings
                {
                    CadLineOpacity = useUpperLimits ? 1 : 0,
                    RegularLineOpacity = useUpperLimits ? 1 : 0,
                    AreaBucketTolerance = 0,
                },
                Notch = new NotchSettings
                {
                    LenScale = 1,
                    ThresholdPercentV22 = useUpperLimits ? 100 : 0,
                    MultiOwnerStrictOverlapPercent = useUpperLimits ? 100 : 0,
                    ComputationMode = useUpperLimits ? NotchComputationMode.CadAllocation : NotchComputationMode.LegacyRegularAnchor,
                    CompensationModel = useUpperLimits ? NotchCompensationModel.Disabled : NotchCompensationModel.ConservativeNoGain,
                    ExportProfile = useUpperLimits ? NotchExportProfile.Debug : NotchExportProfile.Release,
                },
            };

            var error = Record.Exception(settings.ValidateOrThrow);

            Assert.Null(error);
        }
    }

    private static ProjectSettings CreateInvalidSettings(string property, double value)
    {
        return property switch
        {
            nameof(MatchingSettings.MatchThreshold) => new() { Matching = new MatchingSettings { MatchThreshold = value } },
            nameof(MatchingSettings.FreeformAxisThreshold) => new() { Matching = new MatchingSettings { FreeformAxisThreshold = value } },
            nameof(ViewSettings.CadLineOpacity) => new() { View = new ViewSettings { CadLineOpacity = value } },
            nameof(ViewSettings.RegularLineOpacity) => new() { View = new ViewSettings { RegularLineOpacity = value } },
            nameof(ViewSettings.AreaBucketTolerance) => new() { View = new ViewSettings { AreaBucketTolerance = value } },
            nameof(NotchSettings.LenScale) => new() { Notch = new NotchSettings { LenScale = (int)value } },
            nameof(NotchSettings.ThresholdPercentV22) => new() { Notch = new NotchSettings { ThresholdPercentV22 = value } },
            nameof(NotchSettings.MultiOwnerStrictOverlapPercent) => new()
            {
                Notch = new NotchSettings { MultiOwnerStrictOverlapPercent = value },
            },
            _ => throw new ArgumentOutOfRangeException(nameof(property)),
        };
    }
}

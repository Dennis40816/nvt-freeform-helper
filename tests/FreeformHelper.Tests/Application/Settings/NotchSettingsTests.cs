using FreeformHelper.Application.Settings;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchSettingsTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(129)]
    public void ProjectSettingsValidate_RejectsV21ThresholdOutsideIndependentQ7Range(int thresholdQ7)
    {
        var settings = new ProjectSettings();
        settings.Notch.ThresholdQ7 = thresholdQ7;

        var error = Assert.Throws<InvalidOperationException>(settings.ValidateOrThrow);

        Assert.Contains("[0,128]", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(128)]
    public void ProjectSettingsValidate_AcceptsV21ThresholdQ7Boundaries(int thresholdQ7)
    {
        var settings = new ProjectSettings();
        settings.Notch.ThresholdQ7 = thresholdQ7;

        settings.ValidateOrThrow();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public void ProjectSettingsValidate_RejectsNullValueOutsideFirmwareUint16Domain(int nullValue)
    {
        var settings = new ProjectSettings();
        settings.Notch.NullValue = nullValue;

        var error = Assert.Throws<InvalidOperationException>(settings.ValidateOrThrow);

        Assert.Equal("NullValue must be in [0,65535].", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65535)]
    public void ProjectSettingsValidate_AcceptsNullValueFirmwareUint16Boundaries(int nullValue)
    {
        var settings = new ProjectSettings();
        settings.Notch.NullValue = nullValue;

        settings.ValidateOrThrow();
    }

    [Fact]
    public void ResolveCombinedPercent_CurrentGain_ReturnsLegacyDiagnosticMultiplier()
    {
        var settings = new NotchSettings
        {
            CompensationModel = NotchCompensationModel.CurrentGain,
        };

        Assert.Equal(150, settings.ResolveCombinedPercent(toRegularPercent: 75, toFullPercent: 200));
        Assert.Equal(1.5, settings.ResolveCombinedRatio(toRegularRatio: 0.75, toFullRatio: 2.0), 6);
    }

    [Fact]
    public void ResolveCombinedPercent_ConservativeNoGain_ReturnsLegacyDiagnosticToRegular()
    {
        var settings = new NotchSettings
        {
            CompensationModel = NotchCompensationModel.ConservativeNoGain,
        };

        Assert.Equal(75, settings.ResolveCombinedPercent(toRegularPercent: 75, toFullPercent: 200));
        Assert.Equal(0.75, settings.ResolveCombinedRatio(toRegularRatio: 0.75, toFullRatio: 2.0), 6);
    }

    [Fact]
    public void ResolveCombinedPercent_Disabled_UsesUnityCombine()
    {
        var settings = new NotchSettings
        {
            CompensationModel = NotchCompensationModel.Disabled,
        };

        Assert.Equal(100, settings.ResolveCombinedPercent(toRegularPercent: 75, toFullPercent: 200));
        Assert.Equal(1.0, settings.ResolveCombinedRatio(toRegularRatio: 0.75, toFullRatio: 2.0), 6);
    }

    [Fact]
    public void Defaults_EnableBeta09BoundaryGuards()
    {
        var settings = new NotchSettings();

        Assert.True(settings.EnableBoundaryVirtualAreaCap);
        Assert.Equal(1.0, settings.BoundaryVirtualAreaCapRatio, 6);
        Assert.True(settings.EnableTargetCoverageGuard);
        Assert.Equal(120, settings.TargetCoverageCapPercent);
    }
}

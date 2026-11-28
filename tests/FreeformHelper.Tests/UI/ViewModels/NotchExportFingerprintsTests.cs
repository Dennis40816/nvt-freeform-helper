// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportFingerprintsTests
{
    private const int DefaultSettingsFingerprint = 1811823771;

    [Theory]
    [InlineData(nameof(NotchSettings.ComputationMode))]
    [InlineData(nameof(NotchSettings.CompensationModel))]
    [InlineData(nameof(NotchSettings.LenScale))]
    [InlineData(nameof(NotchSettings.NullValue))]
    [InlineData(nameof(NotchSettings.ThresholdQ7))]
    [InlineData(nameof(NotchSettings.ThresholdPercentV22))]
    [InlineData(nameof(NotchSettings.LinkVersionThresholds))]
    [InlineData(nameof(NotchSettings.EnableToRegular))]
    [InlineData(nameof(NotchSettings.EnableToFull))]
    [InlineData(nameof(NotchSettings.EnableToFullRuleEngine))]
    [InlineData(nameof(NotchSettings.EnableToFullRuleTrace))]
    [InlineData(nameof(NotchSettings.EnableBoundaryVirtualAreaCap))]
    [InlineData(nameof(NotchSettings.BoundaryVirtualAreaCapRatio))]
    [InlineData(nameof(NotchSettings.EnableTargetCoverageGuard))]
    [InlineData(nameof(NotchSettings.TargetCoverageCapPercent))]
    [InlineData(nameof(NotchSettings.EnabledVersions))]
    [InlineData(nameof(NotchSettings.MultiOwnerStrictOverlapPercent))]
    public void SettingsFingerprintChangesWhenAnyInputSettingChanges(string setting)
    {
        var changed = new ProjectSettings();
        var change = GetSettingChange(setting);
        change(changed.Notch);

        Assert.NotEqual(
            DefaultSettingsFingerprint,
            NotchExportFingerprints.ComputeNotchExportSettingsFingerprint(changed));
    }

    [Fact]
    public void SettingsFingerprintIsRepeatableForEquivalentSettings()
    {
        var first = new ProjectSettings();
        var second = new ProjectSettings();

        Assert.Equal(DefaultSettingsFingerprint, NotchExportFingerprints.ComputeNotchExportSettingsFingerprint(first));
        Assert.Equal(DefaultSettingsFingerprint, NotchExportFingerprints.ComputeNotchExportSettingsFingerprint(second));
    }

    [Fact]
    public void SettingsFingerprintIgnoresEnabledVersionInsertionOrder()
    {
        var first = new ProjectSettings();
        first.Notch.EnabledVersions = [NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V22];
        var second = new ProjectSettings();
        second.Notch.EnabledVersions = [NotchAlgorithmVersion.V22, NotchAlgorithmVersion.V21];

        Assert.Equal(DefaultSettingsFingerprint, NotchExportFingerprints.ComputeNotchExportSettingsFingerprint(first));
        Assert.Equal(DefaultSettingsFingerprint, NotchExportFingerprints.ComputeNotchExportSettingsFingerprint(second));
    }

    [Theory]
    [InlineData(1, 2, 3, 1026)]
    [InlineData(2, 1, 3, 1956)]
    [InlineData(1, 3, 2, 1056)]
    [InlineData(2, 3, 1, 2016)]
    [InlineData(3, 1, 2, 2916)]
    [InlineData(3, 2, 1, 2946)]
    public void CombinedFingerprintPreservesArgumentOrder(int settings, int activeRegular, int cadOutput, int expected)
    {
        Assert.Equal(expected, NotchExportFingerprints.CombineNotchExportSettingsFingerprint(settings, activeRegular, cadOutput));
    }

    [Fact]
    public void CadOutputHashIgnoresDictionaryInsertionOrder()
    {
        var first = new Dictionary<int, int> { [1] = 10, [2] = 20 };
        var second = new Dictionary<int, int> { [2] = 20, [1] = 10 };

        Assert.Equal(15739340, NotchExportFingerprints.ComputeCadOutputFwDiffHash(first));
        Assert.Equal(15739340, NotchExportFingerprints.ComputeCadOutputFwDiffHash(second));
    }

    [Fact]
    public void CadOutputHashChangesWhenAValueChanges()
    {
        var mapping = new Dictionary<int, int> { [1] = 10, [2] = 21 };

        Assert.Equal(15739341, NotchExportFingerprints.ComputeCadOutputFwDiffHash(mapping));
    }

    [Fact]
    public void SettingsFingerprintRejectsNullSettings()
    {
        Assert.Throws<ArgumentNullException>("settings",
            () => NotchExportFingerprints.ComputeNotchExportSettingsFingerprint(null!));
    }

    [Fact]
    public void CadOutputHashRejectsNullDictionary()
    {
        Assert.Throws<ArgumentNullException>("cadOutputFwDiffIndexByCadId",
            () => NotchExportFingerprints.ComputeCadOutputFwDiffHash(null!));
    }

    private static Action<NotchSettings> GetSettingChange(string setting) => setting switch
    {
        nameof(NotchSettings.ComputationMode) => settings => settings.ComputationMode = NotchComputationMode.LegacyRegularAnchor,
        nameof(NotchSettings.CompensationModel) => settings => settings.CompensationModel = NotchCompensationModel.Disabled,
        nameof(NotchSettings.LenScale) => settings => settings.LenScale = 11,
        nameof(NotchSettings.NullValue) => settings => settings.NullValue = 65534,
        nameof(NotchSettings.ThresholdQ7) => settings => settings.ThresholdQ7 = 1,
        nameof(NotchSettings.ThresholdPercentV22) => settings => settings.ThresholdPercentV22 = 1,
        nameof(NotchSettings.LinkVersionThresholds) => settings => settings.LinkVersionThresholds = false,
        nameof(NotchSettings.EnableToRegular) => settings => settings.EnableToRegular = false,
        nameof(NotchSettings.EnableToFull) => settings => settings.EnableToFull = false,
        nameof(NotchSettings.EnableToFullRuleEngine) => settings => settings.EnableToFullRuleEngine = false,
        nameof(NotchSettings.EnableToFullRuleTrace) => settings => settings.EnableToFullRuleTrace = true,
        nameof(NotchSettings.EnableBoundaryVirtualAreaCap) => settings => settings.EnableBoundaryVirtualAreaCap = false,
        nameof(NotchSettings.BoundaryVirtualAreaCapRatio) => settings => settings.BoundaryVirtualAreaCapRatio = 0.5,
        nameof(NotchSettings.EnableTargetCoverageGuard) => settings => settings.EnableTargetCoverageGuard = false,
        nameof(NotchSettings.TargetCoverageCapPercent) => settings => settings.TargetCoverageCapPercent = 121,
        nameof(NotchSettings.EnabledVersions) => settings => settings.EnabledVersions = [NotchAlgorithmVersion.V22],
        nameof(NotchSettings.MultiOwnerStrictOverlapPercent) => settings => settings.MultiOwnerStrictOverlapPercent = 0.2,
        _ => throw new ArgumentOutOfRangeException(nameof(setting)),
    };

    [Fact]
    public void ActiveRegularHashUsesMinusOneForNullAndEmptySets()
    {
        Assert.Equal(-1, NotchExportFingerprints.ComputeActiveRegularHash(null));
        Assert.Equal(-1, NotchExportFingerprints.ComputeActiveRegularHash(new HashSet<int>()));
    }

    [Fact]
    public void ActiveRegularHashIgnoresInsertionOrder()
    {
        var first = new HashSet<int> { 10, 20, 30 };
        var second = new HashSet<int> { 30, 10, 20 };

        Assert.Equal(
            NotchExportFingerprints.ComputeActiveRegularHash(first),
            NotchExportFingerprints.ComputeActiveRegularHash(second));
    }

    [Fact]
    public void ActiveRegularHashChangesWithTheIds()
    {
        var first = new HashSet<int> { 10, 20 };
        var second = new HashSet<int> { 10, 30 };

        Assert.NotEqual(
            NotchExportFingerprints.ComputeActiveRegularHash(first),
            NotchExportFingerprints.ComputeActiveRegularHash(second));
    }

    [Fact]
    public void ActiveRegularHashForOneIdIsNotTheEmptySentinel()
    {
        Assert.NotEqual(-1, NotchExportFingerprints.ComputeActiveRegularHash(new HashSet<int> { 10 }));
    }

    [Fact]
    public void CadPoolFingerprintIsRepeatableForTheSameOrderedIds()
    {
        var first = new[]
        {
            TestInfrastructure.TestGeometryFactory.CreateCadPad(10, "AA", 0, 0, 10, 10),
            TestInfrastructure.TestGeometryFactory.CreateCadPad(20, "AA", 10, 0, 20, 10),
        };
        var second = new[]
        {
            TestInfrastructure.TestGeometryFactory.CreateCadPad(10, "AA", 0, 0, 10, 10),
            TestInfrastructure.TestGeometryFactory.CreateCadPad(20, "AA", 10, 0, 20, 10),
        };

        Assert.Equal(
            NotchExportFingerprints.ComputeCadPoolFingerprint(first),
            NotchExportFingerprints.ComputeCadPoolFingerprint(second));
    }

    [Fact]
    public void CadPoolFingerprintChangesWithTheCount()
    {
        var pad = TestInfrastructure.TestGeometryFactory.CreateCadPad(10, "AA", 0, 0, 10, 10);

        Assert.NotEqual(
            NotchExportFingerprints.ComputeCadPoolFingerprint([pad]),
            NotchExportFingerprints.ComputeCadPoolFingerprint([pad, pad]));
    }

    [Fact]
    public void CadPoolFingerprintChangesWithAnId()
    {
        var first = TestInfrastructure.TestGeometryFactory.CreateCadPad(10, "AA", 0, 0, 10, 10);
        var second = TestInfrastructure.TestGeometryFactory.CreateCadPad(20, "AA", 0, 0, 10, 10);

        Assert.NotEqual(
            NotchExportFingerprints.ComputeCadPoolFingerprint([first]),
            NotchExportFingerprints.ComputeCadPoolFingerprint([second]));
    }

    [Fact]
    public void CadPoolFingerprintPreservesIdOrder()
    {
        var first = TestInfrastructure.TestGeometryFactory.CreateCadPad(10, "AA", 0, 0, 10, 10);
        var second = TestInfrastructure.TestGeometryFactory.CreateCadPad(20, "AA", 10, 0, 20, 10);

        Assert.NotEqual(
            NotchExportFingerprints.ComputeCadPoolFingerprint([first, second]),
            NotchExportFingerprints.ComputeCadPoolFingerprint([second, first]));
    }

    [Fact]
    public void EmptyCadPoolFingerprintIsStable()
    {
        var first = NotchExportFingerprints.ComputeCadPoolFingerprint([]);
        var second = NotchExportFingerprints.ComputeCadPoolFingerprint([]);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(2, 10, 3)]
    [InlineData(1, 11, 3)]
    [InlineData(1, 10, 4)]
    public void CadFingerprintChangesWithEachInput(int cadRevision, int cadPadCount, int hiddenCadPadCount)
    {
        var original = NotchExportFingerprints.BuildCadFingerprint(1, 10, 3);
        var changed = NotchExportFingerprints.BuildCadFingerprint(cadRevision, cadPadCount, hiddenCadPadCount);

        Assert.NotEqual(original, changed);
    }

    [Fact]
    public void CadFingerprintIsRepeatableForEqualInputs()
    {
        var first = NotchExportFingerprints.BuildCadFingerprint(1, 10, 3);
        var second = NotchExportFingerprints.BuildCadFingerprint(1, 10, 3);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(2, 10, 3)]
    [InlineData(1, 11, 3)]
    [InlineData(1, 10, 4)]
    public void ExportGridFingerprintChangesWithEachInput(int gridRevision, int gridPadCount, int indexRevision)
    {
        var original = NotchExportFingerprints.BuildExportGridFingerprint(1, 10, 3);
        var changed = NotchExportFingerprints.BuildExportGridFingerprint(gridRevision, gridPadCount, indexRevision);

        Assert.NotEqual(original, changed);
    }

    [Fact]
    public void ExportGridFingerprintIsRepeatableForEqualInputs()
    {
        var first = NotchExportFingerprints.BuildExportGridFingerprint(1, 10, 3);
        var second = NotchExportFingerprints.BuildExportGridFingerprint(1, 10, 3);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(2, 10)]
    [InlineData(1, 11)]
    public void ComputationGridFingerprintChangesWithEachInput(int gridRevision, int gridPadCount)
    {
        var original = NotchExportFingerprints.BuildComputationGridFingerprint(1, 10);
        var changed = NotchExportFingerprints.BuildComputationGridFingerprint(gridRevision, gridPadCount);

        Assert.NotEqual(original, changed);
    }

    [Fact]
    public void ComputationGridFingerprintIsRepeatableForEqualInputs()
    {
        var first = NotchExportFingerprints.BuildComputationGridFingerprint(1, 10);
        var second = NotchExportFingerprints.BuildComputationGridFingerprint(1, 10);

        Assert.Equal(first, second);
    }
}

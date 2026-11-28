// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchComputationSettingsSnapshotFactoryTests
{
    [Fact]
    public void CreateCopiesEveryWhitelistedNotchSetting()
    {
        var source = NotchComputationSettingsSnapshotTestData.CreateSource();

        var snapshot = NotchComputationSettingsSnapshotFactory.Create(source);

        Assert.NotSame(source, snapshot);
        Assert.NotSame(source.Notch, snapshot.Notch);
        NotchComputationSettingsSnapshotTestData.AssertCopiedNotchSettings(source.Notch, snapshot.Notch);
    }

    [Fact]
    public void CreateOwnsAnEqualVersionSetIsolatedFromLaterSourceMutation()
    {
        var source = NotchComputationSettingsSnapshotTestData.CreateSource();

        var snapshot = NotchComputationSettingsSnapshotFactory.Create(source);

        Assert.NotSame(source.Notch.EnabledVersions, snapshot.Notch.EnabledVersions);
        Assert.True(source.Notch.EnabledVersions.SetEquals(snapshot.Notch.EnabledVersions));
        source.Notch.EnabledVersions.Remove(NotchAlgorithmVersion.V21);
        source.Notch.EnabledVersions.Add(NotchAlgorithmVersion.V22);
        Assert.Equal(NotchAlgorithmVersion.V21, Assert.Single(snapshot.Notch.EnabledVersions));
        Assert.Equal(NotchAlgorithmVersion.V22, Assert.Single(source.Notch.EnabledVersions));
    }

    [Fact]
    public void CreateLeavesSourceUnchanged()
    {
        var source = NotchComputationSettingsSnapshotTestData.CreateSource();
        var expected = NotchComputationSettingsSnapshotTestData.CreateSource();
        var sections = (source.Grid, source.Matching, source.View, source.Notch, source.IndexMapping, source.ActiveAreaPixels);
        var versions = source.Notch.EnabledVersions;

        _ = NotchComputationSettingsSnapshotFactory.Create(source);

        Assert.Same(sections.Grid, source.Grid);
        Assert.Same(sections.Matching, source.Matching);
        Assert.Same(sections.View, source.View);
        Assert.Same(sections.Notch, source.Notch);
        Assert.Same(sections.IndexMapping, source.IndexMapping);
        Assert.Same(sections.ActiveAreaPixels, source.ActiveAreaPixels);
        Assert.Same(versions, source.Notch.EnabledVersions);
        Assert.Equivalent(expected, source, strict: true);
    }

    [Fact]
    public void CreateKeepsUnrelatedSectionsAtConstructorDefaults()
    {
        var source = NotchComputationSettingsSnapshotTestData.CreateSource();
        var defaults = new ProjectSettings();

        var snapshot = NotchComputationSettingsSnapshotFactory.Create(source);

        Assert.Equivalent(defaults.Grid, snapshot.Grid, strict: true);
        Assert.Equivalent(defaults.Matching, snapshot.Matching, strict: true);
        Assert.Equivalent(defaults.View, snapshot.View, strict: true);
        Assert.Equivalent(defaults.IndexMapping, snapshot.IndexMapping, strict: true);
        Assert.Equivalent(defaults.ActiveAreaPixels, snapshot.ActiveAreaPixels, strict: true);
    }

    [Fact]
    public void CreateRejectsNullSource()
    {
        Assert.Throws<ArgumentNullException>("source", () => NotchComputationSettingsSnapshotFactory.Create(null!));
    }
}

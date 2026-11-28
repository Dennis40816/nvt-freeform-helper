// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CadLoadSpinnerVisibilitySessionTests
{
    [Fact]
    public void WarmupCreatesHostOnceAndWarmsItOnEveryCall()
    {
        using var host = new FakeCadLoadSpinnerHost();
        var session = new CadLoadSpinnerVisibilitySession();
        var factoryCalls = 0;
        ICadLoadSpinnerHost CreateHost()
        {
            factoryCalls++;
            return host;
        }

        session.Warmup(CreateHost);
        session.Warmup(CreateHost);

        Assert.Same(host, session.Host);
        Assert.Equal(1, factoryCalls);
        Assert.Equal(2, host.WarmupCallCount);
        Assert.Equal(0, host.ShowCallCount);
    }

    [Fact]
    public void NoObservedViewModelWhileHiddenDoesNotTouchHostOrRecordSync()
    {
        using var host = new FakeCadLoadSpinnerHost();
        var session = new CadLoadSpinnerVisibilitySession();
        session.Warmup(() => host);
        var before = CadLoadSpinnerDebugState.GetSnapshot();

        session.Sync(false, true, true,
            () => throw new InvalidOperationException("Owner lookup is not expected."),
            () => throw new InvalidOperationException("Host creation is not expected."));

        var after = CadLoadSpinnerDebugState.GetSnapshot();
        Assert.Same(host, session.Host);
        Assert.Equal(0, host.ShowCallCount);
        Assert.Equal(0, host.HideCallCount);
        Assert.Equal(before.ViewHostShowCount, after.ViewHostShowCount);
        Assert.Equal(before.ViewHostHideCount, after.ViewHostHideCount);
        Assert.Equal(LastSequence(before.Events), LastSequence(after.Events));
    }

    [AvaloniaFact]
    public void MissingOwnerDoesNotCreateHostOrMarkVisibleAndRetriesOwner()
    {
        using var host = new FakeCadLoadSpinnerHost();
        var session = new CadLoadSpinnerVisibilitySession();
        Window? owner = null;
        var ownerResolutions = 0;
        Window? ResolveOwner()
        {
            ownerResolutions++;
            return owner;
        }

        session.Sync(true, true, false, ResolveOwner, () => host);
        session.Sync(true, true, false, ResolveOwner, () => host);

        Assert.Null(session.Host);
        Assert.Equal(0, host.ShowCallCount);
        Assert.Equal(2, ownerResolutions);

        owner = new Window();
        session.Sync(true, true, false, ResolveOwner, () => host);

        Assert.Same(host, session.Host);
        Assert.Same(owner, host.LastOwner);
        Assert.Equal(1, host.ShowCallCount);
        Assert.Equal(3, ownerResolutions);
    }

    [AvaloniaFact]
    public void RepeatedShowResolvesOwnerAndShowsOnlyOnce()
    {
        using var host = new FakeCadLoadSpinnerHost();
        var session = new CadLoadSpinnerVisibilitySession();
        var owner = new Window();
        var ownerResolutions = 0;
        Window ResolveOwner()
        {
            ownerResolutions++;
            return owner;
        }

        session.Sync(true, true, false, ResolveOwner, () => host);
        session.Sync(true, false, true, ResolveOwner,
            () => throw new InvalidOperationException("Host creation is not expected while visible."));

        Assert.Equal(1, host.ShowCallCount);
        Assert.Equal(1, ownerResolutions);
        Assert.Same(owner, host.LastOwner);
        Assert.Equal(0, host.HideCallCount);
    }

    [Fact]
    public void HideWhenNotVisibleDoesNotHideHost()
    {
        using var host = new FakeCadLoadSpinnerHost();
        var session = new CadLoadSpinnerVisibilitySession();
        session.Warmup(() => host);

        session.Sync(true, false, false,
            () => throw new InvalidOperationException("Owner lookup is not expected when hiding."),
            () => throw new InvalidOperationException("Host creation is not expected when hiding."));

        Assert.Equal(0, host.ShowCallCount);
        Assert.Equal(0, host.HideCallCount);
        Assert.Same(host, session.Host);
    }

    [AvaloniaFact]
    public void NoObservedViewModelWhileVisibleHidesWithoutDebugRecord()
    {
        using var host = new FakeCadLoadSpinnerHost();
        var session = new CadLoadSpinnerVisibilitySession();
        var owner = new Window();
        session.Sync(true, true, false, () => owner, () => host);
        var before = CadLoadSpinnerDebugState.GetSnapshot();

        session.Sync(false, true, true, () => owner, () => host);
        session.Sync(false, true, true, () => owner, () => host);

        var after = CadLoadSpinnerDebugState.GetSnapshot();
        Assert.Equal(1, host.ShowCallCount);
        Assert.Equal(1, host.HideCallCount);
        Assert.Equal(before.ViewHostShowCount, after.ViewHostShowCount);
        Assert.Equal(before.ViewHostHideCount, after.ViewHostHideCount);
        Assert.Equal(LastSequence(before.Events), LastSequence(after.Events));
    }

    [AvaloniaFact]
    public void DetachHidesDisposesOnceAndRetainsVisibleFlag()
    {
        using var firstHost = new FakeCadLoadSpinnerHost();
        using var freshHost = new FakeCadLoadSpinnerHost();
        var session = new CadLoadSpinnerVisibilitySession();
        var owner = new Window();
        session.Sync(true, true, false, () => owner, () => firstHost);

        session.Detach();
        session.Detach();

        Assert.Null(session.Host);
        Assert.Equal(1, firstHost.HideCallCount);
        Assert.Equal(1, firstHost.DisposeCallCount);

        session.Warmup(() => freshHost);
        session.Sync(true, true, false,
            () => throw new InvalidOperationException("The retained visible flag must skip owner lookup."),
            () => freshHost);

        Assert.Same(freshHost, session.Host);
        Assert.Equal(1, freshHost.WarmupCallCount);
        Assert.Equal(0, freshHost.ShowCallCount);
        Assert.Equal(0, freshHost.HideCallCount);

        session.Sync(true, false, false, () => owner, () => freshHost);
        session.Sync(true, true, false, () => owner, () => freshHost);

        Assert.Equal(1, freshHost.HideCallCount);
        Assert.Equal(1, freshHost.ShowCallCount);
        Assert.Same(owner, freshHost.LastOwner);
    }

    [Fact]
    public void SyncRecordsShowAndHideIntentEvenWithoutTransitions()
    {
        using var host = new FakeCadLoadSpinnerHost();
        var session = new CadLoadSpinnerVisibilitySession();
        var before = CadLoadSpinnerDebugState.GetSnapshot();

        session.Sync(true, true, false, () =>
        {
            Assert.Equal(before.ViewHostShowCount + 1, CadLoadSpinnerDebugState.GetSnapshot().ViewHostShowCount);
            return null;
        }, () => host);
        session.Sync(true, false, false, () => null, () => host);
        session.Sync(true, false, false, () => null, () => host);

        var after = CadLoadSpinnerDebugState.GetSnapshot();
        Assert.Equal(before.ViewHostShowCount + 1, after.ViewHostShowCount);
        Assert.Equal(before.ViewHostHideCount + 2, after.ViewHostHideCount);
        var previousSequence = LastSequence(before.Events) ?? 0;
        Assert.Collection(after.Events.Where(entry => entry.Sequence > previousSequence),
            entry => AssertViewHostEvent(entry, "action=show,overlayVisible=True"),
            entry => AssertViewHostEvent(entry, "action=hide,overlayVisible=False"),
            entry => AssertViewHostEvent(entry, "action=hide,overlayVisible=False"));
        Assert.Null(session.Host);
        Assert.Equal(0, host.ShowCallCount);
        Assert.Equal(0, host.HideCallCount);
    }

    private static void AssertViewHostEvent(CadLoadSpinnerDebugEvent entry, string detail)
    {
        Assert.Equal("view.host", entry.Source);
        Assert.Equal(detail, entry.Detail);
    }

    private static long? LastSequence(IReadOnlyList<CadLoadSpinnerDebugEvent> events) =>
        events.Count == 0 ? null : events[events.Count - 1].Sequence;
}

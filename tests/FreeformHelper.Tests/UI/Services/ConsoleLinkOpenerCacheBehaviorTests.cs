// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Nvt.Core.LogConsole;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ConsoleLinkOpenerCacheBehaviorTests
{
    [Fact]
    public void GetUnavailableReasonRechecksDeletedReportAfterCacheEviction()
    {
        using var culture = TestCulture.UseEnUs();
        var target = new LinkTarget(LinkKind.File, "reports/deleted.csv");
        var reportExists = true;
        var opener = new ConsoleLinkOpener(
            path => path == target.Path && !reportExists ? default : new ConsoleTargetResolution(path, false),
            static (_, _) => { }, static work => work(), static () => DateTimeOffset.UnixEpoch);
        Assert.Null(opener.GetUnavailableReason(target));
        FillCache(opener);
        reportExists = false;
        Assert.Null(opener.GetUnavailableReason(target));

        opener.GetUnavailableReason(new LinkTarget(LinkKind.File, "reports/overflow.csv"));
        opener.GetUnavailableReason(target);

        Assert.Equal("The path does not exist.", opener.GetUnavailableReason(target));
    }

    [Fact]
    public void HttpReportLinkIsAvailableAndOpensWithoutResolvingFileSystem()
    {
        var work = new Queue<Action>();
        var resolved = new List<string>();
        var opened = new List<(LinkTarget Target, ConsoleTargetResolution Resolution)>();
        var opener = new ConsoleLinkOpener(
            path => { resolved.Add(path); return default; },
            (link, resolution) => opened.Add((link, resolution)), work.Enqueue, static () => DateTimeOffset.UnixEpoch);
        var target = new LinkTarget(LinkKind.Url, "http://example.test/reports?file=run%201.csv");

        var reason = opener.GetUnavailableReason(target);
        opener.Open(target);

        Assert.Null(reason);
        Assert.Empty(opened);
        Assert.Single(work);
        work.Dequeue()();
        var result = Assert.Single(opened);
        Assert.Equal(target, result.Target);
        Assert.False(result.Resolution.Exists);
        Assert.Empty(resolved);
        Assert.Empty(work);
    }

    private static void FillCache(ConsoleLinkOpener opener)
    {
        for (var index = 0; index < 1023; index++)
        {
            var path = "reports/" + index.ToString(CultureInfo.InvariantCulture) + ".csv";
            opener.GetUnavailableReason(new LinkTarget(LinkKind.File, path));
        }
    }
}

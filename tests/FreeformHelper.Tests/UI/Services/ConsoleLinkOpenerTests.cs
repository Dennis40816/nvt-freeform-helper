using FreeformHelper.UI.Services;
using Nvt.Core.LogConsole;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ConsoleLinkOpenerTests
{
    private const string MissingReason = "The path does not exist.";
    private const string PendingReason = "Checking the path...";

    [Fact]
    public void GetUnavailableReason_ForUrl_ReturnsNullWithoutResolving()
    {
        var fixture = new Fixture();

        var reason = fixture.Opener.GetUnavailableReason(new LinkTarget(LinkKind.Url, "https://example.test/a"));

        Assert.Null(reason);
        Assert.Empty(fixture.Scheduled);
    }

    [Fact]
    public void GetUnavailableReason_FirstAskForAPath_ReturnsCheckingAndSchedulesOneResolve()
    {
        var fixture = new Fixture();
        var target = new LinkTarget(LinkKind.File, "C:/work/a.txt");

        var first = fixture.Opener.GetUnavailableReason(target);
        var second = fixture.Opener.GetUnavailableReason(target);

        Assert.Equal(PendingReason, first);
        Assert.Equal(PendingReason, second);
        Assert.Single(fixture.Scheduled);
        Assert.Empty(fixture.Resolved);
    }

    [Fact]
    public void GetUnavailableReason_AfterTheResolveFoundThePath_ReturnsNull()
    {
        var fixture = new Fixture();
        fixture.Existing.Add("C:/work/a.txt");
        var target = new LinkTarget(LinkKind.File, "C:/work/a.txt");
        fixture.Opener.GetUnavailableReason(target);

        fixture.RunScheduled();

        Assert.Null(fixture.Opener.GetUnavailableReason(target));
    }

    [Fact]
    public void GetUnavailableReason_AfterTheResolveMissedThePath_ReturnsMissingWithoutRetryingEarly()
    {
        var fixture = new Fixture();
        var target = new LinkTarget(LinkKind.File, "C:/work/missing.txt");
        fixture.Opener.GetUnavailableReason(target);
        fixture.RunScheduled();

        var reason = fixture.Opener.GetUnavailableReason(target);

        Assert.Equal(MissingReason, reason);
        Assert.Empty(fixture.Scheduled);
    }

    [Fact]
    public void GetUnavailableReason_ForAMissingPathAfterTheRetryAge_SchedulesAnotherResolve()
    {
        var fixture = new Fixture();
        var target = new LinkTarget(LinkKind.File, "C:/work/late.txt");
        fixture.Opener.GetUnavailableReason(target);
        fixture.RunScheduled();
        fixture.Existing.Add("C:/work/late.txt");
        fixture.Advance(TimeSpan.FromSeconds(3));

        var reason = fixture.Opener.GetUnavailableReason(target);
        fixture.RunScheduled();

        Assert.Equal(MissingReason, reason);
        Assert.Null(fixture.Opener.GetUnavailableReason(target));
    }

    [Fact]
    public void Open_ForAnExistingPath_OpensWithTheResolvedFullPath()
    {
        var fixture = new Fixture();
        fixture.Existing.Add("C:/work/a.txt");
        var target = new LinkTarget(LinkKind.File, "C:/work/a.txt");

        fixture.Opener.Open(target);
        fixture.RunScheduled();

        var opened = Assert.Single(fixture.Opened);
        Assert.Equal(target, opened.Target);
        Assert.Equal("full:C:/work/a.txt", opened.Resolution.FullPath);
    }

    [Fact]
    public void Open_ForAMissingPath_DoesNotOpen()
    {
        var fixture = new Fixture();

        fixture.Opener.Open(new LinkTarget(LinkKind.File, "C:/work/missing.txt"));
        fixture.RunScheduled();

        Assert.Empty(fixture.Opened);
    }

    [Fact]
    public void Open_ForUrl_OpensWithoutResolving()
    {
        var fixture = new Fixture();
        var target = new LinkTarget(LinkKind.Url, "https://example.test/a");

        fixture.Opener.Open(target);
        fixture.RunScheduled();

        Assert.Equal(target, Assert.Single(fixture.Opened).Target);
        Assert.Empty(fixture.Resolved);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("ms-msdt:/id PCWDiagnostic")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    public void Open_ForAUrlThatIsNotHttpOrHttps_DoesNotOpen(string url)
    {
        var fixture = new Fixture();

        fixture.Opener.Open(new LinkTarget(LinkKind.Url, url));
        fixture.RunScheduled();

        Assert.Empty(fixture.Opened);
    }

    [Fact]
    public void GetUnavailableReason_ForAUrlThatIsNotHttpOrHttps_ReturnsTheSchemeReason()
    {
        var fixture = new Fixture();

        var reason = fixture.Opener.GetUnavailableReason(new LinkTarget(LinkKind.Url, "file:///C:/a.exe"));

        Assert.Equal("Only http and https links can be opened.", reason);
    }

    [Fact]
    public void Open_WithNullTarget_Throws()
    {
        var fixture = new Fixture();

        Assert.Throws<ArgumentNullException>(() => fixture.Opener.Open(null!));
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Opener = new ConsoleLinkOpener(
                Resolve,
                (target, resolution) => Opened.Add((target, resolution)),
                Scheduled.Enqueue,
                () => Now);
        }

        public ConsoleLinkOpener Opener { get; }
        public DateTimeOffset Now { get; private set; } = DateTimeOffset.UnixEpoch;
        public HashSet<string> Existing { get; } = new(StringComparer.Ordinal);
        public Queue<Action> Scheduled { get; } = new();
        public List<string> Resolved { get; } = [];
        public List<(LinkTarget Target, ConsoleTargetResolution Resolution)> Opened { get; } = [];

        public void Advance(TimeSpan amount)
        {
            Now += amount;
        }

        public void RunScheduled()
        {
            while (Scheduled.TryDequeue(out var work))
            {
                work();
            }
        }

        private ConsoleTargetResolution Resolve(string path)
        {
            Resolved.Add(path);
            return Existing.Contains(path) ? new ConsoleTargetResolution("full:" + path, false) : default;
        }
    }
}

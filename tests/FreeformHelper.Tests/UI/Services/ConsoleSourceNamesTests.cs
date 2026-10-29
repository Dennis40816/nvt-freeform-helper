using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Nvt.Core.LogConsole;
using Nvt.Core.Threading;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ConsoleSourceNamesTests
{
    [Fact]
    public void Registry_HasUniqueSourceIdsAndUniqueDisplayNames()
    {
        var registry = ConsoleSourceNames.Registry;

        Assert.Equal(registry.Length, registry.Select(static source => source.SourceId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(registry.Length, registry.Select(static source => source.DisplayName).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Registry_ListsTheRuntimeQueryLoggerUnderItsShortName()
    {
        // The IPC host names its logger by hand, so the class name alone would not match.
        var source = Assert.Single(
            ConsoleSourceNames.Registry,
            static candidate => candidate.SourceId == "FreeformHelper.UI.Services.RuntimeQueryIpcServer");

        Assert.Equal("RuntimeQuery", source.DisplayName);
    }

    [AvaloniaFact]
    public async Task Session_ShowsTheShortNameForARegisteredSourceAndTheFullIdForAnUnknownOne()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var registered = ConsoleSourceNames.Registry[0];
        var unknownId = $"names-unknown-{Guid.NewGuid():N}";
        using var store = new LogStore();
        store.Add(LogLevel.Info, registered.SourceId, "registered line", DateTimeOffset.UnixEpoch);
        store.Add(LogLevel.Info, unknownId, "unknown line", DateTimeOffset.UnixEpoch);
        using var session = new ConsoleSession(store, new UiEventRunner(static (_, _) => { }, static (_, _) => { }), new ConsoleLinkOpener());

        await ConsoleLogTestSupport.WaitForProjectionAsync(
            session.Controller,
            projection => projection.Sources.Any(source => source.SourceId == unknownId));

        var sources = session.Controller.Projection.Sources;
        Assert.Equal(registered.DisplayName, sources.Single(source => source.SourceId == registered.SourceId).DisplayName);
        Assert.Equal(unknownId, sources.Single(source => source.SourceId == unknownId).DisplayName);
    }
}

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Nvt.Core.Threading;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ConsoleDedupeContractTests
{
    [AvaloniaFact]
    public async Task Dedupe_GroupsNonadjacentEqualMessages_AndOrdersGroupsByTheirLatestEvent()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var store = await ConsoleLogTestSupport.CreateSeededStoreAsync("dedupe-order", "A", "B", "A");
        using var session = new ConsoleSession(store, new UiEventRunner(static (_, _) => { }, static (_, _) => { }), new ConsoleLinkOpener());
        await ConsoleLogTestSupport.WaitForProjectionAsync(session.Controller, projection => projection.Rows.Count() == 3);

        session.Controller.ToggleDedupeCommand.Execute(null);
        await ConsoleLogTestSupport.WaitForProjectionAsync(session.Controller, projection => projection.Rows.Count() == 2);

        // The old console folded only consecutive equal lines. Core groups equal messages across the history
        // and shows the groups in the order of their latest event, so A, B, A becomes B, A x2.
        var projection = session.Controller.Projection;
        Assert.Equal(["B", "A"], ConsoleLogTestSupport.RowTexts(projection));
        Assert.Equal(1, projection.Rows[0].Count);
        Assert.Equal(2, projection.Rows[1].Count);
    }
}

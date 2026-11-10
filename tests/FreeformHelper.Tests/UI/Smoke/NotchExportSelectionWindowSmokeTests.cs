using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchExportSelectionWindowSmokeTests
{
    private static readonly int[] _smokeRowValues = [12, 100, 65535, 0, 65535, 0, 0];

    [AvaloniaFact]
    public void NotchExportSelectionWindow_Can_Layout_Headless()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                NotchAlgorithmVersion.V22,
                icIndex: 0,
                diffIndex: 12,
                regularPadIndex: 3456,
                cadPadId: 7890,
                values: _smokeRowValues,
                comment: "smoke")
        });

        var window = new NotchExportSelectionWindow
        {
            DataContext = new NotchExportSelectionViewModel(table)
        };
        HeadlessSessionGuardAttribute.CloseAtTestEnd(window);

        var size = new Size(1200, 800);
        window.Measure(size);
        window.Arrange(new Rect(size));

        Assert.NotNull(window);
    }

    [AvaloniaFact]
    public async Task ExportClick_ReturnsCheckedRowsInTableOrderAsync()
    {
        using var session = await NotchSelectionSession.CreateAsync();
        Assert.Equal(6, session.Model.Rows.Count);
        session.Model.Rows[1].IsSelected = false;
        session.Model.Rows[4].IsSelected = false;
        session.Model.SelectedSortModeOption = session.Model.SortModeOptions.Single(
            static option => option.Value == NotchExportSortMode.DiffAscending);

        UiEventProbe.Click(session.Window, button => Equals(button.Content, session.Model.ExportButtonText));
        await HeadlessUiPump.WaitForConditionAsync(() => session.Result.IsCompleted,
            "Export did not complete the notch selection request.");

        var result = Assert.IsType<NotchTable>(await session.Result);
        Assert.Equal(session.Table.Rows.Where((_, index) => index is not 1 and not 4), result.Rows);
        Assert.False(session.Window.IsVisible);
    }

    [AvaloniaFact]
    public async Task CancelClick_ReturnsCancelledSelectionAsync()
    {
        using var session = await NotchSelectionSession.CreateAsync();

        UiEventProbe.Click(session.Window, static button => Equals(button.Content, "Cancel"));
        await HeadlessUiPump.WaitForConditionAsync(() => session.Result.IsCompleted,
            "Cancel did not complete the notch selection request.");

        Assert.Null(await session.Result);
        Assert.False(session.Window.IsVisible);
    }

    [AvaloniaFact]
    public async Task ExportClick_WithUnsafeSimulation_OpensWarningAndKeepsSelectionPendingAsync()
    {
        using var session = await NotchSelectionSession.CreateAsync();
        session.Model.AttachSimulationSafetyAudit(SimulationSafetyAuditService.Analyze(
        [
            new NotchApplySimulationDiffCell(RegularPadId: 1, RegularRow: 0, RegularCol: 0,
                IcIndex: 0, DiffIndex: 10, BeforeValue: 400d, AfterValue: 500d),
        ]));

        UiEventProbe.Click(session.Window, button => Equals(button.Content, session.Model.ExportButtonText));
        var warning = await HeadlessUiPump.WaitForOwnedWindowAsync<WarningDialog>(session.Window, nameof(WarningDialog));
        try
        {
            Assert.True(session.Window.IsVisible);
            Assert.False(session.Result.IsCompleted);
            UiEventProbe.Click(warning, static button => button.Name == "OkButton");
            await HeadlessUiPump.WaitForConditionAsync(() => !warning.IsVisible && session.Window.IsEnabled,
                "Dismissing the export warning did not re-enable the selection window.");
            Assert.False(session.Result.IsCompleted);
            Assert.Equal(6, session.Model.SelectedCount);
        }
        finally
        {
            warning.Close();
        }
    }

    [AvaloniaFact]
    public async Task CopyPayloadClick_CopiesThePreviewedRowAsync()
    {
        using var session = await NotchSelectionSession.CreateAsync();
        session.Model.SelectedRow = session.Model.Rows[3];
        var expected = session.Model.SelectedRowCodePreviewText;
        var clipboard = Assert.IsAssignableFrom<IClipboard>(session.Window.Clipboard);
        await clipboard.SetTextAsync("previous clipboard value");

        UiEventProbe.Click(session.Window, static button => Equals(ToolTip.GetTip(button), "Copy payload"));
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(expected, await clipboard.TryGetTextAsync());
        Assert.False(session.Result.IsCompleted);
        Assert.True(session.Window.IsVisible);
    }

    [AvaloniaFact]
    public async Task FilterBuilderClick_AppliesQueryAndFiltersRowsAsync()
    {
        using var session = await NotchSelectionSession.CreateAsync();
        UiEventProbe.Click(session.Window, static button => Equals(ToolTip.GetTip(button), "Filter helper"));
        var dialog = await HeadlessUiPump.WaitForOwnedWindowAsync<NotchExportFilterBuilderWindow>(
            session.Window, nameof(NotchExportFilterBuilderWindow));
        try
        {
            dialog.FindControl<TextBox>("DiffTextBox")!.Text = "3";
            UiEventProbe.Click(dialog, static button => Equals(button.Content, "Apply"));
            await HeadlessUiPump.WaitForConditionAsync(() => session.Model.SearchKeyword == "diff=3",
                "Applying the filter builder did not update the selection query.");

            Assert.Equal(3, Assert.Single(session.Model.Rows).Row.DiffIndex);
            Assert.False(dialog.IsVisible);
            Assert.True(session.Window.IsVisible);
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public async Task ColumnFilterClick_AppliesCheckedValuesAsync()
    {
        using var session = await NotchSelectionSession.CreateAsync();
        UiEventProbe.Click(session.Window, static button => Equals(button.Tag, "diff"));
        var dialog = await HeadlessUiPump.WaitForOwnedWindowAsync<NotchExportColumnFilterWindow>(
            session.Window, nameof(NotchExportColumnFilterWindow));
        try
        {
            var filter = Assert.IsType<NotchExportColumnFilterDialogViewModel>(dialog.DataContext);
            filter.ClearAll();
            var selected = filter.Options[2];
            selected.IsSelected = true;
            UiEventProbe.Click(dialog, static button => Equals(button.Content, "Apply"));
            await HeadlessUiPump.WaitForConditionAsync(() => session.Model.Rows.Count == 1,
                "Applying the column filter did not filter the notch rows.");

            Assert.Equal(selected.Key, Assert.Single(session.Model.Rows).Row.DiffIndex.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            Assert.False(dialog.IsVisible);
            Assert.True(session.Window.IsVisible);
        }
        finally
        {
            dialog.Close();
        }
    }
}

internal sealed class NotchSelectionSession : IDisposable
{
    private NotchSelectionSession()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        Table = new NotchTable(Enumerable.Range(0, 6).Select(static index => new NotchTableRow(
            0, 5 - index, index, 100 + index,
            new NotchV22Node(5 - index, 100, 6 - index, 25, 65535, 0, 0), string.Empty)).ToArray());
        Model = new NotchExportSelectionViewModel(Table);
        Model.SetViewFilterModeCommand.Execute(NotchExportRowDisplayMode.AllRows);
        Helper = new FreeformHelperViewModel
        {
            CascadeNum = 1, XChannels = 2, YChannels = 1, ActiveAreaWidth = 20, ActiveAreaHeight = 10,
        };
        View = new FreeformHelperView { DataContext = Helper };
        Owner = new Window { Content = View, Width = 1200, Height = 800 };
        Owner.Show();
        Result = FreeformHelperViewPrivateAccess.ShowNotchExportSelectionWindowAsync(View, Model);
    }

    public NotchTable Table { get; }
    public NotchExportSelectionViewModel Model { get; }
    public FreeformHelperViewModel Helper { get; }
    public FreeformHelperView View { get; }
    public Window Owner { get; }
    public NotchExportSelectionWindow Window { get; private set; } = null!;
    public Task<NotchTable?> Result { get; }

    public static async Task<NotchSelectionSession> CreateAsync()
    {
        var session = new NotchSelectionSession();
        try
        {
            session.Window = await HeadlessUiPump.WaitForOwnedWindowAsync<NotchExportSelectionWindow>(
                session.Owner, nameof(NotchExportSelectionWindow));
            await session.Helper.WaitForGridRebuildIdleAsync();
            await HeadlessUiPump.FlushQueueAsync();
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        var window = FreeformHelperViewPrivateAccess.NotchExportSelectionWindow(View);
        if (window is not null)
        {
            foreach (var child in window.OwnedWindows.ToArray()) child.Close();
            window.Close();
        }

        Owner.Close();
    }
}

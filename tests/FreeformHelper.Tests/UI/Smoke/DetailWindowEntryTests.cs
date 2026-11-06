// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.SettingsSections;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DetailWindowEntryTests
{
    [AvaloniaFact]
    public async Task NotchDetail_ShowCommandDisplaysLoadedProjectAllocationsAndClosesAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var projectPath = Path.Combine(AppContext.BaseDirectory, Path.ChangeExtension(Path.GetRandomFileName(), ".json"));
        var vm = new FreeformHelperViewModel();
        var view = new FreeformHelperView { DataContext = vm };
        var owner = new Window { Content = view };
        try
        {
            await WriteNotchProjectAsync(projectPath);
            owner.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var cad = await LoadNotchProjectAsync(vm, projectPath);
            vm.ApplyCanvasSelection([cad.Id], Array.Empty<int>());

            Assert.True(vm.ShowNotchDetailCommand.CanExecute(null));
            await vm.ShowNotchDetailCommand.ExecuteAsync(null);
            var window = Assert.Single(owner.OwnedWindows.OfType<NotchDetailWindow>());
            var closedCount = 0;
            window.Closed += (_, _) => closedCount++;
            await HeadlessUiPump.FlushQueueAsync();

            var detail = Assert.IsType<NotchDetailViewModel>(window.DataContext);
            Assert.True(window.IsVisible);
            Assert.Equal("Notch detail", window.Title);
            Assert.Same(cad, detail.CadPad);
            Assert.Equal(FreeformType.XWay, detail.FreeformType);
            var text = window.GetVisualDescendants().OfType<SelectableTextBlock>().Select(static block => block.Text).ToArray();
            Assert.Contains(detail.CadTitle, text);
            Assert.Contains(detail.CadLayer, text);
            Assert.Contains(detail.FreeformLabel, text);
            var allocation = Assert.Single(detail.Allocations);
            var table = Assert.Single(window.GetVisualDescendants().OfType<ItemsControl>());
            Assert.Equal(detail.Allocations.Count, table.ItemCount);
            Assert.Same(allocation, Assert.Single(table.Items));
            Assert.Contains(allocation.Q7Text, text);

            window.Close();
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal(1, closedCount);
            Assert.False(window.IsVisible);
            Assert.DoesNotContain(window, owner.OwnedWindows);
            Assert.True(owner.IsVisible);
            Assert.True(owner.IsEnabled);
        }
        finally
        {
            CloseWindows(owner);
            vm.ClearSelectionCommand.Execute(null);
            File.Delete(projectPath);
        }
    }

    [AvaloniaFact]
    public async Task CascadeDetails_EditAndDoneUpdatesSettingsDraftAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var probe = new UiEventProbe();
        var vm = new FreeformHelperViewModel(null, probe.Runner) { CascadeNum = 1m };
        var original = Assert.Single(vm.CascadeIcSettings);
        original.XChannels = 3m;
        original.YChannels = 2m;
        var draft = vm.CreateSettingsWindowViewModel();
        var view = new SettingsGeneralSectionView { DataContext = draft };
        var owner = new Window { Content = view };
        try
        {
            owner.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var edit = Assert.Single(view.GetVisualDescendants().OfType<Button>(),
                static button => ToolTip.GetTip(button) is TextBlock { Text: "Edit per-IC X/Y channels." });
            edit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var window = Assert.Single(owner.OwnedWindows.OfType<CascadeIcSettingsWindow>());
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(window.IsVisible);
            Assert.Equal("Cascade IC details", window.Title);
            Assert.Same(draft, window.DataContext);
            var row = Assert.Single(draft.CascadeIcSettings);
            var scrubber = Assert.Single(window.GetVisualDescendants().OfType<NumberScrubber>(),
                control => ReferenceEquals(control.DataContext, row) && control.Value == 3m);
            var input = scrubber.FindControl<TextBox>("InputBox");
            Assert.NotNull(input);
            Assert.True(input.Focus());
            input.Text = "4";
            input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            await HeadlessUiPump.FlushQueueAsync();
            var done = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                static button => button.Content is string content && content == "Done");
            done.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await HeadlessUiPump.FlushQueueAsync();

            Assert.False(window.IsVisible);
            Assert.Empty(owner.OwnedWindows);
            Assert.True(owner.IsEnabled);
            Assert.Equal(4m, row.XChannels);
            Assert.Equal(4m, draft.XChannels);
            Assert.Equal(2m, row.YChannels);
            Assert.Equal(3m, original.XChannels);
            Assert.Empty(probe.Reports);
            Assert.Empty(probe.FallbackReports);
        }
        finally
        {
            CloseWindows(owner);
            await vm.WaitForGridRebuildIdleAsync().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
    }

    private static async Task<CadPad> LoadNotchProjectAsync(FreeformHelperViewModel vm, string projectPath)
    {
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);
        await vm.LoadProjectCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await vm.WaitForGridRebuildIdleAsync().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.Equal(9, vm.RegularPads.Count);
        await vm.MatchCommand.ExecuteAsync(null);
        await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);
        var cad = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
        var regular = Assert.Single(vm.RegularPads, pad => pad.MatchedCadPadId == cad.Id);
        regular.Freeform = FreeformType.XWay;
        return cad;
    }

    private static Task WriteNotchProjectAsync(string path)
    {
        var project = new ProjectFile
        {
            EmbeddedDxfName = "notch-detail.dxf",
            EmbeddedDxf = CreateNotchDxf(),
            Settings = new ProjectSettings
            {
                Grid = new GridSettings
                {
                    XChannels = 3,
                    YChannels = 3,
                    PerIcXChannels = [3],
                    PerIcYChannels = [3],
                    ActiveAreaWidth = 30,
                    ActiveAreaHeight = 30,
                    BoundsPaddingRatio = 0,
                },
            },
        };
        return new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
    }

    private static byte[] CreateNotchDxf()
    {
        (string Layer, int MinX, int MinY, int MaxX, int MaxY)[] pads =
        [
            ("frame", 0, 0, 1, 1),
            ("signal-a", 12, 12, 18, 18),
            ("signal-b", 22, 12, 28, 18),
            ("frame", 29, 29, 30, 30),
        ];
        var builder = new StringBuilder("0\r\nSECTION\r\n2\r\nENTITIES\r\n");
        foreach (var pad in pads)
        {
            builder.Append("0\r\nLWPOLYLINE\r\n8\r\n");
            builder.Append(pad.Layer).Append("\r\n70\r\n1\r\n90\r\n4\r\n");
            AppendVertex(builder, pad.MinX, pad.MinY);
            AppendVertex(builder, pad.MaxX, pad.MinY);
            AppendVertex(builder, pad.MaxX, pad.MaxY);
            AppendVertex(builder, pad.MinX, pad.MaxY);
        }

        builder.Append("0\r\nENDSEC\r\n0\r\nEOF\r\n");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void AppendVertex(StringBuilder builder, int x, int y)
    {
        builder.Append("10\r\n").Append(x.ToString(CultureInfo.InvariantCulture)).Append("\r\n20\r\n");
        builder.Append(y.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
    }

    private static void CloseWindows(Window owner)
    {
        foreach (var window in owner.OwnedWindows.ToArray())
        {
            window.Close();
        }

        owner.Close();
    }
}

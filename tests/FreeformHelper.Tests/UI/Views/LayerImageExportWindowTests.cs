// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.CadExport;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class LayerImageExportWindowTests
{
    [AvaloniaFact]
    public async Task LargeLayerListRealizesOnlyTheViewportAsync()
    {
        using var session = new LayerImageExportViewTestSession(LayerImageExportViewTestSession.CreateManyLayersContext(500));
        await session.ShowAsync();

        var list = session.Window.FindControl<ListBox>("LayerList")!;

        Assert.Equal(500, list.ItemCount);
        Assert.InRange(list.GetRealizedContainers().Count(), 1, 99);
    }

    [AvaloniaFact]
    public async Task SearchTypingFiltersAndEscapeClearsAsync()
    {
        using var session = new LayerImageExportViewTestSession();
        await session.ShowAsync();
        var search = session.Window.FindControl<TextBox>("SearchBox")!;
        search.Focus();

        session.Window.KeyTextInput("ref");
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(2, session.Model.VisibleRows.Count);
        Assert.Equal("ref", session.Model.SearchText);
        Assert.All(session.Model.VisibleRows, static row => Assert.NotEmpty(row.HighlightRanges));

        session.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        session.Window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(string.Empty, search.Text);
        Assert.Equal(9, session.Model.VisibleRows.Count);
    }

    [AvaloniaFact]
    public async Task SearchWithNoMatchesShowsTheEmptyHintAsync()
    {
        using var session = new LayerImageExportViewTestSession();
        await session.ShowAsync();

        session.Model.SearchText = "missing-layer";

        Assert.Equal(0, session.Window.FindControl<ListBox>("LayerList")!.ItemCount);
        Assert.True(session.Window.FindControl<TextBlock>("NoMatchesHint")!.IsVisible);
        Assert.False(session.Window.FindControl<TextBlock>("SelectLayerHint")!.IsVisible);
    }

    [AvaloniaFact]
    public async Task OpeningFocusesTheLayerListAsync()
    {
        using var session = new LayerImageExportViewTestSession();
        await session.ShowAsync();

        Assert.Same(session.Window.FindControl<ListBox>("LayerList"), session.Window.FocusManager!.GetFocusedElement());
    }

    [AvaloniaFact]
    public async Task ControlFFocusesSearchFromTheFooterAsync()
    {
        using var session = new LayerImageExportViewTestSession();
        await session.ShowAsync();
        session.Window.FindControl<Button>("ExportButton")!.Focus();

        session.Window.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");
        session.Window.KeyRelease(Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");

        Assert.Same(session.Window.FindControl<TextBox>("SearchBox"), session.Window.FocusManager!.GetFocusedElement());
    }

    [AvaloniaFact]
    public async Task AllLayersReflectsTheModelAndTogglesOnlyVisibleRowsAsync()
    {
        using var session = new LayerImageExportViewTestSession();
        await session.ShowAsync();
        var all = session.Window.FindControl<CheckBox>("AllLayersCheckBox")!;
        Assert.Null(all.IsChecked);
        session.Model.SearchText = "ref";
        await HeadlessUiPump.FlushQueueAsync();
        Assert.True(all.IsChecked);

        Click(session.Window, all);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.False(all.IsChecked);
        Assert.All(session.Model.VisibleRows, static row => Assert.False(row.IsChecked));
        session.Model.SearchText = string.Empty;
        Assert.True(Assert.Single(session.Model.VisibleRows, static row => row.Name == "NC.drawing").IsChecked);
        Assert.Null(all.IsChecked);
    }

    [AvaloniaFact]
    public async Task ExportIsDisabledUntilALayerIsCheckedAsync()
    {
        var context = LayerImageExportViewTestSession.CreateContext() with { InitialCheckedLayers = Array.Empty<string>() };
        using var session = new LayerImageExportViewTestSession(context);
        await session.ShowAsync();
        var export = session.Window.FindControl<Button>("ExportButton")!;

        Assert.False(export.IsEffectivelyEnabled);
        Assert.True(session.Window.FindControl<TextBlock>("SelectLayerHint")!.IsVisible);
        session.Model.VisibleRows[0].IsChecked = true;
        Assert.True(export.IsEffectivelyEnabled);
        Assert.False(session.Window.FindControl<TextBlock>("SelectLayerHint")!.IsVisible);
    }

    [AvaloniaFact]
    public async Task DisablingTheOwnerKeepsTheSelectedPreviewVisibleAsync()
    {
        using var session = new LayerImageExportViewTestSession();
        await session.ShowAsync();

        session.Window.IsEnabled = false;

        Assert.True(session.Window.FindControl<Image>("PreviewImage")!.IsVisible);
        Assert.False(session.Window.FindControl<TextBlock>("SelectLayerHint")!.IsVisible);
    }

    [AvaloniaFact]
    public async Task FileSettingsBindingsReachTheSaveRequestAsync()
    {
        var context = LayerImageExportViewTestSession.CreateContext() with
        {
            PickSavePathAsync = static (_, _) => Task.FromResult<string?>("sample.png"),
        };
        using var session = new LayerImageExportViewTestSession(context);
        var settings = new LayerImageExportSettingsViewModel(session.Model, context.PickSavePathAsync);
        var dialog = new LayerImageExportSettingsDialog { DataContext = settings };
        try
        {
            dialog.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var format = dialog.FindControl<ComboBox>("FormatComboBox")!;
            Assert.Equal(DxfLayerImageFormat.Bmp, format.SelectedValue);

            format.SelectedValue = DxfLayerImageFormat.Png;
            dialog.FindControl<CheckBox>("OneFilePerLayerCheckBox")!.IsChecked = true;
            await settings.SaveCommand.ExecuteAsync(null);

            Assert.NotNull(settings.Request);
            Assert.Equal(DxfLayerImageFormat.Png, settings.Request.Format);
            Assert.True(settings.Request.OneFilePerLayer);
            Assert.Equal("sample.png", settings.Request.SavePath);
            Assert.Same(settings.SaveCommand, dialog.FindControl<Button>("SaveButton")!.Command);
        }
        finally
        {
            dialog.Close();
        }
    }

    private static void Click(Window window, Control target)
    {
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window);
        Assert.NotNull(point);
        window.MouseDown(point.Value, MouseButton.Left);
        window.MouseUp(point.Value, MouseButton.Left);
    }
}

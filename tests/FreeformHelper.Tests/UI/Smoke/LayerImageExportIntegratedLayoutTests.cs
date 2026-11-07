// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class LayerImageExportIntegratedLayoutTests
{
    [AvaloniaFact]
    public async Task ControlFFocusesSearchAndEscapeClearsItAsync()
    {
        using var session = new LayerImageExportViewTestSession();
        await session.ShowAsync();
        var search = session.Window.FindControl<TextBox>("SearchBox")!;
        Assert.Same(session.Window.FindControl<ListBox>("LayerList"), session.Window.FocusManager!.GetFocusedElement());
        session.Window.FindControl<Button>("ExportButton")!.Focus();

        PressKey(session.Window, Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");
        Assert.Same(search, session.Window.FocusManager!.GetFocusedElement());
        session.Window.KeyTextInput("ref");
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal("ref", session.Model.SearchText);
        Assert.Equal(2, session.Model.VisibleRows.Count);

        PressKey(session.Window, Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(string.Empty, search.Text);
        Assert.Equal(string.Empty, session.Model.SearchText);
        Assert.Equal(9, session.Model.VisibleRows.Count);
    }

    [AvaloniaFact]
    public async Task SelectAllNamesItsScopeAndClearButtonRestoresAllLayersAsync()
    {
        using var session = new LayerImageExportViewTestSession();
        await session.ShowAsync();
        var all = session.Window.FindControl<CheckBox>("AllLayersCheckBox")!;
        var clear = session.Window.FindControl<Button>("ClearSearchButton")!;
        Assert.Equal("All layers", all.Content);
        Assert.Same(session.Model.ClearSearchCommand, clear.Command);
        Assert.Equal("Clear search (Esc)", TipText(clear));

        session.Model.SearchText = "ref";
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal("All shown layers", all.Content);
        Click(session.Window, all);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.All(session.Model.VisibleRows, static row => Assert.False(row.IsChecked));

        Click(session.Window, clear);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(string.Empty, session.Model.SearchText);
        Assert.Equal("All layers", all.Content);
        Assert.Equal(9, session.Model.VisibleRows.Count);
        Assert.True(Assert.Single(session.Model.VisibleRows, static row => row.Name == "NC.drawing").IsChecked);
    }

    [AvaloniaFact]
    public async Task AdvancedStartsCollapsedAndKeepsSizeFieldsVisibleAsync()
    {
        using var session = new LayerImageExportViewTestSession();
        await session.ShowAsync();
        var advanced = session.Window.FindControl<HidePanelBlock>("AdvancedOptions")!;
        var width = NumberField(session.Window, "Pixels X");
        var height = NumberField(session.Window, "Pixels Y");
        Assert.Equal("Advanced", advanced.Title);
        Assert.False(advanced.IsExpanded);
        Assert.True(Label(session.Window, "Width").IsEffectivelyVisible);
        Assert.True(Label(session.Window, "Height").IsEffectivelyVisible);
        Assert.True(width.IsEffectivelyVisible);
        Assert.True(height.IsEffectivelyVisible);
        advanced.IsExpanded = true;
        await HeadlessUiPump.FlushQueueAsync();

        var paddingX = NumberField(session.Window, "Padding X");
        var paddingY = NumberField(session.Window, "Padding Y");
        var lineWidth = NumberField(session.Window, "Line width in pixels");
        Assert.Contains(advanced, paddingX.GetVisualAncestors());
        Assert.Contains(advanced, paddingY.GetVisualAncestors());
        Assert.Contains(advanced, lineWidth.GetVisualAncestors());
        Assert.True(Label(session.Window, "Padding").IsEffectivelyVisible);
        Assert.True(Label(session.Window, "Line width").IsEffectivelyVisible);
        Assert.True(width.IsEffectivelyVisible);
        Assert.True(height.IsEffectivelyVisible);
        Assert.DoesNotContain(advanced, width.GetVisualAncestors());
        Assert.DoesNotContain(advanced, height.GetVisualAncestors());
        paddingX.SetCurrentValue(NumberScrubber.ValueProperty, 12m);
        paddingY.SetCurrentValue(NumberScrubber.ValueProperty, 14m);
        lineWidth.SetCurrentValue(NumberScrubber.ValueProperty, 2.5m);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal(12m, session.Model.PreviewSettings.PaddingX);
        Assert.Equal(14m, session.Model.PreviewSettings.PaddingY);
        Assert.Equal(2.5m, session.Model.PreviewSettings.LineWidthPixels);
    }

    [AvaloniaFact]
    public async Task LeftSummaryAndFooterKeepVisibleAndTotalScopesAsync()
    {
        using var session = new LayerImageExportViewTestSession();
        await session.ShowAsync();
        var left = session.Window.FindControl<TextBlock>("SelectionSummary")!;
        var footer = session.Window.FindControl<TextBlock>("ExportSummary")!;
        Assert.True(left.IsEffectivelyVisible);
        Assert.True(footer.IsEffectivelyVisible);
        Assert.Equal("5 of 9 shown selected · 5 / 9 layers selected", left.Text);
        Assert.Contains("5 layers selected", footer.Text!, StringComparison.Ordinal);
        Assert.Contains(session.Model.PreviewSettings.OutputText, footer.Text!, StringComparison.Ordinal);

        session.Model.SearchText = "ref";
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal("2 of 2 shown selected · 5 / 9 layers selected", left.Text);
        Assert.Contains("5 layers selected", footer.Text!, StringComparison.Ordinal);
        session.Model.ToggleAllLayersCommand.Execute(null);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal("0 of 2 shown selected · 3 / 9 layers selected", left.Text);
        Assert.Contains("3 layers selected", footer.Text!, StringComparison.Ordinal);

        session.Model.SearchText = "missing-layer";
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal("0 of 0 shown selected · 3 / 9 layers selected", left.Text);
        Assert.Contains("3 layers selected", footer.Text!, StringComparison.Ordinal);
        Assert.True(session.Window.FindControl<Button>("ExportButton")!.IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public async Task FooterDisablesExportUntilSelectedAndTracksOutputSizeAsync()
    {
        var context = LayerImageExportViewTestSession.CreateContext() with { InitialCheckedLayers = Array.Empty<string>() };
        using var session = new LayerImageExportViewTestSession(context);
        await session.ShowAsync();
        var export = session.Window.FindControl<Button>("ExportButton")!;
        var footer = session.Window.FindControl<TextBlock>("ExportSummary")!;
        Assert.Equal("Export…", export.Content);
        Assert.Same(session.Model.ConfirmCommand, export.Command);
        Assert.False(export.IsEffectivelyEnabled);
        Assert.Contains("0 layers selected", footer.Text!, StringComparison.Ordinal);

        session.Model.VisibleRows[0].IsChecked = true;
        session.Model.PreviewSettings.KeepRatio = false;
        NumberField(session.Window, "Pixels X").SetCurrentValue(NumberScrubber.ValueProperty, 720m);
        NumberField(session.Window, "Pixels Y").SetCurrentValue(NumberScrubber.ValueProperty, 480m);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.True(export.IsEffectivelyEnabled);
        Assert.Contains("1 layer selected", footer.Text!, StringComparison.Ordinal);
        Assert.Contains("720 × 480 px", footer.Text!, StringComparison.Ordinal);
        Assert.Equal(720m, session.Model.PreviewSettings.PixelsX);
        Assert.Equal(480m, session.Model.PreviewSettings.PixelsY);
    }

    [AvaloniaFact]
    public async Task ImageBackgroundHintUsesTheExistingPreviewSettingAsync()
    {
        using var session = new LayerImageExportViewTestSession();
        await session.ShowAsync();
        var theme = Assert.Single(session.Window.GetVisualDescendants().OfType<Button>(),
            button => ReferenceEquals(button.Command, session.Model.PreviewSettings.ToggleThemeCommand));
        Assert.Equal("Image background: light / dark", TipText(theme));
        Assert.Contains("actionIconButton", theme.Classes);
        Assert.True(session.Model.PreviewSettings.UseDarkTheme);

        Click(session.Window, theme);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.False(session.Model.PreviewSettings.UseDarkTheme);
    }

    private static NumberScrubber NumberField(LayerImageExportWindow window, string name) =>
        Assert.Single(window.GetVisualDescendants().OfType<NumberScrubber>(),
            field => string.Equals(name, AutomationProperties.GetName(field), StringComparison.Ordinal));

    private static TextBlock Label(LayerImageExportWindow window, string text) =>
        Assert.Single(window.GetVisualDescendants().OfType<TextBlock>(),
            label => string.Equals(text, label.Text, StringComparison.Ordinal));

    private static string TipText(Control control) => ToolTip.GetTip(control) switch
    {
        string text => text,
        TextBlock text => text.Text ?? string.Empty,
        _ => string.Empty,
    };

    private static void PressKey(Window window, Key key, RawInputModifiers modifiers, PhysicalKey physicalKey, string? symbol)
    {
        window.KeyPress(key, modifiers, physicalKey, symbol);
        window.KeyRelease(key, modifiers, physicalKey, symbol);
    }

    private static void Click(Window window, Control target)
    {
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window);
        Assert.NotNull(point);
        window.MouseDown(point.Value, MouseButton.Left);
        window.MouseUp(point.Value, MouseButton.Left);
    }
}

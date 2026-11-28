// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class P1RoundThreeAppearanceTests
{
    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task ExistingExportChoiceRowsKeepSoftFillWithoutChangingApplicationDefaultAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var session = new LayerImageExportViewTestSession();
        session.Window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        session.Window.Classes.Add("reducedMotion");
        var outside = new CheckBox { Content = "Application default", IsChecked = true };
        var window = CreateWindow(outside, theme);
        try
        {
            window.Show();
            await session.ShowAsync();
            await HeadlessUiPump.FlushQueueAsync();
            var list = session.Window.FindControl<ListBox>("LayerList")!;
            var item = Assert.IsType<ListBoxItem>(list.ContainerFromIndex(0));
            var row = item.GetVisualDescendants().OfType<CheckBox>().Single();
            var fill = row.GetVisualDescendants().OfType<Border>().Single(static border => border.Name == "ChoiceRow");
            var outsideFill = outside.GetVisualDescendants().OfType<Border>().Single(static border => border.Name == "ChoiceRow");

            Assert.True(row.IsChecked);
            Assert.True(ColorOf(fill.Background).A > 0);
            Assert.Equal(0, ColorOf(outsideFill.Background).A);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task SettingsScrubberHasNoRestDiscAndKeepsHoverAndDisabledCuesAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var scrubber = new NumberScrubber { Value = 5, Width = 200 };
        var page = new StackPanel { Children = { scrubber } };
        page.Classes.Add("settingsPage");
        var window = CreateWindow(page, theme);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var area = scrubber.FindControl<Border>("ScrubArea")!;
            var field = scrubber.FindControl<Border>("RootBorder")!;
            var enabledFill = ColorOf(field.Background);
            Assert.Equal(0, ColorOf(area.Background).A);
            Assert.Equal(default, area.BorderThickness);
            var point = Avalonia.VisualExtensions.TranslatePoint(area, new Avalonia.Point(area.Bounds.Width / 2, area.Bounds.Height / 2), window)!.Value;

            window.MouseMove(point);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(area.IsPointerOver);
            Assert.True(ColorOf(area.Background).A > 0);
            Assert.NotEqual(ColorOf(field.Background), ColorOf(area.Background));

            scrubber.IsEnabled = false;
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal(0, ColorOf(area.Background).A);
            Assert.NotEqual(enabledFill, ColorOf(field.Background));
            Assert.Equal(ColorOf((IBrush)scrubber.FindResource(scrubber.ActualThemeVariant, "NfcTextMutedBrush")!), ColorOf(scrubber.FindControl<TextBox>("InputBox")!.Foreground));
        }
        finally
        {
            window.Close();
        }
    }

    private static Window CreateWindow(Control content, string theme)
    {
        var window = new Window
        {
            Width = 1000,
            Height = 800,
            Content = content,
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        window.Classes.Add("reducedMotion");
        return window;
    }

    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
}

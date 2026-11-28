// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class P1SelectionAppearanceTests
{
    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task CoreControlFamiliesUseApplicationSelectionPaletteAsync(string theme)
    {
        using var session = new ThemeSession(theme);
        var list = new ListBox { ItemsSource = new[] { "Synthetic layer", "Synthetic grid" }, SelectedIndex = 0 };
        var tab = new TabItem { Header = "Overview" };
        var tabs = new TabControl { Items = { tab, new TabItem { Header = "Details" } }, SelectedIndex = 0 };
        var check = new CheckBox { Content = "Include layer", IsChecked = true };
        var toggle = new ToggleSwitch { IsChecked = true };
        var window = new Window { Width = 400, Height = 400, Content = new StackPanel { Children = { tabs, list, check, toggle } } };
        window.Classes.Add("reducedMotion");
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal(ResourceColor(toggle, "NfcAccentBrush"), ColorOf(toggle.Background));
            Assert.Equal(ResourceColor(tab, "Nvt.Tab.SelectedFillBrush"), ColorOf(tab.Background));
            Assert.Equal(ResourceColor(list, "Nvt.List.SelectedBrush"), ColorOf(Assert.IsType<ListBoxItem>(list.ContainerFromIndex(0)).Background));
            var indicator = check.GetVisualDescendants().OfType<Border>().Single(static border => border.Name == "ChoiceIndicator");
            Assert.Equal(ResourceColor(check, "NfcAccentBrush"), ColorOf(indicator.Background));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task SettingsNavigationKeepsMarkWithoutBlockFillAfterSelectionChangesAsync(string theme)
    {
        using var session = new ThemeSession(theme);
        var draft = new FreeformHelperViewModel().CreateSettingsWindowViewModel();
        var window = new SettingsWindow { DataContext = draft };
        window.Classes.Add("reducedMotion");
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var general = window.FindControl<ToggleButton>("GeneralNavButton")!;
            var geometry = window.FindControl<ToggleButton>("Step1NavButton")!;
            AssertMarked(general);
            AssertUnmarked(geometry);

            geometry.IsChecked = true;
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal(SettingsWindowSection.Step1GeometryMatch, draft.SelectedSection);
            AssertMarked(geometry);
            AssertUnmarked(general);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task RightTabsAndQuickToolbarKeepMarksAndFullOpacityAsync(string theme)
    {
        using var session = new ThemeSession(theme);
        var viewModel = new FreeformHelperViewModel { ShowCad = true, ShowRegular = false };
        var view = new FreeformHelperView { DataContext = viewModel };
        var window = new Window { Width = 1440, Height = 900, Content = view };
        window.Classes.Add("reducedMotion");
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var tabHost = view.GetVisualDescendants().OfType<Border>()
                .Single(static border => border.Classes.Contains("rightPanelTabHost"));
            var tabs = tabHost.GetVisualDescendants().OfType<ToggleButton>().ToArray();
            Assert.Equal(2, tabs.Length);
            AssertMarked(tabs[0]);
            AssertUnmarked(tabs[1]);
            var toolbar = view.GetVisualDescendants().OfType<Border>()
                .Single(static border => border.Classes.Contains("canvasQuickToolbar"));
            Assert.Equal(1, toolbar.Opacity);
            var toggles = toolbar.GetVisualDescendants().OfType<ToggleButton>().ToArray();
            AssertMarked(toggles[0]);
            AssertUnmarked(toggles[1]);

            tabs[1].IsChecked = true;
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(viewModel.IsRightPanelInspectorTab);
            AssertMarked(tabs[1]);
            AssertUnmarked(tabs[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task ScrubAreaSharesFieldAtRestAndShowsHoverCueAsync(string theme)
    {
        using var session = new ThemeSession(theme);
        var scrubber = new NumberScrubber { Value = 5, Width = 200 };
        var window = new Window { Width = 320, Height = 180, Content = scrubber };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var area = scrubber.FindControl<Border>("ScrubArea")!;
            Assert.Equal(0, ColorOf(area.Background).A);
            Assert.Equal(default, area.BorderThickness);
            var origin = Avalonia.VisualExtensions.TranslatePoint(area, new Avalonia.Point(area.Bounds.Width / 2, area.Bounds.Height / 2), window)!.Value;

            window.MouseMove(origin);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(area.IsPointerOver);
            Assert.True(ColorOf(area.Background).A > 0);
            Assert.True(area.IsHitTestVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task ProductPanelHeaderKeepsItsAppearanceAndCollapseBindingAsync(string theme)
    {
        using var session = new ThemeSession(theme);
        var panel = new CollapsiblePanel { Title = "Display", Content = new TextBlock { Text = "Panel content" } };
        var window = new Window { Width = 400, Height = 200, Content = panel };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var header = panel.GetVisualDescendants().OfType<ToggleButton>().Single();
            var padding = header.Padding;
            Assert.DoesNotContain("actionGhost", header.Classes);
            Assert.Equal(0, ColorOf(header.Background).A);
            Assert.True(header.BorderThickness.Bottom > 0);
            Assert.True(panel.IsExpanded);

            header.IsChecked = false;
            await HeadlessUiPump.FlushQueueAsync();

            Assert.False(panel.IsExpanded);
            Assert.Equal(padding, header.Padding);
            Assert.Equal(0, ColorOf(header.Background).A);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertMarked(ToggleButton button)
    {
        Assert.True(button.IsChecked);
        Assert.Equal(0, ColorOf(button.Background).A);
        Assert.True(button.BorderThickness.Left > 0);
        Assert.Equal(ResourceColor(button, "NfcAccentBrush"), ColorOf(button.BorderBrush));
        var body = button.GetVisualDescendants().OfType<Border>()
            .Single(static border => border.Name == "RoleBorder");
        Assert.Equal(0, ColorOf(body.Background).A);
        Assert.Equal(ColorOf(button.BorderBrush), ColorOf(body.BorderBrush));
    }

    private static void AssertUnmarked(ToggleButton button)
    {
        Assert.False(button.IsChecked);
        Assert.Equal(0, ColorOf(button.Background).A);
        Assert.Equal(0, ColorOf(button.BorderBrush).A);
    }

    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static Color ResourceColor(Control control, string key)
    {
        Assert.True(global::Avalonia.Application.Current!.TryGetResource(key, control.ActualThemeVariant, out var resource));
        return ColorOf(resource as IBrush);
    }

    private sealed class ThemeSession : IDisposable
    {
        private readonly ThemeVariant? _previousTheme;

        public ThemeSession(string theme)
        {
            HeadlessAppBootstrap.EnsureInitialized();
            _previousTheme = global::Avalonia.Application.Current!.RequestedThemeVariant;
            global::Avalonia.Application.Current.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        public void Dispose() => global::Avalonia.Application.Current!.RequestedThemeVariant = _previousTheme;
    }
}

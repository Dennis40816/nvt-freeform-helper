// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views.SettingsSections;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class P1RoundTwoAppearanceTests
{
    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task SizingScopeKeepsVisibleStateAndAccessibleSwitchAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var draft = new FreeformHelperViewModel().CreateSettingsWindowViewModel();
        draft.UseLocalSizing = false;
        var view = new SettingsGeneralSectionView { DataContext = draft };
        var window = CreateWindow(view, theme);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var toggle = view.FindControl<ToggleSwitch>("SizingScopeSwitch")!;
            var labels = ((StackPanel)toggle.Parent!).Children.OfType<TextBlock>().ToArray();
            Assert.Equal("Sizing scope", AutomationProperties.GetName(toggle));
            Assert.Equal("Global", Assert.Single(labels, static text => text.IsVisible).Text);
            Assert.Equal(Convert.ToDouble(toggle.FindResource("FormControlHeight"), System.Globalization.CultureInfo.InvariantCulture), toggle.Bounds.Height);

            toggle.IsChecked = true;
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(draft.UseLocalSizing);
            Assert.Equal("Local", Assert.Single(labels, static text => text.IsVisible).Text);

            draft.UseLocalSizing = false;
            await HeadlessUiPump.FlushQueueAsync();

            Assert.False(toggle.IsChecked);
            Assert.Equal("Global", Assert.Single(labels, static text => text.IsVisible).Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task ApplicationChoiceRowsHaveNoRestFillButKeepHoverAndMarksAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var checkedRow = new CheckBox { Content = "Selected layer", IsChecked = true };
        var choices = new StackPanel
        {
            Children =
            {
                checkedRow,
                new CheckBox { Content = "Other layer", IsChecked = false },
                new CheckBox { Content = "Partial group", IsThreeState = true, IsChecked = null },
                new CheckBox { Content = "Disabled layer", IsChecked = true, IsEnabled = false },
                new RadioButton { Content = "Selected mode", IsChecked = true },
            },
        };
        var window = CreateWindow(choices, theme);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.All(choices.Children, static row => Assert.Equal(0, ColorOf(ChoiceRow(row).Background).A));
            var indicator = checkedRow.GetVisualDescendants().OfType<Border>()
                .Single(static border => border.Name == "ChoiceIndicator");
            Assert.True(ColorOf(indicator.Background).A > 0);
            var point = Avalonia.VisualExtensions.TranslatePoint(checkedRow, new Avalonia.Point(checkedRow.Bounds.Width / 2, checkedRow.Bounds.Height / 2), window)!.Value;

            window.MouseMove(point);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(checkedRow.IsPointerOver);
            Assert.True(ColorOf(ChoiceRow(checkedRow).Background).A > 0);
            Assert.True(checkedRow.IsChecked);
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

    private static Border ChoiceRow(Control control) => control.GetVisualDescendants().OfType<Border>()
        .Single(static border => border.Name == "ChoiceRow");
}

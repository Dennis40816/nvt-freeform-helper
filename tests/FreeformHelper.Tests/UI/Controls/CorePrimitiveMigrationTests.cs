// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.PadInfoSections;
using FreeformHelper.UI.Views.SettingsSections;
using Nvt.Core.Avalonia.Primitives;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CorePrimitiveMigrationTests
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task SettingsFieldTileReceivesAndOpensLabelTooltipAsync(string theme) =>
        WithWindowAsync(theme, static () => new SettingsGeneralSectionView
        {
            DataContext = new SettingsWindowViewModel(new FreeformHelperViewModel()),
        }, async window =>
        {
            var label = Assert.Single(window.GetVisualDescendants().OfType<InfoLabel>(),
                static label => label.Text == "Cascade");
            var tile = label.GetVisualAncestors().OfType<Control>()
                .First(static control => control.Classes.Contains("settingsFieldTile"));
            Assert.Equal(label.Tip, Assert.IsType<TextBlock>(ToolTip.GetTip(tile)).Text);
            AssertLabelAppearance(label);

            label.Tip = "Updated field help";
            Assert.Equal(label.Tip, Assert.IsType<TextBlock>(ToolTip.GetTip(tile)).Text);
            ToolTip.SetIsOpen(tile, true);
            try
            {
                await HeadlessUiPump.FlushQueueAsync();
                Assert.True(ToolTip.GetIsOpen(tile));
                var tipText = Assert.IsType<TextBlock>(ToolTip.GetTip(tile));
                Assert.NotNull(TopLevel.GetTopLevel(tipText));
                AssertBrush(tipText, "NfcTextStrongBrush", tipText.Foreground);
            }
            finally
            {
                ToolTip.SetIsOpen(tile, false);
            }
        });

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task StandaloneInfoLabelKeepsApplicationAppearanceAsync(string theme) =>
        WithWindowAsync(theme, static () => new InfoLabel { Text = "Cascade" }, window =>
        {
            AssertLabelAppearance(Assert.IsType<InfoLabel>(window.Content));
            return Task.CompletedTask;
        });

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task HelpSectionFramesKeepApplicationAppearanceAsync(string theme) =>
        WithWindowAsync(theme, static () => new HowToUseView { DataContext = new HowToUseViewModel() }, async window =>
        {
            Assert.All(window.GetVisualDescendants().OfType<HidePanelBlock>(), static group => group.IsExpanded = true);
            await HeadlessUiPump.FlushQueueAsync();
            var frames = window.GetVisualDescendants().OfType<SectionFrame>().ToArray();
            Assert.NotEmpty(frames);
            Assert.All(frames, AssertApplicationFrameAppearance);
        });

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task ExistingPadInfoFramesKeepCoreAppearanceAsync(string theme) =>
        WithWindowAsync(theme, static () => new RegularPadInfoContentView(), window =>
        {
            var frames = window.GetVisualDescendants().OfType<SectionFrame>().ToArray();
            Assert.NotEmpty(frames);
            Assert.All(frames, frame =>
            {
                var title = FrameTitle(frame);
                AssertBrush(title, "NfcTextStrongBrush", title.Foreground);
                AssertBrush(title, "NfcSurfaceBrush", title.Background);
                Assert.All(FrameDividers(frame), divider => AssertBrush(divider, "NfcSurfaceBrush", divider.Background));
            });
            return Task.CompletedTask;
        });

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task DevPreviewUsesBothPrimitivesWithApplicationAppearanceAsync(string theme) =>
        WithWindowAsync(theme, static () => new DevView { DataContext = new DevViewModel() }, window =>
        {
            var view = Assert.IsType<DevView>(window.Content);
            var frame = Assert.IsType<SectionFrame>(view.FindControl<SectionFrame>("DevCoreSectionFrame"));
            var label = Assert.IsType<InfoLabel>(view.FindControl<InfoLabel>("DevCoreInfoLabel"));
            AssertApplicationFrameAppearance(frame);
            AssertLabelAppearance(label);
            var tile = label.GetVisualAncestors().OfType<Control>()
                .First(static control => control.Classes.Contains("settingsFieldTile"));
            Assert.False(string.IsNullOrWhiteSpace(label.Tip));
            Assert.Equal(label.Tip, Assert.IsType<TextBlock>(ToolTip.GetTip(tile)).Text);
            return Task.CompletedTask;
        });

    private static void AssertLabelAppearance(InfoLabel label)
    {
        var text = Assert.Single(label.GetVisualDescendants().OfType<SelectableTextBlock>());
        AssertBrush(text, "NfcTextBrush", text.Foreground);
        Assert.Null(text.Background);
        Assert.Null(label.Background);
        Assert.Equal(FontWeight.SemiBold, text.FontWeight);
        Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
        Assert.Equal(Resource(text, "FontFamilyUi"), text.FontFamily);
    }

    private static void AssertApplicationFrameAppearance(SectionFrame frame)
    {
        var title = FrameTitle(frame);
        AssertBrush(title, "NfcTextBrush", title.Foreground);
        AssertBrush(title, "BrushPadInfoPanelBackground", title.Background);
        Assert.Equal(FontWeight.SemiBold, title.FontWeight);
        Assert.Equal(Resource(title, "Inset8_4"), title.Padding);
        Assert.Equal(Resource(title, "FontFamilyUi"), title.FontFamily);
        Assert.All(FrameDividers(frame), divider =>
        {
            AssertBrush(divider, "BrushWhite", divider.Background);
            Assert.Equal(Resource(divider, "Space1"), divider.Height);
        });
    }

    private static SelectableTextBlock FrameTitle(SectionFrame frame) =>
        Assert.Single(frame.GetVisualDescendants().OfType<SelectableTextBlock>(),
            static text => text.Classes.Contains("sectionDividerText"));

    private static Border[] FrameDividers(SectionFrame frame)
    {
        var dividers = frame.GetVisualDescendants().OfType<Border>()
            .Where(static border => border.Classes.Contains("sectionDividerLine")).ToArray();
        Assert.NotEmpty(dividers);
        return dividers;
    }

    private static void AssertBrush(Control control, string resourceKey, IBrush? actual) =>
        Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(Resource(control, resourceKey)).Color,
            Assert.IsAssignableFrom<ISolidColorBrush>(actual).Color);

    private static object Resource(Control control, string key)
    {
        Assert.True(control.TryFindResource(key, control.ActualThemeVariant, out var resource));
        Assert.NotNull(resource);
        return resource;
    }

    private static async Task WithWindowAsync(string theme, Func<Control> createContent, Func<Window, Task> assertions)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var window = new Window
        {
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = createContent(),
            Width = 1000,
            Height = 900,
        };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            await assertions(window);
        }
        finally
        {
            window.Close();
        }
    }
}

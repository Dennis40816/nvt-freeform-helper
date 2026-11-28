// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class P1FluentAccentAppearanceTests(ITestOutputHelper output)
{
    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task FluentIndicatorsUseTheProductAccentAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var app = global::Avalonia.Application.Current!;
        var previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        try
        {
            await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
            var header = Assert.Single(fixture.Window.GetVisualDescendants().OfType<WorkspaceHeader>());
            var popup = header.FindControl<Popup>("ViewMenuPopup")!;
            popup.IsOpen = true;
            foreach (var block in popup.Child!.GetLogicalDescendants().OfType<HidePanelBlock>().ToArray())
            {
                block.IsExpanded = true;
            }

            fixture.Window.ShowTopToast("Accent preview", NotificationType.Information);
            var progress = new ProgressBar { Value = 50 };
            var host = new Window { Content = progress, Width = 320, Height = 100 };
            try
            {
                host.Show();
                await HeadlessUiPump.FlushQueueAsync();
                var accent = ColorOf(header.FindResource(header.ActualThemeVariant, "NfcAccentBrush"));
                var sliders = popup.Child!.GetVisualDescendants().OfType<Slider>().ToArray();
                Assert.NotEmpty(sliders);
                var colors = new List<(string Part, Color Color)>();
                foreach (var slider in sliders)
                {
                    var track = Assert.Single(slider.GetVisualDescendants().OfType<Track>());
                    var fill = Assert.Single(track.DecreaseButton!.GetVisualDescendants().OfType<Border>(),
                        static border => border.Name == "TrackBackground");
                    colors.Add(("Opacity slider value track", ColorOf(fill.Background)));
                }

                var indicator = Assert.Single(progress.GetVisualDescendants().OfType<Border>(),
                    static border => border.Name == "PART_Indicator");
                colors.Add(("ProgressBar indicator", ColorOf(indicator.Background)));
                var card = Assert.Single(fixture.Window.GetVisualDescendants().OfType<NotificationCard>());
                var stripe = Assert.Single(card.GetVisualDescendants().OfType<Panel>(),
                    static panel => panel.Name == "PART_HeaderBar");
                colors.Add(("Information toast stripe", ColorOf(stripe.Background)));
                foreach (var (part, color) in colors)
                {
                    output.WriteLine($"{theme} {part}: {color}; product accent: {accent}");
                }

                Assert.All(colors, item => Assert.Equal(accent, item.Color));
                Assert.Equal(accent, Assert.IsType<Color>(app.FindResource(app.RequestedThemeVariant, "SystemAccentColor")));
            }
            finally
            {
                host.Close();
                popup.IsOpen = false;
            }
        }
        finally
        {
            app.RequestedThemeVariant = previous;
        }
    }

    private static Color ColorOf(object? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
}

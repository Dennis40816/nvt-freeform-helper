// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class P1TextSelectionContrastTests(ITestOutputHelper output)
{
    [AvaloniaTheory]
    [InlineData("Dark", "TextBox")]
    [InlineData("Light", "TextBox")]
    [InlineData("Dark", "NumberScrubber")]
    [InlineData("Light", "NumberScrubber")]
    [InlineData("Dark", "ComboBox")]
    [InlineData("Light", "ComboBox")]
    [InlineData("Dark", "AutoCompleteBox")]
    [InlineData("Light", "AutoCompleteBox")]
    [InlineData("Dark", "NumericUpDown")]
    [InlineData("Light", "NumericUpDown")]
    public async Task FocusedEditorSelectionHasReadableContrastAsync(string theme, string kind)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        Control editor = kind switch
        {
            "NumberScrubber" => new NumberScrubber { Value = 42 },
            "ComboBox" => new ComboBox { IsEditable = true, Text = "Search layers" },
            "AutoCompleteBox" => new AutoCompleteBox { Text = "Search layers" },
            "NumericUpDown" => new NumericUpDown { Value = 42 },
            _ => new TextBox { Text = "Search layers" },
        };
        var window = CreateWindow(theme, editor);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var textBox = editor as TextBox ?? Assert.Single(editor.GetVisualDescendants().OfType<TextBox>());
            Assert.True(textBox.Focus());
            textBox.SelectAll();
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(textBox.IsFocused);
            Assert.False(string.IsNullOrEmpty(textBox.Text));
            Assert.Equal(textBox.Text, textBox.SelectedText);
            var presenter = Assert.Single(textBox.GetVisualDescendants().OfType<TextPresenter>());
            Assert.Equal(textBox.SelectionStart, presenter.SelectionStart);
            Assert.Equal(textBox.SelectionEnd, presenter.SelectionEnd);
            AssertReadable(theme, kind, window, presenter.SelectionForegroundBrush ?? presenter.Foreground, presenter.SelectionBrush);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task SelectableTextSelectionHasReadableContrastAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var text = new SelectableTextBlock { Text = "Selected inspection text" };
        var window = CreateWindow(theme, text);
        try
        {
            window.Show();
            text.SelectAll();
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal(text.Text, text.SelectedText);
            AssertReadable(theme, nameof(SelectableTextBlock), window, text.SelectionForegroundBrush ?? text.Foreground, text.SelectionBrush);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window CreateWindow(string theme, Control content) => new()
    {
        Width = 320,
        Height = 100,
        RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
        Content = content,
    };

    private void AssertReadable(string theme, string control, Window window, IBrush? foreground, IBrush? background)
    {
        var surface = Assert.IsAssignableFrom<ISolidColorBrush>(window.FindResource(window.ActualThemeVariant, "NfcSurfaceBrush")).Color;
        var fill = CompositeOver(background, surface);
        var text = CompositeOver(foreground, fill);
        var first = Luminance(text);
        var second = Luminance(fill);
        var ratio = (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
        output.WriteLine($"{theme} {control}: selection foreground={text}, background={fill}, contrast={ratio:F2}:1");
        Assert.True(ratio >= 4.5, $"{theme} {control}: selected text contrast is {ratio:F2}:1; at least 4.5:1 is required.");
    }

    private static Color CompositeOver(IBrush? brush, Color backdrop)
    {
        var solid = Assert.IsAssignableFrom<ISolidColorBrush>(brush);
        var alpha = solid.Color.A / 255d * solid.Opacity;
        byte Channel(byte source, byte target) => (byte)Math.Round(source * alpha + target * (1 - alpha));
        return Color.FromRgb(Channel(solid.Color.R, backdrop.R), Channel(solid.Color.G, backdrop.G), Channel(solid.Color.B, backdrop.B));
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var channel = value / 255d;
            return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }
}

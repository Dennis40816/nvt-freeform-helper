// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class LayerImageExportScreenshotTests
{
    [AvaloniaFact]
    public async Task CaptureTheRealExportWindowsInBothThemesAsync()
    {
        // NFH_EXPORT_SHOTS keeps the PNG files for review. Without it the test captures into a temp folder and deletes it.
        string? requested = ScreenshotOutputDirectory.Resolve("NFH_EXPORT_SHOTS");
        DirectoryInfo? scratch = requested is null ? TestFiles.CreateTempDirectory() : null;
        string output = requested ?? scratch!.FullName;
        Directory.CreateDirectory(output);
        HeadlessAppBootstrap.EnsureInitialized();
        var app = global::Avalonia.Application.Current!;
        var previousTheme = app.RequestedThemeVariant;
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            await CaptureStepOneAsync(output, ThemeVariant.Dark, "step1-dark.png", string.Empty);
            await CaptureStepOneAsync(output, ThemeVariant.Light, "step1-light.png", string.Empty);
            await CaptureStepOneAsync(output, ThemeVariant.Dark, "step1-search-dark.png", "ref");
            await CaptureStepTwoAsync(output, ThemeVariant.Dark, "step2-dark.png");
            await CaptureStepTwoAsync(output, ThemeVariant.Light, "step2-light.png");
        }
        finally
        {
            scratch?.Delete(recursive: true);
            app.RequestedThemeVariant = previousTheme;
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    private static async Task CaptureStepOneAsync(string output, ThemeVariant theme, string fileName, string searchText)
    {
        global::Avalonia.Application.Current!.RequestedThemeVariant = theme;
        using var session = new LayerImageExportViewTestSession();
        session.Model.PreviewSettings.UseDarkTheme = theme == ThemeVariant.Dark;
        await session.ShowAsync();
        await session.CompletePreviewAsync();
        Assert.NotNull(session.Model.PreviewPng);
        Assert.NotNull(session.Window.FindControl<Image>("PreviewImage")!.Source);

        if (!string.IsNullOrEmpty(searchText))
        {
            session.Window.FindControl<TextBox>("SearchBox")!.Focus();
            session.Window.KeyTextInput(searchText);
            await HeadlessUiPump.FlushQueueAsync();
        }

        SaveFrame(session.Window, output, fileName);
    }

    private static async Task CaptureStepTwoAsync(string output, ThemeVariant theme, string fileName)
    {
        global::Avalonia.Application.Current!.RequestedThemeVariant = theme;
        using var session = new LayerImageExportViewTestSession();
        session.Model.PreviewSettings.UseDarkTheme = theme == ThemeVariant.Dark;
        await session.ShowAsync();
        await session.CompletePreviewAsync();
        var settings = new LayerImageExportSettingsViewModel(session.Model, session.Context.PickSavePathAsync);
        var dialog = new LayerImageExportSettingsDialog { DataContext = settings };
        try
        {
            dialog.Show();
            await HeadlessUiPump.FlushQueueAsync();
            SaveFrame(dialog, output, fileName);
        }
        finally
        {
            dialog.Close();
        }
    }

    private static void SaveFrame(Window window, string output, string fileName)
    {
        Assert.NotEqual(WindowDecorations.None, window.WindowDecorations);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.True(frame.PixelSize.Width > 0);
        Assert.True(frame.PixelSize.Height > 0);
        frame.Save(Path.Combine(output, fileName), new PngBitmapEncoderOptions());
    }
}

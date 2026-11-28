// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsNavigationReclickTests
{
    [AvaloniaTheory]
    [InlineData("GeneralNavButton", SettingsWindowSection.General)]
    [InlineData("Step1NavButton", SettingsWindowSection.Step1GeometryMatch)]
    [InlineData("Step2NavButton", SettingsWindowSection.Step2FreeformTagging)]
    [InlineData("Step3NavButton", SettingsWindowSection.Step3NotchCompensation)]
    [InlineData("Step4NavButton", SettingsWindowSection.Step4IndexDiagnostics)]
    [InlineData("Step5NavButton", SettingsWindowSection.Step5NotchExport)]
    public async Task SecondClickKeepsTheSelectedPageMarkedAsync(string buttonName, SettingsWindowSection section)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var draft = new FreeformHelperViewModel().CreateSettingsWindowViewModel();
        var window = new SettingsWindow { DataContext = draft };
        window.Classes.Add("reducedMotion");
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var button = window.FindControl<ToggleButton>(buttonName)!;

            await ClickAsync(window, button);
            await ClickAsync(window, button);

            Assert.Equal(section, draft.SelectedSection);
            Assert.True(button.IsChecked);
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task ClickAsync(Window window, Control target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        await HeadlessUiPump.FlushQueueAsync();
    }
}

// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ThemeLookScreenshotTests
{
    private static readonly string[] CoordinateLayers = ["alpha", "beta"];

    [AvaloniaFact]
    public async Task CaptureMainScreensWhenRequestedAsync()
    {
        string? output = ScreenshotOutputDirectory.Resolve("FH_THEME_SHOTS");
        if (output is null) return;

        Directory.CreateDirectory(output);
        HeadlessAppBootstrap.EnsureInitialized();
        var app = global::Avalonia.Application.Current!;
        var previousTheme = app.RequestedThemeVariant;
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            app.RequestedThemeVariant = TestEnvironment.Get("FH_THEME_VARIANT") == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            await CaptureMainWindowAsync(output);
        }
        finally
        {
            app.RequestedThemeVariant = previousTheme;
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    private static async Task CaptureMainWindowAsync(string output)
    {
        using var files = new LoadingModeTestScope();
        using var spinner = new FakeCadLoadSpinnerHost();
        using var spinnerFactory = spinner.Install();
        var model = await DxfCharacterizationWorkspace.OpenAsync(files);
        using var store = await ConsoleLogTestSupport.CreateSeededStoreAsync("theme", "Synthetic workspace loaded.");
        using var shell = new ShellViewModel(model) { IsDevPageAvailable = true, ConsoleStore = store };
        var window = new MainWindow { WindowState = WindowState.Normal, Width = 1600, Height = 1000 };
        window.SetShellViewModel(shell);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.NotEmpty(model.CadPads);
            Assert.NotEmpty(model.RegularPads);
            var canvas = Assert.Single(window.GetVisualDescendants().OfType<PadCanvas>());
            Assert.True(canvas.Bounds.Width > 0 && canvas.Bounds.Height > 0);
            model.FitCommand.Execute(null);
            model.IsRightPanelSettingsTab = true;
            await SaveFrameAsync(window, output, "workspace.png");
            await CaptureSectionsAsync(window, output, "workspace");
            model.ApplyCanvasSelection(new[] { model.CadPads[0].Id }, Array.Empty<int>());
            model.IsRightPanelInspectorTab = true;
            await SaveFrameAsync(window, output, "workspace-inspector.png");
            await CaptureSectionsAsync(window, output, "inspector");
            shell.IsConsoleExpanded = true;
            await SaveFrameAsync(window, output, "workspace-console.png");
            shell.IsConsoleExpanded = false;
            await CaptureOtherPagesAsync(window, shell, output);
            await CaptureDialogsAsync(model, output);
        }
        finally
        {
            // Detach the model so closing evidence capture cannot open the unsaved-project prompt.
            window.DataContext = null;
            window.Close();
        }
    }

    private static async Task CaptureOtherPagesAsync(MainWindow window, ShellViewModel shell, string output)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(2, 3, cellWidth: 10d, cellHeight: 10d,
            diffIndexSelector: static (row, col) => row * 3 + col + 10);
        var table = new NotchTable(new[]
        {
            new NotchTableRow(icIndex: 0, diffIndex: 10, regularPadIndex: 0, cadPadId: 1,
                v22Node: new NotchV22Node(10, 94, 11, 44, 65535, 0, 0), comment: "synthetic"),
        });
        var simulation = new SimulationWorkspaceViewModel(
            new FreeformHelper.UI.Services.SimulationWorkspaceUseCase(
                new FreeformHelper.UI.Services.NotchApplySimulationReviewUseCase()),
            new FreeformHelper.UI.Services.SimulationWorkspaceSession(grid, table, 65535,
                shell.FreeformHelper.SimulationWorkspaceSourceRevision,
                grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
                CadPadsForCopperProjection: shell.FreeformHelper.CadPads.ToArray()));
        await shell.ShowSimulationAsync(simulation);
        await SaveFrameAsync(window, output, "simulation.png");
        await CaptureSectionsAsync(window, output, "simulation");
        simulation.SelectedInputSourceOption = simulation.SourceOptions
            .Single(static option => option.Value == SimulationInputSourceMode.Csv);
        await SaveFrameAsync(window, output, "simulation-csv-empty.png");
        simulation.SelectedInputSourceOption = simulation.SourceOptions
            .Single(static option => option.Value == SimulationInputSourceMode.Copper);
        await SaveFrameAsync(window, output, "simulation-copper.png");

        var coordinate = new CoordinatePlannerWorkspaceViewModel(
            new FreeformHelper.UI.Services.CoordinatePlannerWorkspaceUseCase(),
            new FreeformHelper.UI.Services.CoordinatePlannerWorkspaceSession(grid,
                shell.FreeformHelper.CadPads.ToArray(), CoordinateLayers,
                shell.FreeformHelper.WorkspaceDerivedSourceRevision, 100d, 50d, 1000, 500));
        await shell.ShowCoordinateAsync(coordinate);
        await SaveFrameAsync(window, output, "coordinate.png");
        coordinate.ShowCalibrationDetails = true;
        coordinate.ShowCustomArray = true;
        await CaptureSectionsAsync(window, output, "coordinate");
        shell.ShowHowToUseCommand.Execute(null);
        await SaveFrameAsync(window, output, "how-to-use.png");
        shell.ShowDevCommand.Execute(null);
        await SaveFrameAsync(window, output, "dev.png");
        await CaptureDevButtonsAsync(window, output);
    }

    private static async Task CaptureDevButtonsAsync(MainWindow window, string output)
    {
        var dev = Assert.Single(window.GetVisualDescendants().OfType<DevView>());
        foreach (var name in new[] { "Primary", "Neutral", "Danger", "Ghost", "Flatghost", "Chip", "Icon", "Product" })
        {
            var samples = dev.FindControl<Control>($"{name}ButtonSamples");
            Assert.NotNull(samples);
            samples.BringIntoView();
            await SaveFrameAsync(window, output, $"dev-buttons-{name.ToLowerInvariant()}.png");
        }
    }

    private static async Task CaptureSectionsAsync(MainWindow window, string output, string prefix)
    {
        // Real accordions and their scroll hosts stay in the real window; no isolated panel replicas.
        var sections = window.GetVisualDescendants().OfType<CollapsiblePanel>()
            .Where(static panel => panel.IsEffectivelyVisible).ToArray();
        for (int index = 0; index < sections.Length; index++)
        {
            var section = sections[index];
            await CaptureSectionAsync(window, section, section.IsExpanded, value => section.IsExpanded = value,
                output, $"{prefix}-section-{index + 1:00}.png");
        }

        var details = window.GetVisualDescendants().OfType<HidePanelBlock>()
            .Where(static panel => panel.IsEffectivelyVisible && panel.IsCollapsible).ToArray();
        for (int index = 0; index < details.Length; index++)
        {
            var section = details[index];
            await CaptureSectionAsync(window, section, section.IsExpanded, value => section.IsExpanded = value,
                output, $"{prefix}-detail-{index + 1:00}.png");
        }
    }

    private static async Task CaptureSectionAsync(MainWindow window, Control section, bool expanded,
        Action<bool> setExpanded, string output, string fileName)
    {
        try
        {
            setExpanded(true);
            await HeadlessUiPump.FlushQueueAsync();
            section.BringIntoView();
            await SaveFrameAsync(window, output, fileName);
        }
        finally
        {
            setExpanded(expanded);
        }
    }

    private static async Task CaptureDialogsAsync(FreeformHelperViewModel model, string output)
    {
        var settings = new SettingsWindow { DataContext = new SettingsWindowViewModel(model) };
        try
        {
            settings.Show();
            foreach (var section in Enum.GetValues<SettingsWindowSection>())
            {
                settings.NavigateToSection(section);
                await SaveFrameAsync(settings, output, $"settings-{section}.png");
            }
        }
        finally
        {
            settings.Close();
        }

        var confirm = new ConfirmDialog("Reset DXF edits", "Reset all edits in this synthetic workspace?",
            "Reset", "Cancel", consequenceText: "Hidden and moved pads will be restored.");
        try
        {
            confirm.Show();
            await SaveFrameAsync(confirm, output, "confirm.png");
        }
        finally
        {
            confirm.Close();
        }
    }

    private static async Task SaveFrameAsync(Window window, string output, string fileName)
    {
        await HeadlessUiPump.FlushQueueAsync();
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.True(frame.PixelSize.Width > 0);
        Assert.True(frame.PixelSize.Height > 0);
        if (window is MainWindow)
        {
            Assert.Equal(1600, frame.PixelSize.Width);
            Assert.Equal(1000, frame.PixelSize.Height);
        }
        frame.Save(Path.Combine(output, fileName), new PngBitmapEncoderOptions());
    }
}

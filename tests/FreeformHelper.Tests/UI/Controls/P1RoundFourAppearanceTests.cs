// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.SettingsSections;
using Nvt.Core.Avalonia.Icons;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class P1RoundFourAppearanceTests(ITestOutputHelper output)
{
    // SystemAccentColor now carries the product palette; reject Fluent's original blues in either theme.
    private static readonly HashSet<Color> _fluentDefaultBlues = new(new[]
    {
        "#0078D7", "#0078D4", "#007ACC", "#269FFF", "#64BBFF", "#A6D8FF", "#00589E", "#004175", "#002542"
    }.Select(Color.Parse));

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task ProductSelectionPresentersNeverUseFluentAccentAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var app = global::Avalonia.Application.Current!;
        var previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var failures = new List<string>();
        try
        {
            await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
            var shell = Assert.IsType<ShellViewModel>(fixture.Window.DataContext);
            fixture.Helper.ApplyCanvasSelection([fixture.Helper.CadPads[0].Id], []);
            fixture.Helper.IsRightPanelInspectorTab = true;
            fixture.Helper.PadInspectorUi.IsDebugDetailsExpanded = true;
            foreach (var page in Enum.GetValues<ShellPage>().Where(static page => page != ShellPage.Dev))
            {
                await ShowPageAsync(shell, page);
                await InspectAsync(fixture.Window, "MainWindow/" + page, failures);
            }

            var draft = fixture.Helper.CreateSettingsWindowViewModel();
            var settings = new SettingsWindow { DataContext = draft };
            try
            {
                settings.Show();
                foreach (var section in Enum.GetValues<SettingsWindowSection>())
                {
                    settings.NavigateToSection(section);
                    await InspectAsync(settings, "SettingsWindow/" + section, failures);
                }
            }
            finally
            {
                settings.Close();
            }

            foreach (var window in CreateSecondaryWindows())
            {
                try
                {
                    window.Show();
                    await InspectAsync(window, window.GetType().Name, failures);
                }
                finally
                {
                    window.Close();
                }
            }

            using var export = new LayerImageExportViewTestSession();
            await export.ShowAsync();
            await InspectAsync(export.Window, "LayerImageExportWindow", failures);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Distinct(StringComparer.Ordinal)));
        }
        finally
        {
            app.RequestedThemeVariant = previous;
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task ClickingSizingScopeTextTogglesTheBoundSwitchAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var draft = new FreeformHelperViewModel().CreateSettingsWindowViewModel();
        draft.UseLocalSizing = false;
        var view = new SettingsGeneralSectionView { DataContext = draft };
        var window = new Window
        {
            Width = 1100,
            Height = 1600,
            Content = view,
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light
        };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var toggle = view.FindControl<ToggleSwitch>("SizingScopeSwitch")!;
            foreach (var expected in new[] { true, false })
            {
                var text = Assert.Single(((StackPanel)toggle.Parent!).Children.OfType<TextBlock>(), static label => label.IsVisible);
                var point = Avalonia.VisualExtensions.TranslatePoint(text, new Avalonia.Point(text.Bounds.Width / 2, text.Bounds.Height / 2), window)!.Value;
                window.MouseMove(point);
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                await HeadlessUiPump.FlushQueueAsync();
                Assert.Equal(expected, draft.UseLocalSizing);
                Assert.Equal(expected, toggle.IsChecked);
            }

            draft.UseLocalSizing = true;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(toggle.IsChecked);
            Assert.Equal("Sizing scope", Avalonia.Automation.AutomationProperties.GetName(toggle));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task WorkspaceMarksAndBulkChipKeepDistinctInteractionStatesAsync(string theme)
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var window = fixture.Window;
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Classes.Add("reducedMotion");
        await HeadlessUiPump.FlushQueueAsync();
        var marked = window.GetVisualDescendants().OfType<ToggleButton>()
            .Where(static button => button.IsChecked == true && button.Classes.Contains("shellTab")).ToArray();
        var rightTabHost = Assert.Single(window.GetVisualDescendants().OfType<Border>(),
            static border => border.Classes.Contains("rightPanelTabHost"));
        var rightTab = rightTabHost.GetVisualDescendants().OfType<ToggleButton>().ToArray()[0];
        Assert.NotEmpty(marked);
        foreach (var button in marked)
        {
            var body = RoleBorder(button);
            Assert.Equal(0, ColorOf(body.Background).A);
            Assert.True(body.BorderThickness.Left > 0);
            Assert.Equal(ResourceColor(button, "NfcAccentBrush"), ColorOf(body.BorderBrush));
            Assert.Equal(RoleBorder(rightTab).CornerRadius, body.CornerRadius);
        }

        var bulk = Assert.Single(window.GetVisualDescendants().OfType<ToggleButton>(), static button => button.Classes.Contains("dxfLayerBulkToggleButton"));
        var fill = RoleBorder(bulk);
        var checkedFill = ColorOf(fill.Background);
        Assert.Equal(ResourceColor(bulk, "NfcAccentSurfaceBrush"), checkedFill);
        fixture.Helper.IsCadLayerBulkToggleChecked = false;
        await HeadlessUiPump.FlushQueueAsync();
        Assert.False(bulk.IsChecked);
        Assert.Equal("All off", fixture.Helper.CadLayerBulkToggleText);
        var uncheckedFill = ColorOf(fill.Background);
        Assert.NotEqual(checkedFill, uncheckedFill);
        fixture.Helper.LayerToggles[0].IsSelected = true;
        await HeadlessUiPump.FlushQueueAsync();
        Assert.False(bulk.IsChecked);
        Assert.Equal("Partly on", fixture.Helper.CadLayerBulkToggleText);
        Assert.True(Assert.Single(bulk.GetVisualDescendants().OfType<Border>(), static border => border.Classes.Contains("dxfLayerBulkMixedIndicator")).IsVisible);
        var point = Avalonia.VisualExtensions.TranslatePoint(bulk, new Avalonia.Point(bulk.Bounds.Width / 2, bulk.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.True(bulk.IsPointerOver);
        var hoverFill = ColorOf(fill.Background);
        Assert.NotEqual(uncheckedFill, hoverFill);
        window.MouseDown(point, MouseButton.Left);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.NotEqual(hoverFill, ColorOf(fill.Background));
        window.MouseUp(point, MouseButton.Left);
        await HeadlessUiPump.FlushQueueAsync();
        fixture.FocusShortcutTarget();
        Assert.True(bulk.Focus(NavigationMethod.Tab));
        await HeadlessUiPump.FlushQueueAsync();
        var ring = Assert.Single(AdornerLayer.GetAdornerLayer(bulk)!.Children.OfType<Border>(), border => AdornerLayer.GetAdornedElement(border) == bulk);
        Assert.Equal(ResourceColor(bulk, "Nvt.Focus.RingBrush"), ColorOf(ring.BorderBrush));
        var enabledText = ColorOf(bulk.Foreground);
        bulk.SetCurrentValue(Avalonia.Input.InputElement.IsEnabledProperty, false);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.NotEqual(enabledText, ColorOf(bulk.Foreground));
        var failures = new List<string>();
        CheckTemplate(bulk, "disabled bulk chip", failures);
        Assert.Empty(failures);
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task LayerRowsShowVisibilityWithoutRestingBoxesAsync(string theme)
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var window = fixture.Window;
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Classes.Add("reducedMotion");
        fixture.Helper.LayerToggles[0].IsSelected = false;
        await HeadlessUiPump.FlushQueueAsync();
        var rows = window.GetVisualDescendants().OfType<ToggleButton>()
            .Where(static button => button.Classes.Contains("layerToggle")).ToArray();
        var hidden = Assert.Single(rows, static button => button.IsChecked == false);
        var visible = rows.Single(static button => button.DataContext is LayerToggle { Name: "beta" });
        AssertLayerRow(hidden, NvtIcons.VisibilityOff, "NfcTextMutedBrush");
        AssertLayerRow(visible, NvtIcons.Visibility, "NfcTextBrush");
        var hiddenText = hidden.GetVisualDescendants().OfType<SelectableTextBlock>().Single();
        var visibleText = visible.GetVisualDescendants().OfType<SelectableTextBlock>().Single();
        Assert.NotEqual(ColorOf(visibleText.Foreground), ColorOf(hiddenText.Foreground));

        var point = Avalonia.VisualExtensions.TranslatePoint(hidden,
            new Avalonia.Point(hidden.Bounds.Width / 2, hidden.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.True(ColorOf(RoleBorder(hidden).Background).A > 0);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseMove(new Avalonia.Point(window.Bounds.Width / 2, 1));
        await HeadlessUiPump.FlushQueueAsync();
        Assert.True(((LayerToggle)hidden.DataContext!).IsSelected);
        AssertLayerRow(hidden, NvtIcons.Visibility, "NfcTextBrush");
        fixture.FocusShortcutTarget();
        Assert.True(hidden.Focus(NavigationMethod.Tab));
        await HeadlessUiPump.FlushQueueAsync();
        var ring = Assert.Single(AdornerLayer.GetAdornerLayer(hidden)!.Children.OfType<Border>(),
            border => AdornerLayer.GetAdornedElement(border) == hidden);
        Assert.Equal(ResourceColor(hidden, "Nvt.Focus.RingBrush"), ColorOf(ring.BorderBrush));
        Assert.Equal("Ctrl+click: show only this layer.", Assert.IsType<TextBlock>(ToolTip.GetTip(hidden)).Text);
    }

    private void AssertLayerRow(ToggleButton row, string glyph, string textBrush)
    {
        Assert.Equal(glyph, Assert.Single(row.GetVisualDescendants().OfType<FontIcon>()).Glyph);
        var label = Assert.Single(row.GetVisualDescendants().OfType<SelectableTextBlock>());
        var text = ColorOf(label.Foreground);
        Assert.Equal(ResourceColor(row, textBrush), text);
        Assert.Equal(0, ColorOf(row.Background).A);
        Assert.Equal(default, row.BorderThickness);
        Assert.Equal(0, ColorOf(RoleBorder(row).Background).A);
        Assert.Equal(default, RoleBorder(row).BorderThickness);
        var surface = row.GetVisualAncestors().OfType<Border>()
            .Select(static border => border.Background).OfType<ISolidColorBrush>()
            .First(static brush => brush.Color.A == 255).Color;
        var a = RelativeLuminance(text);
        var b = RelativeLuminance(surface);
        var contrast = (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        output.WriteLine($"{row.ActualThemeVariant} {glyph}: text={text}, surface={surface}, contrast={contrast:F2}:1");
        Assert.True(contrast >= 3, $"Layer label contrast is {contrast:F2}:1.");
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte value) => value / 255d <= 0.04045
            ? value / 255d / 12.92 : Math.Pow(((value / 255d) + 0.055) / 1.055, 2.4);
        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task MainWindowPanelSummariesKeepTheirFullWrappedHeightAsync(string theme)
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var window = fixture.Window;
        window.Width = 1920;
        window.Height = 1040;
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        await HeadlessUiPump.FlushQueueAsync();
        var panels = window.GetVisualDescendants().OfType<CollapsiblePanel>().ToArray();
        AssertPanelSummary(Assert.Single(panels, static panel => panel.Title == "Import & DXF check"), "Not checked");
        AssertPanelSummary(Assert.Single(panels, static panel => panel.Title == "DXF edits"), "Hidden 0 · Merged 0");
        var layers = Assert.Single(panels, static panel => panel.Title == "Layers");
        Assert.Equal(51, Assert.Single(layers.GetVisualDescendants().OfType<ToggleButton>(),
            static button => button.Classes.Contains("panelBlockHeader")).Bounds.Height);
    }

    private void AssertPanelSummary(CollapsiblePanel panel, string expected)
    {
        var summary = Assert.IsType<SelectableTextBlock>(panel.HeaderRight);
        var header = Assert.Single(panel.GetVisualDescendants().OfType<ToggleButton>());
        output.WriteLine($"{panel.Title}: header={header.Bounds.Height}, summary={summary.Bounds}, layoutHeight={summary.TextLayout.Height}");
        Assert.Equal(expected, summary.Text);
        Assert.All(summary.TextLayout.TextLines, static line => Assert.False(line.HasCollapsed));
        Assert.Equal(2, summary.TextLayout.TextLines.Count);
        Assert.True(summary.Bounds.Height >= summary.TextLayout.Height);
        Assert.Equal(56, header.Bounds.Height);
    }

    private async Task InspectAsync(Window window, string scope, List<string> failures)
    {
        window.Classes.Add("reducedMotion");
        await HeadlessUiPump.FlushQueueAsync();
        foreach (var panel in window.GetVisualDescendants().OfType<CollapsiblePanel>().ToArray())
        {
            panel.IsExpanded = true;
        }

        foreach (var block in window.GetVisualDescendants().OfType<HidePanelBlock>().ToArray())
        {
            block.IsExpanded = true;
        }

        await HeadlessUiPump.FlushQueueAsync();
        var toggles = window.GetVisualDescendants().OfType<ToggleButton>().ToArray();
        var items = window.GetVisualDescendants().OfType<ListBoxItem>().ToArray();
        Assert.NotEmpty(toggles.Concat<TemplatedControl>(items));
        foreach (var toggle in toggles)
        {
            var original = toggle.IsChecked;
            toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            toggle.ApplyTemplate();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(toggle.IsChecked, scope + "/" + Describe(toggle));
            CheckTemplate(toggle, scope, failures);
            toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, original);
        }

        foreach (var item in items)
        {
            var original = item.IsSelected;
            item.SetCurrentValue(ListBoxItem.IsSelectedProperty, true);
            item.ApplyTemplate();
            await HeadlessUiPump.FlushQueueAsync();
            CheckTemplate(item, scope, failures);
            item.SetCurrentValue(ListBoxItem.IsSelectedProperty, original);
        }

        output.WriteLine($"{scope}: {toggles.Length} toggles/radios, {items.Length} realized list items; classes: " +
            string.Join(", ", toggles.Select(Describe).Distinct(StringComparer.Ordinal)));
    }

    private static void CheckTemplate(TemplatedControl control, string scope, List<string> failures)
    {
        var presenters = control.GetVisualDescendants().OfType<Control>()
            .Where(node => ReferenceEquals(node.TemplatedParent, control) && node is Border or ContentPresenter).ToArray();
        Assert.NotEmpty(presenters);
        foreach (var presenter in presenters)
        {
            var (background, border) = presenter is Border body
                ? (body.Background, body.BorderBrush)
                : (((ContentPresenter)presenter).Background, ((ContentPresenter)presenter).BorderBrush);
            foreach (var (property, brush) in new[] { ("Background", background), ("BorderBrush", border) })
            {
                if (brush is ISolidColorBrush { Color.A: > 0 } solid &&
                    _fluentDefaultBlues.Contains(Color.FromRgb(solid.Color.R, solid.Color.G, solid.Color.B)))
                {
                    failures.Add($"{scope}/{Describe(control)}/{presenter.Name ?? presenter.GetType().Name}.{property} = {solid.Color}");
                }
            }
        }
    }

    private static string Describe(Control control) => control.GetType().Name + "." +
        string.Join('.', control.Classes.Where(static name => !name.StartsWith(':')));

    private static Task ShowPageAsync(ShellViewModel shell, ShellPage page)
    {
        switch (page)
        {
            case ShellPage.Workspace:
                shell.ShowWorkspaceCommand.Execute(null);
                return Task.CompletedTask;
            case ShellPage.HowToUse:
                shell.ShowHowToUseCommand.Execute(null);
                return Task.CompletedTask;
            case ShellPage.Simulation:
                var grid = TestGeometryFactory.CreateRegularGrid(2, 3);
                var table = new NotchTable([new NotchTableRow(0, 10, 0, 1,
                    new NotchV22Node(10, 94, 11, 44, 65535, 0, 0), "Synthetic simulation")]);
                return shell.ShowSimulationAsync(new SimulationWorkspaceViewModel(
                    new FreeformHelper.UI.Services.SimulationWorkspaceUseCase(new FreeformHelper.UI.Services.NotchApplySimulationReviewUseCase()),
                    new FreeformHelper.UI.Services.SimulationWorkspaceSession(grid, table, 65535, 0,
                        grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet())));
            case ShellPage.Coordinate:
                return shell.ShowCoordinateCommand.ExecuteAsync(null);
            default:
                throw new ArgumentOutOfRangeException(nameof(page));
        }
    }

    private static Border RoleBorder(ToggleButton button) => Assert.Single(button.GetVisualDescendants().OfType<Border>(), static border => border.Name == "RoleBorder");

    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static Color ResourceColor(Control control, string key) => ColorOf((IBrush)control.FindResource(control.ActualThemeVariant, key)!);

    private static IEnumerable<Window> CreateSecondaryWindows()
    {
        var entries = new List<DxfEditChangeListEntry>
        {
            new(DxfEditChangeKind.Duplicate, 20, 20, "PAD_20", "Duplicate", "Synthetic copper",
                "Duplicate geometry", "Synthetic duplicate", NvtIcons.Undo, "Restore", "Restore row", canFocus: true, canApply: true),
        };
        yield return new DxfEditChangeListWindow
        {
            DataContext = new DxfEditChangeListViewModel(
            DxfEditChangeKind.Duplicate, () => entries, static _ => { }, static _ => { })
        };
        var table = new NotchTable([new NotchTableRow(0, 10, 100, 200,
            new NotchV22Node(10, 94, 11, 44, 65535, 0, 0), "Synthetic allocation")]);
        yield return new NotchExportSelectionWindow { DataContext = new NotchExportSelectionViewModel(table) };
        var report = new DxfRegularMappingReport(CadCount: 1, RegularCount: 1, MappedCount: 1,
            UnmappedCadCount: 0, UnmappedRegularCount: 0, LowConfidenceCount: 0, AmbiguousCount: 0,
            HasIssues: false, Summary: "Synthetic mapping", TotalIssueCount: 0, IssuesTruncated: false, Issues: [], SampleIssues: []);
        yield return new IndexMappingReportWindow { DataContext = new IndexMappingReportViewModel("Synthetic mapping", report) };
        yield return new Window { Width = 1200, Height = 1000, Content = new DevView { DataContext = new DevViewModel() } };
    }
}

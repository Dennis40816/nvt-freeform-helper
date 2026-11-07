// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class HowToUseIntegratedLayoutTests
{
    private static readonly string[] _groupTitles =
    [
        "Quick Start",
        "Interface tour",
        "Display and DXF settings",
        "Canvas navigation and selection",
        "Manual sizing and right-pane settings",
        "Notch reference",
        "Numeric input and Console",
        "Advanced reference",
    ];

    private static readonly (string Gesture, string Action)[] _gestureRows =
    [
        ("Middle mouse drag", "Pan (recommended)."),
        ("Space + Left mouse drag", "Pan (laptop fallback)."),
        ("Mouse wheel", "Zoom around cursor. Ctrl + wheel = faster zoom."),
        ("Shift + Arrow keys", "Fine pan."),
        ("F", "Fit to content. Ctrl + A: Select all visible pads."),
        ("F / Ctrl + A", "work globally when not editing text."),
        ("Left click", "Select pad under cursor."),
        ("Ctrl + click / Shift + click", "Add/remove selection."),
        ("Alt + click", "Prefer regular pad when CAD/regular overlap."),
        ("Drag empty space", "Box select. Ctrl/Shift = add to selection."),
        ("Double click", "Open pad info for the clicked pad."),
        ("Right click", "Open pad info (keeps selection if already selected)."),
        ("Click empty space", "Clear selection."),
    ];

    private static readonly (string Title, string[] Text)[] _sectionContents =
    [
        ("Quick Start",
        [
            "1) Open DXF → 2) Review grid settings → 3) Match → 4) Tag freeforms → 5) Export.",
            "Regular pads are generated from AA size + channels even before a DXF is loaded.",
        ]),
        ("Interface tour",
        [
            "Navigation",
            "- Top tabs: Freeform Helper, How To Use, Dev.",
            "- Fit icon: top-right of the canvas.",
            "Menu Bar",
            "- File: Open DXF, Save Project, Load Project.",
            "- Match: Run pad matching.",
            "- Export: Notch CSV review or C v2.1/v2.2.",
        ]),
        ("Display and DXF settings",
        [
            "Workspace Header (Display)",
            "- Display menu: CAD layer, Regular grid, Highlight unmatched/freeform, Color by area.",
            "- CAD display: line width and line color.",
            "- Area buckets: CAD-only area grouping colors.",
            "- Status and selection summaries update automatically.",
            "DXF Settings (Left Panel)",
            "- Only closed polylines: import closed shapes only.",
            "- Layer toggles: hide/show CAD pads by layer after import.",
            "- Check DXF overlaps: scan duplicates and overlaps on demand.",
        ]),
        ("Manual sizing and right-pane settings",
        [
            "Manual Sizing (Header Bar)",
            "- Rows/Cols: enter ranges like 5, 5-7, 5,7.",
            "- Width/Height: set target size for selected regular pads.",
            "- Use Selection: fill Rows/Cols from current selection.",
            "- '*' means mixed values in the current selection.",
            "Right Panel: General Settings",
            "- IC layout: Cascade count and per-IC X/Y channels.",
            "- Grid settings: Padding percent, scan order, and sizing scope (Local/Global).",
            "- Panel AA size (mm): Physical panel size used to compute pitch.",
            "- Alignment mode: Choose AA vs DXF bounds.",
            "Right Panel: Analysis",
            "- Matching: overlap-based CAD↔Regular relation; one CAD can map to multiple regular pads.",
            "- Unmatched threshold: Display-only overlap / regular area diagnostic; changing it does not change links.",
            "- Freeform tagging: Assign X/Y/XY/None to selected pads.",
            "- Notch export: Enable versions and legacy values.",
        ]),
        ("Notch reference",
        [
            "Notch Terms & Thresholds",
            "- BBoxArea: CAD pad bounding-box area, formula = width × height (used by V2.1 REGU_TO_FULL field).",
            "- Freeform TH (Matching > Freeform axis threshold): decides XWay/YWay/None classification during auto-detect.",
            "- Notch TH (Notch threshold Q7): after allocation, if max allocation < TH, that CAD is skipped from notch export.",
            "- Effective boundary: To Full can only start from CAD-overlapped regulars that are connected to boundary seeds (outer edge or inactive-neighbor seed).",
            "- Multi-owner gate: if one regular is owned by more than one CAD (strict overlap over TH), that regular cannot be a To Full target.",
            "- Active regular visibility mask: boundary inactive-neighbor check uses current active regular set (match result scope), not all grid cells.",
            "- Allocation cutoff: reserved for future FW-oriented row minimization (not enabled in current build).",
            "- Export notch now opens a row picker, so each generated notch row can be included/excluded manually before file output.",
        ]),
        ("Numeric input and Console",
        [
            "Number Inputs",
            "- Type a value and press Enter or click away to commit.",
            "- Up/Down buttons step by the configured increment.",
            "- Drag the right scrub area to adjust quickly.",
            "- Alt + mouse wheel changes the value.",
            "Console & Undo",
            "- Bottom console streams logs and auto-scrolls to the latest entry.",
            "- Clear removes entries.",
            "- Ctrl + Z undoes settings and pad edits (global when not editing text).",
            "- Delete hides selected CAD pads (global when not editing text).",
        ]),
        ("Advanced reference",
        [
            "Build & Tests",
            "- Run scripts/build.ps1 to build and execute unit tests.",
        ]),
    ];

    [AvaloniaFact]
    public async Task QuickStartStaysOpenAndOtherGroupsStartCollapsedInDraftOrderAsync()
    {
        await WithViewAsync(view =>
        {
            var groups = Groups(view);
            Assert.Equal(_groupTitles, groups.Select(static group => group.Title));
            Assert.True(groups[0].IsExpanded);
            Assert.False(groups[0].IsCollapsible);
            groups[0].IsExpanded = false;
            Assert.True(groups[0].IsExpanded);
            Assert.All(groups.Skip(1), static group => Assert.False(group.IsExpanded));
            Assert.Equal("Advanced reference", groups[^1].Title);
            return Task.CompletedTask;
        });
    }

    [AvaloniaFact]
    public async Task CanvasNavigationTableHasTwoColumnsAndAllOriginalGestureRowsAsync()
    {
        await WithViewAsync(view =>
        {
            var group = Assert.Single(Groups(view), static group => group.Title == "Canvas navigation and selection");
            group.IsExpanded = true;
            return AssertCanvasTableAsync(view);
        });
    }

    [AvaloniaFact]
    public async Task ExistingHelpTextAndNotchFiguresStayInTheirGroupsAsync()
    {
        await WithViewAsync(async view =>
        {
            var groups = Groups(view);
            Assert.All(groups, static group => group.IsExpanded = true);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Contains("How To Use", VisibleText(view));
            Assert.Contains("Workflow, controls, and settings reference.", VisibleText(view));
            Assert.All(_sectionContents, expected =>
            {
                var group = Assert.Single(groups, group => group.Title == expected.Title);
                var text = VisibleText(group).ToArray();
                Assert.All(expected.Text, line => Assert.Contains(line, text));
            });
            var notch = Assert.Single(groups, static group => group.Title == "Notch reference");
            var figures = notch.GetVisualDescendants().OfType<Image>().ToArray();
            Assert.Equal(3, figures.Length);
            Assert.Equal(figures, view.GetVisualDescendants().OfType<Image>());
            Assert.All(figures, static figure =>
            {
                Assert.NotNull(figure.Source);
                Assert.True(figure.IsEffectivelyVisible);
            });
        });
    }

    private static async Task AssertCanvasTableAsync(HowToUseView view)
    {
        await HeadlessUiPump.FlushQueueAsync();
        var header = Assert.IsType<Grid>(view.FindControl<Grid>("CanvasNavigationHeader"));
        Assert.Equal(2, header.ColumnDefinitions.Count);
        Assert.Equal("Gesture or key", Assert.IsType<TextBlock>(header.Children[0]).Text);
        Assert.Equal("Action", Assert.IsType<TextBlock>(header.Children[1]).Text);
        Assert.Equal(1, Grid.GetColumn(header.Children[1]));
        var table = Assert.IsType<ListBox>(view.FindControl<ListBox>("CanvasNavigationTable"));
        Assert.Contains("workspaceDataList", table.Classes);
        Assert.True(table.IsEffectivelyVisible);
        var rows = table.GetVisualDescendants().OfType<Grid>()
            .Where(static grid => grid.Classes.Contains("howToGestureRow")).ToArray();
        Assert.Equal(_gestureRows, rows.Select(ReadGestureRow));
        Assert.All(rows, row => Assert.Equal(
            header.ColumnDefinitions.Select(static column => column.Width),
            row.ColumnDefinitions.Select(static column => column.Width)));
    }

    private static (string Gesture, string Action) ReadGestureRow(Grid row)
    {
        Assert.Equal(2, row.ColumnDefinitions.Count);
        var gesture = Assert.IsType<SelectableTextBlock>(row.Children[0]);
        var action = Assert.IsType<SelectableTextBlock>(row.Children[1]);
        Assert.Equal(0, Grid.GetColumn(gesture));
        Assert.Equal(1, Grid.GetColumn(action));
        Assert.Equal(TextWrapping.Wrap, gesture.TextWrapping);
        Assert.Equal(TextWrapping.Wrap, action.TextWrapping);
        return (gesture.Text ?? string.Empty, action.Text ?? string.Empty);
    }

    private static HidePanelBlock[] Groups(HowToUseView view) =>
        Assert.IsType<StackPanel>(view.FindControl<StackPanel>("HelpSections"))
            .Children.OfType<HidePanelBlock>().ToArray();

    private static IEnumerable<string> VisibleText(Control control) =>
        control.GetVisualDescendants().OfType<Control>()
            .Where(static child => child.IsEffectivelyVisible)
            .Select(static child => child switch
            {
                SelectableTextBlock text => text.Text,
                TextBlock text => text.Text,
                _ => null,
            })
            .OfType<string>();

    private static async Task WithViewAsync(Func<HowToUseView, Task> assertions)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var view = new HowToUseView { DataContext = new HowToUseViewModel() };
        var window = new Window { Content = view, Width = 840, Height = 900 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            await assertions(view);
        }
        finally
        {
            window.Close();
        }
    }
}

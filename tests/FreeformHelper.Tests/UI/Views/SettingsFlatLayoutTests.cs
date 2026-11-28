// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.SettingsSections;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Layout contract of the Settings window: fields, bindings, fit at minimum size, titles and moved text.</summary>
[Collection("HeadlessUiSerial")]
public sealed class SettingsFlatLayoutTests
{
    private static readonly (SettingsWindowSection Section, string Label, string Property)[] _fields =
    [
        (SettingsWindowSection.General, "Cascade", "CascadeNum"),
        (SettingsWindowSection.General, "AA size X (mm)", "ActiveAreaWidth"),
        (SettingsWindowSection.General, "AA size Y (mm)", "ActiveAreaHeight"),
        (SettingsWindowSection.General, "AA pixels X", "ActiveAreaPixels.PixelsX"),
        (SettingsWindowSection.General, "AA pixels Y", "ActiveAreaPixels.PixelsY"),
        (SettingsWindowSection.General, "Scan order", "SelectedScanOrderOption"),
        (SettingsWindowSection.General, "Regular source", "SelectedRegularSourceModeOption"),
        (SettingsWindowSection.General, "Regular layer", "SelectedRegularSourceLayerOption"),
        (SettingsWindowSection.General, "Grid padding (%)", "GridPaddingPercent"),
        (SettingsWindowSection.General, "Coordinate pixel X", "CoordinatePixelWidth"),
        (SettingsWindowSection.General, "Coordinate pixel Y", "CoordinatePixelHeight"),
        (SettingsWindowSection.General, "Panel bias X", "PanelBiasX"),
        (SettingsWindowSection.General, "Panel bias Y", "PanelBiasY"),
        (SettingsWindowSection.General, "Only closed polylines", "ImportOnlyClosedPolylines"),
        (SettingsWindowSection.General, "Import block polylines", "ImportBlockPolylines"),
        (SettingsWindowSection.General, "Auto bounds from visible layers", "RecalcBoundsOnLayerFilter"),
        (SettingsWindowSection.General, "Apply visual preferences on load", "ApplyAppVisualPreferencesOnProjectLoad"),
        (SettingsWindowSection.General, "Grid alignment mode", "SelectedGridAlignmentOption"),
        (SettingsWindowSection.General, "Log level", "SelectedLogLevelOption"),
        (SettingsWindowSection.General, "Global font size (%)", "GlobalFontSizePercent"),
        (SettingsWindowSection.General, "Highlight line width adjust", "HighlightStrokeWidthAdjust"),
        (SettingsWindowSection.General, "Sizing scope", "UseLocalSizing"),
        (SettingsWindowSection.Step1GeometryMatch, "Unmatched TH (overlap / regular area)", "MatchThreshold"),
        (SettingsWindowSection.Step2FreeformTagging, "Freeform TH (dominant axis ratio)", "FreeformAxisThreshold"),
        (SettingsWindowSection.Step2FreeformTagging, "XY WAY EN", "EnableAutoDetectXy"),
        (SettingsWindowSection.Step2FreeformTagging, "Edge spill EN", "EnableFreeformEdgeSpecialization"),
        (SettingsWindowSection.Step2FreeformTagging, "Load auto-run Step 2", "AutoReplayStep2AfterProjectLoad"),
        (SettingsWindowSection.Step3NotchCompensation, "Compensation model", "SelectedNotchCompensationModelOption"),
        (SettingsWindowSection.Step3NotchCompensation, "To Regular EN (derived)", "EnableToRegular"),
        (SettingsWindowSection.Step3NotchCompensation, "To Full EN (derived)", "EnableToFull"),
        (SettingsWindowSection.Step3NotchCompensation, "Multi-owner strict overlap (%)", "ToFullStrictOverlapPercent"),
        (SettingsWindowSection.Step3NotchCompensation, "Use To Full rule engine", "EnableToFullRuleEngine"),
        (SettingsWindowSection.Step3NotchCompensation, "Keep To Full trace", "EnableToFullRuleTrace"),
        (SettingsWindowSection.Step3NotchCompensation, "Stage auto play", "NotchPreviewAutoPlayEnabled"),
        (SettingsWindowSection.Step3NotchCompensation, "Interval (ms)", "NotchPreviewAutoPlayIntervalMs"),
        (SettingsWindowSection.Step3NotchCompensation, "Show To Regular labels", "ShowNotchToRegularLabels"),
        (SettingsWindowSection.Step3NotchCompensation, "Show To Full overlay", "ShowNotchCanvasPreview"),
        (SettingsWindowSection.Step3NotchCompensation, "To Full layer stage", "NotchPreviewVisualizationStep"),
        (SettingsWindowSection.Step4IndexDiagnostics, "Weight IoU", "MappingWeightIou"),
        (SettingsWindowSection.Step4IndexDiagnostics, "Weight distance", "MappingWeightCentroidDistance"),
        (SettingsWindowSection.Step4IndexDiagnostics, "Weight area", "MappingWeightAreaRatio"),
        (SettingsWindowSection.Step4IndexDiagnostics, "Candidate number", "MappingCandidateNumber"),
        (SettingsWindowSection.Step4IndexDiagnostics, "Low confidence threshold", "MappingLowConfidenceThreshold"),
        (SettingsWindowSection.Step4IndexDiagnostics, "Ambiguous margin (best - 2nd)", "MappingAmbiguousMargin"),
        (SettingsWindowSection.Step4IndexDiagnostics, "CAD Output FW Diff strategy", "SelectedCadOutputFwDiffAutoModeOption"),
        (SettingsWindowSection.Step4IndexDiagnostics, "CAD Output FW Diff start", "CadOutputFwDiffIndexStart"),
        (SettingsWindowSection.Step4IndexDiagnostics, "Show CAD Output FW Diff markers", "ShowDiffIndexOverlay"),
        (SettingsWindowSection.Step5NotchExport, "v2.1", "EnableV21"),
        (SettingsWindowSection.Step5NotchExport, "v2.2", "EnableV22"),
        (SettingsWindowSection.Step5NotchExport, "Export type", "SelectedNotchExportFileTypeOption"),
        (SettingsWindowSection.Step5NotchExport, "C export profile", "SelectedNotchExportProfileOption"),
        (SettingsWindowSection.Step5NotchExport, "Null value", "NullValue"),
        (SettingsWindowSection.Step5NotchExport, "Notch gate TH v2.1 (Q7/128)", "NotchThresholdQ7"),
        (SettingsWindowSection.Step5NotchExport, "Notch gate TH v2.2 (%)", "NotchThresholdPercent"),
        (SettingsWindowSection.Step5NotchExport, "Link TH (v2.1 Q7 <-> v2.2 %)", "LinkNotchThresholds"),
    ];

    private static readonly (SettingsWindowSection Section, string Sentence)[] _movedText =
    [
        (SettingsWindowSection.General, "Core setup · most projects start here."),
        (SettingsWindowSection.General, "Advanced mapping · adjust only for coordinate output or panel alignment."),
        (SettingsWindowSection.General, "DXF import scope and project-load behavior."),
        (SettingsWindowSection.General, "Workspace display scale and generated grid alignment."),
        (SettingsWindowSection.Step3NotchCompensation, "Single source of truth for Step 3 behavior."),
        (SettingsWindowSection.Step3NotchCompensation, "To Full expands pads into reachable free area."),
        (SettingsWindowSection.Step3NotchCompensation, "Restore area signal before redistribution."),
        (SettingsWindowSection.Step3NotchCompensation, "Expand into free area; cap impossible expansion."),
        (SettingsWindowSection.Step3NotchCompensation, "Compute gates and diagnostic trace controls."),
        (SettingsWindowSection.Step3NotchCompensation, "Higher ignores smaller overlaps."),
        (SettingsWindowSection.Step3NotchCompensation, "Adds diagnostics only; notch output is unchanged."),
        (SettingsWindowSection.Step3NotchCompensation, "Display-only diagnostics; generated notch rows are unchanged."),
        (SettingsWindowSection.Step3NotchCompensation, "Preview switches do not change computation."),
        (SettingsWindowSection.Step4IndexDiagnostics, "Geometry score weights for mapping diagnostics."),
        (SettingsWindowSection.Step4IndexDiagnostics, "CAD output FW diff numbering and visibility."),
        (SettingsWindowSection.Step4IndexDiagnostics, "reserved for CSV-confirmed geometry seed."),
        (SettingsWindowSection.Step5NotchExport, "Notch versions, output type, and C profile."),
        (SettingsWindowSection.Step5NotchExport, "Gate thresholds for v2.1 and v2.2 export."),
    ];

    [AvaloniaFact]
    public async Task EveryFieldIsPresentAndBoundToTheSamePropertyAsync()
    {
        await WithWindowAsync(async (window, draft) =>
        {
            foreach (var section in Enum.GetValues<SettingsWindowSection>())
            {
                var view = await ShowSectionAsync(window, section);
                var fields = _fields.Where(field => field.Section == section).ToArray();
                Assert.Equal(fields.Length, view.GetVisualDescendants().OfType<Control>().Count(IsInput));
                var seen = new HashSet<Control>();
                foreach (var field in fields)
                {
                    var input = FindInput(view, field.Label);
                    Assert.True(seen.Add(input), $"{field.Label} shares an input with another field.");
                    await AssertRoundTripAsync(input, draft, field.Property);
                }

                AssertEnableRules(view, draft);
            }
        });
    }

    [AvaloniaFact]
    public async Task ChannelTotalsAndCascadeDetailsStayInTheCoreRowAsync()
    {
        await WithWindowAsync(async (window, draft) =>
        {
            var view = await ShowSectionAsync(window, SettingsWindowSection.General);
            foreach (var (label, value) in new[] { ("Total X channels", draft.XChannels), ("Total Y channels", draft.YChannels) })
            {
                var field = (Panel)FindLabel(view, label).Parent!;
                var total = Assert.Single(field.GetVisualDescendants().OfType<SelectableTextBlock>(),
                    static text => text.Classes.Contains("settingsPassiveValue"));
                Assert.Equal($"{value:0}", total.Text);
            }

            var cascade = (Panel)FindLabel(view, "Cascade").Parent!;
            var edit = Assert.Single(cascade.GetVisualDescendants().OfType<Button>(),
                static button => button.Classes.Contains("actionIconButton"));
            Assert.Equal("Edit per-IC X/Y channels.", TipText(edit));
        });
    }

    [AvaloniaFact]
    public async Task Step3FieldsShareTwoColumnsAndOneRowRhythmAsync()
    {
        await WithWindowAsync(async (window, draft) =>
        {
            var view = await ShowSectionAsync(window, SettingsWindowSection.Step3NotchCompensation);
            Rect Area(Control control) => new(control.TranslatePoint(default, view)!.Value, control.Bounds.Size);
            var pairs = new[]
            {
                ("To Regular EN (derived)", "To Full EN (derived)"),
                ("Use To Full rule engine", "Keep To Full trace"),
                ("Stage auto play", "Interval (ms)"),
                ("Show To Regular labels", "Show To Full overlay"),
            };
            string[] singles = ["Multi-owner strict overlap (%)", "To Full layer stage"];
            // A switch starts where its track is drawn, not where its click area begins.
            double Start(Control input) => Area(input is ToggleSwitch
                ? input.GetVisualDescendants().OfType<Border>().Single(static part => part.Name == "ToggleBody")
                : input).X;
            var left = Area(FindLabel(view, pairs[0].Item1)).X;
            var right = Area(FindLabel(view, pairs[0].Item2)).X;

            foreach (var (first, second) in pairs)
            {
                foreach (var (label, column) in new[] { (first, left), (second, right) })
                {
                    Assert.Equal(column, Area(FindLabel(view, label)).X, .5);
                    Assert.Equal(column, Start(FindInput(view, label)), .5);
                }

                Assert.Equal(Area(FindLabel(view, first)).Y, Area(FindLabel(view, second)).Y, .5);
                Assert.Equal(Area(FindInput(view, first)).Center.Y, Area(FindInput(view, second)).Center.Y, .5);
            }

            var columnWidth = right - left;
            foreach (var label in singles)
            {
                Assert.Equal(left, Area(FindLabel(view, label)).X, .5);
                Assert.Equal(left, Area(FindInput(view, label)).X, .5);
            }

            var widths = singles.Append("Interval (ms)").Select(label => Area(FindInput(view, label)).Width).ToArray();
            Assert.All(widths, width => Assert.Equal(widths[0], width, .5));
            Assert.True(widths[0] < columnWidth, $"A number input of {widths[0]} spans more than one column.");

            // The full-width model list ends where the right column ends.
            var model = Area(FindInput(view, "Compensation model"));
            Assert.Equal(left, model.X, .5);
            Assert.Equal(Area(FindInput(view, "Interval (ms)")).Right, model.Right, .5);
        });
    }

    [AvaloniaFact]
    public async Task Step3ModelSummaryChipShowsTheWholeLineAsync()
    {
        await WithWindowAsync(async (window, draft) =>
        {
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            var view = await ShowSectionAsync(window, SettingsWindowSection.Step3NotchCompensation);
            foreach (var model in draft.NotchCompensationModelOptions)
            {
                draft.SelectedNotchCompensationModelOption = model;
                await HeadlessUiPump.FlushQueueAsync();
                var text = Assert.Single(view.GetVisualDescendants().OfType<SelectableTextBlock>(),
                    item => item.Text == draft.NotchEffectiveModelSummary);
                Assert.False(text.TextLayout.TextLines.Any(static line => line.HasCollapsed),
                    $"'{text.Text}' is cut in {text.Bounds.Width}.");
                AssertFits(text, view);
            }
        });
    }

    [AvaloniaTheory]
    [InlineData(SettingsWindowSection.General)]
    [InlineData(SettingsWindowSection.Step1GeometryMatch)]
    [InlineData(SettingsWindowSection.Step2FreeformTagging)]
    [InlineData(SettingsWindowSection.Step3NotchCompensation)]
    [InlineData(SettingsWindowSection.Step4IndexDiagnostics)]
    [InlineData(SettingsWindowSection.Step5NotchExport)]
    public async Task NoControlIsClippedAtTheMinimumWindowSizeAsync(SettingsWindowSection section)
    {
        await WithWindowAsync(async (window, draft) =>
        {
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            // The longest optional lines are shown too.
            draft.ActiveAreaPixels.PixelsX = 6480m;
            draft.ActiveAreaPixels.PixelsY = 100m;
            var view = await ShowSectionAsync(window, section);
            Assert.Equal(new Size(window.MinWidth, window.MinHeight), window.ClientSize);
            var scroll = window.FindControl<ScrollViewer>("SettingsWindowScroll")!;
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + .5, "The page scrolls sideways.");

            var parts = view.GetVisualDescendants().OfType<Control>()
                .Where(static part => part.IsEffectivelyVisible && (IsInput(part) || part is Button or TextBlock))
                .ToArray();
            Assert.NotEmpty(parts);
            foreach (var part in parts)
            {
                AssertFits(part, view);
            }
        });
    }

    [AvaloniaFact]
    public async Task EachPageTitleAppearsOnceInTheNavigationAsync()
    {
        var titles = new Dictionary<SettingsWindowSection, string[]>
        {
            [SettingsWindowSection.General] = ["General"],
            [SettingsWindowSection.Step1GeometryMatch] = ["Step 1 · Geometry", "Step 1 - Geometry match", "Geometry match"],
            [SettingsWindowSection.Step2FreeformTagging] = ["Step 2 · Freeform", "Step 2 - Freeform tagging", "Freeform tagging"],
            [SettingsWindowSection.Step3NotchCompensation] =
                ["Step 3 · Notch", "Step 3 - Notch compensation", "Notch compensation", "Current compensation model"],
            [SettingsWindowSection.Step4IndexDiagnostics] = ["Step 4 · Index", "Step 4 - Index diagnostics", "Index diagnostics"],
            [SettingsWindowSection.Step5NotchExport] = ["Step 5 · Export", "Step 5 - Notch export", "Notch export"],
        };

        await WithWindowAsync(async (window, draft) =>
        {
            foreach (var (section, names) in titles)
            {
                await ShowSectionAsync(window, section);
                var shown = window.GetVisualDescendants().OfType<TextBlock>()
                    .Where(static text => text.IsEffectivelyVisible)
                    .Select(static text => text.Text)
                    .ToArray();
                Assert.Equal(names[0], Assert.Single(shown, text => names.Contains(text)));
                Assert.DoesNotContain("Navigation", shown);
                Assert.DoesNotContain("Freeform Helper", shown);
                var selected = Assert.Single(window.GetVisualDescendants().OfType<ToggleButton>(),
                    static button => button is not ToggleSwitch && button.Classes.Contains("marked") && button.IsChecked == true);
                Assert.Equal(names[0], selected.Content);
                Assert.EndsWith(draft.SettingsSelectedSectionDescription, TipText(selected), StringComparison.Ordinal);
            }
        });
    }

    [AvaloniaFact]
    public async Task RemovedDescriptionsRemainAvailableAsTooltipsAsync()
    {
        await WithWindowAsync(async (window, draft) =>
        {
            foreach (var section in Enum.GetValues<SettingsWindowSection>())
            {
                var view = await ShowSectionAsync(window, section);
                var tips = view.GetVisualDescendants().OfType<Control>().Select(TipText).OfType<string>().ToArray();
                foreach (var moved in _movedText.Where(moved => moved.Section == section))
                {
                    Assert.Contains(tips, tip => tip.Contains(moved.Sentence, StringComparison.Ordinal));
                }

                if (section == SettingsWindowSection.Step3NotchCompensation)
                {
                    Assert.Contains(draft.NotchCompensationModelSummary, tips);
                }

                if (section == SettingsWindowSection.Step4IndexDiagnostics)
                {
                    Assert.Contains(draft.SelectedCadOutputFwDiffAutoModeOption.Description, tips);
                }

                if (section == SettingsWindowSection.Step5NotchExport)
                {
                    // Safety warnings stay on screen.
                    var shown = view.GetVisualDescendants().OfType<TextBlock>()
                        .Where(static text => text.IsEffectivelyVisible).Select(static text => text.Text).ToArray();
                    Assert.Contains(draft.NotchExportSafetyPolicySummary, shown);
                    Assert.Contains(draft.NotchEmsSafetyPolicySummary, shown);
                }
            }
        });
    }

    private static async Task WithWindowAsync(Func<SettingsWindow, SettingsWindowViewModel, Task> body)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var draft = new FreeformHelperViewModel().CreateSettingsWindowViewModel();
        var window = new SettingsWindow { DataContext = draft };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            await body(window, draft);
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task<UserControl> ShowSectionAsync(SettingsWindow window, SettingsWindowSection section)
    {
        window.NavigateToSection(section);
        await HeadlessUiPump.FlushQueueAsync();
        return Assert.Single(window.GetVisualDescendants().OfType<UserControl>(), static view =>
            view.IsVisible && view is SettingsGeneralSectionView or SettingsStep1SectionView or SettingsStep2SectionView
                or SettingsStep3SectionView or SettingsStep4SectionView or SettingsStep5SectionView);
    }

    private static bool IsInput(Control control) => control is NumberScrubber or ComboBox or ToggleSwitch;

    private static Control FindLabel(Control view, string label)
    {
        var infoLabels = view.GetVisualDescendants().OfType<SettingsInfoLabel>().Where(item => item.Text == label).ToArray();
        return infoLabels.Length > 0
            ? Assert.Single(infoLabels)
            : Assert.Single(view.GetVisualDescendants().OfType<TextBlock>(), item => item.Text == label);
    }

    private static Control FindInput(Control view, string label)
    {
        foreach (var scope in FindLabel(view, label).GetVisualAncestors().OfType<Control>())
        {
            var inputs = scope.GetVisualDescendants().OfType<Control>().Where(IsInput).ToArray();
            if (inputs.Length > 0)
            {
                return Assert.Single(inputs);
            }
        }

        throw new InvalidOperationException($"No input for {label}.");
    }

    private static object? Read(SettingsWindowViewModel draft, string path)
    {
        object? value = draft;
        foreach (var name in path.Split('.'))
        {
            value = value!.GetType().GetProperty(name)!.GetValue(value);
        }

        return value;
    }

    private static async Task AssertRoundTripAsync(Control input, SettingsWindowViewModel draft, string property)
    {
        if (input is NumberScrubber scrubber)
        {
            var before = Assert.IsType<decimal>(Read(draft, property));
            Assert.Equal(before, scrubber.Value);
            scrubber.SetCurrentValue(NumberScrubber.ValueProperty, OtherValue(scrubber));
            await HeadlessUiPump.FlushQueueAsync();
            Assert.NotEqual(before, Read(draft, property));
            Assert.Equal(Read(draft, property), scrubber.Value);
        }
        else if (input is ComboBox combo)
        {
            await AssertSelectionRoundTripAsync(combo, draft, property);
        }
        else
        {
            var toggle = Assert.IsType<ToggleSwitch>(input);
            Assert.Equal(Read(draft, property), toggle.IsChecked);
            if (!toggle.IsEnabled && property is "EnableToRegular" or "EnableToFull")
            {
                return;
            }

            var next = toggle.IsChecked != true;
            toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, next);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(next, Read(draft, property));
        }
    }

    private static async Task AssertSelectionRoundTripAsync(ComboBox combo, SettingsWindowViewModel draft, string property)
    {
        if (property == "SelectedRegularSourceLayerOption")
        {
            Assert.Same(draft.RegularSourceLayerOptions, combo.ItemsSource);
            return;
        }

        Assert.Equal(Read(draft, property), combo.SelectedItem);
        if (combo.ItemCount < 2)
        {
            return;
        }

        var next = (combo.SelectedIndex + 1) % combo.ItemCount;
        var item = combo.Items[next];
        combo.SetCurrentValue(SelectingItemsControl.SelectedIndexProperty, next);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal(item, Read(draft, property));
    }

    private static decimal OtherValue(NumberScrubber scrubber)
    {
        if (scrubber.Maximum - scrubber.Minimum <= 10m)
        {
            var quarter = scrubber.Minimum + ((scrubber.Maximum - scrubber.Minimum) / 4m);
            var half = scrubber.Minimum + ((scrubber.Maximum - scrubber.Minimum) / 2m);
            return Math.Abs(scrubber.Value - half) > .5m ? half : quarter;
        }

        return scrubber.Value + 2m <= scrubber.Maximum ? scrubber.Value + 2m : scrubber.Value - 2m;
    }

    private static void AssertEnableRules(Control view, SettingsWindowViewModel draft)
    {
        var rules = view switch
        {
            SettingsGeneralSectionView => new[]
            {
                ("Regular layer", draft.IsDxfLayerRegularSource),
                ("Panel bias X", draft.IsPanelAlignment),
                ("Panel bias Y", draft.IsPanelAlignment),
            },
            SettingsStep3SectionView => new[]
            {
                ("To Regular EN (derived)", false),
                ("To Full EN (derived)", false),
                ("Keep To Full trace", draft.EnableToFullRuleEngine),
            },
            _ when view is SettingsStep5SectionView => new[]
            {
                ("Notch gate TH v2.1 (Q7/128)", draft.EnableV21),
                ("Notch gate TH v2.2 (%)", draft.EnableV22),
            },
            _ => [],
        };

        foreach (var (label, enabled) in rules)
        {
            Assert.Equal(enabled, FindInput(view, label).IsEffectivelyEnabled);
        }
    }

    private static void AssertFits(Control part, Control view)
    {
        var name = part is TextBlock block ? block.Text : part.GetType().Name;
        var left = part.TranslatePoint(default, view)!.Value.X;
        Assert.True(left >= -.5 && left + part.Bounds.Width <= view.Bounds.Width + .5, $"'{name}' leaves the page.");
        var parent = (Visual)part.GetVisualParent()!;
        Assert.True(part.Bounds.Right <= parent.Bounds.Width + .5 && part.Bounds.Bottom <= parent.Bounds.Height + .5,
            $"'{name}' {part.Bounds} exceeds its parent {parent.Bounds}.");
        if (part is NumberScrubber)
        {
            Assert.True(part.Bounds.Width >= 80, $"A number input is only {part.Bounds.Width} wide.");
        }

        if (part is TextBlock { TextTrimming: var trimming } text && trimming == TextTrimming.None)
        {
            Assert.True(text.TextLayout.Width <= text.Bounds.Width + .5,
                $"'{name}' needs {text.TextLayout.Width} but has {text.Bounds.Width}.");
            Assert.True(text.TextLayout.Height <= text.Bounds.Height + .5, $"'{name}' is cut off vertically.");
            // A line that does not fit the height is left out of the layout, so every character must be laid out.
            var laidOut = text.TextLayout.TextLines.Sum(static line => line.Length);
            Assert.True(laidOut >= (text.Text?.Length ?? 0), $"'{name}' shows only {laidOut} characters.");
        }
    }

    private static string? TipText(Control control) => ToolTip.GetTip(control) switch
    {
        TextBlock block => block.Text,
        string text => text,
        _ => null,
    };
}

using System.Text;
using System.Text.RegularExpressions;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class UiLayoutGuardTests
{
    [Fact]
    public void ViewsAndViewModels_DoNotIntroduceLiteralEmsCapOrRiskWording()
    {
        var uiRoot = Path.Combine(TestPaths.RepoRoot, "src", "FreeformHelper.UI");
        var allowedExistingRiskLines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Views/DevView.axaml"] = "<SelectableTextBlock Text=\"EMS OK\"/>",
        };
        var literal = new Regex(
            @"\b480\b|\bEMS\s+(?:after\s+)?cap\s*[:=]?\s*\d+|\bAfter\s*(?:<=|>=|>|<)\s*\d+|EMS risk|EMS OK|audit warning|Near cap|overflow risk >255%",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var offenders = new List<string>();
        var observedExistingRiskLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in new[] { "Views", "ViewModels" })
        {
            var root = Path.Combine(uiRoot, directory);
            foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                         .Where(static path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                                               path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase)))
            {
                var relativePath = Path.GetRelativePath(uiRoot, file).Replace('\\', '/');
                var lines = TestFiles.ReadContentLines(file);
                for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
                {
                    var line = lines[lineNumber].Trim();
                    if (literal.IsMatch(line) &&
                        (!allowedExistingRiskLines.TryGetValue(relativePath, out var allowedLine) ||
                         line != allowedLine || !observedExistingRiskLines.Add(relativePath)))
                    {
                        offenders.Add($"{relativePath}:{lineNumber + 1}");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, "EMS cap and risk text belong to shared projectors. Offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Xaml_DoesNotBindScrollContentToBoundsWidth()
    {
        var repoRoot = TestPaths.RepoRoot;
        var uiRoot = Path.Combine(repoRoot, "src", "FreeformHelper.UI");
        Assert.True(Directory.Exists(uiRoot), $"UI root not found: {uiRoot}");

        var offenders = new List<string>();
        var pattern = new Regex(@"(Width|MaxWidth)\s*=\s*""\{Binding\s+#.+?\.Bounds\.Width\}""", RegexOptions.Compiled);
        foreach (var file in Directory.EnumerateFiles(uiRoot, "*.axaml", SearchOption.AllDirectories))
        {
            var lines = TestFiles.ReadContentLines(file);
            for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
            {
                if (pattern.IsMatch(lines[lineNumber]))
                {
                    var relativePath = Path.GetRelativePath(repoRoot, file);
                    offenders.Add($"{relativePath}:{lineNumber + 1}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Avoid binding scroll content Width/MaxWidth to Bounds.Width. Use Viewport.Width for horizontal clipping safety. Offenders: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void NotchExportSelection_PhysicalAuditWarning_UsesExistingWarningChip()
    {
        var repoRoot = TestPaths.RepoRoot;
        var xaml = TestFiles.ReadContent(
            Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "NotchExportSelectionWindow.axaml"));
        var warningChip = FindXamlElementTagContaining(xaml, "Border", "HasSimulationPhysicalAuditWarning");

        Assert.Contains("Classes=\"chipStatus warning\"", warningChip, StringComparison.Ordinal);
        Assert.Contains("Classes=\"chipStatusText\"", warningChip, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding HasSimulationPhysicalAuditWarning}\"", warningChip, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding SimulationSafetyExportSummaryText}\"", warningChip, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding SimulationSafetyExportSummaryText}\"", warningChip, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=", warningChip, StringComparison.Ordinal);
        Assert.DoesNotContain("Foreground=", warningChip, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize=", warningChip, StringComparison.Ordinal);
    }

    [Fact]
    public void NotchSafetyGuidance_UsesSharedDynamicCapProjection()
    {
        var repoRoot = TestPaths.RepoRoot;
        var mainState = TestFiles.ReadContent(Path.Combine(
            repoRoot,
            "src",
            "FreeformHelper.UI",
            "ViewModels",
            "FreeformHelperViewModel.State.NotchPreview.cs"));
        var settingsState = TestFiles.ReadContent(Path.Combine(
            repoRoot,
            "src",
            "FreeformHelper.UI",
            "ViewModels",
            "SettingsWindowViewModel.cs"));
        var rightWorkflowSettings = TestFiles.ReadContent(Path.Combine(
            repoRoot,
            "src",
            "FreeformHelper.UI",
            "Views",
            "RightWorkflowSections",
            "RightWorkflowSettingsTabView.axaml"));

        Assert.DoesNotContain("After > 480", mainState, StringComparison.Ordinal);
        Assert.DoesNotContain("EMS cap 480", mainState, StringComparison.Ordinal);
        Assert.DoesNotContain("After > 480", settingsState, StringComparison.Ordinal);
        Assert.DoesNotContain("EMS cap 480", settingsState, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Safety gate blocks export when Simulation has After > 480.",
            rightWorkflowSettings,
            StringComparison.Ordinal);
        var step5View = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "WorkflowSteps", "RightWorkflowStep5View.axaml"));
        Assert.Contains("ToolTip.Tip=\"{Binding NotchExportSafetyPolicySummary}\"", step5View, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowStepTextActions_UseDefaultButtonStyle()
    {
        var repoRoot = TestPaths.RepoRoot;
        var stepsRoot = Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "WorkflowSteps");
        var checks = new[]
        {
            (Path.Combine(stepsRoot, "RightWorkflowStep1View.axaml"), "ImportRegularVisibilityMaskCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep1View.axaml"), "ClearRegularVisibilityMaskCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep2View.axaml"), "SetFreeformNoneCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep2View.axaml"), "SetFreeformXCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep2View.axaml"), "SetFreeformYCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep2View.axaml"), "SetFreeformXYCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep4View.axaml"), "SelectStep4DuplicateDiffGroupCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep4View.axaml"), "OffsetSelectedCadOutputFwDiffIndicesCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep4View.axaml"), "AnalyzeIndexMappingCommand"),
        };

        foreach (var (file, commandName) in checks)
        {
            var button = FindXamlElementTagContaining(TestFiles.ReadContent(file), "Button", commandName);
            Assert.DoesNotContain("actionTextButton", button, StringComparison.Ordinal);
            Assert.DoesNotContain("actionNeutral", button, StringComparison.Ordinal);
            Assert.DoesNotContain("actionPrimary", button, StringComparison.Ordinal);
        }

        var step2View = TestFiles.ReadContent(Path.Combine(stepsRoot, "RightWorkflowStep2View.axaml"));
        Assert.Contains("ColumnSpacing=\"{StaticResource Space6}\"", step2View, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowAndWorkspaceHeader_DoNotUseInlineFontSize()
    {
        var repoRoot = TestPaths.RepoRoot;
        var files = new[]
        {
            Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "RightWorkflowPanel.axaml"),
            Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Controls", "WorkspaceHeader.axaml"),
        };

        var inlineFontSizePattern = new Regex(@"\bFontSize\s*=", RegexOptions.Compiled);
        var offenders = new List<string>();

        foreach (var file in files)
        {
            var lines = TestFiles.ReadContentLines(file);
            for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
            {
                if (!inlineFontSizePattern.IsMatch(lines[lineNumber]))
                {
                    continue;
                }

                offenders.Add($"{Path.GetRelativePath(repoRoot, file)}:{lineNumber + 1}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "High-frequency workflow/header text should inherit global font scale (no inline FontSize). Offenders: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void HoverAffordances_KeepContrastTooltipsAndInputBorderScope()
    {
        var repoRoot = TestPaths.RepoRoot;
        var stylesRoot = Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Styles");
        var actionStyles = TestFiles.ReadContent(Path.Combine(stylesRoot, "Controls.Action.axaml"));
        var coreStyles = TestFiles.ReadContent(Path.Combine(stylesRoot, "Controls.Core.axaml"));
        var overlayStyles = TestFiles.ReadContent(Path.Combine(stylesRoot, "Controls.Overlay.axaml"));
        var formStyles = TestFiles.ReadContent(Path.Combine(stylesRoot, "Controls.Form.axaml"));
        var tokens = TestFiles.ReadContent(Path.Combine(stylesRoot, "Tokens.axaml"));
        var allStyles = ReadAllStylesText(repoRoot);
        AssertNoStyleSetterCollisions(stylesRoot);

        var inverseForegroundOffenders = FindInverseBackgroundForegroundOffenders(actionStyles);
        Assert.True(
            inverseForegroundOffenders.Count == 0,
            "Interactive hover/focus/pressed styles using inverse backgrounds must use BrushActionInverseForeground. Offenders: "
            + string.Join(", ", inverseForegroundOffenders));

        AssertMinimumContrast(tokens, "ColorActionInverseBackground", "ColorActionInverseForeground", 4.5);
        AssertMinimumContrast(tokens, "ColorActionNeutralBackground", "ColorActionNeutralForeground", 4.5);
        AssertMinimumContrast(tokens, "ColorButtonNeutralHover", "ColorButtonNeutralForeground", 4.5);
        AssertMinimumContrast(tokens, "ColorButtonNeutralBackground", "ColorButtonDisabledForeground", 4.5);
        AssertMinimumContrast(tokens, "ColorTooltipBackground", "ColorTooltipForeground", 4.5);
        Assert.Equal(2, Regex.Count(tokens, "<Color x:Key=\"ColorTooltipBackground\">#FFFFFF</Color>"));
        Assert.Equal(2, Regex.Count(tokens, "<Color x:Key=\"ColorTooltipForeground\">#000000</Color>"));
        AssertInteractiveActionStateContrast(actionStyles, tokens, 4.5);

        Assert.Contains("Button.actionTextButton.actionNeutral, ToggleButton.actionTextButton.actionNeutral", actionStyles, StringComparison.Ordinal);
        Assert.Contains("BrushButtonNeutralBackground", actionStyles, StringComparison.Ordinal);
        Assert.Contains("BrushButtonNeutralHover", actionStyles, StringComparison.Ordinal);
        Assert.Contains("BrushButtonNeutralPressed", actionStyles, StringComparison.Ordinal);
        Assert.Contains("BrushButtonNeutralForeground", actionStyles, StringComparison.Ordinal);
        Assert.Contains("BrushButtonDisabledForeground", actionStyles, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(tokens, "<Color x:Key=\"ColorButtonNeutralForeground\">#1E2430</Color>"));
        Assert.Equal(2, Regex.Count(tokens, "<Color x:Key=\"ColorButtonDisabledForeground\">#4A5568</Color>"));

        var appCode = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "App.axaml.cs"));
        var tooltipServiceCode = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Services", "SharedToolTipStyleService.cs"));
        Assert.Contains("Controls.Overlay.axaml", TestFiles.ReadContent(Path.Combine(stylesRoot, "Controls.axaml")), StringComparison.Ordinal);
        Assert.Contains("Style Selector=\"ToolTip TextBlock, ToolTip SelectableTextBlock\"", overlayStyles, StringComparison.Ordinal);
        Assert.Contains("Style Selector=\"ToolTip /template/ ContentPresenter\"", overlayStyles, StringComparison.Ordinal);
        Assert.Contains("BrushTooltipBackground", overlayStyles, StringComparison.Ordinal);
        Assert.Contains("BrushTooltipBorder", overlayStyles, StringComparison.Ordinal);
        Assert.Contains("BrushTooltipForeground", overlayStyles, StringComparison.Ordinal);
        Assert.Contains("SharedToolTipStyleService.Register();", appCode, StringComparison.Ordinal);
        Assert.Contains("ToolTip.TipProperty.Changed.AddClassHandler<Control, object?>", tooltipServiceCode, StringComparison.Ordinal);
        Assert.Contains("target.SetCurrentValue(ToolTip.TipProperty", tooltipServiceCode, StringComparison.Ordinal);
        Assert.Contains("BrushTooltipForeground", tooltipServiceCode, StringComparison.Ordinal);
        var tooltipBaseStyle = ExtractStyleBlocks(overlayStyles)
            .Single(static style => style.Selector == "ToolTip");
        Assert.Equal("{DynamicResource BrushTooltipForeground}", GetSetterValue(tooltipBaseStyle.Body, "Foreground"));
        Assert.DoesNotContain("TextBlock.Foreground", tooltipBaseStyle.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("AncestorType=ToolTip", allStyles, StringComparison.Ordinal);
        Assert.Contains("Button:pointerover", coreStyles, StringComparison.Ordinal);
        Assert.Contains("BrushButtonNeutralForeground", coreStyles, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(coreStyles, "ToolTip.ShowOnDisabled"));
        Assert.Contains("Property=\"ToolTip.ShowOnDisabled\" Value=\"True\"", coreStyles, StringComparison.Ordinal);
        Assert.Contains("Button:disabled:pointerover, Button:disabled:pressed", coreStyles, StringComparison.Ordinal);
        Assert.Contains("ToggleButton:disabled:pointerover, ToggleButton:disabled:pressed", coreStyles, StringComparison.Ordinal);
        Assert.Contains("Button.dangerTextButton:disabled:pointerover, Button.dangerTextButton:disabled:pressed", actionStyles, StringComparison.Ordinal);
        AssertDisabledInteractiveStatesKeepMutedForeground(coreStyles, actionStyles);

        Assert.Contains("<Thickness x:Key=\"BorderInput\">2</Thickness>", tokens, StringComparison.Ordinal);
        Assert.Contains("<Thickness x:Key=\"BorderControl\">1.5</Thickness>", tokens, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"{StaticResource BorderInput}\"", formStyles, StringComparison.Ordinal);
        Assert.Contains("Style Selector=\"ToggleSwitch\"", formStyles, StringComparison.Ordinal);
        Assert.Contains("Style Selector=\"ToggleSwitch:checked\"", formStyles, StringComparison.Ordinal);
        Assert.Contains("BorderBrush\" Value=\"{DynamicResource BrushBorderStrong}\"", formStyles, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderInput", actionStyles, StringComparison.Ordinal);

        var tooltipOffenders = FindIconActionButtonsWithoutToolTips(repoRoot);
        Assert.True(
            tooltipOffenders.Count == 0,
            "Icon-only/chrome action controls need ToolTip.Tip so hover help remains inspectable. Offenders: "
            + string.Join(", ", tooltipOffenders));
    }

    [Fact]
    public void RegularLayerSelector_UsesFixedPopupWidth_AndSettingsOpenLogIncludesSourceSection()
    {
        var repoRoot = TestPaths.RepoRoot;

        var controlsStyles = ReadAllStylesText(repoRoot);
        Assert.Contains("Style Selector=\"ComboBox.regularLayerSelector /template/ Popup\"", controlsStyles, StringComparison.Ordinal);
        Assert.Contains("MinWidth\" Value=\"{Binding $parent[ComboBox].Bounds.Width}\"", controlsStyles, StringComparison.Ordinal);
        Assert.Contains("MaxWidth\" Value=\"{Binding $parent[ComboBox].Bounds.Width}\"", controlsStyles, StringComparison.Ordinal);
        Assert.Contains("PlacementConstraintAdjustment\" Value=\"All\"", controlsStyles, StringComparison.Ordinal);

        var rightWorkflowSettings = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "RightWorkflowSections", "RightWorkflowSettingsTabView.axaml"));
        Assert.DoesNotContain("ComboBox Classes=\"regularLayerSelector\"", rightWorkflowSettings, StringComparison.Ordinal);

        var settingsGeneralSection = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "SettingsSections", "SettingsGeneralSectionView.axaml"));
        Assert.Contains("ComboBox Classes=\"regularLayerSelector\"", settingsGeneralSection, StringComparison.Ordinal);

        var settingsWindowCode = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "FreeformHelperView.SettingsWindow.cs"));
        Assert.Contains("Open settings window requested: source={0}, section={1}.", settingsWindowCode, StringComparison.Ordinal);
    }

    [Fact]
    public void CriticalDynamicTextBindings_UseWrapOrTrimmingContract()
    {
        var repoRoot = TestPaths.RepoRoot;
        var checks = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "SettingsSections", "SettingsGeneralSectionView.axaml")] = new[]
            {
                "Description",
                "Summary"
            },
            [Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "RightWorkflowPanel.axaml")] = new[]
            {
                "PadInspectorPrimaryText",
                "PadInspectorSecondaryText",
                "PadInspectorMatchText",
                "NotchValidationSummaryText"
            },
            [Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "NotchExportSelectionWindow.axaml")] = new[]
            {
                "ExportSummaryText",
                "HiddenSelectedRowsHintText",
                "InspectorHeaderText",
                "SelectedRowCodePreviewText",
                "SelectedRowAnalysisText",
                "SelectedRowPadRelationText",
                "SelectedRowPayloadAnchorText",
                "SelectedRowPayloadCombineText",
                "SelectedRowPayloadTarget1Text",
                "SelectedRowPayloadTarget2Text",
                "SelectedRowFlagsText",
                "SelectedRowCommentText"
            }
        };

        var offenders = new List<string>();
        foreach (var pair in checks)
        {
            var xaml = TestFiles.ReadContent(pair.Key);
            var relativePath = Path.GetRelativePath(repoRoot, pair.Key);
            foreach (var bindingPath in pair.Value)
            {
                foreach (var tagSnippet in FindNonWrappedSelectableTextTags(xaml, bindingPath))
                {
                    offenders.Add($"{relativePath}::{bindingPath} => {tagSnippet}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Critical dynamic text must wrap, trim, or inherit stepNote wrapping style. Offenders:\n" + string.Join('\n', offenders));
    }

    [Fact]
    public void PadInfoPopover_RegularDetails_UsesBoundedScrollPattern()
    {
        var repoRoot = TestPaths.RepoRoot;
        var regularPadInfoContent = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "PadInfoSections", "RegularPadInfoContentView.axaml"));

        Assert.Contains("x:Name=\"RegularPadInfoScroll\"", regularPadInfoContent, StringComparison.Ordinal);
        Assert.Contains("MaxHeight=\"{DynamicResource PadInfoContentMaxHeight}\"", regularPadInfoContent, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding ShowGeometryDetails}\"", regularPadInfoContent, StringComparison.Ordinal);
    }

    [Fact]
    public void PadInfoPopover_UsesDividerToggleForLowFrequencyDetailSections()
    {
        var repoRoot = TestPaths.RepoRoot;
        var regularPadInfoContent = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "PadInfoSections", "RegularPadInfoContentView.axaml"));

        Assert.Contains("panels:CollapsiblePanel x:Name=\"GeometryDebugSection\" Title=\"Geometry / Debug\"", regularPadInfoContent, StringComparison.Ordinal);
        Assert.Contains("IsExpanded=\"{Binding ShowGeometryDetails, Mode=TwoWay}\"", regularPadInfoContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"{Binding GeometryDetailsToggleText}\"", regularPadInfoContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewsAndControls_DoNotUseInlineHexColors()
    {
        var repoRoot = TestPaths.RepoRoot;
        var uiRoot = Path.Combine(repoRoot, "src", "FreeformHelper.UI");
        var hexPattern = new Regex(@"#[0-9A-Fa-f]{3,8}", RegexOptions.Compiled);
        var offenders = new List<string>();

        foreach (var relative in new[] { "Views", "Controls" })
        {
            var dir = Path.Combine(uiRoot, relative);
            foreach (var file in Directory.EnumerateFiles(dir, "*.axaml", SearchOption.AllDirectories))
            {
                var lines = TestFiles.ReadContentLines(file);
                for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
                {
                    if (!hexPattern.IsMatch(lines[lineNumber]))
                    {
                        continue;
                    }

                    offenders.Add($"{Path.GetRelativePath(repoRoot, file)}:{lineNumber + 1}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Use tokens/resources instead of inline hex colors in Views/Controls. Offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void StylesOutsideTokens_DoNotUseInlineHexColors()
    {
        var repoRoot = TestPaths.RepoRoot;
        var stylesRoot = Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Styles");
        var hexPattern = new Regex(@"#[0-9A-Fa-f]{3,8}", RegexOptions.Compiled);
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(stylesRoot, "*.axaml", SearchOption.AllDirectories))
        {
            if (string.Equals(Path.GetFileName(file), "Tokens.axaml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lines = TestFiles.ReadContentLines(file);
            for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
            {
                if (!hexPattern.IsMatch(lines[lineNumber]))
                {
                    continue;
                }

                offenders.Add($"{Path.GetRelativePath(repoRoot, file)}:{lineNumber + 1}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Keep hex colors centralized in Tokens.axaml. Offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void ShortcutContract_DoesNotExposeResetViewR()
    {
        var repoRoot = TestPaths.RepoRoot;
        var freeformViewCode = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "FreeformHelperView.axaml.cs"));
        var freeformShortcutCode = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "FreeformHelperView.InputAndShortcuts.cs"));
        var canvasInputCode = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Controls", "PadCanvas.Input.cs"));
        var howToUseXaml = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "HowToUseView.axaml"));

        var keyRPattern = new Regex(@"Key\.R\b", RegexOptions.Compiled);
        Assert.False(keyRPattern.IsMatch(freeformViewCode), "Global shortcut handler should not bind Key.R.");
        Assert.False(keyRPattern.IsMatch(freeformShortcutCode), "Global shortcut handler should not bind Key.R.");
        Assert.False(keyRPattern.IsMatch(canvasInputCode), "Canvas shortcut handler should not bind Key.R.");
        Assert.DoesNotContain("Reset view", howToUseXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("R:", howToUseXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ShortcutContract_GlobalAndCanvasScopesRemainConsistent()
    {
        var repoRoot = TestPaths.RepoRoot;
        var overlayXaml = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "CanvasOverlayControls.axaml"));
        var freeformLifecycleCode = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "FreeformHelperView.Lifecycle.cs"));
        var freeformShortcutCode = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "FreeformHelperView.InputAndShortcuts.cs"));
        var canvasNavigationCode = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Controls", "PadCanvas.Input.Navigation.cs"));
        var dxfEditChangeListWindowCode = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "DxfEditChangeListWindow.axaml.cs"));
        var mainWindowCode = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "MainWindow.axaml.cs"));

        Assert.Contains("Alt: prefer Regular", overlayXaml, StringComparison.Ordinal);
        Assert.Contains("_topLevel.KeyDown += OnTopLevelKeyDown;", freeformLifecycleCode, StringComparison.Ordinal);
        Assert.Contains("_topLevel.KeyDown -= OnTopLevelKeyDown;", freeformLifecycleCode, StringComparison.Ordinal);
        Assert.Matches(@"IsTextEntryControlSource\(e\.Source\)[\s\S]*private static bool IsTextEntryControlSource[\s\S]*?ConsolePanel[\s\S]*?private static bool IsCanvasInputSource", freeformShortcutCode);
        Assert.Contains("if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.None)", freeformShortcutCode, StringComparison.Ordinal);
        Assert.Contains("if (e.Key == Key.A && e.KeyModifiers == KeyModifiers.Control)", freeformShortcutCode, StringComparison.Ordinal);
        Assert.Contains("if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control)", freeformShortcutCode, StringComparison.Ordinal);
        Assert.Contains("if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)", freeformShortcutCode, StringComparison.Ordinal);
        Assert.Contains("if (e.Key == Key.Delete && e.KeyModifiers == KeyModifiers.None)", freeformShortcutCode, StringComparison.Ordinal);
        AssertShortcutBranchDefersCanvasSource(
            freeformShortcutCode,
            "if (e.Key == Key.A && e.KeyModifiers == KeyModifiers.Control)",
            "_canvas?.SelectAllPads();");
        AssertShortcutBranchDefersCanvasSource(
            freeformShortcutCode,
            "if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.None)",
            "viewModel.FitCommand.Execute(null);");
        Assert.Contains("SaveProjectFromShortcutAsync(viewModel)", freeformShortcutCode, StringComparison.Ordinal);
        Assert.Contains("window.ShowTopToast(message, type);", freeformShortcutCode, StringComparison.Ordinal);
        Assert.Contains("public void ShowTopToast(string message, NotificationType type = NotificationType.Information)", mainWindowCode, StringComparison.Ordinal);

        Assert.Contains("if (e.Key == Key.F)", canvasNavigationCode, StringComparison.Ordinal);
        Assert.Contains("if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.A)", canvasNavigationCode, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\bKey\.Delete\b", canvasNavigationCode);
        Assert.DoesNotMatch(@"\bKey\.Z\b", canvasNavigationCode);
        Assert.DoesNotMatch(@"\bKey\.S\b", canvasNavigationCode);
        Assert.Contains("if (e.Key == Key.A && e.KeyModifiers.HasFlag(KeyModifiers.Control))", dxfEditChangeListWindowCode, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\bKey\.F\b", dxfEditChangeListWindowCode);
        Assert.DoesNotMatch(@"\bKey\.Delete\b", dxfEditChangeListWindowCode);
    }

    [Fact]
    public void CanvasLowDetailDecimation_UsesTokenizedThresholds()
    {
        var repoRoot = TestPaths.RepoRoot;
        var tokens = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Styles", "Tokens.axaml"));
        var visibleDrawListBuilder = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Controls", "PadCanvas.VisibleDrawListBuilder.cs"));

        Assert.Contains("CanvasLowDetailRegularDecimationMaxStep", tokens, StringComparison.Ordinal);
        Assert.Contains("CanvasLowDetailCadMaxVisibleUnselected", tokens, StringComparison.Ordinal);
        Assert.Contains("CanvasLowDetailCadMinScreenSize", tokens, StringComparison.Ordinal);

        Assert.Contains("CanvasLowDetailRegularDecimationMaxStep", visibleDrawListBuilder, StringComparison.Ordinal);
        Assert.Contains("CanvasLowDetailCadMaxVisibleUnselected", visibleDrawListBuilder, StringComparison.Ordinal);
        Assert.Contains("CanvasLowDetailCadMinScreenSize", visibleDrawListBuilder, StringComparison.Ordinal);
        Assert.Contains("_cadIndex.Query(worldViewport)", visibleDrawListBuilder, StringComparison.Ordinal);
    }

    private static List<string> FindNonWrappedSelectableTextTags(string xaml, string bindingPath)
    {
        var pattern = new Regex(
            $@"<SelectableTextBlock\b[\s\S]*?Text=""\{{Binding\s+{Regex.Escape(bindingPath)}(?:[^""]*)""[\s\S]*?/>",
            RegexOptions.Compiled);
        var matches = pattern.Matches(xaml);
        var offenders = new List<string>();
        foreach (Match match in matches)
        {
            var tag = match.Value;
            var hasWrap = tag.Contains("TextWrapping=", StringComparison.Ordinal);
            var hasTrim = tag.Contains("TextTrimming=", StringComparison.Ordinal);
            var hasMaxWidth = tag.Contains("MaxWidth=", StringComparison.Ordinal);
            var hasStepNoteClass = Regex.IsMatch(
                tag,
                @"Classes\s*=\s*""[^""]*\bstepNote\b[^""]*""",
                RegexOptions.Compiled);

            if (hasWrap || hasTrim || hasMaxWidth || hasStepNoteClass)
            {
                continue;
            }

            offenders.Add(NormalizeTagSnippet(tag));
        }

        return offenders;
    }

    private static string FindXamlElementTagContaining(string xaml, string tagName, string requiredText)
    {
        var pattern = new Regex(
            $@"<{Regex.Escape(tagName)}\b[^>]*/>|<{Regex.Escape(tagName)}\b[\s\S]*?</{Regex.Escape(tagName)}>",
            RegexOptions.Compiled);
        foreach (Match match in pattern.Matches(xaml))
        {
            if (match.Value.Contains(requiredText, StringComparison.Ordinal))
            {
                return match.Value;
            }
        }

        throw new InvalidOperationException($"Cannot find <{tagName}> containing '{requiredText}'.");
    }

    private static void AssertShortcutBranchDefersCanvasSource(string source, string branchCondition, string actionSnippet)
    {
        var branchStart = source.IndexOf(branchCondition, StringComparison.Ordinal);
        Assert.True(branchStart >= 0, $"Shortcut branch missing: {branchCondition}");

        var actionStart = source.IndexOf(actionSnippet, branchStart, StringComparison.Ordinal);
        Assert.True(actionStart > branchStart, $"Shortcut action missing after branch: {actionSnippet}");

        var branchPrefix = source[branchStart..actionStart];
        Assert.Contains("if (fromCanvas)", branchPrefix, StringComparison.Ordinal);
        Assert.Contains("return false;", branchPrefix, StringComparison.Ordinal);
    }

    private static List<string> FindIconActionButtonsWithoutToolTips(string repoRoot)
    {
        var uiRoot = Path.Combine(repoRoot, "src", "FreeformHelper.UI");
        var tagPattern = new Regex(
            @"<(?<tag>Button|ToggleButton)\b[\s\S]*?(?:/>|</\k<tag>>)",
            RegexOptions.Compiled);
        var classPattern = new Regex(
            @"Classes\s*=\s*""[^""]*\b(actionIconButton|viewportOverlayAction|consoleHeaderAction|panelChromeToggle|dxfEditCompactIconAction)\b[^""]*""",
            RegexOptions.Compiled);
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(uiRoot, "*.axaml", SearchOption.AllDirectories))
        {
            var xaml = TestFiles.ReadContent(file);
            foreach (Match match in tagPattern.Matches(xaml))
            {
                var tag = match.Value;
                if (!classPattern.IsMatch(tag) || tag.Contains("ToolTip.Tip=", StringComparison.Ordinal))
                {
                    continue;
                }

                offenders.Add($"{Path.GetRelativePath(repoRoot, file)}:{NormalizeTagSnippet(tag)}");
            }
        }

        return offenders;
    }

    private static List<string> FindInverseBackgroundForegroundOffenders(string stylesText)
    {
        var offenders = new List<string>();
        foreach (var style in ExtractStyleBlocks(stylesText))
        {
            var selector = style.Selector;
            var body = style.Body;

            var usesInverseBackground =
                body.Contains("BrushActionInverseBackground", StringComparison.Ordinal) ||
                body.Contains("BrushActionInversePressedBackground", StringComparison.Ordinal);
            if (!usesInverseBackground)
            {
                continue;
            }

            var isInteractiveControlState =
                (selector.Contains("Button", StringComparison.Ordinal) || selector.Contains("ToggleButton", StringComparison.Ordinal)) &&
                (selector.Contains(":pointerover", StringComparison.Ordinal) ||
                 selector.Contains(":focus-visible", StringComparison.Ordinal) ||
                 selector.Contains(":pressed", StringComparison.Ordinal)) &&
                !selector.Contains(" Border.", StringComparison.Ordinal) &&
                !selector.Contains(" controls|", StringComparison.Ordinal) &&
                !selector.Contains(" TextBlock", StringComparison.Ordinal) &&
                !selector.Contains(" SelectableTextBlock", StringComparison.Ordinal);
            if (!isInteractiveControlState)
            {
                continue;
            }

            var controlForeground = GetSetterValue(body, "Foreground");
            if (!string.Equals(controlForeground, "{DynamicResource BrushActionInverseForeground}", StringComparison.Ordinal))
            {
                offenders.Add($"{selector} -> Foreground={controlForeground ?? "<missing>"}");
            }
        }

        return offenders;
    }

    private static void AssertNoStyleSetterCollisions(string stylesRoot)
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(stylesRoot, "*.axaml", SearchOption.TopDirectoryOnly))
        {
            var stylesText = TestFiles.ReadContent(file);
            foreach (var style in ExtractStyleBlocks(stylesText))
            {
                var setterProperties = ExtractSetterProperties(style.Body);
                var duplicateProperties = setterProperties
                    .GroupBy(static property => property, StringComparer.Ordinal)
                    .Where(static group => group.Count() > 1)
                    .Select(static group => group.Key)
                    .ToList();
                foreach (var duplicateProperty in duplicateProperties)
                {
                    offenders.Add($"{Path.GetFileName(file)}::{style.Selector} duplicate {duplicateProperty}");
                }

                var hasControlForeground = setterProperties.Contains("Foreground", StringComparer.Ordinal);
                var attachedForegroundProperties = setterProperties
                    .Where(static property => property.EndsWith(".Foreground", StringComparison.Ordinal))
                    .ToList();
                if (hasControlForeground && attachedForegroundProperties.Count > 0)
                {
                    offenders.Add(
                        $"{Path.GetFileName(file)}::{style.Selector} mixes Foreground with "
                        + string.Join("/", attachedForegroundProperties));
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Style blocks must not contain duplicate or aliased setters; Avalonia can fail at runtime when opening controls such as ToolTip. Offenders: "
            + string.Join(", ", offenders));
    }

    private static string NormalizeTagSnippet(string tag)
    {
        var normalized = Regex.Replace(tag, @"\s+", " ");
        const int maxLength = 180;
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return string.Concat(normalized.AsSpan(0, maxLength), "...");
    }

    private static string ReadAllStylesText(string repoRoot)
    {
        var stylesRoot = Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Styles");
        var builder = new StringBuilder();
        foreach (var file in Directory.EnumerateFiles(stylesRoot, "*.axaml", SearchOption.AllDirectories).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(TestFiles.ReadContent(file));
        }

        return builder.ToString();
    }

}

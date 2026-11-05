using System.Text;
using System.Text.RegularExpressions;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class UiLayoutGuardTests
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
    public void CriticalScrollContainers_UseViewportBoundedWidth()
    {
        var repoRoot = TestPaths.RepoRoot;

        var controlsStyles = ReadAllStylesText(repoRoot);
        Assert.Contains("Style Selector=\"ScrollViewer.viewportBoundScroll\"", controlsStyles, StringComparison.Ordinal);
        Assert.Contains("HorizontalContentAlignment\" Value=\"Stretch", controlsStyles, StringComparison.Ordinal);
        Assert.Contains("Padding\" Value=\"0", controlsStyles, StringComparison.Ordinal);
        Assert.Contains("Style Selector=\"Border.viewportBoundContent\"", controlsStyles, StringComparison.Ordinal);
        Assert.Contains("$parent[ScrollViewer].Viewport.Width", controlsStyles, StringComparison.Ordinal);
        Assert.Contains("Style Selector=\"ToggleButton.rightPanelTab\"", controlsStyles, StringComparison.Ordinal);

        var mainWindow = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "MainWindow.axaml"));
        Assert.Contains("WindowState=\"Maximized\"", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowState=\"FullScreen\"", mainWindow, StringComparison.Ordinal);

        var freeformView = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "FreeformHelperView.axaml"));
        Assert.Contains("x:Name=\"RightSettingsScroll\"", freeformView, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"LeftSettingsScroll\"", freeformView, StringComparison.Ordinal);
        Assert.Contains("Classes=\"scrollDevCandidate viewportBoundScroll\"", freeformView, StringComparison.Ordinal);
        Assert.Contains("Classes=\"viewportBoundContent\"", freeformView, StringComparison.Ordinal);
        Assert.Contains("Padding=\"{StaticResource InsetRight12Bottom20}\"", freeformView, StringComparison.Ordinal);
        Assert.Contains("ShowNotchToRegularLabels=\"{Binding IsNotchToRegularPreviewVisible}\"", freeformView, StringComparison.Ordinal);
        Assert.Contains("Classes=\"rightPanelTabHost\"", freeformView, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsRightPanelSettingsTab, Mode=TwoWay}\"", freeformView, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsRightPanelInspectorTab, Mode=TwoWay}\"", freeformView, StringComparison.Ordinal);
        Assert.Matches(@"GridSplitter\s+Grid\.Row=""1""[\s\S]*ResizeDirection=""Rows""[\s\S]*ShowsPreview=""True""", freeformView);

        var leftPanel = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "LeftDxfPanel.axaml"));
        Assert.Contains("Classes=\"boundLayerSelector\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("<leftPanel:LayerListSearchBox DataContext=\"{Binding LayerSearch}\"/>", leftPanel, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding LayerSearch.VisibleLayers}\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("MaxDropDownHeight=\"{StaticResource LayerListMaxHeight}\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("controls:BalancedWrapPanel Classes=\"badgeGroup dxfEditBadgeGroup\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dxfLayerBulkToggleButton\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsCadLayerBulkToggleChecked, Mode=TwoWay}\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dxfLayerBulkToggleContent\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dxfLayerBulkMixedIndicator\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding AreSomeCadLayersSelected}\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dxfLayerBulkToggleLabel\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding CadLayerBulkToggleText}\"", leftPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("dxfLayerBulkToggleIcon", leftPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("dxfLayerBulkToggleSwitch", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dangerTextButton dxfEditMiniAction\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dxfEditAlignedFieldRow\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dxfEditFieldActionSpacer\"", leftPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("actionDanger actionTextButton dxfEditMiniAction", leftPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("actionGhost actionTextButton dxfEditMiniAction", leftPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("actionNeutral actionTextButton dxfEditMiniAction", leftPanel, StringComparison.Ordinal);
        var dxfOverlapCheckButton = FindXamlElementTagContaining(leftPanel, "Button", "CheckDxfQualityCommand");
        Assert.Contains("Classes=\"workspaceActionButton\"", dxfOverlapCheckButton, StringComparison.Ordinal);
        Assert.DoesNotContain("actionTextButton", dxfOverlapCheckButton, StringComparison.Ordinal);
        Assert.DoesNotContain("actionNeutral", dxfOverlapCheckButton, StringComparison.Ordinal);
        Assert.DoesNotContain("dxfLayerSegmentRail", leftPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("dxfLayerSegmentAction", leftPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("Margin=\"{StaticResource InsetRight12Bottom12}\"", leftPanel, StringComparison.Ordinal);

        var overlayControls = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "CanvasOverlayControls.axaml"));
        Assert.Contains("Text=\"To Regular\"", overlayControls, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding ShowNotchToRegularLabels, Mode=TwoWay}\"", overlayControls, StringComparison.Ordinal);

        var settingsWindow = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "SettingsWindow.axaml"));
        Assert.Contains("x:Name=\"SettingsWindowScroll\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("Classes=\"viewportBoundScroll\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("Classes=\"viewportBoundContent\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("Padding=\"{StaticResource InsetRight10Bottom12}\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsGeneralSectionSelected, Mode=TwoWay}\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsStep1SectionSelected, Mode=TwoWay}\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsStep2SectionSelected, Mode=TwoWay}\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsStep3SectionSelected, Mode=TwoWay}\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsStep4SectionSelected, Mode=TwoWay}\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsStep5SectionSelected, Mode=TwoWay}\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"GeneralSection\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsGeneralSectionSelected}\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"Step1Section\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsStep1SectionSelected}\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"Step2Section\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsStep2SectionSelected}\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"Step3Section\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsStep3SectionSelected}\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"Step4Section\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsStep4SectionSelected}\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"Step5Section\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsStep5SectionSelected}\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dangerTextButton\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("Content=\"Cancel\"", settingsWindow, StringComparison.Ordinal);
        Assert.Contains("Content=\"Save Settings\"", settingsWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("actionDanger actionTextButton", settingsWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("actionGhost actionTextButton", settingsWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("actionPrimary actionTextButton", settingsWindow, StringComparison.Ordinal);

        var settingsGeneralSection = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "SettingsSections", "SettingsGeneralSectionView.axaml"));
        Assert.Contains("Value=\"{Binding HighlightStrokeWidthAdjust, Mode=TwoWay}\"", settingsGeneralSection, StringComparison.Ordinal);
        Assert.Contains("Click=\"EditCascadeDetailsButton_Click\"", settingsGeneralSection, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding XChannels, StringFormat='{}{0:0}'}\"", settingsGeneralSection, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding YChannels, StringFormat='{}{0:0}'}\"", settingsGeneralSection, StringComparison.Ordinal);
        Assert.DoesNotContain("Value=\"{Binding XChannels, Mode=TwoWay}\"", settingsGeneralSection, StringComparison.Ordinal);
        Assert.DoesNotContain("Value=\"{Binding YChannels, Mode=TwoWay}\"", settingsGeneralSection, StringComparison.Ordinal);

        var cascadeDetailsWindow = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "CascadeIcSettingsWindow.axaml"));
        Assert.Contains("ItemsSource=\"{Binding CascadeIcSettings}\"", cascadeDetailsWindow, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding XChannels, Mode=TwoWay}\"", cascadeDetailsWindow, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding YChannels, Mode=TwoWay}\"", cascadeDetailsWindow, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding CascadeNum, Mode=TwoWay}\"", cascadeDetailsWindow, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding CascadeLayoutSummary}\"", cascadeDetailsWindow, StringComparison.Ordinal);

        var settingsStep3Section = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "SettingsSections", "SettingsStep3SectionView.axaml"));
        Assert.Contains("IsChecked=\"{Binding ShowNotchToRegularLabels, Mode=TwoWay}\"", settingsStep3Section, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding EnableToFullRuleEngine, Mode=TwoWay}\"", settingsStep3Section, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding EnableToFullRuleTrace, Mode=TwoWay}\"", settingsStep3Section, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ToFullRuleEngineSummary}\"", settingsStep3Section, StringComparison.Ordinal);

        var workspaceHeader = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Controls", "WorkspaceHeader.axaml"));
        Assert.Contains("x:Name=\"ViewMenuScroll\"", workspaceHeader, StringComparison.Ordinal);
        Assert.Contains("Classes=\"viewportBoundScroll\"", workspaceHeader, StringComparison.Ordinal);
        Assert.Contains("Classes=\"viewportBoundContent\"", workspaceHeader, StringComparison.Ordinal);
        Assert.Contains("Padding=\"{StaticResource InsetRight12Bottom20}\"", workspaceHeader, StringComparison.Ordinal);
        Assert.Contains("Text=\"Quick toggles\"", workspaceHeader, StringComparison.Ordinal);
        Assert.Contains("Text=\"Line width\"", workspaceHeader, StringComparison.Ordinal);
        Assert.Contains("Title=\"Colors and opacity (advanced)\"", workspaceHeader, StringComparison.Ordinal);
        Assert.Contains("Title=\"CAD area buckets (advanced)\"", workspaceHeader, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding HighlightStrokeWidthAdjust, Mode=TwoWay}\"", workspaceHeader, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{Binding ColorCadByArea}\"", workspaceHeader, StringComparison.Ordinal);
        Assert.Contains("Style Selector=\"TextBox.workspaceHeaderInput\"", workspaceHeader, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"{StaticResource BorderInput}\"", workspaceHeader, StringComparison.Ordinal);
        Assert.Equal(4, Regex.Count(workspaceHeader, "Classes=\"workspaceHeaderInput\""));
        Assert.DoesNotContain("<Expander", workspaceHeader, StringComparison.OrdinalIgnoreCase);

        var rightPanel = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "RightWorkflowPanel.axaml"));
        Assert.Contains("<views:RightWorkflowInspectorView/>", rightPanel, StringComparison.Ordinal);
        Assert.Contains("<views:RightWorkflowSettingsTabView", rightPanel, StringComparison.Ordinal);
        Assert.Contains("DataTemplate x:Key=\"NotchValidationRowTemplate\"", rightPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("<Expander", rightPanel, StringComparison.OrdinalIgnoreCase);

        var actionStyles = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Styles", "Controls.Action.axaml"));
        Assert.Contains("Button.viewportOverlayAction:pointerover", actionStyles, StringComparison.Ordinal);
        Assert.Contains("BrushActionInverseBackground", actionStyles, StringComparison.Ordinal);
        Assert.Contains("Button.dangerTextButton", actionStyles, StringComparison.Ordinal);
        Assert.Contains("BrushActionDangerOnNeutralForeground", actionStyles, StringComparison.Ordinal);
        Assert.Contains("BrushActionDangerForeground", actionStyles, StringComparison.Ordinal);
        Assert.Contains("Button.dxfEditMiniAction:pointerover", actionStyles, StringComparison.Ordinal);
        Assert.Contains("BrushActionTextHoverBackground", actionStyles, StringComparison.Ordinal);
        Assert.Contains("Button.dxfEditMiniAction", actionStyles, StringComparison.Ordinal);
        Assert.Contains("CornerRadius\" Value=\"{StaticResource RadiusSm}\"", actionStyles, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment\" Value=\"Stretch\"", actionStyles, StringComparison.Ordinal);
        var actionChipBaseStyle = ExtractStyleBlocks(actionStyles)
            .Single(static style => style.Selector == "Button.actionChip, ToggleButton.actionChip");
        var chipStatusBaseStyle = ExtractStyleBlocks(actionStyles)
            .Single(static style => style.Selector == "Border.chipStatus");
        Assert.Equal("{StaticResource BorderControl}", GetSetterValue(actionChipBaseStyle.Body, "BorderThickness"));
        Assert.Equal("{StaticResource BorderControl}", GetSetterValue(chipStatusBaseStyle.Body, "BorderThickness"));
        Assert.Contains("Button.dangerTextButton.dxfEditMiniAction:pointerover", actionStyles, StringComparison.Ordinal);
        Assert.DoesNotContain("ToggleButton.dxfLayerBulkToggle", actionStyles, StringComparison.Ordinal);
        Assert.Contains("ToggleButton.actionTextButton.actionNeutral:checked", actionStyles, StringComparison.Ordinal);
        Assert.DoesNotContain("Button.actionNeutral.dxfEditMiniAction", actionStyles, StringComparison.Ordinal);
        Assert.DoesNotContain("Button.actionGhost.dxfEditMiniAction", actionStyles, StringComparison.Ordinal);
        var dxfMiniHoverStyle = ExtractStyleBlocks(actionStyles)
            .Single(static style => style.Selector == "Button.dxfEditMiniAction:pointerover");
        Assert.Equal("{DynamicResource BrushActionTextHoverForeground}", GetSetterValue(dxfMiniHoverStyle.Body, "Foreground"));
        Assert.DoesNotContain("BrushButtonNeutralForeground", dxfMiniHoverStyle.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("BrushTextMuted", dxfMiniHoverStyle.Body, StringComparison.Ordinal);
        var dxfMiniPressedStyle = ExtractStyleBlocks(actionStyles)
            .Single(static style => style.Selector == "Button.dxfEditMiniAction:pressed");
        Assert.Equal("{DynamicResource BrushActionTextHoverForeground}", GetSetterValue(dxfMiniPressedStyle.Body, "Foreground"));
        var dxfDangerMiniHoverStyle = ExtractStyleBlocks(actionStyles)
            .Single(static style => style.Selector == "Button.dangerTextButton.dxfEditMiniAction:pointerover");
        Assert.Equal("{DynamicResource BrushActionDangerForeground}", GetSetterValue(dxfDangerMiniHoverStyle.Body, "Foreground"));
        var dxfMiniDisabledStyle = ExtractStyleBlocks(actionStyles)
            .Single(static style => style.Selector == "Button.dxfEditMiniAction:disabled");
        Assert.Equal("{DynamicResource BrushButtonDisabledForeground}", GetSetterValue(dxfMiniDisabledStyle.Body, "Foreground"));
        var dxfMiniDisabledHoverStyle = ExtractStyleBlocks(actionStyles)
            .Single(static style => style.Selector == "Button.dxfEditMiniAction:disabled:pointerover, Button.dxfEditMiniAction:disabled:pressed");
        Assert.Equal("{DynamicResource BrushButtonDisabledForeground}", GetSetterValue(dxfMiniDisabledHoverStyle.Body, "Foreground"));
        Assert.DoesNotContain("BrushButtonNeutralForeground", dxfMiniDisabledHoverStyle.Body, StringComparison.Ordinal);

        var dxfResetButton = FindXamlElementTagContaining(leftPanel, "Button", "ResetAllDxfEditsButton_Click");
        var dxfRestoreLastButton = FindXamlElementTagContaining(leftPanel, "Button", "RestoreLastDeletedCadPadsCommand");
        var dxfRestoreAllButton = FindXamlElementTagContaining(leftPanel, "Button", "RestoreAllHiddenCadPadsCommand");
        Assert.Contains("IconGlyphs.RestartAlt", dxfResetButton, StringComparison.Ordinal);
        Assert.Contains("IconGlyphs.Undo", dxfRestoreLastButton, StringComparison.Ordinal);
        Assert.Contains("IconGlyphs.Visibility", dxfRestoreAllButton, StringComparison.Ordinal);

        var dxfHideSectionStart = leftPanel.IndexOf("Text=\"Hide / restore\"", StringComparison.Ordinal);
        var dxfCombineSectionStart = leftPanel.IndexOf("Text=\"Combine / split\"", StringComparison.Ordinal);
        var dxfAdvancedSectionStart = leftPanel.IndexOf("Text=\"Advanced edit\"", dxfCombineSectionStart, StringComparison.Ordinal);
        Assert.True(dxfHideSectionStart >= 0, "DXF Hide / restore section is missing.");
        Assert.True(dxfCombineSectionStart > dxfHideSectionStart, "DXF Combine / split section must follow Hide / restore.");
        Assert.True(dxfAdvancedSectionStart > dxfCombineSectionStart, "DXF Advanced edit section must follow quick actions.");
        Assert.Equal(2, Regex.Count(leftPanel, "Classes=\"dxfEditActionGroup\""));
        var dxfHideGroupStart = leftPanel.LastIndexOf("<Border Classes=\"dxfEditActionGroup\"", dxfHideSectionStart, StringComparison.Ordinal);
        var dxfCombineGroupStart = leftPanel.LastIndexOf("<Border Classes=\"dxfEditActionGroup\"", dxfCombineSectionStart, StringComparison.Ordinal);
        Assert.True(dxfHideGroupStart >= 0, "DXF Hide / restore group container is missing.");
        Assert.True(dxfCombineGroupStart > dxfHideGroupStart, "DXF Combine / split group container must follow Hide / restore.");

        var dxfHideSection = leftPanel[dxfHideGroupStart..dxfCombineGroupStart];
        Assert.Contains("Classes=\"dxfEditActionGroup\"", dxfHideSection, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dxfEditActionGroupRow\"", dxfHideSection, StringComparison.Ordinal);
        Assert.Contains("DeleteSelectedCadPadsCommand", dxfHideSection, StringComparison.Ordinal);
        Assert.Contains("RestoreLastDeletedCadPadsCommand", dxfHideSection, StringComparison.Ordinal);
        Assert.Contains("RestoreAllHiddenCadPadsCommand", dxfHideSection, StringComparison.Ordinal);
        Assert.Contains("Text=\"Hide selected\"", dxfHideSection, StringComparison.Ordinal);
        Assert.Contains("Text=\"Undo hide\"", dxfHideSection, StringComparison.Ordinal);
        Assert.Contains("Text=\"Show all\"", dxfHideSection, StringComparison.Ordinal);
        Assert.DoesNotContain("CombineSelectedCadPadsCommand", dxfHideSection, StringComparison.Ordinal);

        var dxfCombineSection = leftPanel[dxfCombineGroupStart..dxfAdvancedSectionStart];
        Assert.Contains("Classes=\"dxfEditActionGroup\"", dxfCombineSection, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"*,*\"", dxfCombineSection, StringComparison.Ordinal);
        Assert.Contains("CombineSelectedCadPadsCommand", dxfCombineSection, StringComparison.Ordinal);
        Assert.Contains("ClearCombinedCadPadsCommand", dxfCombineSection, StringComparison.Ordinal);
        Assert.Contains("IconGlyphs.CallSplit", dxfCombineSection, StringComparison.Ordinal);
        Assert.Contains("Text=\"Combine\"", dxfCombineSection, StringComparison.Ordinal);
        Assert.Contains("Text=\"Split selected\"", dxfCombineSection, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteSelectedCadPadsCommand", dxfCombineSection, StringComparison.Ordinal);
        Assert.DoesNotContain("controls:BalancedWrapPanel Classes=\"dxfEditToolbar\"", leftPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("dxfEditToolbarAction", leftPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("dxfEditToolbarSurface", leftPanel, StringComparison.Ordinal);

        var coreStyles = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Styles", "Controls.Core.axaml"));
        Assert.Contains("controls|BalancedWrapPanel.badgeGroup", coreStyles, StringComparison.Ordinal);
        Assert.Contains("BalanceLastRow", coreStyles, StringComparison.Ordinal);
        Assert.Contains("DxfEditSummaryBadgeMinWidth", coreStyles, StringComparison.Ordinal);
        Assert.Contains("Button.workspaceActionButton", coreStyles, StringComparison.Ordinal);
        Assert.Contains("ToggleButton.workspaceFoldHeader:checked", coreStyles, StringComparison.Ordinal);
        Assert.Contains("Border.workspaceFileRow", coreStyles, StringComparison.Ordinal);
        Assert.Contains("SelectableTextBlock.workspaceFileRowTitle", coreStyles, StringComparison.Ordinal);
        Assert.Contains("Border.dxfEditAdvancedContent", coreStyles, StringComparison.Ordinal);
        Assert.Contains("Border.dxfEditActionGroup", coreStyles, StringComparison.Ordinal);
        Assert.Contains("Grid.dxfEditActionGroupRow", coreStyles, StringComparison.Ordinal);
        Assert.Contains("ToggleButton.dxfLayerBulkToggleButton", coreStyles, StringComparison.Ordinal);
        Assert.Contains("ToggleButton.dxfLayerBulkToggleButton:checked", coreStyles, StringComparison.Ordinal);
        Assert.Contains("Border.dxfLayerBulkMixedIndicator", coreStyles, StringComparison.Ordinal);
        Assert.Contains("SelectableTextBlock.dxfLayerBulkToggleLabel", coreStyles, StringComparison.Ordinal);
        Assert.DoesNotContain("dxfLayerBulkToggleIcon", coreStyles, StringComparison.Ordinal);
        Assert.DoesNotContain("dxfLayerBulkToggleSwitch", coreStyles, StringComparison.Ordinal);
        Assert.Contains("Grid.dxfEditAlignedFieldRow", coreStyles, StringComparison.Ordinal);
        Assert.Contains("Border.dxfEditFieldActionSpacer", coreStyles, StringComparison.Ordinal);
        Assert.Contains("AncestorType=Button", coreStyles, StringComparison.Ordinal);
        Assert.DoesNotContain("controls|BalancedWrapPanel.dxfEditToolbar", coreStyles, StringComparison.Ordinal);
        Assert.DoesNotContain("dxfLayerSegmentRail", coreStyles, StringComparison.Ordinal);
        Assert.DoesNotContain("dxfLayerSegmentAction", coreStyles, StringComparison.Ordinal);
        Assert.DoesNotContain("dxfEditAccordionHeader", coreStyles, StringComparison.Ordinal);

        var devView = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "DevView.axaml"));
        Assert.Contains("Classes=\"dangerTextButton dxfEditMiniAction\"", devView, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dxfLayerBulkToggleButton\"", devView, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dxfLayerBulkMixedIndicator\"", devView, StringComparison.Ordinal);
        Assert.Contains("Classes=\"workspaceActionButton\"", devView, StringComparison.Ordinal);
        Assert.DoesNotContain("actionDanger actionTextButton dxfEditMiniAction", devView, StringComparison.Ordinal);
        Assert.DoesNotContain("actionGhost actionTextButton dxfEditMiniAction", devView, StringComparison.Ordinal);
        Assert.DoesNotContain("actionNeutral actionTextButton dxfEditMiniAction", devView, StringComparison.Ordinal);
        Assert.DoesNotContain("actionNeutral actionTextButton workspaceActionButton", devView, StringComparison.Ordinal);

        var simulationControlsPane = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "WorkspaceSections", "SimulationWorkspaceControlsPaneView.axaml"));
        Assert.Contains("Classes=\"workspaceFileRow\"", simulationControlsPane, StringComparison.Ordinal);
        Assert.Contains("Classes=\"workspaceFileRowTitle\"", simulationControlsPane, StringComparison.Ordinal);
        Assert.Contains("Glyph=\"{x:Static icons:IconGlyphs.DataObject}\"", simulationControlsPane, StringComparison.Ordinal);

        var simulationWorkspace = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "SimulationWorkspaceView.axaml"));
        Assert.Matches(@"GridSplitter\s+Grid\.Row=""2""[\s\S]*ResizeDirection=""Rows""[\s\S]*ShowsPreview=""True""", simulationWorkspace);

        var rightWorkflowSettings = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "RightWorkflowSections", "RightWorkflowSettingsTabView.axaml"));
        Assert.Contains("Title=\"Overview\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("Text=\"Project / grid\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("Text=\"Simulation safety\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding SimulationSafetyOverviewStatusText}\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"Loaded CAD pad count after DXF import and current DXF edits.\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"Generated regular-grid pad count.\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding SimulationSafetyOverviewSummaryText}\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding SimulationSafetyOverviewHighRiskDiffsText}\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"Step 5 export file type.\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding NotchExportSafetyPolicySummary}\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("Tag=\"General\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("Tag=\"Step3NotchCompensation\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("Tag=\"Step5NotchExport\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("Value=\"{Binding CascadeNum, Mode=TwoWay}\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("ComboBox Classes=\"regularLayerSelector\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsRightPanelSettingsTab}\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("Title=\"DIRECT details\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("Title=\"IN details\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("Title=\"OUT details\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("IsExpanded=\"False\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding NotchValidationDirectItems}\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding NotchValidationIncomingItems}\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding NotchValidationOutgoingItems}\"", rightWorkflowSettings, StringComparison.Ordinal);

        var rightWorkflowInspector = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "RightWorkflowSections", "RightWorkflowInspectorView.axaml"));
        Assert.Contains("IsVisible=\"{Binding IsRightPanelInspectorTab}\"", rightWorkflowInspector, StringComparison.Ordinal);

        var step3View = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "WorkflowSteps", "RightWorkflowStep3View.axaml"));
        Assert.Contains("Text=\"{Binding NotchPreviewStageSummary}\"", step3View, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding NotchToFullRuleEngineSummary}\"", step3View, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{Binding CanControlNotchPreviewStage}\"", step3View, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding NotchPreviewSeedLayerStateText}\"", step3View, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding NotchTargetCoverageCapHelpText}\"", step3View, StringComparison.Ordinal);
        Assert.Contains("Tip=\"{Binding NotchTargetCoverageCapHelpText}\"", settingsStep3Section, StringComparison.Ordinal);
        Assert.DoesNotContain("120% maps uniform 400 to EMS cap 480", step3View, StringComparison.Ordinal);
        Assert.DoesNotContain("120% maps uniform 400 to EMS cap 480", settingsStep3Section, StringComparison.Ordinal);

        var step5View = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "WorkflowSteps", "RightWorkflowStep5View.axaml"));
        Assert.Contains("Text=\"Handoff checklist\"", step5View, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding NotchExportHandoffFileText}\"", step5View, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding NotchExportHandoffVersionText}\"", step5View, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding NotchExportHandoffProfileText}\"", step5View, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding NotchExportHandoffChecklistText}\"", step5View, StringComparison.Ordinal);
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
        Assert.Contains("Text=\"{Binding SimulationSafetyExportBadgeText}\"", warningChip, StringComparison.Ordinal);
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
        Assert.Contains(
            "ToolTip.Tip=\"{Binding NotchExportSafetyPolicySummary}\"",
            rightWorkflowSettings,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowStepTextActions_UseDefaultButtonStyle()
    {
        var repoRoot = TestPaths.RepoRoot;
        var stepsRoot = Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "WorkflowSteps");
        var checks = new[]
        {
            (Path.Combine(stepsRoot, "RightWorkflowStep1View.axaml"), "MatchCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep1View.axaml"), "ImportRegularVisibilityMaskCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep1View.axaml"), "ClearRegularVisibilityMaskCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep2View.axaml"), "AutoDetectFreeformsCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep2View.axaml"), "SetFreeformNoneCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep2View.axaml"), "SetFreeformXCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep2View.axaml"), "SetFreeformYCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep2View.axaml"), "SetFreeformXYCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep4View.axaml"), "SelectStep4DuplicateDiffGroupCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep4View.axaml"), "OffsetSelectedCadOutputFwDiffIndicesCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep4View.axaml"), "AnalyzeIndexMappingCommand"),
            (Path.Combine(stepsRoot, "RightWorkflowStep5View.axaml"), "ExportNotchCommand"),
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
                "SummaryText",
                "DistributionText",
                "PrimaryOutcomeText",
                "CompactMetaText",
                "SelectedRowHeaderText",
                "SelectedRowCodePreviewText",
                "SelectedRowOutcomeText",
                "SelectedRowPadRelationText",
                "SelectedRowPayloadText",
                "SelectedRowColumnBreakdownText",
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
        Assert.Contains("MaxHeight=\"{StaticResource PadInfoContentMaxHeight}\"", regularPadInfoContent, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding ShowGeometryDetails}\"", regularPadInfoContent, StringComparison.Ordinal);
    }

    [Fact]
    public void PadInfoPopover_UsesDividerToggleForLowFrequencyDetailSections()
    {
        var repoRoot = TestPaths.RepoRoot;
        var regularPadInfoContent = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "PadInfoSections", "RegularPadInfoContentView.axaml"));

        Assert.Contains("Classes=\"padInfoSectionToggle debugCard\"", regularPadInfoContent, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ToggleGeometryDetailsCommand}\"", regularPadInfoContent, StringComparison.Ordinal);
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

        Assert.Contains("Alt = Regular priority", overlayXaml, StringComparison.Ordinal);
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

    private static void AssertInteractiveActionStateContrast(string actionStyles, string tokens, double minimumRatio)
    {
        var colorsByTheme = ReadThemeColors(tokens);
        var colorKeysByBrush = ReadBrushColorKeys(tokens);
        var offenders = new List<string>();

        foreach (var style in ExtractStyleBlocks(actionStyles))
        {
            if (!IsInteractiveActionControlStateSelector(style.Selector))
            {
                continue;
            }

            var backgroundBrush = GetDynamicResourceSetterValue(style.Body, "Background");
            var foregroundBrush = GetDynamicResourceSetterValue(style.Body, "Foreground");
            if (backgroundBrush is null || foregroundBrush is null)
            {
                continue;
            }

            if (!colorKeysByBrush.TryGetValue(backgroundBrush, out var backgroundKey) ||
                !colorKeysByBrush.TryGetValue(foregroundBrush, out var foregroundKey))
            {
                continue;
            }

            foreach (var theme in new[] { "Dark", "Light" })
            {
                Assert.True(colorsByTheme.TryGetValue(theme, out var colors), $"Theme not found: {theme}");
                Assert.True(colors.TryGetValue(backgroundKey, out var background), $"{theme} token missing: {backgroundKey}");
                Assert.True(colors.TryGetValue(foregroundKey, out var foreground), $"{theme} token missing: {foregroundKey}");

                var ratio = ContrastRatio(background, foreground);
                if (ratio < minimumRatio)
                {
                    offenders.Add(
                        $"{theme}: {style.Selector} {backgroundBrush}/{foregroundBrush} contrast={ratio:0.00}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Interactive action states with explicit tokenized background/foreground must keep readable contrast. Offenders: "
            + string.Join(", ", offenders));
    }

    private static void AssertDisabledInteractiveStatesKeepMutedForeground(params string[] stylesTexts)
    {
        var offenders = new List<string>();
        foreach (var stylesText in stylesTexts)
        {
            foreach (var style in ExtractStyleBlocks(stylesText))
            {
                var selector = style.Selector;
                var isDisabledInteractiveState =
                    selector.Contains(":disabled", StringComparison.Ordinal) &&
                    (selector.Contains(":pointerover", StringComparison.Ordinal) ||
                     selector.Contains(":pressed", StringComparison.Ordinal)) &&
                    (selector.Contains("Button", StringComparison.Ordinal) ||
                     selector.Contains("ToggleButton", StringComparison.Ordinal)) &&
                    !selector.Contains(" Border.", StringComparison.Ordinal) &&
                    !selector.Contains(" controls|", StringComparison.Ordinal) &&
                    !selector.Contains(" TextBlock", StringComparison.Ordinal) &&
                    !selector.Contains(" SelectableTextBlock", StringComparison.Ordinal);
                if (!isDisabledInteractiveState)
                {
                    continue;
                }

                var foreground = GetSetterValue(style.Body, "Foreground");
                if (!string.Equals(foreground, "{DynamicResource BrushButtonDisabledForeground}", StringComparison.Ordinal))
                {
                    offenders.Add($"{selector} -> Foreground={foreground ?? "<missing>"}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Disabled hover/press states must use disabled button foreground so ToolTip.ShowOnDisabled does not make disabled controls look active. Offenders: "
            + string.Join(", ", offenders));
    }

    private static bool IsInteractiveActionControlStateSelector(string selector)
    {
        return (selector.Contains("Button", StringComparison.Ordinal) || selector.Contains("ToggleButton", StringComparison.Ordinal)) &&
            (selector.Contains(":pointerover", StringComparison.Ordinal) ||
             selector.Contains(":focus-visible", StringComparison.Ordinal) ||
             selector.Contains(":pressed", StringComparison.Ordinal) ||
             selector.Contains(":checked", StringComparison.Ordinal)) &&
            !selector.Contains(" Border.", StringComparison.Ordinal) &&
            !selector.Contains(" controls|", StringComparison.Ordinal) &&
            !selector.Contains(" TextBlock", StringComparison.Ordinal) &&
            !selector.Contains(" SelectableTextBlock", StringComparison.Ordinal);
    }

    private static List<(string Selector, string Body)> ExtractStyleBlocks(string stylesText)
    {
        var pattern = new Regex(
            @"<Style\s+Selector=""(?<selector>[^""]+)""\s*>(?<body>[\s\S]*?)</Style>",
            RegexOptions.Compiled);
        return pattern.Matches(stylesText)
            .Cast<Match>()
            .Select(static match => (match.Groups["selector"].Value, match.Groups["body"].Value))
            .ToList();
    }

    private static List<string> ExtractSetterProperties(string styleBody)
    {
        var pattern = new Regex(
            @"<Setter\s+Property=""(?<property>[^""]+)""",
            RegexOptions.Compiled);
        return pattern.Matches(styleBody)
            .Cast<Match>()
            .Select(static match => match.Groups["property"].Value)
            .ToList();
    }

    private static string? GetSetterValue(string body, string propertyName)
    {
        var match = Regex.Match(
            body,
            $@"<Setter\s+Property=""{Regex.Escape(propertyName)}""\s+Value=""(?<value>[^""]+)""",
            RegexOptions.Compiled);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? GetDynamicResourceSetterValue(string body, string propertyName)
    {
        var setterValue = GetSetterValue(body, propertyName);
        if (setterValue is null)
        {
            return null;
        }

        var match = Regex.Match(
            setterValue,
            @"^\{DynamicResource (?<key>[^}]+)\}$",
            RegexOptions.Compiled);
        return match.Success ? match.Groups["key"].Value : null;
    }

    private static void AssertMinimumContrast(string tokens, string backgroundKey, string foregroundKey, double minimumRatio)
    {
        var colorsByTheme = ReadThemeColors(tokens);
        foreach (var theme in new[] { "Dark", "Light" })
        {
            Assert.True(colorsByTheme.TryGetValue(theme, out var colors), $"Theme not found: {theme}");
            Assert.True(colors.TryGetValue(backgroundKey, out var background), $"{theme} token missing: {backgroundKey}");
            Assert.True(colors.TryGetValue(foregroundKey, out var foreground), $"{theme} token missing: {foregroundKey}");

            var ratio = ContrastRatio(background, foreground);
            Assert.True(
                ratio >= minimumRatio,
                $"{theme} contrast {backgroundKey}/{foregroundKey} is {ratio:0.00}, below {minimumRatio:0.0}.");
        }
    }

    private static Dictionary<string, Dictionary<string, string>> ReadThemeColors(string tokens)
    {
        var themePattern = new Regex(
            @"<ResourceDictionary\s+x:Key=""(?<theme>Dark|Light)"">(?<body>[\s\S]*?)</ResourceDictionary>",
            RegexOptions.Compiled);
        var colorPattern = new Regex(
            @"<Color\s+x:Key=""(?<key>[^""]+)"">(?<value>#[0-9A-Fa-f]{6,8})</Color>",
            RegexOptions.Compiled);
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (Match themeMatch in themePattern.Matches(tokens))
        {
            var colors = colorPattern.Matches(themeMatch.Groups["body"].Value)
                .Cast<Match>()
                .ToDictionary(
                    static match => match.Groups["key"].Value,
                    static match => match.Groups["value"].Value,
                    StringComparer.Ordinal);
            result[themeMatch.Groups["theme"].Value] = colors;
        }

        return result;
    }

    private static Dictionary<string, string> ReadBrushColorKeys(string tokens)
    {
        var brushPattern = new Regex(
            @"<SolidColorBrush\s+x:Key=""(?<brush>[^""]+)""\s+Color=""\{DynamicResource (?<color>[^}]+)\}""/>",
            RegexOptions.Compiled);
        return brushPattern.Matches(tokens)
            .Cast<Match>()
            .ToDictionary(
                static match => match.Groups["brush"].Value,
                static match => match.Groups["color"].Value,
                StringComparer.Ordinal);
    }

    private static double ContrastRatio(string first, string second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        var rgb = NormalizeRgbHex(hex);
        var red = SrgbToLinear(Convert.ToInt32(rgb[..2], 16) / 255d);
        var green = SrgbToLinear(Convert.ToInt32(rgb[2..4], 16) / 255d);
        var blue = SrgbToLinear(Convert.ToInt32(rgb[4..6], 16) / 255d);
        return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
    }

    private static string NormalizeRgbHex(string hex)
    {
        var value = hex.TrimStart('#');
        return value.Length == 8 ? value[2..] : value;
    }

    private static double SrgbToLinear(double channel)
    {
        return channel <= 0.03928
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
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

using System.Text.RegularExpressions;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class UiLayoutGuardTests
{
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
    }

    [Fact]
    public void LeftDxfPanel_KeepsLayoutContract()
    {
        var repoRoot = TestPaths.RepoRoot;

        var leftPanel = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "LeftDxfPanel.axaml"));
        Assert.Contains("Classes=\"boundLayerSelector\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("MaxDropDownHeight=\"{StaticResource LayerListMaxHeight}\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("controls:BalancedWrapPanel Classes=\"badgeGroup dxfEditBadgeGroup\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dxfLayerBulkToggleButton\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding Layers.IsCadLayerBulkToggleChecked, Mode=TwoWay}\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dxfLayerBulkToggleContent\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dxfLayerBulkMixedIndicator\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding Layers.AreSomeCadLayersSelected}\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dxfLayerBulkToggleLabel\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Layers.CadLayerBulkToggleText}\"", leftPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("dxfLayerBulkToggleIcon", leftPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("dxfLayerBulkToggleSwitch", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dangerTextButton dxfEditMiniAction\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Content=\"Rotate\"", leftPanel, StringComparison.Ordinal);
        Assert.Contains("Content=\"Move\"", leftPanel, StringComparison.Ordinal);
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
    }

    [Fact]
    public void CanvasOverlayControls_KeepLayoutContract()
    {
        var repoRoot = TestPaths.RepoRoot;

        var overlayControls = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "CanvasOverlayControls.axaml"));
        Assert.Contains("Content=\"To Regular Preview\"", overlayControls, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding ShowNotchToRegularLabels, Mode=TwoWay}\"", overlayControls, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindow_KeepsLayoutContract()
    {
        var repoRoot = TestPaths.RepoRoot;

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
    }

    [Fact]
    public void SettingsSections_KeepLayoutContract()
    {
        var repoRoot = TestPaths.RepoRoot;

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
    }

    [Fact]
    public void WorkspaceHeaderAndRightPanel_KeepLayoutContract()
    {
        var repoRoot = TestPaths.RepoRoot;

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
        Assert.Equal(2, Regex.Count(workspaceHeader, "Classes=\"workspaceHeaderInput\""));
        Assert.DoesNotContain("<Expander", workspaceHeader, StringComparison.OrdinalIgnoreCase);

        var rightPanel = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "RightWorkflowPanel.axaml"));
        Assert.Contains("<views:RightWorkflowInspectorView/>", rightPanel, StringComparison.Ordinal);
        Assert.Contains("<views:RightWorkflowSettingsTabView", rightPanel, StringComparison.Ordinal);
        Assert.Contains("DataTemplate x:Key=\"NotchValidationRowTemplate\"", rightPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("<Expander", rightPanel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActionButtonStyles_KeepLayoutContract()
    {
        var repoRoot = TestPaths.RepoRoot;

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
    }

    [Fact]
    public void DxfEditPanelGroups_KeepLayoutContract()
    {
        var repoRoot = TestPaths.RepoRoot;

        var leftPanel = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "LeftDxfPanel.axaml"));

        var dxfResetButton = FindXamlElementTagContaining(leftPanel, "Button", "ResetAllDxfEditsButton_Click");
        var dxfRestoreLastButton = FindXamlElementTagContaining(leftPanel, "Button", "RestoreLastDeletedCadPadsCommand");
        var dxfRestoreAllButton = FindXamlElementTagContaining(leftPanel, "Button", "RestoreAllHiddenCadPadsCommand");
        Assert.Contains("actionDanger", dxfResetButton, StringComparison.Ordinal);
        Assert.Contains("IconGlyphs.Undo", dxfRestoreLastButton, StringComparison.Ordinal);
        Assert.Contains("IconGlyphs.Visibility", dxfRestoreAllButton, StringComparison.Ordinal);

        var dxfHideSectionStart = leftPanel.IndexOf("Text=\"Hide / restore\"", StringComparison.Ordinal);
        var dxfCombineSectionStart = leftPanel.IndexOf("Text=\"Combine / split\"", StringComparison.Ordinal);
        var dxfAdvancedSectionStart = leftPanel.IndexOf("Title=\"Advanced edit\"", dxfCombineSectionStart, StringComparison.Ordinal);
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
    }

    [Fact]
    public void CoreStyles_KeepLayoutContract()
    {
        var repoRoot = TestPaths.RepoRoot;

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
    }

    [Fact]
    public void DevView_KeepsLayoutContract()
    {
        var repoRoot = TestPaths.RepoRoot;

        var devView = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "DevView.axaml"));
        Assert.Contains("Classes=\"dangerTextButton dxfEditMiniAction\"", devView, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dxfLayerBulkToggleButton\"", devView, StringComparison.Ordinal);
        Assert.Contains("Classes=\"dxfLayerBulkMixedIndicator\"", devView, StringComparison.Ordinal);
        Assert.Contains("Classes=\"workspaceActionButton\"", devView, StringComparison.Ordinal);
        Assert.DoesNotContain("actionDanger actionTextButton dxfEditMiniAction", devView, StringComparison.Ordinal);
        Assert.DoesNotContain("actionGhost actionTextButton dxfEditMiniAction", devView, StringComparison.Ordinal);
        Assert.DoesNotContain("actionNeutral actionTextButton dxfEditMiniAction", devView, StringComparison.Ordinal);
        Assert.DoesNotContain("actionNeutral actionTextButton workspaceActionButton", devView, StringComparison.Ordinal);
    }

    [Fact]
    public void SimulationWorkspace_KeepsLayoutContract()
    {
        var repoRoot = TestPaths.RepoRoot;

        var simulationControlsPane = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "WorkspaceSections", "SimulationWorkspaceControlsPaneView.axaml"));
        Assert.Contains("Classes=\"workspaceFileRow\"", simulationControlsPane, StringComparison.Ordinal);
        Assert.Contains("Classes=\"workspaceFileRowTitle\"", simulationControlsPane, StringComparison.Ordinal);
        Assert.Contains("Glyph=\"{x:Static icons:IconGlyphs.DataObject}\"", simulationControlsPane, StringComparison.Ordinal);

        var simulationWorkspace = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "SimulationWorkspaceView.axaml"));
        Assert.Matches(@"GridSplitter\s+Grid\.Row=""2""[\s\S]*ResizeDirection=""Rows""[\s\S]*ShowsPreview=""True""", simulationWorkspace);
    }

    [Fact]
    public void RightWorkflowSections_KeepLayoutContract()
    {
        var repoRoot = TestPaths.RepoRoot;

        var rightWorkflowSettings = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "RightWorkflowSections", "RightWorkflowSettingsTabView.axaml"));
        Assert.Contains("Title=\"Overview\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("Text=\"Project / grid\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("Text=\"Simulation safety\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"Loaded CAD pad count after DXF import and current DXF edits.\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"Generated regular-grid pad count.\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding SimulationSafetyOverviewSummaryText}\"", rightWorkflowSettings, StringComparison.Ordinal);
        Assert.Contains("Tag=\"General\"", rightWorkflowSettings, StringComparison.Ordinal);
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
    }

    [Fact]
    public void WorkflowStepViews_KeepLayoutContract()
    {
        var repoRoot = TestPaths.RepoRoot;

        var settingsStep3Section = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "SettingsSections", "SettingsStep3SectionView.axaml"));

        var step3View = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "WorkflowSteps", "RightWorkflowStep3View.axaml"));
        Assert.Contains("Text=\"{Binding NotchPreviewStageSummary}\"", step3View, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding NotchToFullRuleEngineSummary}\"", step3View, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{Binding CanControlNotchPreviewStage}\"", step3View, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding NotchTargetCoverageCapHelpText}\"", step3View, StringComparison.Ordinal);
        Assert.DoesNotContain("Tip=\"{Binding NotchTargetCoverageCapHelpText}\"", settingsStep3Section, StringComparison.Ordinal);
        Assert.DoesNotContain("120% maps uniform 400 to EMS cap 480", step3View, StringComparison.Ordinal);
        Assert.DoesNotContain("120% maps uniform 400 to EMS cap 480", settingsStep3Section, StringComparison.Ordinal);

        var step5View = TestFiles.ReadContent(Path.Combine(repoRoot, "src", "FreeformHelper.UI", "Views", "WorkflowSteps", "RightWorkflowStep5View.axaml"));
        Assert.Contains("Text=\"Handoff checklist\"", step5View, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding NotchExportHandoffFileText}\"", step5View, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding NotchExportHandoffVersionText}\"", step5View, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding NotchExportHandoffProfileText}\"", step5View, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding NotchExportHandoffChecklistText}\"", step5View, StringComparison.Ordinal);
    }
}

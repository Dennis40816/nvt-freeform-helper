using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia.Media;
using Avalonia.Styling;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class UiLayoutGuardTests
{
    private static readonly string[] ThemeVariantKeys = ["Dark", "Light"];

    private static readonly string[] AccentOverrideKeys =
    [
        "NfcAccentBorderBrush", "NfcAccentBorderLightBrush", "NfcAccentBorderStrongBrush", "NfcAccentBrush",
        "NfcAccentStrongBrush", "NfcAccentSurfaceBrush", "NfcAccentSurfaceSubtleBrush",
    ];

    [Theory]
    [InlineData("White", "#FFFFFF")]
    [InlineData("Black", "#FF000000")]
    [InlineData("#FF1E293B", "#1E293B")]
    public void ContrastLuminanceMeasuresNamedAndArgbColors(string value, string rgb)
    {
        Assert.Equal(RelativeLuminance(rgb), RelativeLuminance(value));
    }

    [Fact]
    public void CoreButtonRolesHaveOneApplicationIncludeAndSevenAccentOverrides()
    {
        var uiRoot = Path.Combine(TestPaths.RepoRoot, "src", "FreeformHelper.UI");
        var app = XDocument.Parse(TestFiles.ReadContent(Path.Combine(uiRoot, "App.axaml")));
        var styles = app.Descendants().Single(static element => element.Name.LocalName == "Application.Styles").Elements().ToArray();
        Assert.Equal("FluentTheme", styles[0].Name.LocalName);
        Assert.Equal("avares://Nvt.Core.Avalonia/Theme/ButtonStyles.axaml", (string?)styles[1].Attribute("Source"));
        var includes = Directory.EnumerateFiles(uiRoot, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(static path => XDocument.Parse(TestFiles.ReadContent(path)).Descendants())
            .Where(static element => element.Name.LocalName == "StyleInclude" &&
                (string?)element.Attribute("Source") == "avares://Nvt.Core.Avalonia/Theme/ButtonStyles.axaml");
        Assert.Single(includes);
        var tokens = XDocument.Parse(TestFiles.ReadContent(Path.Combine(uiRoot, "Styles", "Tokens.axaml")));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var themes = tokens.Descendants().Where(element => (string?)element.Attribute(xaml + "Key") is "Dark" or "Light").ToArray();
        Assert.Equal(ThemeVariantKeys, themes.Select(element => (string?)element.Attribute(xaml + "Key")).Order(StringComparer.Ordinal));
        foreach (var theme in themes)
        {
            var accents = theme.Elements().Select(element => (string?)element.Attribute(xaml + "Key"))
                .Where(static key => key?.StartsWith("NfcAccent", StringComparison.Ordinal) == true).ToArray();
            Assert.Equal(AccentOverrideKeys, accents.Order(StringComparer.Ordinal));
        }
    }

    private static void AssertCoreRoleAppearanceIsOwnedByCore(string repoRoot)
    {
        var uiRoot = Path.Combine(repoRoot, "src", "FreeformHelper.UI");
        var appearance = new HashSet<string>(StringComparer.Ordinal)
        {
            "Theme", "Background", "BorderBrush", "BorderThickness", "Foreground", "Template", "CornerRadius", "Width", "Height",
            "MinHeight", "Padding", "FontFamily", "FontSize", "FontWeight", "Opacity", "FocusAdorner", "Transitions", "ClipToBounds",
            "HorizontalContentAlignment", "VerticalContentAlignment",
        };
        var role = new Regex(@"(?:Button|ToggleButton)\.(?:actionPrimary|actionNeutral|actionDanger|actionGhost|actionIconButton|chipAction|consoleHeaderAction|panelChromeToggle|simulationActionButton|workspaceActionButton|workspaceGroupActionButton|workspaceHeaderAction|workspaceInfoButton|workspaceSearchClearButton|workspaceSubtleAction|workspaceTextAction|padInfoActionButton|padInfoHeaderActionButton|padInfoInfoButton|padInfoOverrideIconButton|dxfEditMiniAction|dxfEditCompactIconAction|viewMenuButton|iconTextButton|dangerTextButton|confirmDialogActionButton|confirmDialogDangerButton)\b|Border\.chipStatus\b");
        foreach (var path in Directory.EnumerateFiles(uiRoot, "*.axaml", SearchOption.AllDirectories))
        {
            var document = XDocument.Parse(TestFiles.ReadContent(path));
            foreach (var style in document.Descendants().Where(static element => element.Name.LocalName == "Style"))
            {
                var originalSelector = (string?)style.Attribute("Selector") ?? string.Empty;
                var selector = Regex.Replace(originalSelector, @":not\([^)]*\)", string.Empty);
                Assert.DoesNotMatch(@"\.(actionTextButton|actionChip|actionButton)\b", selector);
                var publicRoles = new[] { "actionPrimary", "actionNeutral", "actionDanger", "actionGhost", "actionIconButton", "chipAction" };
                var broadRoleStyle = originalSelector.Split(',').Any(part =>
                    Regex.IsMatch(Regex.Replace(part, @":not\([^)]*\)", string.Empty), @"(?:^|[ >])(Button|ToggleButton)(?=:|$)") &&
                    publicRoles.Any(name => !part.Contains($":not(.{name})", StringComparison.Ordinal)));
                // A glyph inside a role button is its own control; Core sets no icon size.
                var controlSelector = string.Join(',', selector.Split(',')
                    .Where(static part => !Regex.IsMatch(part, @"controls\|FontIcon[.\w]*\s*$")));
                if (!role.IsMatch(controlSelector) && !broadRoleStyle) continue;
                Assert.DoesNotContain(style.Elements().Where(static element => element.Name.LocalName == "Setter"),
                    setter => appearance.Contains((string?)setter.Attribute("Property") ?? string.Empty));
            }
            foreach (var element in document.Descendants())
            {
                var classes = ((string?)element.Attribute("Classes") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                Assert.DoesNotContain(classes, static token => token is "actionTextButton" or "actionChip" or "actionButton");
                Assert.DoesNotContain(element.Attributes(), static attribute =>
                    attribute.Name.LocalName is "Classes.actionTextButton" or "Classes.actionChip" or "Classes.actionButton");
                if (classes.Any(static token => token is "viewportOverlayAction" or "canvasQuickToggle" or "canvasToolbarIcon" or
                    "productChipAction" or "productFilterChip" or "shellTab" or "rightPanelTab" or "rightPanelBrowserTab" or
                    "settingsNavItem" or "panelBlockHeader" or "workspaceFoldHeader" or "workspaceTableHeaderButton"))
                {
                    Assert.DoesNotContain(classes, static token => token is "actionPrimary" or "actionNeutral" or "actionDanger" or
                        "actionGhost" or "actionIconButton" or "chipAction");
                }
                var key = element.Attributes().SingleOrDefault(static attribute => attribute.Name.LocalName == "Key")?.Value;
                Assert.False(key?.StartsWith("Nvt.Button.", StringComparison.Ordinal) == true ||
                    key?.StartsWith("Nvt.Focus.", StringComparison.Ordinal) == true, $"Core resource override in {path}: {key}");
            }
        }
    }

    private static void AssertInteractiveActionStateContrast(string actionStyles, string tokens, double minimumRatio)
    {
        var localColorKeys = ReadBrushColorKeys(tokens);
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

            foreach (var theme in new[] { "Dark", "Light" })
            {
                var background = ReadThemeColor(localColorKeys.GetValueOrDefault(backgroundBrush, backgroundBrush), theme);
                var foreground = ReadThemeColor(localColorKeys.GetValueOrDefault(foregroundBrush, foregroundBrush), theme);

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
                if (!string.Equals(foreground, "{DynamicResource NfcTextDisabledBrush}", StringComparison.Ordinal))
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

    private static void AssertMinimumContrast(string backgroundKey, string foregroundKey, double minimumRatio)
    {
        foreach (var theme in new[] { "Dark", "Light" })
        {
            var background = ReadThemeColor(backgroundKey, theme);
            var foreground = ReadThemeColor(foregroundKey, theme);

            var ratio = ContrastRatio(background, foreground);
            Assert.True(
                ratio >= minimumRatio,
                $"{theme} contrast {backgroundKey}/{foregroundKey} is {ratio:0.00}, below {minimumRatio:0.0}.");
        }
    }

    private static string ReadThemeColor(string key, string theme)
    {
        var app = global::Avalonia.Application.Current!;
        var variant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        Assert.True(app.TryGetResource(key, variant, out var resource), $"{theme} token missing: {key}");
        return resource switch
        {
            Color color => color.ToString(),
            ISolidColorBrush brush => brush.Color.ToString(),
            _ => throw new InvalidOperationException($"{theme} token is not a solid color or brush: {key}"),
        };
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

    private static double RelativeLuminance(string value)
    {
        var color = Color.Parse(value);
        var red = SrgbToLinear(color.R / 255d);
        var green = SrgbToLinear(color.G / 255d);
        var blue = SrgbToLinear(color.B / 255d);
        return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
    }

    private static double SrgbToLinear(double channel)
    {
        return channel <= 0.03928
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }
}

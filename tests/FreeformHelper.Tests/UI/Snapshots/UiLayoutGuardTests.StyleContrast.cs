using System.Text.RegularExpressions;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class UiLayoutGuardTests
{
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
}

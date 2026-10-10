// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Icons;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class FontAdoptionTests(ITestOutputHelper output)
{
    [AvaloniaTheory]
    [InlineData("FontFamilyUi", "Nvt.Font.Body.Family", "Inter")]
    [InlineData("FontFamilyCode", "Nvt.Font.Mono.Family", "Cascadia Mono")]
    public void FontTokensResolveToEmbeddedCoreFamilies(string token, string role, string expectedFamily)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var family = Resource<FontFamily>(token);
        Assert.Equal(Resource<FontFamily>(role), family);
        Assert.Single(family.FamilyNames);
        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var typeface));
        Assert.Equal(expectedFamily, typeface.FamilyName);
        Assert.True(typeface.PlatformTypeface.TryGetStream(out var stream));
        stream.Dispose();
    }

    [AvaloniaFact]
    public void ChineseTextUsesCoreNotoSansTcFallback()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var text = new TextBlock
        {
            Text = "A中文測試Z",
            FontFamily = Resource<FontFamily>("FontFamilyUi"),
            FontSize = Resource<double>("FontSizeMd"),
        };
        text.Measure(Size.Infinity);
        var chineseRuns = text.TextLayout.TextLines.SelectMany(static line => line.TextRuns)
            .OfType<ShapedTextRun>()
            .Where(static run => run.Text.Span.Contains('中')).ToArray();
        Assert.NotEmpty(chineseRuns);
        Assert.All(chineseRuns, static run =>
        {
            Assert.Equal("Noto Sans TC", run.GlyphRun.GlyphTypeface.FamilyName);
            Assert.All(run.GlyphRun.GlyphInfos, static glyph => Assert.NotEqual((ushort)0, glyph.GlyphIndex));
        });
    }

    [AvaloniaFact]
    public void EveryNfhIconExistsInCoreMaterialSymbolsFont()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var family = Resource<FontFamily>("Nvt.Font.Icon.Family");
        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var typeface));
        Assert.Equal("Material Symbols Outlined", typeface.FamilyName);
        var icons = ReadNfhIcons();
        Assert.NotEmpty(icons);
        foreach (var icon in icons)
        {
            if (icon.EnumerateRunes().All(static rune => rune.Value is >= 0xE000 and <= 0xF8FF))
            {
                foreach (var rune in icon.EnumerateRunes())
                {
                    Assert.True(typeface.CharacterToGlyphMap.TryGetGlyph(rune.Value, out var glyph),
                        $"Missing icon code point U+{rune.Value:X4}");
                    Assert.NotEqual((ushort)0, glyph);
                }
            }
            else
            {
                // Shape the actual NFH spelling, including uppercase ligature constants.
                var text = new TextBlock
                {
                    Text = icon,
                    FontFamily = family,
                    FontSize = Resource<double>("IconSizeMd"),
                };
                text.Measure(Size.Infinity);
                var run = Assert.Single(Assert.Single(text.TextLayout.TextLines).TextRuns.OfType<ShapedTextRun>());
                Assert.Equal("Material Symbols Outlined", run.GlyphRun.GlyphTypeface.FamilyName);
                var glyph = Assert.Single(run.GlyphRun.GlyphInfos);
                Assert.True(glyph.GlyphIndex != 0, $"Missing icon ligature: {icon}");
            }
        }

        output.WriteLine($"Checked {icons.Count} distinct NFH icon names/code points: {string.Join(", ", icons)}");
    }

    [Fact]
    public void DistributionIncludesRestoredCoreAndFontLicenses()
    {
        // Set this to a publish directory to check the exact distribution with the same guard.
        var distribution = Environment.GetEnvironmentVariable("NFH_FONTS_DISTRIBUTION_PATH") ?? AppContext.BaseDirectory;
        using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            TestPaths.RepoRoot, "build", "obj", "FreeformHelper.UI", "project.assets.json")));
        var package = assets.RootElement.GetProperty("packageFolders").EnumerateObject()
            .Select(static folder => Path.Combine(folder.Name, "nvt.core.fonts", "0.1.0"))
            .First(static path => File.Exists(Path.Combine(path, "LICENSE")));
        AssertLicenseCopy(package, "LICENSE", distribution, Path.Combine("licenses", "Nvt.Core", "LICENSE"));
        var fontLicenses = Directory.GetFiles(Path.Combine(package, "licenses"), "*", SearchOption.AllDirectories);
        Assert.NotEmpty(fontLicenses);
        foreach (var license in fontLicenses)
        {
            var relative = Path.GetRelativePath(package, license);
            AssertLicenseCopy(package, relative, distribution, relative);
        }
    }

    private const string InterLicenseSha256 = "4d7d9c95e7d7f2f0ebf76d5e0b344826b74e903a34028ee18ab54bb639e45906";

    [Fact]
    public void DistributionIncludesTheUpstreamInterLicenseUnmodified()
    {
        var distribution = Environment.GetEnvironmentVariable("NFH_FONTS_DISTRIBUTION_PATH") ?? AppContext.BaseDirectory;
        var target = Path.Combine(distribution, "licenses", "Inter", "LICENSE");
        Assert.True(File.Exists(target), $"Distribution is missing licenses/Inter/LICENSE: {distribution}");
        Assert.Equal(InterLicenseSha256, HashLicenseText(File.ReadAllText(target)));
    }

    [Fact]
    public void InterLicenseHash_WhenTextIsModified_DiffersFromThePinnedHash()
    {
        var text = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "src", "FreeformHelper.UI", "licenses", "Inter", "LICENSE"));
        Assert.Equal(InterLicenseSha256, HashLicenseText(text));
        Assert.NotEqual(InterLicenseSha256, HashLicenseText(text + " "));
    }

    // The pinned hash is of the LF text; a checkout may convert line endings.
    private static string HashLicenseText(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n"))));

    private static void AssertLicenseCopy(string package, string source, string distribution, string relativeOutput)
    {
        var target = Path.Combine(distribution, relativeOutput);
        Assert.True(File.Exists(target), $"Distribution is missing {relativeOutput}: {distribution}");
        Assert.Equal(SHA256.HashData(File.ReadAllBytes(Path.Combine(package, source))),
            SHA256.HashData(File.ReadAllBytes(target)));
    }

    private static SortedSet<string> ReadNfhIcons()
    {
        var icons = new SortedSet<string>(StringComparer.Ordinal);
        var constants = typeof(IconGlyphs).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
            .ToDictionary(static field => field.Name, static field => (string)field.GetRawConstantValue()!, StringComparer.Ordinal);
        icons.UnionWith(constants.Values);
        var uiRoot = Path.Combine(TestPaths.RepoRoot, "src", "FreeformHelper.UI");
        foreach (var file in Directory.EnumerateFiles(uiRoot, "*.axaml", SearchOption.AllDirectories))
        {
            var document = XDocument.Load(file);
            foreach (var attribute in document.Descendants().Where(static element => element.Name.LocalName == "FontIcon")
                         .SelectMany(static element => element.Attributes())
                         .Where(static attribute => attribute.Name.LocalName is "Glyph" or "Text"))
            {
                if (attribute.Value.StartsWith("{x:Static icons:IconGlyphs.", StringComparison.Ordinal))
                {
                    var name = attribute.Value["{x:Static icons:IconGlyphs.".Length..].TrimEnd('}');
                    icons.Add(constants[name]);
                }
                else if (!attribute.Value.StartsWith('{') && !string.IsNullOrWhiteSpace(attribute.Value))
                {
                    icons.Add(attribute.Value);
                }
            }
        }

        foreach (var file in Directory.EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match icon in Regex.Matches(File.ReadAllText(file), @"new\s+FontIcon\s*\{(?<body>[\s\S]*?)\}"))
            {
                foreach (Match literal in Regex.Matches(icon.Groups["body"].Value, @"\b(?:Glyph|Text)\s*=\s*""(?<text>(?:\\.|[^""\\])*)"""))
                {
                    icons.Add(Regex.Unescape(literal.Groups["text"].Value));
                }
            }
        }

        return icons;
    }

    private static T Resource<T>(string key)
    {
        Assert.True(Avalonia.Application.Current!.Resources.TryGetResource(key, null, out var resource), $"Missing resource: {key}");
        return Assert.IsType<T>(resource);
    }
}

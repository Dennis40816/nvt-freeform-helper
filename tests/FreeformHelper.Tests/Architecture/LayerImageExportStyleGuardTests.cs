// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Xml.Linq;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests.Architecture;

public sealed class LayerImageExportStyleGuardTests
{
    private const string LiteralPattern = "#[0-9A-Fa-f]{3,8}\\b|FontFamily\\s*=\\s*[\"'][^{\"']";

    [Theory]
    [InlineData("LayerImageExportWindow.axaml")]
    [InlineData("LayerImageExportSettingsDialog.axaml")]
    public void ExportViewsUseResourceColorsAndFontFamilies(string fileName)
    {
        string path = Path.Combine(TestPaths.RepoRoot, "src", "FreeformHelper.UI", "Views", fileName);

        Assert.DoesNotMatch(LiteralPattern, TestFiles.ReadContent(path));
    }

    [Fact]
    public void ExportWindowDoesNotUseLiteralIconNames()
    {
        string path = Path.Combine(TestPaths.RepoRoot, "src", "FreeformHelper.UI", "Views", "LayerImageExportWindow.axaml");
        var elements = XDocument.Parse(TestFiles.ReadContent(path)).Descendants().ToArray();
        var icons = elements
            .Where(static element => element.Name.LocalName is "FontIcon" or "BooleanGlyphConverter")
            .SelectMany(static element => element.Attributes())
            .Where(static attribute => attribute.Name.LocalName is "Glyph" or "TrueGlyph" or "FalseGlyph")
            .ToArray();
        // The property element form (<FontIcon.Glyph>link</FontIcon.Glyph>) must not carry a literal either.
        var elementText = elements
            .Where(static element => element.Name.LocalName.EndsWith("Glyph", StringComparison.Ordinal))
            .SelectMany(static element => element.DescendantNodes().OfType<XText>())
            .Select(static text => text.Value.Trim());

        Assert.NotEmpty(icons);
        Assert.All(icons, static icon => Assert.StartsWith("{", icon.Value, StringComparison.Ordinal));
        Assert.All(elementText, static text => Assert.Empty(text));
    }

    [Theory]
    [InlineData("<Border Background=\"#123456\"/>")]
    [InlineData("<TextBlock FontFamily=\"Arial\"/>")]
    public void LiteralGuardRejectsForbiddenValues(string markup)
    {
        Assert.Matches(LiteralPattern, markup);
    }
}

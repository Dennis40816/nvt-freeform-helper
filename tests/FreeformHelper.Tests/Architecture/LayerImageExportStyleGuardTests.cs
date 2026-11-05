// Copyright (c) 2026 Dennis Liu. All rights reserved.

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

    [Theory]
    [InlineData("<Border Background=\"#123456\"/>")]
    [InlineData("<TextBlock FontFamily=\"Arial\"/>")]
    public void LiteralGuardRejectsForbiddenValues(string markup)
    {
        Assert.Matches(LiteralPattern, markup);
    }
}

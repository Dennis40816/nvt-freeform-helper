// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Xml;
using System.Xml.Linq;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests.Architecture;

public sealed class AccessibilityNameGuardTests
{
    private const string NameAttribute = "AutomationProperties.Name";

    [Fact]
    public void IconOnlyButtons_InViews_DeclareAutomationName()
    {
        var errors = new List<string>();
        foreach (var source in ArchitectureSource.Read(TestPaths.RepoRoot, ".axaml"))
        {
            var root = XDocument.Parse(source.Text, LoadOptions.SetLineInfo).Root!;
            foreach (var element in root.DescendantsAndSelf().Where(IsIconOnlyButton))
            {
                if (string.IsNullOrWhiteSpace((string?)element.Attribute(NameAttribute)))
                {
                    errors.Add($"{source.Path}:{((IXmlLineInfo)element).LineNumber}: icon-only {element.Name.LocalName} needs {NameAttribute}.");
                }
            }
        }

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    private static bool IsIconOnlyButton(XElement element) =>
        element.Name.LocalName is "Button" or "ToggleButton"
        && ((string?)element.Attribute("Classes") ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains("actionIconButton", StringComparer.Ordinal)
        && !element.Ancestors().Any(static ancestor => ancestor.Name.LocalName is "Style" or "ControlTemplate");
}

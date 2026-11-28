// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Xml.Linq;
using FreeformHelper.Tests.TestInfrastructure;

namespace FreeformHelper.Tests.UI.Bindings;

internal sealed class BindingDocuments
{
    public static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private readonly IReadOnlyDictionary<string, XElement> _roots;

    public BindingDocuments(IReadOnlyDictionary<string, string> documents)
    {
        _roots = documents.ToDictionary(static pair => pair.Key,
            static pair => XElement.Parse(pair.Value, LoadOptions.SetLineInfo), StringComparer.Ordinal);
        Sites = _roots.SelectMany(static pair => Discover(pair.Key, pair.Value)).ToArray();
    }

    public IReadOnlyList<BindingSite> Sites { get; }
    public IEnumerable<XElement> Roots => _roots.Values;
    public XElement Root(string file) => _roots[file];
    public string FileOf(XElement element) => _roots.First(pair => ReferenceEquals(pair.Value, element.AncestorsAndSelf().Last())).Key;

    public XElement? ForClass(Type type) => _roots.Values.FirstOrDefault(root =>
        string.Equals((string?)root.Attribute(Xaml + "Class"), type.FullName, StringComparison.Ordinal));

    public IEnumerable<XElement> Resources(XElement owner)
    {
        var pending = new Queue<XElement>(owner.Elements().Where(static child =>
            child.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal) ||
            child.Name.LocalName.EndsWith(".Styles", StringComparison.Ordinal)));
        var visited = new HashSet<XElement>();
        while (pending.TryDequeue(out var root))
        {
            if (!visited.Add(root))
            {
                continue;
            }
            foreach (var element in root.DescendantsAndSelf())
            {
                if (element.Attribute(Xaml + "Key") is not null)
                {
                    yield return element;
                }
                if (element.Name.LocalName is "ResourceInclude" or "StyleInclude" && element.Attribute("Source") is { } source)
                {
                    const string prefix = "avares://FreeformHelper.UI/";
                    if (source.Value.StartsWith("avares://", StringComparison.Ordinal) && !source.Value.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        // External assemblies are exercised by headless activation, not claimed as repository AXAML.
                        continue;
                    }
                    var path = source.Value.StartsWith(prefix, StringComparison.Ordinal) ? source.Value[prefix.Length..]
                        : Path.GetRelativePath(".", Path.Combine(Path.GetDirectoryName(FileOf(element)) ?? string.Empty, source.Value))
                            .Replace('\\', '/');
                    if (_roots.TryGetValue(path, out var included))
                    {
                        pending.Enqueue(included);
                    }
                }
            }
        }
    }

    public IEnumerable<XElement> ApplicationResources() => _roots.TryGetValue("App.axaml", out var app) ? Resources(app) : [];

    public static BindingDocuments ReadRepository()
    {
        var root = TestPaths.FromRepo("src", "FreeformHelper.UI");
        return new BindingDocuments(Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories)
            .Where(static path => !path.Split(Path.DirectorySeparatorChar).Any(static part => part is "bin" or "obj"))
            .ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), TestFiles.ReadContent, StringComparer.Ordinal));
    }

    public static bool IsProperty(XElement element) => element.Name.LocalName.Contains('.', StringComparison.Ordinal);
    public static bool IsTemplate(XElement element) => element.Name.LocalName is "DataTemplate" or "ControlTemplate" or "ItemsPanelTemplate";
    public static bool IsDefinition(XElement element) => IsTemplate(element) || element.Name.LocalName is "Style" or "ControlTheme" ||
        element.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal) || element.Name.LocalName == "ResourceDictionary";
    public static bool IsDesign(XElement element) => element.AncestorsAndSelf().Any(static ancestor =>
        ancestor.Name.LocalName.StartsWith("Design.", StringComparison.Ordinal) ||
        ancestor.Name.NamespaceName == "http://schemas.microsoft.com/expression/blend/2008");

    private static IEnumerable<BindingSite> Discover(string file, XElement root)
    {
        foreach (var element in root.DescendantsAndSelf().Where(static element => !IsDesign(element)))
        {
            if (IsBindingKind(element.Name.LocalName))
            {
                var property = element.Ancestors().FirstOrDefault(static ancestor => IsProperty(ancestor) &&
                    ancestor.Name.LocalName != "MultiBinding.Bindings");
                if (property?.Parent is { } owner)
                {
                    yield return new BindingSite(file, owner, element, property.Name.LocalName.Split('.')[^1],
                        element.ToString(SaveOptions.DisableFormatting));
                }
                else
                {
                    yield return new BindingSite(file, element, element, "unknown", element.ToString(SaveOptions.DisableFormatting));
                }
            }
            foreach (var attribute in element.Attributes().Where(static attribute => !attribute.IsNamespaceDeclaration))
            {
                var text = attribute.Value.TrimStart();
                var end = text.IndexOfAny([' ', ',', '}']);
                var extension = text[..(end < 0 ? text.Length : end)];
                if (text.StartsWith('{') && !text.StartsWith("{}", StringComparison.Ordinal) &&
                    (extension.Contains("Binding", StringComparison.Ordinal) || text.Contains("{Binding", StringComparison.Ordinal)))
                {
                    var target = element.Name.LocalName == "Setter" ? (string?)element.Attribute("Property") ?? "unknown"
                        : attribute.Name.LocalName;
                    yield return new BindingSite(file, element, attribute, target, text);
                }
            }
        }
    }

    private static bool IsBindingKind(string kind) => kind.EndsWith("Binding", StringComparison.Ordinal);
}

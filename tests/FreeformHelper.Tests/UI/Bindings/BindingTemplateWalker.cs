// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FreeformHelper.Tests.UI.Bindings;

internal sealed class BindingTemplateWalker(AxamlBindingWalker walker, BindingDocuments documents, BindingTypeResolver types)
{
    public void VisitFrame(BindingFrame frame)
    {
        try
        {
            VisitPropertyTemplate(frame, "ItemTemplate", "ItemsSource", true);
            VisitPropertyTemplate(frame, "ContentTemplate", "Content", false);
            VisitImplicitTemplates(frame);
            foreach (var template in frame.Element.Elements().Where(static element => element.Name.LocalName.EndsWith(".Template", StringComparison.Ordinal))
                         .SelectMany(static element => element.Elements()).Where(static element => element.Name.LocalName == "ControlTemplate"))
            {
                walker.Visit(template, frame, null, frame);
            }
        }
        catch (BindingWalkException error)
        {
            foreach (var site in documents.Sites.Where(site => site.Owner == frame.Element))
            {
                walker.RecordProblem(site, error, frame.Type.Name);
            }
        }
    }

    public void VisitStyles()
    {
        foreach (var style in documents.Roots.SelectMany(static root => root.DescendantsAndSelf())
                     .Where(static element => element.Name.LocalName is "Style" or "ControlTheme"))
        {
            if (!documents.Sites.Any(site => site.Owner.AncestorsAndSelf().Contains(style)))
            {
                continue;
            }
            try
            {
                var selector = (string?)style.Attribute("Selector");
                foreach (var branch in BindingMarkup.Split(selector ?? string.Empty, ','))
                {
                    VisitStyle(style, branch);
                }
            }
            catch (BindingWalkException error)
            {
                foreach (var site in documents.Sites.Where(site => site.Owner.AncestorsAndSelf().Contains(style)))
                {
                    walker.RecordProblem(site, error);
                }
            }
        }
    }

    private void VisitPropertyTemplate(BindingFrame frame, string target, string source, bool items)
    {
        var templates = frame.Element.Elements()
            .Where(element => element.Name.LocalName.EndsWith("." + target, StringComparison.Ordinal))
            .SelectMany(static element => element.Elements()).Where(static element => element.Name.LocalName == "DataTemplate").ToList();
        if (frame.Element.Attribute(target) is { } reference)
        {
            var markup = BindingMarkup.Parse(reference.Value);
            if (markup.Kind is "Binding" or "ReflectionBinding" or "CompiledBinding" or "TemplateBinding")
            {
                // The source path is checked separately. Definitions that have no proven consumer stay uncovered.
                return;
            }
            if (markup.Kind != "StaticResource")
            {
                throw BindingWalkException.Uncovered(reference.Value, "Template reference is not a static resource.");
            }
            templates.Add(walker.Resource(frame, markup.Get("Key") ?? string.Empty));
        }
        foreach (var template in templates)
        {
            var value = walker.Property(frame, source);
            var context = items ? new BindingValue(BindingTypeResolver.ItemType(value.Type)) : value;
            VisitDataTemplate(template, frame, context);
        }
    }

    private void VisitImplicitTemplates(BindingFrame frame)
    {
        var contentSite = documents.Sites.FirstOrDefault(site => site.Owner == frame.Element && site.Target == "Content");
        if (contentSite is null)
        {
            return;
        }
        var templates = frame.Ancestors().Prepend(frame).SelectMany(static ancestor => ancestor.Element.Elements()
                .Where(static element => element.Name.LocalName.EndsWith(".DataTemplates", StringComparison.Ordinal)))
            .SelectMany(static property => property.Elements()).Where(static element => element.Name.LocalName == "DataTemplate").ToArray();
        if (templates.Length == 0)
        {
            return;
        }
        var value = walker.Property(frame, "Content");
        var alternatives = MainWindowBindingCatalog.ContentAlternatives(value.Type, contentSite.Read().Path, walker.Context(frame).Type);
        foreach (var alternative in alternatives)
        {
            var matches = templates.Where(template => Declared(template) is { } declared && declared.IsAssignableFrom(alternative)).ToArray();
            if (matches.Length == 1)
            {
                VisitDataTemplate(matches[0], frame, new BindingValue(alternative));
            }
            else if (matches.Length > 1)
            {
                throw BindingWalkException.Uncovered(contentSite.Text, "Multiple applicable data templates need runtime selection.");
            }
        }
    }

    private void VisitDataTemplate(XElement template, BindingFrame host, BindingValue value)
    {
        var declared = Declared(template);
        if (declared is not null && !declared.IsAssignableFrom(value.Type))
        {
            throw BindingWalkException.Failed("DataTemplate", $"Declared {declared.Name} disagrees with item/content source {value.Type.Name}.");
        }
        walker.Visit(template, host, value);
    }

    private Type? Declared(XElement template) =>
        (template.Attribute("DataType") ?? template.Attribute(BindingDocuments.Xaml + "DataType")) is { } declaration
            ? types.Resolve(template, declaration.Value) : null;

    private void VisitStyle(XElement style, string selector)
    {
        var scope = new BindingScope(style, null, null);
        BindingFrame? target = null;
        if (style.Attribute("TargetType") is { } targetType)
        {
            target = new BindingFrame(style, types.Resolve(style, targetType.Value), scope, null);
        }
        else
        {
            foreach (var token in selector.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token is ">" or "/template/")
                {
                    continue;
                }
                var match = Regex.Match(token, @"^(?::is\((?<type>[\w|:]+)\)|(?<type>[\w|]+))", RegexOptions.CultureInvariant);
                if (!match.Success)
                {
                    throw BindingWalkException.Uncovered(selector, "Style selector does not establish a target type.");
                }
                target = new BindingFrame(style, types.Resolve(style, match.Groups["type"].Value.Replace('|', ':')), scope, target);
            }
        }
        if (target is null)
        {
            throw BindingWalkException.Uncovered(selector, "Styled target is unknown.");
        }
        foreach (var setter in style.Elements().Where(static element => element.Name.LocalName == "Setter"))
        {
            var frame = new BindingFrame(setter, target.Type, scope, target.Parent);
            foreach (var site in documents.Sites.Where(site => site.Owner == setter))
            {
                walker.CheckSite(site, frame);
            }
            foreach (var template in setter.Descendants().Where(static element => element.Name.LocalName == "ControlTemplate"))
            {
                walker.Visit(template, target, null, target);
            }
        }
    }
}

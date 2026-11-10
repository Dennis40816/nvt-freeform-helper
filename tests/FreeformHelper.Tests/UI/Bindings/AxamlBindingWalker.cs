// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using System.Windows.Input;
using System.Xml.Linq;
using Avalonia.Data;
using Avalonia.Data.Converters;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;

namespace FreeformHelper.Tests.UI.Bindings;

internal sealed class AxamlBindingWalker
{
    // All mutable traversal state belongs to this invocation, on the calling test thread.
    private readonly BindingDocuments _documents;
    private readonly BindingTypeResolver _types = new();
    private readonly BindingWalkReport _report = new();
    private readonly HashSet<(BindingFrame Frame, string Property)> _resolving = [];
    private readonly HashSet<(XElement Root, Type? Context)> _active = [];
    private readonly BindingPathVerifier _paths;
    private readonly BindingTemplateWalker _templates;

    public AxamlBindingWalker(BindingDocuments documents)
    {
        _documents = documents;
        _paths = new BindingPathVerifier(_types, Property);
        _templates = new BindingTemplateWalker(this, documents, _types);
    }

    public BindingWalkReport Walk(string file, Type context)
    {
        Visit(_documents.Root(file), null, new BindingValue(context));
        _report.AccountFor(_documents.Sites);
        return _report;
    }

    public BindingWalkReport WalkMainWindow()
    {
        Visit(_documents.Root("MainWindow.axaml"), null, new BindingValue(typeof(ShellViewModel)));
        foreach (var entry in MainWindowBindingCatalog.DetachedContexts)
        {
            Visit(_documents.Root(entry.File), null, new BindingValue(_types.Find(entry.Context)));
        }
        _templates.VisitStyles();
        MainWindowBindingCatalog.CheckCodePaths(_types, _paths, _report);
        _report.AccountFor(_documents.Sites);
        return _report;
    }

    internal void Visit(XElement root, BindingFrame? host, BindingValue? context, BindingFrame? templateParent = null)
    {
        var key = (root, context?.Type);
        if (!_active.Add(key))
        {
            throw BindingWalkException.Uncovered(root.Name.LocalName, "Recursive composition needs an explicit context boundary.");
        }
        try
        {
            var scope = new BindingScope(root, host, context, templateParent);
            BuildFrames(root, scope, BindingDocuments.IsTemplate(root) ? host : host?.Parent);
            foreach (var frame in scope.Frames.Values.ToArray())
            {
                CheckFrame(frame);
                _templates.VisitFrame(frame);
                VisitControl(frame);
            }
        }
        finally
        {
            _active.Remove(key);
        }
    }

    internal BindingValue Context(BindingFrame frame, bool inherited = false)
    {
        if (!inherited && BoundSite(frame, "DataContext") is { } site)
        {
            return Resolve(site.Read(), frame, true);
        }
        if (!inherited && frame.Element.Attribute("DataContext") is { } assignment)
        {
            return ExplicitSource(frame, assignment.Value);
        }
        var contextObject = frame.Element.Elements().FirstOrDefault(static element =>
            element.Name.LocalName.EndsWith(".DataContext", StringComparison.Ordinal))?.Elements().FirstOrDefault();
        if (!inherited && contextObject is not null)
        {
            return new BindingValue(_types.ElementType(contextObject));
        }
        if (ReferenceEquals(frame.Element, frame.Scope.Root) || frame.Parent is null ||
            !ReferenceEquals(frame.Parent.Scope, frame.Scope))
        {
            return frame.Scope.Context ?? (frame.Scope.Host is { } host ? Property(host, "DataContext")
                : throw BindingWalkException.Uncovered("DataContext", "No runtime context was registered."));
        }
        return Property(frame.Parent, "DataContext");
    }

    internal BindingValue Property(BindingFrame frame, string property)
    {
        if (!_resolving.Add((frame, property)))
        {
            throw BindingWalkException.Uncovered(property, "Cyclic context or control-property binding.");
        }
        try
        {
            if (property == "DataContext")
            {
                return Context(frame);
            }
            if (BoundSite(frame, property) is { } site)
            {
                return Resolve(site.Read(), frame);
            }
            if (ReferenceEquals(frame.Element, frame.Scope.Root) && frame.Scope.Host is { } host &&
                host.Type == frame.Type)
            {
                return Property(host, property);
            }
            return new BindingValue(BindingTypeResolver.Readable(frame.Type, property).PropertyType);
        }
        finally
        {
            _resolving.Remove((frame, property));
        }
    }

    internal BindingValue Resolve(BindingMarkup binding, BindingFrame frame, bool inherited = false, bool writable = false)
    {
        binding.RequireSupportedArguments();
        var path = binding.Path;
        var source = Source(binding, frame, ref path, inherited);
        return _paths.Follow(source, path, frame.Element, writable);
    }

    internal XElement Resource(BindingFrame frame, string key)
    {
        foreach (var ancestor in frame.Ancestors().Prepend(frame))
        {
            var matches = _documents.Resources(ancestor.Element)
                .Where(element => (string?)element.Attribute(BindingDocuments.Xaml + "Key") == key).ToArray();
            if (matches.Length == 1)
            {
                return matches[0];
            }
        }
        var global = _documents.ApplicationResources()
            .Where(element => (string?)element.Attribute(BindingDocuments.Xaml + "Key") == key).ToArray();
        return global.Length == 1 ? global[0]
            : throw BindingWalkException.Uncovered(key, "Resource provenance is absent or ambiguous (external resources need runtime coverage).");
    }

    internal void RecordProblem(BindingSite site, BindingWalkException error, string source = "unknown") =>
        _report.Add(new BindingFinding(site, error.Coverage, source, SafePath(site), error.Segment, error.Message));

    internal void CheckSite(BindingSite site, BindingFrame frame)
    {
        var source = frame.Type.Name;
        try
        {
            var binding = site.Read();
            if (binding.Kind is not ("Binding" or "ReflectionBinding" or "CompiledBinding" or "TemplateBinding" or "MultiBinding"))
            {
                throw BindingWalkException.Uncovered(binding.Kind, "Unsupported binding extension.");
            }
            binding.RequireSupportedArguments();
            if (binding.Kind != "TemplateBinding" && binding.Get("Source") is null && binding.Get("ElementName") is null &&
                binding.Get("RelativeSource") is null && !binding.Path.TrimStart('!').StartsWith('#') &&
                !binding.Path.TrimStart('!').StartsWith('$'))
            {
                CheckConstraint(frame, site.Target == "DataContext");
            }
            CheckConverter(binding, frame);
            if (binding.Kind == "MultiBinding")
            {
                _report.Add(new BindingFinding(site, BindingCoverage.Checked, source, "(inputs)", string.Empty,
                    "MultiBinding converter checked; each child path is accounted independently."));
                return;
            }
            var mode = _types.Mode(frame.Type, site.Target, binding.Get("Mode") ??
                (binding.Kind == "TemplateBinding" ? "OneWay" : null), frame.Element);
            var path = binding.Path;
            var resolvedSource = IsItemBinding(site.Target) ? ItemSource(binding, frame)
                : Source(binding, frame, ref path, site.Target == "DataContext");
            source = resolvedSource.Type.FullName ?? resolvedSource.Type.Name;
            if (binding.Get("DataType") is { } declared && !_types.Resolve(frame.Element, declared).IsAssignableFrom(resolvedSource.Type))
            {
                throw BindingWalkException.Failed("DataType", "Binding declaration disagrees with proven source type.");
            }
            var value = _paths.Follow(resolvedSource, path, frame.Element, mode is BindingMode.TwoWay or BindingMode.OneWayToSource);
            if (site.Target.EndsWith("Command", StringComparison.Ordinal) && !typeof(ICommand).IsAssignableFrom(value.Type))
            {
                throw BindingWalkException.Failed(path, "Command source does not implement ICommand.");
            }
            _report.Add(new BindingFinding(site, BindingCoverage.Checked, source, binding.Path, string.Empty, "Readable path."));
        }
        catch (BindingWalkException error)
        {
            RecordProblem(site, error, source);
        }
    }

    private void BuildFrames(XElement element, BindingScope scope, BindingFrame? parent)
    {
        if (BindingDocuments.IsDesign(element))
        {
            return;
        }
        if (BindingDocuments.IsDefinition(element) && !ReferenceEquals(element, scope.Root))
        {
            return;
        }
        if (element.Name.LocalName.EndsWith("Binding", StringComparison.Ordinal))
        {
            return;
        }
        if (!BindingDocuments.IsProperty(element) && !BindingDocuments.IsTemplate(element))
        {
            try
            {
                parent = new BindingFrame(element, _types.ElementType(element), scope, parent);
                scope.Frames.Add(element, parent);
            }
            catch (BindingWalkException error)
            {
                foreach (var site in _documents.Sites.Where(site => site.Owner == element))
                {
                    RecordProblem(site, error);
                }
                return;
            }
        }
        foreach (var child in element.Elements())
        {
            BuildFrames(child, scope, parent);
        }
    }

    private void CheckFrame(BindingFrame frame)
    {
        foreach (var site in _documents.Sites.Where(site => site.Owner == frame.Element))
        {
            CheckSite(site, frame);
        }
    }

    private void VisitControl(BindingFrame frame)
    {
        var definition = _documents.ForClass(frame.Type);
        if (definition is null || ReferenceEquals(definition, frame.Scope.Root))
        {
            return;
        }
        try
        {
            if (frame.Type == typeof(PadInfoPopover))
            {
                Visit(definition, frame, new BindingValue(typeof(CadPadInfoViewModel)));
                Visit(definition, frame, new BindingValue(typeof(RegularPadInfoViewModel)));
            }
            else
            {
                Visit(definition, frame, Context(frame));
            }
        }
        catch (BindingWalkException error)
        {
            foreach (var site in _documents.Sites.Where(site => site.File == _documents.FileOf(definition)))
            {
                RecordProblem(site, error);
            }
        }
    }

    private BindingValue Source(BindingMarkup binding, BindingFrame frame, ref string path, bool inherited)
    {
        if (new[] { binding.Get("Source"), binding.Get("ElementName"), binding.Get("RelativeSource") }.Count(static value => value is not null) > 1)
        {
            throw BindingWalkException.Uncovered(path, "Multiple source selectors are not supported.");
        }
        if (binding.Kind == "TemplateBinding")
        {
            return frame.Scope.TemplateParent?.AsValue()
                ?? throw BindingWalkException.Uncovered(path, "TemplateBinding has no templated control.");
        }
        if (binding.Get("ElementName") is { } name)
        {
            return Named(frame, name);
        }
        if (binding.Get("Source") is { } explicitSource)
        {
            return ExplicitSource(frame, explicitSource);
        }
        if (binding.Get("RelativeSource") is { } relative)
        {
            return Relative(frame, BindingMarkup.Parse(relative));
        }
        var prefix = path.StartsWith('!') ? "!" : string.Empty;
        var parts = BindingMarkup.Split(path.TrimStart('!'), '.');
        if (parts[0].StartsWith('#'))
        {
            path = prefix + string.Join('.', parts.Skip(1));
            return Named(frame, parts[0][1..]);
        }
        if (parts[0] == "$self")
        {
            path = prefix + string.Join('.', parts.Skip(1));
            return frame.AsValue();
        }
        if (parts[0].StartsWith("$parent[", StringComparison.Ordinal) && parts[0].EndsWith(']'))
        {
            var selector = BindingMarkup.Split(parts[0][8..^1], ';');
            if (selector.Count > 2)
            {
                throw BindingWalkException.Uncovered(parts[0], "Unsupported ancestor selector.");
            }
            var level = selector.Count == 2 ? ParseLevel(selector[1], 0) + 1 : 1;
            path = prefix + string.Join('.', parts.Skip(1));
            return Ancestor(frame, _types.Resolve(frame.Element, selector[0]), level);
        }
        return Context(frame, inherited);
    }

    private BindingValue Relative(BindingFrame frame, BindingMarkup relative)
    {
        if (relative.Kind != "RelativeSource" || relative.Arguments.Keys.Any(static key =>
            key is not ("Mode" or "AncestorType" or "AncestorLevel" or "Tree")))
        {
            throw BindingWalkException.Uncovered("RelativeSource", "Unknown relative-source syntax.");
        }
        if (relative.Get("Tree") is { } tree)
        {
            throw BindingWalkException.Uncovered(tree, "An explicit logical/visual tree needs separate attachment provenance.");
        }
        var mode = relative.Get("Mode") ?? "FindAncestor";
        return mode switch
        {
            "Self" => frame.AsValue(),
            "TemplatedParent" => frame.Scope.TemplateParent?.AsValue()
                ?? throw BindingWalkException.Uncovered(mode, "Templated parent is unknown."),
            "FindAncestor" => Ancestor(frame, _types.Resolve(frame.Element, relative.Get("AncestorType") ?? "Control"),
                relative.Get("AncestorLevel") is { } level ? ParseLevel(level, 1) : 1),
            _ => throw BindingWalkException.Uncovered(mode, "Unsupported relative-source mode."),
        };
    }

    private static BindingValue Ancestor(BindingFrame frame, Type type, int level) =>
        frame.Ancestors().Where(ancestor => type.IsAssignableFrom(ancestor.Type)).Skip(level - 1).FirstOrDefault()?.AsValue()
        ?? throw BindingWalkException.Uncovered(type.Name, "No matching instantiated ancestor; runtime attachment must establish it.");

    private static BindingValue Named(BindingFrame frame, string name) => frame.Scope.Named(name)?.AsValue()
        ?? throw BindingWalkException.Failed(name, "Named element is absent from this namescope.");

    private BindingValue ExplicitSource(BindingFrame frame, string text)
    {
        var extension = BindingMarkup.Parse(text);
        if (extension.Kind is "StaticResource" or "DynamicResource")
        {
            return new BindingValue(_types.ElementType(Resource(frame, extension.Get("Key") ?? string.Empty)));
        }
        if (extension.Kind == "x:Static" && extension.Get("Member") is { } member)
        {
            var dot = member.LastIndexOf('.');
            if (dot > 0)
            {
                var type = _types.Resolve(frame.Element, member[..dot]);
                var property = type.GetProperty(member[(dot + 1)..], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var field = type.GetField(member[(dot + 1)..], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                return new BindingValue(property?.PropertyType ?? field?.FieldType
                    ?? throw BindingWalkException.Failed(member, "Static source member does not exist."));
            }
        }
        throw BindingWalkException.Uncovered(text, "Unknown explicit source provenance.");
    }

    private void CheckConverter(BindingMarkup binding, BindingFrame frame)
    {
        if (binding.Get("Converter") is not { } converter)
        {
            return;
        }
        var type = ExplicitSource(frame, converter).Type;
        var contract = binding.Kind == "MultiBinding" ? typeof(IMultiValueConverter) : typeof(IValueConverter);
        if (!contract.IsAssignableFrom(type))
        {
            throw BindingWalkException.Failed("Converter", $"{type.Name} does not implement {contract.Name}.");
        }
    }

    private static bool IsItemBinding(string target) =>
        target is "SelectedValueBinding" or "DisplayMemberBinding" or "SelectedItemBinding" or "ValueMemberBinding";

    // These bindings run against each item of the control, so the data context of the control is not their source.
    private BindingValue ItemSource(BindingMarkup binding, BindingFrame frame) => binding.Get("DataType") is { } declared
        ? new BindingValue(_types.Resolve(frame.Element, declared))
        : throw BindingWalkException.Uncovered(binding.Path, "Item-level binding without a declared DataType.");

    private void CheckConstraint(BindingFrame frame, bool inheritedContext = false)
    {
        var declaration = frame.Element.AncestorsAndSelf().TakeWhile(element =>
                ReferenceEquals(element, frame.Scope.Root) || element.Ancestors().Contains(frame.Scope.Root))
            .Select(static element => element.Attribute(BindingDocuments.Xaml + "DataType")).FirstOrDefault(static attribute => attribute is not null);
        if (declaration is not null)
        {
            var expected = _types.Resolve(declaration.Parent!, declaration.Value);
            var actual = Context(frame, inheritedContext).Type;
            if (!expected.IsAssignableFrom(actual))
            {
                throw BindingWalkException.Failed("x:DataType", $"Declared {expected.Name} disagrees with runtime source {actual.Name}.");
            }
        }
    }

    private BindingSite? BoundSite(BindingFrame frame, string property) => _documents.Sites.FirstOrDefault(site =>
        site.Owner == frame.Element && site.Target == property);

    private static int ParseLevel(string text, int minimum) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var level) && level >= minimum
        ? level : throw BindingWalkException.Uncovered(text, "Invalid ancestor level.");

    private static string SafePath(BindingSite site)
    {
        try
        {
            return site.Read().Path;
        }
        catch (BindingWalkException)
        {
            return site.Text;
        }
    }
}

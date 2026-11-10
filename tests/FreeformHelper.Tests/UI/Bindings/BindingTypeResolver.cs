// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using FreeformHelper.UI;

namespace FreeformHelper.Tests.UI.Bindings;

internal sealed class BindingTypeResolver
{
    private readonly Assembly[] _assemblies = LoadAssemblies();

    public Type Resolve(XElement element, string name)
    {
        if (name.StartsWith('{'))
        {
            name = BindingMarkup.Parse(name).Get("Type")
                ?? throw BindingWalkException.Uncovered(name, "Expected x:Type.");
        }
        var pieces = name.Split(':', 2);
        // An unprefixed type name in a binding or selector uses the default xmlns of the document, not the namespace of the element.
        var ns = pieces.Length == 2 ? element.GetNamespaceOfPrefix(pieces[0])?.NamespaceName : element.GetDefaultNamespace().NamespaceName;
        return ResolveName(ns ?? string.Empty, pieces[^1]);
    }

    public Type ElementType(XElement element) => element.Attribute(BindingDocuments.Xaml + "Class") is { } declared
        ? Find(declared.Value) : ResolveName(element.Name.NamespaceName, element.Name.LocalName);

    public Type Find(string fullName) => _assemblies.Select(assembly => assembly.GetType(fullName)).OfType<Type>().FirstOrDefault()
        ?? throw BindingWalkException.Uncovered(fullName, "Type is not present in the loaded assembly graph.");

    public static PropertyInfo Readable(Type type, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? type.GetInterfaces().Select(contract => contract.GetProperty(name)).FirstOrDefault(static member => member is not null);
        if (property?.GetMethod?.IsPublic != true)
        {
            throw BindingWalkException.Failed(name, $"Missing readable member on {type.FullName}.");
        }
        return property;
    }

    public static void RequireWritable(PropertyInfo property)
    {
        if (property.SetMethod?.IsPublic != true ||
            property.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
        {
            throw BindingWalkException.Failed(property.Name, $"Endpoint on {property.DeclaringType?.Name} is not publicly writable.");
        }
    }

    public static Type ItemType(Type collection)
    {
        if (collection.IsArray)
        {
            return collection.GetElementType()!;
        }
        var choices = collection.GetInterfaces().Prepend(collection)
            .Where(static type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(static type => type.GetGenericArguments()[0]).Distinct().ToArray();
        return choices.Length == 1 ? choices[0]
            : throw BindingWalkException.Uncovered(collection.Name, "Collection item type is erased or ambiguous; caller provenance is required.");
    }

    public BindingMode Mode(Type target, string propertyName, string? explicitMode, XElement element)
    {
        if (explicitMode is not null && (!Enum.TryParse<BindingMode>(explicitMode, out _) ||
            !Enum.IsDefined(Enum.Parse<BindingMode>(explicitMode))))
        {
            throw BindingWalkException.Uncovered(explicitMode, "Unknown binding mode.");
        }
        var mode = explicitMode is null ? BindingMode.Default : Enum.Parse<BindingMode>(explicitMode);
        if (propertyName.StartsWith("Classes.", StringComparison.Ordinal))
        {
            return mode == BindingMode.Default ? BindingMode.OneWay : mode;
        }
        var pieces = propertyName.Split('.', 2);
        var owner = pieces.Length == 2 ? Resolve(element, pieces[0]) : target;
        for (Type? current = owner; current is not null; current = current.BaseType)
        {
            RuntimeHelpers.RunClassConstructor(current.TypeHandle);
        }
        var property = AvaloniaPropertyRegistry.Instance.FindRegistered(owner, pieces[^1]) ??
            owner.GetField(pieces[^1] + "Property", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetValue(null)
                as AvaloniaProperty;
        if (property is null)
        {
            // CLR wrapper properties without Avalonia metadata default to OneWay.
            Readable(owner, pieces[^1]);
            return mode == BindingMode.Default ? BindingMode.OneWay : mode;
        }
        return mode == BindingMode.Default ? property.GetMetadata(target).DefaultBindingMode : mode;
    }

    private Type ResolveName(string ns, string name)
    {
        if (ns == BindingDocuments.Xaml.NamespaceName)
        {
            return Find("System." + name);
        }
        if (ns.StartsWith("clr-namespace:", StringComparison.Ordinal) || ns.StartsWith("using:", StringComparison.Ordinal))
        {
            var qualified = ns[(ns.IndexOf(':', StringComparison.Ordinal) + 1)..].Split(';')[0];
            return Find(qualified + "." + name);
        }
        string[] prefixes = ["Avalonia.Controls", "Avalonia.Controls.Primitives", "Avalonia.Controls.Presenters", "Avalonia.Controls.Templates", "Avalonia.Controls.Shapes", "Avalonia.Layout",
            "Avalonia", "Avalonia.Styling", "Avalonia.Media", "Avalonia.Data.Converters", "Avalonia.Automation", "Avalonia.Input",
            "Avalonia.Controls.Documents"];
        return prefixes.SelectMany(prefix => _assemblies.Select(assembly => assembly.GetType(prefix + "." + name)))
            .OfType<Type>().FirstOrDefault()
            ?? throw BindingWalkException.Uncovered(name, $"Unknown XML type in namespace {ns}.");
    }

    private static Assembly[] LoadAssemblies()
    {
        var assemblies = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        var pending = new Queue<Assembly>([typeof(MainWindow).Assembly, typeof(Window).Assembly, typeof(string).Assembly,
            typeof(BindingTypeResolver).Assembly]);
        while (pending.TryDequeue(out var assembly))
        {
            if (!assemblies.TryAdd(assembly.FullName!, assembly))
            {
                continue;
            }
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (!assemblies.ContainsKey(reference.FullName))
                {
                    pending.Enqueue(Assembly.Load(reference));
                }
            }
        }
        return assemblies.Values.ToArray();
    }
}

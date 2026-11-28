// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Xml.Linq;

namespace FreeformHelper.Tests.UI.Bindings;

internal sealed record BindingValue(Type Type, BindingFrame? Control = null);

internal sealed class BindingScope(XElement root, BindingFrame? host, BindingValue? context, BindingFrame? templateParent = null)
{
    // The scope is built and consumed synchronously by one walker.
    public Dictionary<XElement, BindingFrame> Frames { get; } = [];
    public XElement Root { get; } = root;
    public BindingFrame? Host { get; } = host;
    public BindingValue? Context { get; } = context;
    public BindingFrame? TemplateParent { get; } = templateParent;

    public BindingFrame? Named(string name)
    {
        var matches = Frames.Values.Where(frame =>
            string.Equals((string?)frame.Element.Attribute(BindingDocuments.Xaml + "Name") ??
                (string?)frame.Element.Attribute("Name"), name, StringComparison.Ordinal)).ToArray();
        return matches.Length switch
        {
            0 => Host?.Scope.Named(name),
            1 => matches[0],
            _ => throw BindingWalkException.Failed(name, "Ambiguous name within the instantiated namescope."),
        };
    }
}

internal sealed record BindingFrame(XElement Element, Type Type, BindingScope Scope, BindingFrame? Parent)
{
    public BindingValue AsValue() => new(Type, this);

    public IEnumerable<BindingFrame> Ancestors()
    {
        for (var frame = Parent; frame is not null; frame = frame.Parent)
        {
            yield return frame;
        }
    }
}
